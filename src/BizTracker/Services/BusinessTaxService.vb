''' <summary>
''' What is owed on a business permit application on a date (computed, never stored).
''' Tax = the assessed business tax (principal); Surcharge and Interest are the late-renewal charges.
''' </summary>
Public Class BusinessTaxBalance
    Public Property Permit As BusinessPermit
    ''' <summary>Active (not voided) payments, in the order they were recorded.</summary>
    Public Property Payments As New List(Of BusinessPermitPayment)
    Public Property AsOf As Date
    Public Property Deadline As Date
    ''' <summary>True once the application has an assessed tax (Assessed / Paid / Issued).</summary>
    Public Property IsAssessed As Boolean
    ''' <summary>Paid / Issued before payments were recorded in BizTracker: counts as fully paid, nothing to collect.</summary>
    Public Property IsLegacy As Boolean
    Public Property Tax As Decimal
    ''' <summary>Surcharge owed in total as of AsOf (once, on the tax still unpaid at the deadline).</summary>
    Public Property SurchargeCharged As Decimal
    Public Property SurchargeDue As Decimal
    Public Property InterestDue As Decimal
    Public Property MonthsLate As Integer
    Public Property InterestRate As Decimal

    Public ReadOnly Property TaxPaid As Decimal
        Get
            Return Payments.Sum(Function(p) p.AmountPaid)
        End Get
    End Property

    Public ReadOnly Property SurchargePaid As Decimal
        Get
            Return Payments.Sum(Function(p) p.Surcharge)
        End Get
    End Property

    Public ReadOnly Property InterestPaid As Decimal
        Get
            Return Payments.Sum(Function(p) p.Interest)
        End Get
    End Property

    Public ReadOnly Property TotalPaid As Decimal
        Get
            Return Payments.Sum(Function(p) p.Total)
        End Get
    End Property

    Public ReadOnly Property TaxRemaining As Decimal
        Get
            Return If(IsLegacy, 0D, Math.Max(0D, Tax - TaxPaid))
        End Get
    End Property

    ''' <summary>What it costs to settle the application on AsOf: remaining tax + surcharge due + interest due.</summary>
    Public ReadOnly Property Balance As Decimal
        Get
            If IsLegacy OrElse Not IsAssessed Then Return 0D
            Return TaxRemaining + SurchargeDue + InterestDue
        End Get
    End Property

    Public ReadOnly Property IsFullyPaid As Boolean
        Get
            Return IsLegacy OrElse (IsAssessed AndAlso TaxRemaining = 0D AndAlso SurchargeDue = 0D)
        End Get
    End Property

    Public ReadOnly Property IsPartiallyPaid As Boolean
        Get
            Return Not IsFullyPaid AndAlso Payments.Count > 0
        End Get
    End Property
End Class

''' <summary>How one payment is split (BusinessTaxService.AllocatePayment).</summary>
Public Class BusinessTaxSplit
    Public Property ToInterest As Decimal
    Public Property ToSurcharge As Decimal
    Public Property ToTax As Decimal

    Public ReadOnly Property Total As Decimal
        Get
            Return ToInterest + ToSurcharge + ToTax
        End Get
    End Property
End Class

''' <summary>Live numbers for the business tax Record Payment dialog. Nothing is saved.</summary>
Public Class BusinessTaxPreview
    Public Property Balance As BusinessTaxBalance
    Public Property Amount As Decimal
    Public Property Split As New BusinessTaxSplit

    Public ReadOnly Property RemainingAfter As Decimal
        Get
            Return Math.Max(0D, Balance.Balance - Split.Total)
        End Get
    End Property

    ''' <summary>True when this payment pays the rest of the tax and surcharge (the application becomes Paid).</summary>
    Public ReadOnly Property Completes As Boolean
        Get
            Return Balance.Balance > 0D AndAlso Split.ToTax >= Balance.TaxRemaining AndAlso Split.ToSurcharge >= Balance.SurchargeDue
        End Get
    End Property
End Class

