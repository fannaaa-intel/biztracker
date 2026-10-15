''' <summary>
''' One quarter of one property's assessment (computed for the ledger, never stored).
''' A quarter can have several payments (partial payments); only active (not voided) payments count.
''' </summary>
Public Class RptQuarter
    Public Property AssessmentId As Integer
    Public Property PropertyId As Integer
    Public Property Pin As String = ""
    Public Property TaxYear As Integer
    Public Property Quarter As Integer
    Public Property DueDate As Date
    ''' <summary>The quarter's share of the annual tax (the principal billed).</summary>
    Public Property Amount As Decimal
    ''' <summary>Active payments of this quarter, in the order they were recorded.</summary>
    Public Property Payments As New List(Of RptPayment)
    ''' <summary>Penalty still due if paid today (0 once the principal is fully paid).</summary>
    Public Property PenaltyDue As Decimal
    ''' <summary>Paid / Overdue / Partially Paid / Due Soon / Not Yet Due (from StatusService).</summary>
    Public Property Status As String = ""

    Public ReadOnly Property PrincipalPaid As Decimal
        Get
            Return Payments.Sum(Function(p) p.AmountPaid)
        End Get
    End Property

    Public ReadOnly Property PenaltyPaid As Decimal
        Get
            Return Payments.Sum(Function(p) p.Penalty)
        End Get
    End Property

    ''' <summary>Everything paid on this quarter (principal + penalty).</summary>
    Public ReadOnly Property TotalPaid As Decimal
        Get
            Return PrincipalPaid + PenaltyPaid
        End Get
    End Property

    Public ReadOnly Property RemainingPrincipal As Decimal
        Get
            Return Math.Max(0D, Amount - PrincipalPaid)
        End Get
    End Property

    ''' <summary>What it costs to settle the quarter today: remaining principal + penalty due.</summary>
    Public ReadOnly Property BalanceDue As Decimal
        Get
            Return RemainingPrincipal + PenaltyDue
        End Get
    End Property

    ''' <summary>A quarter is Paid only when its principal is fully paid.</summary>
    Public ReadOnly Property IsPaid As Boolean
        Get
            Return RemainingPrincipal = 0D
        End Get
    End Property

    Public ReadOnly Property IsPartiallyPaid As Boolean
        Get
            Return Not IsPaid AndAlso Payments.Count > 0
        End Get
    End Property

    Public ReadOnly Property IsOverdue As Boolean
        Get
            Return Status = StatusService.Overdue
        End Get
    End Property

    ''' <summary>The latest active payment, or Nothing.</summary>
    Public ReadOnly Property LastPayment As RptPayment
        Get
            Return Payments.LastOrDefault()
        End Get
    End Property

    ''' <summary>"Q2 2026"</summary>
    Public ReadOnly Property Label As String
        Get
            Return "Q" & Quarter & " " & TaxYear
        End Get
    End Property

    ''' <summary>"partially paid, ₱67.10 left" (for details and tooltips), or "" when not partly paid.</summary>
    Public ReadOnly Property PartialNote As String
        Get
            Return If(IsPartiallyPaid, "partially paid, " & UiHelper.FormatMoney(BalanceDue) & " left", "")
        End Get
    End Property
End Class

''' <summary>One row of the property ledger: a property, its assessment for the year and the 4 quarters.</summary>
Public Class RptLedgerRow
    Public Property RealProperty As RealProperty
    ''' <summary>Nothing when the property has no assessment for the year yet.</summary>
    Public Property Assessment As RptAssessment
    ''' <summary>Q1-Q4 (empty when there is no assessment).</summary>
    Public Property Quarters As New List(Of RptQuarter)

    Public ReadOnly Property IsAssessed As Boolean
        Get
            Return Assessment IsNot Nothing
        End Get
    End Property

    ''' <summary>Tax (principal) paid so far this year.</summary>
    Public ReadOnly Property PrincipalPaid As Decimal
        Get
            Return Quarters.Sum(Function(q) q.PrincipalPaid)
        End Get
    End Property

    Public ReadOnly Property PenaltyPaid As Decimal
        Get
            Return Quarters.Sum(Function(q) q.PenaltyPaid)
        End Get
    End Property

    ''' <summary>What is left to pay this year if paid today: remaining principal + current penalties.</summary>
    Public ReadOnly Property Balance As Decimal
        Get
            Return Quarters.Sum(Function(q) q.BalanceDue)
        End Get
    End Property

    ''' <summary>Active payments of the year (all quarters).</summary>
    Public ReadOnly Property ActivePayments As IEnumerable(Of RptPayment)
        Get
            Return Quarters.SelectMany(Function(q) q.Payments)
        End Get
    End Property

    ''' <summary>The quarter that can be paid next (quarters are paid in order), or Nothing if all are paid.</summary>
    Public ReadOnly Property NextPayable As RptQuarter
        Get
            Return Quarters.FirstOrDefault(Function(q) Not q.IsPaid)
        End Get
    End Property

    ''' <summary>The most urgent quarter status: Overdue, Due Soon, Partially Paid, Not Yet Due, Paid (or Not Assessed).</summary>
    Public ReadOnly Property WorstStatus As String
        Get
            If Not IsAssessed Then Return RptService.NotAssessed
            For Each s In {StatusService.Overdue, StatusService.DueSoon, StatusService.PartiallyPaid, StatusService.NotYetDue}
                If Quarters.Any(Function(q) q.Status = s) Then Return s
            Next
            Return StatusService.Paid
        End Get
    End Property
End Class

