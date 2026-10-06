''' <summary>One quarter of one property's assessment (computed for the ledger, never stored).</summary>
Public Class RptQuarter
    Public Property AssessmentId As Integer
    Public Property PropertyId As Integer
    Public Property Pin As String = ""
    Public Property TaxYear As Integer
    Public Property Quarter As Integer
    Public Property DueDate As Date
    ''' <summary>The quarter's share of the annual tax.</summary>
    Public Property Amount As Decimal
    ''' <summary>The payment of this quarter, or Nothing while unpaid.</summary>
    Public Property Payment As RptPayment
    ''' <summary>Paid / Overdue / Due Soon / Not Yet Due (from StatusService).</summary>
    Public Property Status As String = ""
    ''' <summary>Paid: the penalty that was charged. Unpaid: the penalty if paid today.</summary>
    Public Property Penalty As Decimal

    Public ReadOnly Property IsPaid As Boolean
        Get
            Return Payment IsNot Nothing
        End Get
    End Property

    Public ReadOnly Property IsOverdue As Boolean
        Get
            Return Status = StatusService.Overdue
        End Get
    End Property

    ''' <summary>"Q2 2026"</summary>
    Public ReadOnly Property Label As String
        Get
            Return "Q" & Quarter & " " & TaxYear
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

    Public ReadOnly Property PaidAmount As Decimal
        Get
            Return Quarters.Where(Function(q) q.IsPaid).Sum(Function(q) q.Payment.AmountPaid)
        End Get
    End Property

    ''' <summary>Unpaid tax of the year (without penalties).</summary>
    Public ReadOnly Property Balance As Decimal
        Get
            Return Quarters.Where(Function(q) Not q.IsPaid).Sum(Function(q) q.Amount)
        End Get
    End Property

    ''' <summary>The quarter that can be paid next (quarters are paid in order), or Nothing if all are paid.</summary>
    Public ReadOnly Property NextPayable As RptQuarter
        Get
            Return Quarters.FirstOrDefault(Function(q) Not q.IsPaid)
        End Get
    End Property

    ''' <summary>The most urgent quarter status: Overdue, Due Soon, Not Yet Due, Paid (or Not Assessed).</summary>
    Public ReadOnly Property WorstStatus As String
        Get
            If Not IsAssessed Then Return RptService.NotAssessed
            For Each s In {StatusService.Overdue, StatusService.DueSoon, StatusService.NotYetDue}
                If Quarters.Any(Function(q) q.Status = s) Then Return s
            Next
            Return StatusService.Paid
        End Get
    End Property
End Class

''' <summary>Outcome of RptService.RecordPayment.</summary>
Public Class RptPaymentResult
    ''' <summary>Nothing when the payment was saved.</summary>
    Public Property ErrorMessage As String
    Public Property Payment As RptPayment
    ''' <summary>Reference no. of the business permit whose RPT endorsement was set to Endorsed (or Nothing).</summary>
    Public Property EndorsedReference As String
End Class