''' <summary>Outcome of BusinessTaxService.RecordPayment.</summary>
Public Class BusinessTaxPaymentResult
    ''' <summary>Nothing when the payment was saved.</summary>
    Public Property ErrorMessage As String
    Public Property Payment As BusinessPermitPayment
    ''' <summary>True when this payment completed the balance and the application became Paid.</summary>
    Public Property BecamePaid As Boolean
End Class

''' <summary>What voiding a business tax payment would do, shown before the user confirms.</summary>
Public Class BusinessTaxVoidImpact
    Public Property Payment As BusinessPermitPayment
    Public Property Permit As BusinessPermit
    ''' <summary>Why it cannot be voided (Nothing = it can).</summary>
    Public Property Blocker As String
    ''' <summary>True if the application goes back from Paid to Assessed.</summary>
    Public Property RevertsToAssessed As Boolean
    ''' <summary>Set when the Mayor's Permit was already issued (it stays Issued).</summary>
    Public Property IssuedWarning As String
End Class

''' <summary>
''' Business tax walk-in payments (BPLO / Admin today - see AccessService.CanCollectPayments).
'''   - The tax is the assessed amount. Renewals paid after the deadline (bp_renewal_deadline, Jan 20) carry:
'''       * a one-time SURCHARGE (bp_surcharge_rate, 25%) on the tax still unpaid on the deadline, and
'''       * INTEREST (bp_interest_rate_monthly, 2% per month, a part of a month counts, at most bp_interest_max_months)
'''         on the unpaid tax + surcharge, each peso for the months it stayed unpaid (ComputeInterestDue).
'''     New applications are never late.
'''   - Partial payments are allowed. A payment pays interest, then surcharge, then tax (AllocationOrder).
'''   - The application becomes Paid automatically (same transaction) when the tax and surcharge are fully paid;
'''     the surcharge and interest actually charged are then stored on the permit.
'''   - Mistakes are voided (newest payment first, reason required); a Paid application goes back to Assessed.
'''     Voiding after the Mayor's Permit was issued is Admin only and keeps the permit Issued (warning + audit note).
'''   - Permits marked legacy_paid (paid before payments were recorded) count as fully paid.
''' </summary>
Public NotInheritable Class BusinessTaxService

    Private Sub New()
    End Sub

    Public Const InterestPart As String = "Interest"
    Public Const SurchargePart As String = "Surcharge"
    Public Const TaxPart As String = "Tax"

    ''' <summary>
    ''' THE PAYMENT ORDER RULE (one place): a payment pays these parts in this order.
    ''' To apply payments to the tax first, change it to {TaxPart, SurchargePart, InterestPart}.
    ''' </summary>
    Public Shared ReadOnly AllocationOrder As String() = {InterestPart, SurchargePart, TaxPart}

    Private Const PaymentsTable As String = "business_permit_payments"
    Private Const PermitsTable As String = "business_permits"

    ' ==================== Rates (settings table) ====================

    Public Shared ReadOnly Property SurchargeRate As Decimal
        Get
            Return SettingsRepository.GetDecimal("bp_surcharge_rate", 0.25D)
        End Get
    End Property

    Public Shared ReadOnly Property InterestRateMonthly As Decimal
        Get
            Return SettingsRepository.GetDecimal("bp_interest_rate_monthly", 0.02D)
        End Get
    End Property

    ' ==================== The math ====================

    Private Shared Function Round2(value As Decimal) As Decimal
        Return Math.Round(value, 2, MidpointRounding.AwayFromZero)
    End Function

    Private Shared Function IsRenewal(p As BusinessPermit) As Boolean
        Return p.ApplicationType = "Renewal"
    End Function

    ''' <summary>Months late on a date (0 = on time; capped at bp_interest_max_months). New applications are never late.</summary>
    Public Shared Function GetMonthsLate(p As BusinessPermit, onDate As Date) As Integer
        If Not IsRenewal(p) Then Return 0
        Return BusinessPermitService.ComputeLateCharges(p.ApplicationType, p.PermitYear, 0D, onDate).MonthsLate
    End Function

    ''' <summary>Interest rate on a date: monthly rate x months late (2% x 5 = 10%).</summary>
    Public Shared Function GetInterestRate(p As BusinessPermit, onDate As Date) As Decimal
        Return InterestRateMonthly * GetMonthsLate(p, onDate)
    End Function

    ''' <summary>
    ''' THE LATE-CHARGES RULE (one place). What is owed on "asOf", given the active payments:
    '''   surcharge = 25% x the tax still unpaid ON the deadline (charged once; tax paid by Jan 20 never gets it)
    '''   interest  = each peso of (tax + surcharge) x the rate for the months it stayed unpaid
    '''             = sum(base paid by each earlier payment x rate on its date) + base still unpaid x rate on asOf
    '''               - interest already paid                                  (base = tax + surcharge part)
    ''' Example: tax 10,000, nothing paid by Jan 20 -> surcharge 2,500. Mar 15 (4%): interest 500; 5,000 paid =
    ''' 500 interest + 2,500 surcharge + 2,000 tax. Jun 10 (10%): 4,500 x 4% + 8,000 x 10% - 500 = 480 interest due.
    ''' </summary>
    Public Shared Function GetBalance(p As BusinessPermit, asOf As Date,
                                      Optional activePayments As List(Of BusinessPermitPayment) = Nothing) As BusinessTaxBalance
        Dim payments = If(activePayments, ActiveOnly(BusinessPermitPaymentRepository.GetByPermitId(p.PermitId))).
                       OrderBy(Function(x) x.PaymentId).ToList()
        Dim b As New BusinessTaxBalance With {
            .Permit = p, .Payments = payments, .AsOf = asOf.Date,
            .Deadline = BusinessPermitService.GetRenewalDeadline(p.PermitYear),
            .IsLegacy = p.LegacyPaid,
            .IsAssessed = p.AssessedAmount > 0D AndAlso
                          {BusinessPermitService.Assessed, BusinessPermitService.Paid, BusinessPermitService.Issued}.Contains(p.Status),
            .Tax = p.AssessedAmount,
            .MonthsLate = GetMonthsLate(p, asOf), .InterestRate = GetInterestRate(p, asOf)
        }
        If b.IsLegacy OrElse Not b.IsAssessed Then Return b

        ' Surcharge: once, on the tax still unpaid on the deadline
        If IsRenewal(p) AndAlso asOf.Date > b.Deadline Then
            Dim paidByDeadline = payments.Where(Function(x) x.PaymentDate.Date <= b.Deadline).Sum(Function(x) x.AmountPaid)
            b.SurchargeCharged = Round2(Math.Max(0D, b.Tax - paidByDeadline) * SurchargeRate)
        End If
        b.SurchargeDue = Math.Max(0D, b.SurchargeCharged - b.SurchargePaid)

        ' Interest: each peso of the base (tax + surcharge) for the months it stayed unpaid
        Dim baseUnpaid = b.TaxRemaining + b.SurchargeDue
        If baseUnpaid > 0D Then
            Dim charged = payments.Sum(Function(x) Round2((x.AmountPaid + x.Surcharge) * GetInterestRate(p, x.PaymentDate)))
            charged += Round2(baseUnpaid * b.InterestRate)
            b.InterestDue = Math.Max(0D, charged - b.InterestPaid)
        End If
        Return b
    End Function

    ''' <summary>THE PAYMENT ORDER RULE (see AllocationOrder): how a payment amount is split.</summary>
    Public Shared Function AllocatePayment(amount As Decimal, b As BusinessTaxBalance) As BusinessTaxSplit
        Dim split As New BusinessTaxSplit
        Dim left = Math.Max(0D, amount)
        For Each part In AllocationOrder
            Select Case part
                Case InterestPart
                    split.ToInterest = Math.Min(left, b.InterestDue) : left -= split.ToInterest
                Case SurchargePart
                    split.ToSurcharge = Math.Min(left, b.SurchargeDue) : left -= split.ToSurcharge
                Case TaxPart
                    split.ToTax = Math.Min(left, b.TaxRemaining) : left -= split.ToTax
            End Select
        Next
        Return split
    End Function

    ''' <summary>Live numbers for paying "amount" on "payDate" (nothing is saved).</summary>
    Public Shared Function PreviewPayment(p As BusinessPermit, activePayments As List(Of BusinessPermitPayment),
                                          payDate As Date, amount As Decimal) As BusinessTaxPreview
        Dim b = GetBalance(p, payDate, activePayments)
        Return New BusinessTaxPreview With {
            .Balance = b, .Amount = amount, .Split = AllocatePayment(Math.Min(Math.Max(0D, amount), b.Balance), b)}
    End Function

    ' ==================== Reading ====================

    Private Shared Function ActiveOnly(payments As List(Of BusinessPermitPayment)) As List(Of BusinessPermitPayment)
        Return payments.Where(Function(x) Not x.IsVoided).ToList()
    End Function

    ''' <summary>Active payments of an application, in the order they were recorded.</summary>
    Public Shared Function GetActivePayments(permitId As Integer) As List(Of BusinessPermitPayment)
        Return ActiveOnly(BusinessPermitPaymentRepository.GetByPermitId(permitId))
    End Function

    ''' <summary>Every payment of an application (voided ones too), newest first. Empty if the user may not see it.</summary>
    Public Shared Function GetPaymentHistory(permitId As Integer) As List(Of BusinessPermitPayment)
        Dim p = BusinessPermitRepository.GetById(permitId)
        If p Is Nothing OrElse Not AccessService.CanSeeBusiness(p.BusinessId) Then Return New List(Of BusinessPermitPayment)
        Return BusinessPermitPaymentRepository.GetByPermitId(permitId).
               OrderByDescending(Function(x) x.PaymentDate).ThenByDescending(Function(x) x.PaymentId).ToList()
    End Function

    ''' <summary>Every payment of a business (all applications, voided ones too). Empty if the user may not see it.</summary>
    Public Shared Function GetBusinessPayments(businessId As Integer) As List(Of BusinessPermitPayment)
        If Not AccessService.CanSeeBusiness(businessId) Then Return New List(Of BusinessPermitPayment)
        Return BusinessPermitPaymentRepository.GetByBusinessId(businessId)
    End Function

    ''' <summary>"Partially Paid" while an Assessed application has payments; otherwise the stored status.</summary>
    Public Shared Function GetDisplayStatus(p As BusinessPermit) As String
        If p Is Nothing Then Return ""
        If p.Status = BusinessPermitService.Assessed AndAlso Not p.LegacyPaid AndAlso GetActivePayments(p.PermitId).Count > 0 Then
            Return StatusService.PartiallyPaid
        End If
        Return p.Status
    End Function

    ''' <summary>What was still owed on the application right after a payment (for the receipt).</summary>
    Public Shared Function GetBalanceAfter(pay As BusinessPermitPayment, permitPayments As IEnumerable(Of BusinessPermitPayment),
                                           p As BusinessPermit) As Decimal
        Dim earlier = permitPayments.Where(Function(x) Not x.IsVoided AndAlso x.PaymentId < pay.PaymentId).ToList()
        Dim copy = New BusinessPermit With {
            .PermitId = p.PermitId, .ApplicationType = p.ApplicationType, .PermitYear = p.PermitYear,
            .AssessedAmount = p.AssessedAmount, .Status = BusinessPermitService.Assessed}
        Return Math.Max(0D, GetBalance(copy, pay.PaymentDate, earlier).Balance - pay.Total)
    End Function

    ' ==================== Recording ====================

    ''' <summary>Why the application cannot take a payment now (Nothing = it can).</summary>
    Public Shared Function GetPaymentBlocker(p As BusinessPermit) As String
        If p Is Nothing Then Return "Select an application first."
        If Not AccessService.CanCollectPayments(AppScreen.BusinessPermits) Then Return "Only the BPLO or an Admin can record business tax payments."
        If p.LegacyPaid Then Return p.ReferenceNo & " was paid before payment recording started - nothing to collect."
        If p.Status = BusinessPermitService.Paid OrElse p.Status = BusinessPermitService.Issued Then Return p.ReferenceNo & " is already fully paid."
        If p.Status <> BusinessPermitService.Assessed OrElse p.AssessedAmount <= 0D Then
            Return "Assess the application first (business tax is collected once it is Assessed)."
        End If
        Return Nothing
    End Function

    ''' <summary>The earliest allowed payment date: the filing date, or the last active payment.</summary>
    Public Shared Function GetEarliestPaymentDate(p As BusinessPermit, activePayments As List(Of BusinessPermitPayment)) As Date
        Dim last = activePayments.Select(Function(x) x.PaymentDate.Date).DefaultIfEmpty(p.DateFiled.Date).Max()
        Return If(last > p.DateFiled.Date, last, p.DateFiled.Date)
    End Function

    ''' <summary>Checks what the cashier typed. Keys: "date", "amount", "method", "reference", "or", "remarks".</summary>
    Public Shared Function ValidatePayment(p As BusinessPermit, activePayments As List(Of BusinessPermitPayment),
                                           input As PaymentInput) As Dictionary(Of String, String)
        Dim earliest = GetEarliestPaymentDate(p, activePayments)
        Dim message = If(activePayments.Count = 0, "Not before the filing date (" & UiHelper.FormatDate(earliest) & ").",
                         "Not before " & UiHelper.FormatDate(earliest) & " (last payment).")
        Return PaymentRules.ValidateCommon(input, earliest, message,
                                           Function(d As Date) GetBalance(p, d, activePayments).Balance)
    End Function

    ''' <summary>
    ''' Records a walk-in business tax payment (BPLO / Admin). The amount pays interest, surcharge, then tax.
    ''' When it completes the balance, the application becomes Paid in the SAME transaction.
    ''' </summary>
    Public Shared Function RecordPayment(permitId As Integer, input As PaymentInput) As BusinessTaxPaymentResult
        Dim result As New BusinessTaxPaymentResult
        Dim p = BusinessPermitRepository.GetById(permitId)
        If p Is Nothing OrElse Not AccessService.CanSeeBusiness(p.BusinessId) Then
            result.ErrorMessage = "Application not found."
            Return result
        End If
        result.ErrorMessage = GetPaymentBlocker(p)
        If result.ErrorMessage IsNot Nothing Then Return result
        Dim payments = GetActivePayments(permitId)
        Dim errors = ValidatePayment(p, payments, input)
        If errors.Count > 0 Then
            result.ErrorMessage = errors.Values.First()
            Return result
        End If

        Dim preview = PreviewPayment(p, payments, input.PaymentDate.Date, input.Amount)
        Dim pay As New BusinessPermitPayment With {
            .PermitId = permitId, .BusinessId = p.BusinessId, .PermitReferenceNo = p.ReferenceNo, .PermitYear = p.PermitYear,
            .AmountPaid = preview.Split.ToTax, .Surcharge = preview.Split.ToSurcharge, .Interest = preview.Split.ToInterest,
            .PaymentMethod = input.Method,
            .ReferenceNo = If(PaymentMethods.NeedsReference(input.Method), input.ReferenceNo.Trim(), ""),
            .OrNo = input.OrNo.Trim(), .PaymentDate = input.PaymentDate.Date, .ReceivedBy = Session.UserId,
            .Remarks = If(input.Remarks, "").Trim(), .ReceivedByName = Session.FullName
        }
        Dim b = preview.Balance
        Dim newId = BusinessPermitPaymentRepository.SavePayment(pay, payments.Count, payments.Sum(Function(x) x.Total),
                                                                preview.Completes, b.SurchargePaid + pay.Surcharge,
                                                                b.InterestPaid + pay.Interest)
        Select Case newId
            Case -2
                result.ErrorMessage = PaymentRules.OrTakenMessage(pay.OrNo)
                Return result
            Case 0
                result.ErrorMessage = "Another payment for " & p.ReferenceNo & " was saved or voided while you were typing. " &
                                      "Close this window and open Record Payment again."
                Return result
            Case Is < 0
                result.ErrorMessage = "The payment could not be saved."
                Return result
        End Select

        pay.PaymentId = newId
        result.Payment = pay
        result.BecamePaid = preview.Completes
        AuditService.LogInsert(PaymentsTable, newId, "OR " & pay.OrNo & ": " & p.ReferenceNo & " " & UiHelper.FormatMoney(pay.Total) &
                               " (" & pay.PaymentMethod & If(pay.ReferenceNo <> "", " " & pay.ReferenceNo, "") &
                               "; tax " & UiHelper.FormatMoney(pay.AmountPaid) & ", surcharge " & UiHelper.FormatMoney(pay.Surcharge) &
                               ", interest " & UiHelper.FormatMoney(pay.Interest) &
                               If(preview.Completes, "; fully paid", "; " & UiHelper.FormatMoney(preview.RemainingAfter) & " left") & ")")
        If preview.Completes Then
            AuditService.LogStatusChange(PermitsTable, p.PermitId, BusinessPermitService.Assessed, BusinessPermitService.Paid,
                                         "fully paid with OR " & pay.OrNo)
        End If
        Return result
    End Function

    ' ==================== Voiding ====================

    ''' <summary>What voiding a payment would do (blocker, Paid -> Assessed, the Issued warning), for the confirm step.</summary>
    Public Shared Function GetVoidImpact(paymentId As Integer) As BusinessTaxVoidImpact
        Dim impact As New BusinessTaxVoidImpact
        Dim pay = BusinessPermitPaymentRepository.GetById(paymentId)
        Dim p = If(pay Is Nothing, Nothing, BusinessPermitRepository.GetById(pay.PermitId))
        impact.Payment = pay
        impact.Permit = p
        If pay Is Nothing OrElse p Is Nothing OrElse Not AccessService.CanSeeBusiness(p.BusinessId) Then
            impact.Blocker = "Payment not found."
            Return impact
        End If
        Dim issued = p.Status = BusinessPermitService.Issued
        If Not AccessService.CanVoidPayments(AppScreen.BusinessPermits, issued) Then
            impact.Blocker = If(issued AndAlso AccessService.CanVoidPayments(AppScreen.BusinessPermits),
                                "The Mayor's Permit " & p.MayorsPermitNo & " is already issued - only an Admin can void its payments.",
                                "Only the BPLO or an Admin can void business tax payments.")
            Return impact
        End If
        If pay.IsVoided Then
            impact.Blocker = "OR " & pay.OrNo & " is already voided."
            Return impact
        End If
        Dim newest = GetActivePayments(p.PermitId).OrderByDescending(Function(x) x.PaymentId).First()
        If newest.PaymentId <> pay.PaymentId Then
            impact.Blocker = "Only the newest payment of " & p.ReferenceNo & " can be voided. Void OR " & newest.OrNo & " first."
            Return impact
        End If
        impact.RevertsToAssessed = p.Status = BusinessPermitService.Paid
        If issued Then
            impact.IssuedWarning = "Mayor's Permit " & p.MayorsPermitNo & " was already issued. It stays Issued although its business tax " &
                                   "will no longer be fully paid."
        End If
        Return impact
    End Function

    ''' <summary>
    ''' Voids a payment with a reason. It is never deleted: it stays in the history as Voided, no longer counts,
    ''' and its OR number stays used. A Paid application goes back to Assessed. Returns an error message, or Nothing.
    ''' </summary>
    Public Shared Function VoidPayment(paymentId As Integer, reason As String) As String
        Dim reasonError = PaymentRules.ValidateVoidReason(reason)
        If reasonError IsNot Nothing Then Return reasonError
        reason = reason.Trim()
        Dim impact = GetVoidImpact(paymentId)
        If impact.Blocker IsNot Nothing Then Return impact.Blocker
        Dim pay = impact.Payment
        Dim p = impact.Permit

        Select Case BusinessPermitPaymentRepository.VoidPayment(pay, Session.UserId, reason)
            Case 0 : Return "Another payment of " & p.ReferenceNo & " was recorded or voided meanwhile. Refresh (F5) and try again."
            Case -1 : Return "The payment could not be voided."
        End Select

        AuditService.LogStatusChange(PaymentsTable, pay.PaymentId, PaymentRules.Active, PaymentRules.Voided,
            "OR " & pay.OrNo & ", " & p.ReferenceNo & ", " & UiHelper.FormatMoney(pay.Total) & ": " & reason &
            If(impact.IssuedWarning IsNot Nothing, " | Mayor's Permit " & p.MayorsPermitNo & " already issued - stays Issued", ""))
        If impact.RevertsToAssessed Then
            AuditService.LogStatusChange(PermitsTable, p.PermitId, BusinessPermitService.Paid, BusinessPermitService.Assessed,
                                         "OR " & pay.OrNo & " voided")
        End If
        Return Nothing
    End Function

End Class