''' <summary>How one payment amount is split (RptService.AllocatePayment).</summary>
Public Class PaymentSplit
    Public Property ToPenalty As Decimal
    Public Property ToPrincipal As Decimal
End Class

''' <summary>Live numbers for the Record Payment dialog (RptService.PreviewPayment). Nothing is saved.</summary>
Public Class PaymentPreview
    Public Property QuarterAmount As Decimal
    ''' <summary>Already paid on the quarter (principal + penalty).</summary>
    Public Property AlreadyPaid As Decimal
    Public Property RemainingPrincipal As Decimal
    Public Property MonthsLate As Integer
    Public Property PenaltyRate As Decimal
    Public Property PenaltyDue As Decimal
    ''' <summary>Remaining principal + penalty due on the payment date.</summary>
    Public Property TotalDue As Decimal
    Public Property Amount As Decimal
    Public Property Split As New PaymentSplit
    ''' <summary>What is still owed on the quarter after this payment (as of the payment date).</summary>
    Public ReadOnly Property RemainingAfter As Decimal
        Get
            Return Math.Max(0D, TotalDue - Split.ToPenalty - Split.ToPrincipal)
        End Get
    End Property

    ''' <summary>True when this payment pays the rest of the quarter's principal.</summary>
    Public ReadOnly Property CompletesQuarter As Boolean
        Get
            Return RemainingPrincipal > 0D AndAlso Split.ToPrincipal >= RemainingPrincipal
        End Get
    End Property
End Class

''' <summary>What the cashier typed in the Record Payment dialog.</summary>
Public Class RptPaymentInput
    Public Property PaymentDate As Date = Date.Today
    Public Property Amount As Decimal
    Public Property Method As String = PaymentMethods.Cash
    Public Property ReferenceNo As String = ""
    Public Property OrNo As String = ""
    Public Property Remarks As String = ""
End Class

''' <summary>Outcome of RptService.RecordPayment.</summary>
Public Class RptPaymentResult
    ''' <summary>Nothing when the payment was saved.</summary>
    Public Property ErrorMessage As String
    Public Property Payment As RptPayment
    ''' <summary>Reference no. of the business permit whose RPT endorsement was set to Endorsed (or Nothing).</summary>
    Public Property EndorsedReference As String
End Class

