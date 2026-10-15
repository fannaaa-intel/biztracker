Imports System.Security.Cryptography
Imports System.Text

''' <summary>Result of DocumentService.Verify.</summary>
Public Class VerifyResult
    ''' <summary>Valid / Expired / Not Found / Code Mismatch (or "" when the input was empty).</summary>
    Public Property Status As String = ""
    Public Property Message As String = ""
    ''' <summary>The document found (Nothing when not found).</summary>
    Public Property Document As IssuedDocument

    Public ReadOnly Property IsValid As Boolean
        Get
            Return Status = DocumentService.VerifyValid
        End Get
    End Property
End Class

''' <summary>
''' The Permit Vault: every issued document of a business, built from the module services
''' (nothing is stored - a document exists as long as its record is Issued / Approved / Paid).
''' Also the verification code printed on each document and the "Verify Document" check.
''' Owners see only their business; staff with access to the vault see every business.
''' </summary>
Public NotInheritable Class DocumentService

    Private Sub New()
    End Sub

    Public Const VerifyValid As String = "Valid"
    Public Const VerifyExpired As String = "Expired"
    Public Const VerifyNotFound As String = "Not Found"
    Public Const VerifyMismatch As String = "Code Mismatch"
    Public Const VerifyVoided As String = "Voided"

    Private Const CodeSecret As String = "BizTracker|LGU document"

    ''' <summary>Admin, BPLO and Owner (CLAUDE.md roles table).</summary>
    Public Shared Function CanView() As Boolean
        Return AccessService.CanAccess(AppScreen.PermitVault)
    End Function

    ''' <summary>
    ''' Who may print a document: vault users, and - for RPT official receipts - anyone who can open the
    ''' Real Property Tax screen (the Assessor prints the receipt right after recording a payment).
    ''' </summary>
    Public Shared Function CanPrint(d As IssuedDocument) As Boolean
        If d Is Nothing OrElse Not AccessService.CanSeeBusiness(d.BusinessId) Then Return False
        Return CanView() OrElse (d.Kind = DocumentKinds.OfficialReceipt AndAlso AccessService.CanAccess(AppScreen.RealPropertyTax))
    End Function

    ''' <summary>The official receipt of one RPT payment (voided ones too), or Nothing if the user may not see it.</summary>
    Public Shared Function GetOfficialReceipt(paymentId As Integer) As IssuedDocument
        Dim pay = RptRepository.GetPaymentById(paymentId)
        Dim prop = If(pay Is Nothing, Nothing, PropertyRepository.GetById(pay.PropertyId))
        If prop Is Nothing OrElse Not AccessService.CanSeeBusiness(prop.BusinessId) Then Return Nothing
        If Not CanView() AndAlso Not AccessService.CanAccess(AppScreen.RealPropertyTax) Then Return Nothing
        Dim b = BusinessRepository.GetById(prop.BusinessId)
        If b Is Nothing Then Return Nothing
        Dim list As New List(Of IssuedDocument)
        AddRptReceipts(b, RptService.GetProperties(b.BusinessId), list)
        Dim d = list.FirstOrDefault(Function(x) x.RelatedId = paymentId)
        If d IsNot Nothing Then
            d.BusinessId = b.BusinessId
            d.BusinessName = b.BusinessName
        End If
        Return d
    End Function

    ' ==================== Listing ====================

    ''' <summary>Issued documents of one business, newest first (empty if the user may not see it).</summary>
    Public Shared Function GetDocuments(businessId As Integer) As List(Of IssuedDocument)
        Dim list As New List(Of IssuedDocument)
        If Not CanView() OrElse Not AccessService.CanSeeBusiness(businessId) Then Return list
        Dim b = BusinessRepository.GetById(businessId)
        If b Is Nothing Then Return list

        AddBusinessPermitDocuments(b, list)
        AddSanitaryDocuments(b, list)
        AddHealthCertificates(b, list)
        AddRptDocuments(b, list)
        AddInspectionCertificates(b, list)
        AddConstructionClearances(b, list)

        For Each d In list
            d.BusinessId = b.BusinessId
            d.BusinessName = b.BusinessName
            If d.Holder = "" Then d.Holder = b.BusinessName
        Next
        Return list.OrderByDescending(Function(d) d.IssueDate).ThenBy(Function(d) d.ReferenceNo).ToList()
    End Function

    ''' <summary>Counts for the summary cards: valid (incl. Issued / Paid / Approved), expiring soon, expired.</summary>
    Public Shared Function CountByStatus(docs As IEnumerable(Of IssuedDocument)) As Dictionary(Of String, Integer)
        Dim counts As New Dictionary(Of String, Integer) From {
            {StatusService.Valid, 0}, {StatusService.ExpiringSoon, 0}, {StatusService.Expired, 0}, {RptService.Voided, 0}}
        For Each d In docs
            Select Case d.Status
                Case StatusService.ExpiringSoon : counts(StatusService.ExpiringSoon) += 1
                Case StatusService.Expired : counts(StatusService.Expired) += 1
                Case RptService.Voided : counts(RptService.Voided) += 1        ' a voided receipt is not a valid document
                Case Else : counts(StatusService.Valid) += 1
            End Select
        Next
        Return counts
    End Function

    Private Shared Sub AddBusinessPermitDocuments(b As Business, list As List(Of IssuedDocument))
        For Each p In BusinessPermitRepository.GetByBusinessId(b.BusinessId)
            ' Mayor's Permit: Issued applications only
            If p.Status = BusinessPermitService.Issued AndAlso p.MayorsPermitNo <> "" Then
                Dim d = NewDoc(DocumentKinds.MayorsPermit, "Mayor's Permit", p.MayorsPermitNo, If(p.DateIssued, p.DateFiled),
                               p.ValidUntil, StatusService.GetExpiryStatus(p.ValidUntil), p.PermitId, "business_permits", AppScreen.BusinessPermits)
                d.Fields("permit_year") = p.PermitYear.ToString()
                d.Fields("application_type") = p.ApplicationType
                d.Fields("application_ref") = p.ReferenceNo
                d.Fields("line_of_business") = b.LineOfBusiness
                d.Fields("business_type") = b.BusinessType
                d.Fields("tin") = If(b.Tin <> "", b.Tin, "—")
                d.Fields("dti_sec_no") = If(b.DtiSecNo <> "", b.DtiSecNo, "—")
                d.Fields("amount_paid") = UiHelper.FormatMoney(BusinessPermitService.GetTotalDue(p))
                For Each e In BusinessPermitRepository.GetEndorsements(p.PermitId)
                    d.Rows.Add({e.Office, e.Status, If(e.EndorsedAt.HasValue, UiHelper.FormatDate(e.EndorsedAt), "—")})
                Next
                list.Add(d)
            End If
            ' Tax Order of Payment: once the application is assessed
            If (p.Status = BusinessPermitService.Assessed OrElse p.Status = BusinessPermitService.Paid OrElse
                p.Status = BusinessPermitService.Issued) AndAlso p.AssessedAmount > 0 Then
                Dim paid = p.Status <> BusinessPermitService.Assessed
                Dim d = NewDoc(DocumentKinds.OrderOfPayment, "Tax Order of Payment", p.ReferenceNo, p.DateFiled, Nothing,
                               If(paid, "Paid", "Unpaid"), p.PermitId, "business_permits", AppScreen.BusinessPermits)
                d.Fields("permit_year") = p.PermitYear.ToString()
                d.Fields("application_type") = p.ApplicationType
                d.Fields("gross_receipts") = UiHelper.FormatMoney(p.GrossReceipts)
                d.Fields("total_due") = UiHelper.FormatMoney(BusinessPermitService.GetTotalDue(p))
                d.Fields("payment_status") = If(paid, "PAID", "UNPAID - please pay at the Municipal Treasurer's Office")
                d.Rows.Add({"Business tax and regulatory fees (" & p.ApplicationType & " " & p.PermitYear & ")", UiHelper.FormatMoney(p.AssessedAmount)})
                If p.Surcharge > 0 Then d.Rows.Add({"Surcharge (late renewal)", UiHelper.FormatMoney(p.Surcharge)})
                If p.Interest > 0 Then d.Rows.Add({"Interest", UiHelper.FormatMoney(p.Interest)})
                list.Add(d)
            End If
        Next
    End Sub

    Private Shared Sub AddSanitaryDocuments(b As Business, list As List(Of IssuedDocument))
        For Each p In SanitaryPermitService.GetPermits(b.BusinessId).Where(Function(x) x.Status = SanitaryPermitService.Issued)
            Dim d = NewDoc(DocumentKinds.SanitaryPermit, "Sanitary Permit to Operate", p.PermitNo, If(p.DateIssued, p.DateFiled),
                           p.ValidUntil, StatusService.GetExpiryStatus(p.ValidUntil), p.SanitaryId, "sanitary_permits", AppScreen.SanitaryPermits)
            d.Fields("category") = p.Category
            d.Fields("permit_year") = p.PermitYear.ToString()
            d.Fields("inspection_date") = If(p.InspectionDate.HasValue, UiHelper.FormatDate(p.InspectionDate), "—")
            d.Fields("inspection_score") = If(p.InspectionScore.HasValue, p.InspectionScore.Value & " / 100", "—")
            d.Fields("inspector_name") = If(p.InspectorName <> "", p.InspectorName, "—")
            list.Add(d)
        Next
    End Sub

    Private Shared Sub AddHealthCertificates(b As Business, list As List(Of IssuedDocument))
        For Each r In HealthCertificateService.GetRoster(b.BusinessId).Where(Function(x) x.Certificate IsNot Nothing)
            Dim c = r.Certificate
            Dim d = NewDoc(DocumentKinds.HealthCertificate, "Health Certificate", c.CertificateNo, c.IssueDate, c.ExpiryDate,
                           r.Status, c.CertId, "health_certificates", AppScreen.HealthCertificates)
            d.Holder = r.Employee.FullName
            d.Fields("employee_name") = r.Employee.FullName
            d.Fields("position") = r.Employee.Position
            d.Fields("category") = r.Employee.Category
            d.Fields("issued_by") = If(c.IssuedBy <> "", c.IssuedBy, "Municipal Health Officer")
            list.Add(d)
        Next
    End Sub

    Private Shared Sub AddRptDocuments(b As Business, list As List(Of IssuedDocument))
        Dim properties = RptService.GetProperties(b.BusinessId)
        If properties.Count = 0 Then Return
        AddRptReceipts(b, properties, list)
        ' Tax clearance: only while nothing is overdue
        If RptService.GetTaxClearanceStatus(b.BusinessId) = RptService.ClearanceIssued Then
            Dim year = Date.Today.Year
            Dim d = NewDoc(DocumentKinds.TaxClearance, "Real Property Tax Clearance", GetTaxClearanceNo(b.BusinessId, year), Date.Today,
                           Nothing, "Issued", b.BusinessId, "properties", AppScreen.RealPropertyTax)
            d.CodeSeed = DocumentKinds.TaxClearance & "|" & d.ReferenceNo & "|" & b.BusinessId & "|" & year   ' stable all year
            d.Fields("tax_year") = year.ToString()
            For Each p In properties
                d.Rows.Add({p.Pin, p.TdNo, p.Location, UiHelper.FormatMoney(p.AssessedValue)})
            Next
            list.Add(d)
        End If
    End Sub

    ''' <summary>Official receipts: one per RPT payment (partial payments too). Voided ones stay listed as "Voided".</summary>
    Private Shared Sub AddRptReceipts(b As Business, properties As List(Of RealProperty), list As List(Of IssuedDocument))
        Dim payments = RptService.GetBusinessPayments(b.BusinessId)
        Dim assessments = RptRepository.GetAssessmentsByBusinessId(b.BusinessId)
        For Each pay In payments
            Dim prop = properties.FirstOrDefault(Function(x) x.PropertyId = pay.PropertyId)
            Dim a = assessments.FirstOrDefault(Function(x) x.AssessmentId = pay.AssessmentId)
            If prop Is Nothing OrElse a Is Nothing Then Continue For
            Dim d = NewDoc(DocumentKinds.OfficialReceipt, "Official Receipt - Real Property Tax", pay.OrNo, pay.PaymentDate, Nothing,
                           If(pay.IsVoided, RptService.Voided, StatusService.Paid), pay.PaymentId, "rpt_payments", AppScreen.RealPropertyTax)
            d.Holder = "PIN " & prop.Pin & " · " & pay.QuarterLabel
            d.Fields("pin") = prop.Pin
            d.Fields("td_no") = prop.TdNo
            d.Fields("location") = prop.Location
            d.Fields("period") = pay.QuarterLabel
            d.Fields("total_paid") = UiHelper.FormatMoney(pay.Total)
            d.Fields("payment_method") = pay.PaymentMethod & If(pay.ReferenceNo <> "", " · Ref. " & pay.ReferenceNo, "")
            d.Fields("received_by") = If(pay.ReceivedByName <> "", pay.ReceivedByName, "Collecting Officer")
            Dim left = RptService.GetBalanceAfter(pay, payments.Where(Function(x) x.AssessmentId = pay.AssessmentId), a.TotalDue)
            d.Fields("balance_after") = If(left = 0D, "None - " & pay.QuarterLabel & " fully paid", UiHelper.FormatMoney(left) & " still due on " & pay.QuarterLabel)
            d.Fields("void_reason") = pay.VoidReason
            d.Fields("voided_at") = If(pay.VoidedAt.HasValue, UiHelper.FormatDate(pay.VoidedAt), "")
            d.Rows.Add({"Real property tax " & pay.QuarterLabel & " (basic + SEF)", UiHelper.FormatMoney(pay.AmountPaid)})
            d.Rows.Add({"Penalty (late payment)", UiHelper.FormatMoney(pay.Penalty)})
            list.Add(d)
        Next
    End Sub

    ''' <summary>Tax clearances are not stored: "TC-2026-00003" = year + business number.</summary>
    Public Shared Function GetTaxClearanceNo(businessId As Integer, year As Integer) As String
        Return ReferenceNoService.FormatNumber("TC", year, businessId)
    End Function

    Private Shared Sub AddInspectionCertificates(b As Business, list As List(Of IssuedDocument))
        For Each i In InspectionService.GetInspections(b.BusinessId).
                      Where(Function(x) x.Status = InspectionService.Completed AndAlso x.CertificateNo <> "")
            Dim items = InspectionService.GetItems(i)
            Dim lastDate = items.Where(Function(x) x.InspectedAt.HasValue).Select(Function(x) x.InspectedAt.Value.Date).DefaultIfEmpty(i.ScheduleDate.Date).Max()
            Dim validUntil = New Date(i.InspectionYear, 12, 31)
            Dim d = NewDoc(DocumentKinds.InspectionCertificate, "Annual Inspection Certificate", i.CertificateNo, lastDate, validUntil,
                           StatusService.GetExpiryStatus(validUntil), i.InspectionId, "inspections", AppScreen.AnnualInspections)
            d.Fields("inspection_ref") = i.ReferenceNo
            d.Fields("inspection_year") = i.InspectionYear.ToString()
            For Each x In items
                d.Rows.Add({x.Department, x.Result, If(x.InspectorName <> "", x.InspectorName, "—"),
                            If(x.InspectedAt.HasValue, UiHelper.FormatDate(x.InspectedAt), "—")})
            Next
            list.Add(d)
        Next
    End Sub

    Private Shared Sub AddConstructionClearances(b As Business, list As List(Of IssuedDocument))
        For Each p In ConstructionService.GetProjects(b.BusinessId)
            For Each c In ConstructionService.GetClearances(p).Where(Function(x) x.Status = ConstructionService.Approved AndAlso x.ClearanceNo <> "")
                Dim title = If(c.ClearanceType = ClearanceTypes.FSEC, "Fire Safety Evaluation Clearance", ConstructionService.GetClearanceTitle(c.ClearanceType))
                Dim d = NewDoc(DocumentKinds.ConstructionClearance, title, c.ClearanceNo,
                               If(c.ApprovedAt.HasValue, c.ApprovedAt.Value.Date, p.DateFiled), Nothing, ConstructionService.Approved,
                               c.ClearanceId, "construction_clearances", AppScreen.ConstructionPermits)
                d.Holder = p.ProjectTitle
                d.Fields("project_title") = p.ProjectTitle
                d.Fields("project_ref") = p.ReferenceNo
                d.Fields("project_type") = p.ProjectType
                d.Fields("estimated_cost") = If(p.EstimatedCost.HasValue, UiHelper.FormatMoney(p.EstimatedCost.Value), "—")
                d.Fields("approved_by") = If(c.ApprovedByName <> "", c.ApprovedByName, "—")
                d.Fields("remarks") = If(c.Remarks <> "", c.Remarks, "—")
                list.Add(d)
            Next
        Next
    End Sub

    Private Shared Function NewDoc(kind As String, title As String, referenceNo As String, issueDate As Date, validUntil As Date?,
                                   status As String, relatedId As Integer, tableName As String, screen As AppScreen) As IssuedDocument
        Return New IssuedDocument With {
            .Kind = kind, .Title = title, .ReferenceNo = referenceNo, .IssueDate = issueDate.Date, .ValidUntil = validUntil,
            .Status = status, .RelatedId = relatedId, .TableName = tableName, .Screen = screen,
            .CodeSeed = kind & "|" & referenceNo & "|" & issueDate.ToString("yyyyMMdd")
        }
    End Function

    ' ==================== Printing rules and verification ====================

    ''' <summary>Why the document cannot be printed (Nothing = it can). Expired documents must be renewed first.</summary>
    Public Shared Function GetPrintBlocker(d As IssuedDocument) As String
        If d Is Nothing Then Return "Select a document first."
        If d.Status = RptService.Voided Then
            Return "OR " & d.ReferenceNo & " was voided" & If(d.Fields.ContainsKey("voided_at"), " on " & d.Fields("voided_at"), "") &
                   ". A voided receipt cannot be printed."
        End If
        If d.Status = StatusService.Expired Then
            Return d.ReferenceNo & " expired on " & UiHelper.FormatDate(d.ValidUntil) & ". Renew it first - only valid documents can be printed."
        End If
        Return Nothing
    End Function

    ''' <summary>"7F3A2-9C01B": 10 characters of a SHA-256 hash of the document's details.</summary>
    Public Shared Function GetVerificationCode(d As IssuedDocument) As String
        Using sha = SHA256.Create()
            Dim hash = sha.ComputeHash(Encoding.UTF8.GetBytes(d.CodeSeed & "|" & d.BusinessId & "|" & CodeSecret))
            Dim hex = String.Concat(hash.Take(5).Select(Function(x) x.ToString("X2")))
            Return hex.Substring(0, 5) & "-" & hex.Substring(5, 5)
        End Using
    End Function

    ''' <summary>Finds an issued document by its reference number (among the businesses the user may see).</summary>
    Public Shared Function FindByReference(referenceNo As String) As IssuedDocument
        Dim target = If(referenceNo, "").Trim()
        If target = "" OrElse Not CanView() Then Return Nothing
        For Each b In BusinessRepository.GetAll().Where(Function(x) AccessService.CanSeeBusiness(x.BusinessId))
            Dim found = GetDocuments(b.BusinessId).FirstOrDefault(Function(d) String.Equals(d.ReferenceNo, target, StringComparison.OrdinalIgnoreCase))
            If found IsNot Nothing Then Return found
        Next
        Return Nothing
    End Function

    ''' <summary>
    ''' Checks a printed document: the reference number must belong to an issued document, the verification
    ''' code (optional) must match, and the document must not be expired.
    ''' </summary>
    Public Shared Function Verify(referenceNo As String, Optional code As String = "") As VerifyResult
        Dim result As New VerifyResult
        If If(referenceNo, "").Trim() = "" Then
            result.Message = "Enter the reference number printed on the document."
            Return result
        End If
        Dim d = FindByReference(referenceNo)
        If d Is Nothing Then
            result.Status = VerifyNotFound
            result.Message = "No issued document has the reference number " & referenceNo.Trim().ToUpperInvariant() & "." &
                             If(Session.IsOwner, " (Only your own business's documents can be checked.)", "")
            Return result
        End If
        result.Document = d
        Dim cleanCode = If(code, "").Trim().ToUpperInvariant()
        If cleanCode <> "" AndAlso cleanCode.Replace("-", "") <> GetVerificationCode(d).Replace("-", "") Then
            result.Status = VerifyMismatch
            result.Message = "The verification code does not match " & d.ReferenceNo & ". The printed copy may have been altered."
        ElseIf d.Status = RptService.Voided Then
            result.Status = VerifyVoided
            result.Message = "OR " & d.ReferenceNo & " is genuine but was VOIDED" &
                             If(d.Fields.ContainsKey("voided_at"), " on " & d.Fields("voided_at"), "") &
                             If(d.Fields.ContainsKey("void_reason") AndAlso d.Fields("void_reason") <> "", " (" & d.Fields("void_reason") & ")", "") &
                             ". It is not a valid receipt."
        ElseIf d.Status = StatusService.Expired Then
            result.Status = VerifyExpired
            result.Message = d.Title & " " & d.ReferenceNo & " is genuine but expired on " & UiHelper.FormatDate(d.ValidUntil) & "."
        Else
            result.Status = VerifyValid
            result.Message = d.Title & " " & d.ReferenceNo & " is genuine and valid" &
                             If(d.ValidUntil.HasValue, " until " & UiHelper.FormatDate(d.ValidUntil), "") & "."
        End If
        Return result
    End Function

End Class