''' <summary>
''' Real Property Tax rules (Assessor / Treasurer). Screens call this class.
'''   - Annual tax = basic (rpt_basic_rate) + SEF (rpt_sef_rate) of the assessed value, e.g. ₱40,000 -> ₱400 + ₱400 = ₱800.
'''   - Payable quarterly; each quarter is due at the end of the quarter (Mar 31, Jun 30, Sep 30, Dec 31).
'''   - Late payment: rpt_penalty_rate_monthly per month late (a fraction of a month counts), capped at rpt_penalty_max.
'''   - Quarters are paid in order. The Official Receipt number is OR-YYYY-xxxxx.
'''   - Tax Clearance is "Issued" only when no quarter whose due date has passed is unpaid; then the RPT
'''     endorsement on the business permit application is set to Endorsed.
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

    ''' <summary>Penalty on a quarter amount (₱125 due Jun 30 paid Oct 6 -> 4 months x 2% = ₱10.00).</summary>
    Public Shared Function ComputePenalty(quarterAmount As Decimal, dueDate As Date, payDate As Date) As Decimal
        Return Math.Round(quarterAmount * GetPenaltyRate(dueDate, payDate), 2, MidpointRounding.AwayFromZero)
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
        Dim payments = RptRepository.GetPaymentsByBusinessId(businessId)
        Return props.Select(
            Function(p)
                Dim a = assessments.FirstOrDefault(Function(x) x.PropertyId = p.PropertyId)
                Return New RptLedgerRow With {
                    .RealProperty = p, .Assessment = a,
                    .Quarters = If(a Is Nothing, New List(Of RptQuarter)(), BuildQuarters(p, a, payments))
                }
            End Function).ToList()
    End Function

    ''' <summary>Every quarter of every assessment of the business (all years), oldest first.</summary>
    Public Shared Function GetAllQuarters(businessId As Integer) As List(Of RptQuarter)
        Dim props = GetProperties(businessId)
        If props.Count = 0 Then Return New List(Of RptQuarter)
        Dim payments = RptRepository.GetPaymentsByBusinessId(businessId)
        Dim result As New List(Of RptQuarter)
        For Each a In RptRepository.GetAssessmentsByBusinessId(businessId).OrderBy(Function(x) x.TaxYear)
            Dim p = props.FirstOrDefault(Function(x) x.PropertyId = a.PropertyId)
            If p IsNot Nothing Then result.AddRange(BuildQuarters(p, a, payments))
        Next
        Return result
    End Function

    Private Shared Function BuildQuarters(p As RealProperty, a As RptAssessment, payments As List(Of RptPayment)) As List(Of RptQuarter)
        Dim list As New List(Of RptQuarter)
        For quarterNo = 1 To 4
            Dim q = quarterNo
            Dim pay = payments.FirstOrDefault(Function(x) x.AssessmentId = a.AssessmentId AndAlso x.Quarter = q)
            Dim due = StatusService.GetQuarterDueDate(a.TaxYear, q)
            Dim amount = GetQuarterAmount(a.TotalDue, q)
            list.Add(New RptQuarter With {
                .AssessmentId = a.AssessmentId, .PropertyId = p.PropertyId, .Pin = p.Pin, .TaxYear = a.TaxYear,
                .Quarter = q, .DueDate = due, .Amount = If(pay Is Nothing, amount, pay.AmountPaid), .Payment = pay,
                .Status = StatusService.GetDueStatus(due, pay IsNot Nothing),
                .Penalty = If(pay Is Nothing, ComputePenalty(amount, due, Date.Today), pay.Penalty)
            })
        Next
        Return list
    End Function

    ''' <summary>Sum of the unpaid quarter amounts of the year's assessments (penalties not included).</summary>
    Public Shared Function GetTotalDue(businessId As Integer, taxYear As Integer) As Decimal
        Return GetLedger(businessId, taxYear).Sum(Function(r) r.Balance)
    End Function

    ''' <summary>Unpaid quarters whose due date has passed (all years), oldest first.</summary>
    Public Shared Function GetOverdueQuarters(businessId As Integer) As List(Of RptQuarter)
        Return GetAllQuarters(businessId).Where(Function(q) q.IsOverdue).ToList()
    End Function

    ''' <summary>What the overdue quarters cost if paid today (tax + penalties).</summary>
    Public Shared Function GetOverdueAmount(businessId As Integer) As Decimal
        Return GetOverdueQuarters(businessId).Sum(Function(q) q.Amount + q.Penalty)
    End Function

    ''' <summary>"Issued" when nothing past due is unpaid, otherwise "Not Issued".</summary>
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
            Return "This property already has a " & taxYear & " assessment."
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
        If q.IsPaid Then Return "Q" & quarter & " is already paid (" & q.Payment.OrNo & ")."
        Dim nextQ = row.NextPayable
        If nextQ IsNot Nothing AndAlso nextQ.Quarter < quarter Then Return "Pay Q" & nextQ.Quarter & " first (quarters are paid in order)."
        If row.Assessment.TaxYear > Date.Today.Year Then
            Return row.Assessment.TaxYear & " payments start on Jan 1, " & row.Assessment.TaxYear & "."
        End If
        Return Nothing
    End Function

    ''' <summary>Checks the payment date. Key: "date".</summary>
    Public Shared Function ValidatePayment(row As RptLedgerRow, payDate As Date) As Dictionary(Of String, String)
        Dim errors As New Dictionary(Of String, String)
        If payDate.Date > Date.Today Then
            errors("date") = "The payment date cannot be in the future."
        ElseIf row IsNot Nothing AndAlso row.IsAssessed AndAlso payDate.Year < row.Assessment.TaxYear Then
            errors("date") = "The payment date must be in " & row.Assessment.TaxYear & " or later."
        End If
        Return errors
    End Function

    ''' <summary>
    ''' Records a quarter's payment (Assessor / Admin): amount = the quarter's share, penalty if late,
    ''' OR number generated, received by the logged-in user. When every quarter already due is paid,
    ''' the RPT endorsement on the business permit application is set to Endorsed.
    ''' </summary>
    Public Shared Function RecordPayment(assessmentId As Integer, quarter As Integer, payDate As Date) As RptPaymentResult
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
        If result.ErrorMessage Is Nothing Then
            Dim errors = ValidatePayment(row, payDate)
            If errors.Count > 0 Then result.ErrorMessage = errors.Values.First()
        End If
        If result.ErrorMessage IsNot Nothing Then Return result

        Dim q = row.Quarters.First(Function(x) x.Quarter = quarter)
        Dim pay As New RptPayment With {
            .AssessmentId = assessmentId, .Quarter = quarter, .AmountPaid = q.Amount,
            .Penalty = ComputePenalty(q.Amount, q.DueDate, payDate), .PaymentDate = payDate.Date,
            .OrNo = ReferenceNoService.GetNext(ReferenceNoService.OfficialReceipt), .ReceivedBy = Session.UserId
        }
        Dim newId = RptRepository.InsertPayment(pay)
        If newId <= 0 Then
            result.ErrorMessage = "The payment could not be saved."
            Return result
        End If
        pay.PaymentId = newId
        result.Payment = pay
        AuditService.LogInsert(PaymentsTable, newId, pay.OrNo & ": " & q.Label & " PIN " & p.Pin & " " &
                               UiHelper.FormatMoney(pay.AmountPaid) & If(pay.Penalty > 0, " + " & UiHelper.FormatMoney(pay.Penalty) & " penalty", ""))

        If GetOverdueQuarters(p.BusinessId).Count = 0 Then
            result.EndorsedReference = BusinessPermitService.AutoEndorse(p.BusinessId, EndorsementOffices.RPT,
                                                                         "RPT dues paid as of " & Date.Today.ToString("MMM d, yyyy"))
        End If
        Return result
    End Function

End Class