''' <summary>What voiding a payment would do (RptService.GetVoidImpact), shown before the user confirms.</summary>
Public Class VoidImpact
    Public Property Payment As RptPayment
    ''' <summary>Why it cannot be voided (Nothing = it can).</summary>
    Public Property Blocker As String
    ''' <summary>True if the payment's quarter becomes overdue again.</summary>
    Public Property BecomesOverdue As Boolean
    ''' <summary>RPT endorsement going back to Pending on the open business permit application (or Nothing).</summary>
    Public Property Endorsement As EndorsementChange
    ''' <summary>Set when the quarter becomes overdue but the business permit is already final (endorsement stays).</summary>
    Public Property FinalPermitWarning As String
End Class

''' <summary>
''' Real Property Tax rules (Assessor / Treasurer). Screens call this class.
'''   - Annual tax = basic (rpt_basic_rate) + SEF (rpt_sef_rate) of the assessed value, e.g. ₱40,000 -> ₱400 + ₱400 = ₱800.
'''   - Payable quarterly; each quarter is due at the end of the quarter (Mar 31, Jun 30, Sep 30, Dec 31).
'''   - Walk-in payments: a quarter can be paid in several parts. A quarter is Paid only when its principal is fully paid.
'''   - Late payment: rpt_penalty_rate_monthly per month late (a part of a month counts), at most rpt_penalty_max,
'''     on the REMAINING principal: each peso is charged the months it stayed unpaid (ComputePenaltyDue).
'''   - A payment goes to the penalty first, then to principal (AllocatePayment / PenaltyFirst).
'''   - Quarters are paid in order. The Official Receipt number is typed from the AF 51 booklet (unique).
'''   - Tax Clearance is "Issued" only when every quarter already due is fully paid; then the RPT endorsement on the
'''     business permit application is set to Endorsed (in the same transaction as the payment).
'''   - Payments are never deleted: a mistake is VOIDED (newest payment first) with a reason.
''' Assessor and Admin make changes; an Owner sees only their own business (read-only). Every change is audited.
''' </summary>
Public NotInheritable Class RptService

    Private Sub New()
    End Sub

    Public Shared ReadOnly PropertyTypes As String() = {"Land", "Building", "Machinery"}
    Public Shared ReadOnly Classifications As String() = {"Commercial", "Residential", "Industrial", "Agricultural"}

    Public Const NotAssessed As String = "Not Assessed"
    Public Const ClearanceIssued As String = "Issued"
    Public Const ClearanceNotIssued As String = "Not Issued"
    Public Const Voided As String = "Voided"
    Public Const Active As String = "Active"

    Public Const MaxOrNoLength As Integer = 30          ' rpt_payments.or_no
    Public Const MaxReferenceLength As Integer = 50     ' rpt_payments.reference_no
    Public Const MaxRemarksLength As Integer = 255      ' rpt_payments.remarks
    Public Const MaxVoidReasonLength As Integer = 200

    ''' <summary>
    ''' THE PAYMENT ORDER RULE (one place): True = a payment pays the penalty due first, then the principal.
    ''' To apply payments to principal first instead, set this to False. Note: a quarter counts as Paid as soon as
    ''' its principal is fully paid, so with principal first any penalty still unpaid at that moment is not collected.
    ''' </summary>
    Public Const PenaltyFirst As Boolean = True

    Private Const PropertiesTable As String = "properties"
    Private Const AssessmentsTable As String = "rpt_assessments"
    Private Const PaymentsTable As String = "rpt_payments"

    ' ==================== Rates (settings table) ====================

    Public Shared ReadOnly Property BasicRate As Decimal
        Get
            Return SettingsRepository.GetDecimal("rpt_basic_rate", 0.01D)
        End Get
    End Property

    Public Shared ReadOnly Property SefRate As Decimal
        Get
            Return SettingsRepository.GetDecimal("rpt_sef_rate", 0.01D)
        End Get
    End Property

    Public Shared ReadOnly Property PenaltyRateMonthly As Decimal
        Get
            Return SettingsRepository.GetDecimal("rpt_penalty_rate_monthly", 0.02D)
        End Get
    End Property

    Public Shared ReadOnly Property PenaltyMax As Decimal
        Get
            Return SettingsRepository.GetDecimal("rpt_penalty_max", 0.72D)
        End Get
    End Property

    ''' <summary>Assessments can be generated for last year, this year and next year.</summary>
    Public Shared ReadOnly Property MinTaxYear As Integer
        Get
            Return Date.Today.Year - 1
        End Get
    End Property

    Public Shared ReadOnly Property MaxTaxYear As Integer
        Get
            Return Date.Today.Year + 1
        End Get
    End Property

    ' ==================== Tax math ====================

    ''' <summary>Basic tax, SEF and annual total for an assessed value (not saved).</summary>
    Public Shared Function ComputeTax(assessedValue As Decimal) As RptAssessment
        Dim basic = Math.Round(assessedValue * BasicRate, 2, MidpointRounding.AwayFromZero)
        Dim sef = Math.Round(assessedValue * SefRate, 2, MidpointRounding.AwayFromZero)
        Return New RptAssessment With {.BasicTax = basic, .SefTax = sef, .TotalDue = basic + sef}
    End Function

    ''' <summary>A quarter's share of the annual tax. Q4 takes the centavo remainder so the 4 quarters add up exactly.</summary>
    Public Shared Function GetQuarterAmount(totalDue As Decimal, quarter As Integer) As Decimal
        Dim share = Math.Round(totalDue / 4D, 2, MidpointRounding.AwayFromZero)
        Return If(quarter = 4, totalDue - share * 3, share)
    End Function

    ''' <summary>Whole months late (a fraction of a month counts as a month). 0 = on time.</summary>
    Public Shared Function GetMonthsLate(dueDate As Date, payDate As Date) As Integer
        If payDate.Date <= dueDate.Date Then Return 0
        Dim months = (payDate.Year - dueDate.Year) * 12 + payDate.Month - dueDate.Month
        If payDate.Day > dueDate.Day Then months += 1
        Return Math.Max(1, months)
    End Function

    ''' <summary>Penalty rate for paying on payDate: monthly rate x months late, capped at rpt_penalty_max.</summary>
    Public Shared Function GetPenaltyRate(dueDate As Date, payDate As Date) As Decimal
        Return Math.Min(PenaltyRateMonthly * GetMonthsLate(dueDate, payDate), PenaltyMax)
    End Function

    ''' <summary>Penalty on an amount of principal unpaid until payDate (₱125 due Jun 30 paid Oct 6 -> 4 months x 2% = ₱10.00).</summary>
    Public Shared Function ComputePenalty(principal As Decimal, dueDate As Date, payDate As Date) As Decimal
        Return Math.Round(principal * GetPenaltyRate(dueDate, payDate), 2, MidpointRounding.AwayFromZero)
    End Function

    ''' <summary>
    ''' THE PENALTY RULE (one place): penalty still due on a quarter on "asOf".
    ''' Each peso of principal is charged the penalty rate for the months it stayed unpaid:
    '''   principal paid by each earlier payment x the rate on that payment's date
    ''' + principal still unpaid x the rate on asOf
    ''' - penalty already paid.
    ''' Example: ₱100 due Mar 31. Jun 15 (6%): ₱50 paid -> ₱6 penalty + ₱44 principal. Aug 10 (10%):
    ''' 44 x 6% + 56 x 10% - 6 = ₱2.24 still due. Never below 0.
    ''' </summary>
    Public Shared Function ComputePenaltyDue(quarterAmount As Decimal, dueDate As Date,
                                             payments As IEnumerable(Of RptPayment), asOf As Date) As Decimal
        Dim charged = 0D
        Dim principalPaid = 0D
        Dim penaltyPaid = 0D
        For Each p In payments
            charged += ComputePenalty(p.AmountPaid, dueDate, p.PaymentDate)
            principalPaid += p.AmountPaid
            penaltyPaid += p.Penalty
        Next
        Dim remaining = Math.Max(0D, quarterAmount - principalPaid)
        If remaining = 0D Then Return 0D               ' a fully paid quarter owes nothing more
        charged += ComputePenalty(remaining, dueDate, asOf)
        Return Math.Max(0D, charged - penaltyPaid)
    End Function

    ''' <summary>THE PAYMENT ORDER RULE (see PenaltyFirst): how a payment amount is split between penalty and principal.</summary>
    Public Shared Function AllocatePayment(amount As Decimal, penaltyDue As Decimal, principalDue As Decimal) As PaymentSplit
        Dim split As New PaymentSplit
        amount = Math.Max(0D, amount)
        If PenaltyFirst Then
            split.ToPenalty = Math.Min(amount, penaltyDue)
            split.ToPrincipal = Math.Min(amount - split.ToPenalty, principalDue)
        Else
            split.ToPrincipal = Math.Min(amount, principalDue)
            split.ToPenalty = Math.Min(amount - split.ToPrincipal, penaltyDue)
        End If
        Return split
    End Function

    ''' <summary>Live numbers for paying "amount" on a quarter on "payDate" (nothing is saved).</summary>
    Public Shared Function PreviewPayment(q As RptQuarter, payDate As Date, amount As Decimal) As PaymentPreview
        Dim penaltyDue = ComputePenaltyDue(q.Amount, q.DueDate, q.Payments, payDate)
        Dim preview As New PaymentPreview With {
            .QuarterAmount = q.Amount, .AlreadyPaid = q.TotalPaid, .RemainingPrincipal = q.RemainingPrincipal,
            .MonthsLate = GetMonthsLate(q.DueDate, payDate), .PenaltyRate = GetPenaltyRate(q.DueDate, payDate),
            .PenaltyDue = penaltyDue, .TotalDue = q.RemainingPrincipal + penaltyDue, .Amount = amount
        }
        preview.Split = AllocatePayment(Math.Min(amount, preview.TotalDue), penaltyDue, q.RemainingPrincipal)
        Return preview
    End Function

    ' ==================== Reading ====================

    ''' <summary>Properties of a business (empty if the user may not see it).</summary>
    Public Shared Function GetProperties(businessId As Integer) As List(Of RealProperty)
        If Not AccessService.CanSeeBusiness(businessId) Then Return New List(Of RealProperty)
        Return PropertyRepository.GetByBusinessId(businessId)
    End Function

    ''' <summary>Years for the ledger selector: last/this/next year plus any year already assessed, newest first.</summary>
    Public Shared Function GetLedgerYears(businessId As Integer) As List(Of Integer)
        Dim years = Enumerable.Range(MinTaxYear, MaxTaxYear - MinTaxYear + 1).ToList()
        If AccessService.CanSeeBusiness(businessId) Then
            years.AddRange(RptRepository.GetAssessmentsByBusinessId(businessId).Select(Function(a) a.TaxYear))
        End If
        Return years.Distinct().OrderByDescending(Function(y) y).ToList()
    End Function

    ''' <summary>One ledger row per property for the tax year.</summary>
    Public Shared Function GetLedger(businessId As Integer, taxYear As Integer) As List(Of RptLedgerRow)
        Dim props = GetProperties(businessId)
        If props.Count = 0 Then Return New List(Of RptLedgerRow)
        Dim assessments = RptRepository.GetAssessmentsByBusinessId(businessId).Where(Function(a) a.TaxYear = taxYear).ToList()
        Dim payments = ActiveOnly(RptRepository.GetPaymentsByBusinessId(businessId))
        Return props.Select(
            Function(p)
                Dim a = assessments.FirstOrDefault(Function(x) x.PropertyId = p.PropertyId)
                Return New RptLedgerRow With {
                    .RealProperty = p, .Assessment = a,
                    .Quarters = If(a Is Nothing, New List(Of RptQuarter)(), BuildQuarters(p, a, payments))
                }
            End Function).ToList()
    End Function

    ''' <summary>
    ''' Every quarter of every assessment of the business (all years), oldest first.
    ''' excludePaymentId: work it out as if that payment were voided (to preview a void).
    ''' </summary>
    Public Shared Function GetAllQuarters(businessId As Integer, Optional excludePaymentId As Integer = 0) As List(Of RptQuarter)
        Dim props = GetProperties(businessId)
        If props.Count = 0 Then Return New List(Of RptQuarter)
        Dim payments = ActiveOnly(RptRepository.GetPaymentsByBusinessId(businessId)).
                       Where(Function(x) x.PaymentId <> excludePaymentId).ToList()
        Dim result As New List(Of RptQuarter)
        For Each a In RptRepository.GetAssessmentsByBusinessId(businessId).OrderBy(Function(x) x.TaxYear)
            Dim p = props.FirstOrDefault(Function(x) x.PropertyId = a.PropertyId)
            If p IsNot Nothing Then result.AddRange(BuildQuarters(p, a, payments))
        Next
        Return result
    End Function

    Private Shared Function ActiveOnly(payments As List(Of RptPayment)) As List(Of RptPayment)
        Return payments.Where(Function(x) Not x.IsVoided).ToList()
    End Function

    Private Shared Function BuildQuarters(p As RealProperty, a As RptAssessment, payments As List(Of RptPayment)) As List(Of RptQuarter)
        Dim list As New List(Of RptQuarter)
        For quarterNo = 1 To 4
            Dim qNo = quarterNo
            Dim q As New RptQuarter With {
                .AssessmentId = a.AssessmentId, .PropertyId = p.PropertyId, .Pin = p.Pin, .TaxYear = a.TaxYear,
                .Quarter = qNo, .DueDate = StatusService.GetQuarterDueDate(a.TaxYear, qNo),
                .Amount = GetQuarterAmount(a.TotalDue, qNo),
                .Payments = payments.Where(Function(x) x.AssessmentId = a.AssessmentId AndAlso x.Quarter = qNo).
                                     OrderBy(Function(x) x.PaymentId).ToList()
            }
            q.PenaltyDue = ComputePenaltyDue(q.Amount, q.DueDate, q.Payments, Date.Today)
            q.Status = StatusService.GetQuarterStatus(q.DueDate, q.IsPaid, q.IsPartiallyPaid)
            list.Add(q)
        Next
        Return list
    End Function

    ''' <summary>What is left to pay on the year's assessments if paid today (remaining principal + penalties).</summary>
    Public Shared Function GetTotalDue(businessId As Integer, taxYear As Integer) As Decimal
        Return GetLedger(businessId, taxYear).Sum(Function(r) r.Balance)
    End Function

    ''' <summary>Quarters past their due date whose principal is not fully paid (all years), oldest first.</summary>
    Public Shared Function GetOverdueQuarters(businessId As Integer) As List(Of RptQuarter)
        Return GetAllQuarters(businessId).Where(Function(q) q.IsOverdue).ToList()
    End Function

    ''' <summary>What the overdue quarters cost if paid today (remaining principal + penalties).</summary>
    Public Shared Function GetOverdueAmount(businessId As Integer) As Decimal
        Return GetOverdueQuarters(businessId).Sum(Function(q) q.BalanceDue)
    End Function

    ''' <summary>"Issued" when every quarter already due is fully paid, otherwise "Not Issued".</summary>
    Public Shared Function GetTaxClearanceStatus(businessId As Integer) As String
        Return If(GetOverdueQuarters(businessId).Count = 0, ClearanceIssued, ClearanceNotIssued)
    End Function

    ''' <summary>Why the tax clearance is not issued ("2 overdue quarters: Q2, Q3 2026"), or a short OK note.</summary>
    Public Shared Function GetTaxClearanceNote(businessId As Integer) As String
        Dim overdue = GetOverdueQuarters(businessId)
        If overdue.Count = 0 Then
            Return If(GetAllQuarters(businessId).Count = 0, "no assessments on record", "all due quarters paid")
        End If
        Dim labels = overdue.Select(Function(q) q.Label).Distinct().ToList()
        Return overdue.Count & " overdue quarter" & If(overdue.Count = 1, "", "s") & ": " & String.Join(", ", labels)
    End Function

    ''' <summary>Every payment of a property (all years, voided ones too), newest first. Empty if the user may not see it.</summary>
    Public Shared Function GetPaymentHistory(propertyId As Integer) As List(Of RptPayment)
        Dim p = PropertyRepository.GetById(propertyId)
        If p Is Nothing OrElse Not AccessService.CanSeeBusiness(p.BusinessId) Then Return New List(Of RptPayment)
        Return RptRepository.GetPaymentsByBusinessId(p.BusinessId).
               Where(Function(x) x.PropertyId = propertyId).
               OrderByDescending(Function(x) x.PaymentDate).ThenByDescending(Function(x) x.PaymentId).ToList()
    End Function

    ''' <summary>Every payment of a business (all properties and years, voided ones too). Empty if the user may not see it.</summary>
    Public Shared Function GetBusinessPayments(businessId As Integer) As List(Of RptPayment)
        If Not AccessService.CanSeeBusiness(businessId) Then Return New List(Of RptPayment)
        Return RptRepository.GetPaymentsByBusinessId(businessId)
    End Function

    ''' <summary>
    ''' What was still owed on the payment's quarter right after it (for the receipt):
    ''' replays the quarter's earlier active payments. assessmentPayments = payments of the same assessment.
    ''' </summary>
    Public Shared Function GetBalanceAfter(pay As RptPayment, assessmentPayments As IEnumerable(Of RptPayment),
                                           annualTotal As Decimal) As Decimal
        Dim earlier = assessmentPayments.Where(Function(x) Not x.IsVoided AndAlso x.Quarter = pay.Quarter AndAlso
                                                           x.PaymentId < pay.PaymentId).ToList()
        Dim amount = GetQuarterAmount(annualTotal, pay.Quarter)
        Dim dueDate = StatusService.GetQuarterDueDate(pay.TaxYear, pay.Quarter)
        Dim remaining = Math.Max(0D, amount - earlier.Sum(Function(x) x.AmountPaid))
        Dim penaltyDue = ComputePenaltyDue(amount, dueDate, earlier, pay.PaymentDate)
        Return Math.Max(0D, remaining + penaltyDue - pay.Total)
    End Function

    ' ==================== Properties ====================

    ''' <summary>Checks a property. Keys: "pin", "td", "location", "type", "class", "value".</summary>
    Public Shared Function ValidateProperty(p As RealProperty) As Dictionary(Of String, String)
        Dim errors As New Dictionary(Of String, String)
        Dim pin = If(p.Pin, "").Trim()
        Dim td = If(p.TdNo, "").Trim()
        If pin = "" Then
            errors("pin") = "Enter the Property Index Number (PIN)."
        ElseIf pin.Length > 30 Then
            errors("pin") = "The PIN can be at most 30 characters."
        ElseIf PropertyRepository.PinExists(pin, p.PropertyId) Then
            errors("pin") = "Another property already uses this PIN."
        End If
        If td = "" Then
            errors("td") = "Enter the Tax Declaration No."
        ElseIf td.Length > 30 Then
            errors("td") = "The TD No. can be at most 30 characters."
        ElseIf PropertyRepository.TdNoExists(td, p.PropertyId) Then
            errors("td") = "Another property already uses this TD No."
        End If
        If String.IsNullOrWhiteSpace(p.Location) Then
            errors("location") = "Enter the property location."
        ElseIf p.Location.Trim().Length > 255 Then
            errors("location") = "The location can be at most 255 characters."
        End If
        If Not PropertyTypes.Contains(p.PropertyType) Then errors("type") = "Choose Land, Building or Machinery."
        If Not Classifications.Contains(p.Classification) Then errors("class") = "Choose a classification."
        If p.AssessedValue <= 0 Then
            errors("value") = "Enter an assessed value greater than zero."
        ElseIf p.AssessedValue > 999999999999D Then
            errors("value") = "The assessed value is too large."
        End If
        Return errors
    End Function

    ''' <summary>Adds a property (Assessor / Admin). Returns an error or Nothing (p.PropertyId is set).</summary>
    Public Shared Function AddProperty(p As RealProperty) As String
        If Not AccessService.CanManage(AppScreen.RealPropertyTax) Then Return "You are not allowed to add properties."
        If Not AccessService.CanSeeBusiness(p.BusinessId) Then Return "Business not found."
        Dim errors = ValidateProperty(p)
        If errors.Count > 0 Then Return errors.Values.First()
        Dim newId = PropertyRepository.Insert(p)
        If newId <= 0 Then Return "The property could not be saved."
        p.PropertyId = newId
        AuditService.LogInsert(PropertiesTable, newId, "Added property PIN " & p.Pin.Trim() & " (" & p.PropertyType & ", " &
                               UiHelper.FormatMoney(p.AssessedValue) & ")")
        Return Nothing
    End Function

    ''' <summary>
    ''' Edits a property (Assessor / Admin). Existing assessments keep their amounts; a new
    ''' assessed value applies to assessments generated afterwards. Returns an error or Nothing.
    ''' </summary>
    Public Shared Function UpdateProperty(p As RealProperty) As String
        If Not AccessService.CanManage(AppScreen.RealPropertyTax) Then Return "You are not allowed to edit properties."
        Dim existing = PropertyRepository.GetById(p.PropertyId)
        If existing Is Nothing OrElse existing.BusinessId <> p.BusinessId Then Return "Property not found."
        Dim errors = ValidateProperty(p)
        If errors.Count > 0 Then Return errors.Values.First()
        If Not PropertyRepository.Update(p) Then Return "The property could not be saved."
        Dim changes As New List(Of String)
        If existing.AssessedValue <> p.AssessedValue Then
            changes.Add("assessed value " & UiHelper.FormatMoney(existing.AssessedValue) & " -> " & UiHelper.FormatMoney(p.AssessedValue))
        End If
        AuditService.LogUpdate(PropertiesTable, p.PropertyId, "Edited property PIN " & p.Pin.Trim() &
                               If(changes.Count > 0, " (" & String.Join(", ", changes) & ")", ""))
        Return Nothing
    End Function

    ' ==================== Assessments ====================

    ''' <summary>Why an assessment cannot be generated for the property and year (Nothing = it can).</summary>
    Public Shared Function GetAssessmentBlocker(propertyId As Integer, taxYear As Integer) As String
        If taxYear < MinTaxYear OrElse taxYear > MaxTaxYear Then
            Return "Assessments can be generated for " & MinTaxYear & " to " & MaxTaxYear & " only."
        End If
        If RptRepository.GetAssessmentsByPropertyId(propertyId).Any(Function(a) a.TaxYear = taxYear) Then
            Return "This property already has a " & taxYear & " assessment. To bill another year, pick it in the tax year box."
        End If
        Return Nothing
    End Function

    ''' <summary>Creates the year's assessment from the current assessed value (Assessor / Admin). Returns an error or Nothing.</summary>
    Public Shared Function GenerateAssessment(propertyId As Integer, taxYear As Integer) As String
        If Not AccessService.CanManage(AppScreen.RealPropertyTax) Then Return "You are not allowed to generate assessments."
        Dim p = PropertyRepository.GetById(propertyId)
        If p Is Nothing Then Return "Property not found."
        Dim blocker = GetAssessmentBlocker(propertyId, taxYear)
        If blocker IsNot Nothing Then Return blocker
        Dim a = ComputeTax(p.AssessedValue)
        a.PropertyId = propertyId
        a.TaxYear = taxYear
        Dim newId = RptRepository.InsertAssessment(a)
        If newId <= 0 Then Return "The assessment could not be saved."
        AuditService.LogInsert(AssessmentsTable, newId, taxYear & " assessment for PIN " & p.Pin & ": basic " &
                               UiHelper.FormatMoney(a.BasicTax) & " + SEF " & UiHelper.FormatMoney(a.SefTax) & " = " &
                               UiHelper.FormatMoney(a.TotalDue))
        Return Nothing
    End Function

    ' ==================== Payments ====================

    ''' <summary>Why a quarter cannot be paid (Nothing = it can). Quarters are paid in order.</summary>
    Public Shared Function GetPaymentBlocker(row As RptLedgerRow, quarter As Integer) As String
        If row Is Nothing OrElse Not row.IsAssessed Then Return "Generate the assessment first."
        Dim q = row.Quarters.FirstOrDefault(Function(x) x.Quarter = quarter)
        If q Is Nothing Then Return "Choose a quarter from 1 to 4."
        If q.IsPaid Then Return "Q" & quarter & " is already fully paid (" & q.LastPayment?.OrNo & ")."
        Dim nextQ = row.NextPayable
        If nextQ IsNot Nothing AndAlso nextQ.Quarter < quarter Then
            Return "Pay Q" & nextQ.Quarter & " in full first (quarters are paid in order)."
        End If
        If row.Assessment.TaxYear > Date.Today.Year Then
            Return row.Assessment.TaxYear & " payments start on Jan 1, " & row.Assessment.TaxYear & "."
        End If
        Return Nothing
    End Function

    ''' <summary>The earliest allowed payment date: Jan 1 of the tax year, or the last active payment of the bill.</summary>
    Public Shared Function GetEarliestPaymentDate(row As RptLedgerRow) As Date
        Dim jan1 = New Date(row.Assessment.TaxYear, 1, 1)
        Dim last = row.ActivePayments.Select(Function(x) x.PaymentDate.Date).DefaultIfEmpty(jan1).Max()
        Return If(last > jan1, last, jan1)
    End Function

    ''' <summary>
    ''' Checks what the cashier typed. Keys: "date", "amount", "method", "reference", "or", "remarks".
    ''' The OR number is checked against every payment (voided ones keep their number).
    ''' </summary>
    Public Shared Function ValidatePayment(row As RptLedgerRow, q As RptQuarter, input As RptPaymentInput) As Dictionary(Of String, String)
        Dim errors As New Dictionary(Of String, String)
        Dim payDate = input.PaymentDate.Date

        ' Date: not in the future, within the tax year or later, not before the bill's last payment
        If payDate > Date.Today Then
            errors("date") = "Cannot be in the future."
        ElseIf row IsNot Nothing AndAlso row.IsAssessed AndAlso payDate < GetEarliestPaymentDate(row) Then
            Dim earliest = GetEarliestPaymentDate(row)
            errors("date") = If(Not row.ActivePayments.Any(), "Must be in " & row.Assessment.TaxYear & " or later.",
                                "Not before " & UiHelper.FormatDate(earliest) & " (last payment).")
        End If

        ' Amount: more than 0, whole centavos, at most what is due on that date
        If input.Amount <= 0D Then
            errors("amount") = "Enter more than 0."
        ElseIf Math.Round(input.Amount, 2) <> input.Amount Then
            errors("amount") = "At most 2 decimals."
        ElseIf q IsNot Nothing AndAlso Not errors.ContainsKey("date") Then
            Dim due = PreviewPayment(q, payDate, input.Amount).TotalDue
            If input.Amount > due Then errors("amount") = "Balance is only " & UiHelper.FormatMoney(due) & "."
        End If

        ' Method + reference
        If Not PaymentMethods.All.Contains(input.Method) Then
            errors("method") = "Choose how it was paid."
        ElseIf PaymentMethods.NeedsReference(input.Method) Then
            Dim reference = If(input.ReferenceNo, "").Trim()
            If reference = "" Then
                errors("reference") = "Enter the " & If(input.Method = PaymentMethods.Check, "check number.", "transaction reference no.")
            ElseIf reference.Length > MaxReferenceLength Then
                errors("reference") = "At most " & MaxReferenceLength & " characters."
            End If
        End If

        ' OR number from the AF 51 booklet
        Dim orNo = If(input.OrNo, "").Trim()
        If orNo = "" Then
            errors("or") = "Type the AF 51 OR number."
        ElseIf orNo.Length > MaxOrNoLength Then
            errors("or") = "At most " & MaxOrNoLength & " characters."
        ElseIf Not Text.RegularExpressions.Regex.IsMatch(orNo, "^[A-Za-z0-9-]+$") Then
            errors("or") = "Letters, numbers, dashes only."
        ElseIf RptRepository.OrNoExists(orNo) Then
            errors("or") = "OR " & orNo & " already used."
        End If

        If If(input.Remarks, "").Trim().Length > MaxRemarksLength Then errors("remarks") = "At most " & MaxRemarksLength & " characters."
        Return errors
    End Function

    ''' <summary>
    ''' Records a walk-in payment (Assessor / Admin) for the next unpaid quarter: the amount goes to the penalty due
    ''' first, then to principal (AllocatePayment). received_by = the logged-in user. When no quarter already due is
    ''' left unpaid afterwards, the RPT endorsement on the business permit application is set to Endorsed in the SAME
    ''' transaction as the payment.
    ''' </summary>
    Public Shared Function RecordPayment(assessmentId As Integer, quarter As Integer, input As RptPaymentInput) As RptPaymentResult
        Dim result As New RptPaymentResult
        If Not AccessService.CanManage(AppScreen.RealPropertyTax) Then
            result.ErrorMessage = "You are not allowed to record payments."
            Return result
        End If
        Dim a = RptRepository.GetAssessmentById(assessmentId)
        Dim p = If(a Is Nothing, Nothing, PropertyRepository.GetById(a.PropertyId))
        If p Is Nothing Then
            result.ErrorMessage = "Assessment not found."
            Return result
        End If
        Dim row = GetLedger(p.BusinessId, a.TaxYear).FirstOrDefault(Function(r) r.RealProperty.PropertyId = p.PropertyId)
        result.ErrorMessage = GetPaymentBlocker(row, quarter)
        If result.ErrorMessage IsNot Nothing Then Return result
        Dim q = row.Quarters.First(Function(x) x.Quarter = quarter)
        Dim errors = ValidatePayment(row, q, input)
        If errors.Count > 0 Then
            result.ErrorMessage = errors.Values.First()
            Return result
        End If

        Dim preview = PreviewPayment(q, input.PaymentDate.Date, input.Amount)
        Dim pay As New RptPayment With {
            .AssessmentId = assessmentId, .Quarter = quarter, .TaxYear = a.TaxYear, .PropertyId = p.PropertyId, .Pin = p.Pin,
            .AmountPaid = preview.Split.ToPrincipal, .Penalty = preview.Split.ToPenalty,
            .PaymentMethod = input.Method,
            .ReferenceNo = If(PaymentMethods.NeedsReference(input.Method), input.ReferenceNo.Trim(), ""),
            .OrNo = input.OrNo.Trim(), .PaymentDate = input.PaymentDate.Date, .ReceivedBy = Session.UserId,
            .Remarks = If(input.Remarks, "").Trim(), .ReceivedByName = Session.FullName
        }

        ' Will anything already due still be unpaid after this payment? If not, endorse RPT (same transaction).
        Dim stillOverdue = GetOverdueQuarters(p.BusinessId).Any(
            Function(o) Not (o.AssessmentId = assessmentId AndAlso o.Quarter = quarter AndAlso preview.CompletesQuarter))
        Dim endorsement = If(stillOverdue, Nothing,
                             BusinessPermitService.GetAutoEndorseChange(p.BusinessId, EndorsementOffices.RPT,
                                                                        "RPT dues paid as of " & Date.Today.ToString("MMM d, yyyy")))

        Dim newId = RptRepository.SavePayment(pay, row.ActivePayments.Count(), row.ActivePayments.Sum(Function(x) x.Total), endorsement)
        If newId = 0 Then
            result.ErrorMessage = "Another payment for this property was saved or voided while you were typing. " &
                                  "Close this window and open Record Payment again."
            Return result
        ElseIf newId < 0 Then
            result.ErrorMessage = "The payment could not be saved."
            Return result
        End If

        pay.PaymentId = newId
        result.Payment = pay
        AuditService.LogInsert(PaymentsTable, newId, "OR " & pay.OrNo & ": " & q.Label & " PIN " & p.Pin & " " &
                               UiHelper.FormatMoney(pay.Total) & " (" & pay.PaymentMethod & If(pay.ReferenceNo <> "", " " & pay.ReferenceNo, "") &
                               "; principal " & UiHelper.FormatMoney(pay.AmountPaid) & ", penalty " & UiHelper.FormatMoney(pay.Penalty) &
                               If(preview.CompletesQuarter, "; quarter fully paid", "; " & UiHelper.FormatMoney(preview.RemainingAfter) & " left") & ")")
        If endorsement IsNot Nothing Then
            BusinessPermitService.LogEndorsementChange(endorsement)
            result.EndorsedReference = endorsement.ReferenceNo
        End If
        Return result
    End Function

    ' ==================== Voiding ====================

    ''' <summary>
    ''' What voiding a payment would do, for the confirm step: the blocker (only Assessor / Admin, only the newest
    ''' active payment of its bill), whether its quarter becomes overdue again, and what happens to the RPT endorsement.
    ''' </summary>
    Public Shared Function GetVoidImpact(paymentId As Integer) As VoidImpact
        Dim impact As New VoidImpact
        Dim pay = RptRepository.GetPaymentById(paymentId)
        impact.Payment = pay
        If pay Is Nothing Then
            impact.Blocker = "Payment not found."
            Return impact
        End If
        Dim prop = PropertyRepository.GetById(pay.PropertyId)
        If Not AccessService.CanManage(AppScreen.RealPropertyTax) OrElse prop Is Nothing OrElse
           Not AccessService.CanSeeBusiness(prop.BusinessId) Then
            impact.Blocker = "Only the Assessor's Office or an Admin can void payments."
            Return impact
        End If
        If pay.IsVoided Then
            impact.Blocker = "OR " & pay.OrNo & " is already voided."
            Return impact
        End If
        Dim newest = RptRepository.GetPaymentsByAssessmentId(pay.AssessmentId).Where(Function(x) Not x.IsVoided).
                     OrderByDescending(Function(x) x.PaymentId).First()
        If newest.PaymentId <> pay.PaymentId Then
            impact.Blocker = "Only the newest payment of this " & pay.TaxYear & " bill can be voided. Void OR " & newest.OrNo &
                             " (" & newest.QuarterLabel & ") first."
            Return impact
        End If

        ' Does the quarter become overdue again?
        Dim isQuarter As Func(Of RptQuarter, Boolean) = Function(q) q.AssessmentId = pay.AssessmentId AndAlso q.Quarter = pay.Quarter
        Dim before = GetAllQuarters(prop.BusinessId).FirstOrDefault(isQuarter)
        Dim after = GetAllQuarters(prop.BusinessId, pay.PaymentId).FirstOrDefault(isQuarter)
        impact.BecomesOverdue = after IsNot Nothing AndAlso after.IsOverdue AndAlso Not (before?.IsOverdue).GetValueOrDefault()
        If impact.BecomesOverdue Then
            Dim latest = BusinessPermitService.GetLatestApplication(prop.BusinessId)
            If latest IsNot Nothing AndAlso BusinessPermitService.IsFinal(latest.Status) Then
                impact.FinalPermitWarning = "Business permit " & latest.ReferenceNo & " is already " & latest.Status &
                                            ", so its RPT endorsement cannot go back to Pending."
            Else
                impact.Endorsement = BusinessPermitService.GetEndorsementResetChange(prop.BusinessId, EndorsementOffices.RPT,
                    "OR " & pay.OrNo & " voided - " & pay.QuarterLabel & " is overdue again")
            End If
        End If
        Return impact
    End Function

    ''' <summary>
    ''' Voids a payment with a reason (Assessor / Admin). It is never deleted: it stays in the history as Voided,
    ''' no longer counts toward the balance, and its OR number stays used. Returns an error message, or Nothing.
    ''' </summary>
    Public Shared Function VoidPayment(paymentId As Integer, reason As String) As String
        reason = If(reason, "").Trim()
        If reason = "" Then Return "Enter the reason for voiding."
        If reason.Length > MaxVoidReasonLength Then Return "The reason can be at most " & MaxVoidReasonLength & " characters."
        Dim impact = GetVoidImpact(paymentId)
        If impact.Blocker IsNot Nothing Then Return impact.Blocker
        Dim pay = impact.Payment

        Select Case RptRepository.VoidPayment(pay, Session.UserId, reason, impact.Endorsement)
            Case 0 : Return "Another payment of this property was recorded or voided meanwhile. Refresh (F5) and try again."
            Case -1 : Return "The payment could not be voided."
        End Select

        AuditService.LogStatusChange(PaymentsTable, pay.PaymentId, Active, Voided,
            "OR " & pay.OrNo & ", " & pay.QuarterLabel & " PIN " & pay.Pin & ", " & UiHelper.FormatMoney(pay.Total) & ": " & reason &
            If(impact.FinalPermitWarning IsNot Nothing, " | Quarter overdue again; " & impact.FinalPermitWarning, ""))
        If impact.Endorsement IsNot Nothing Then BusinessPermitService.LogEndorsementChange(impact.Endorsement)
        Return Nothing
    End Function

End Class
