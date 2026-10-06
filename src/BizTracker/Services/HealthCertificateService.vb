''' <summary>One active employee together with their CURRENT health certificate (latest expiry).</summary>
Public Class EmployeeHealth
    Public Property Employee As Employee
    ''' <summary>Nothing when the employee has no certificate yet.</summary>
    Public Property Certificate As HealthCertificate

    ''' <summary>Valid / Expiring Soon / Expired / No Record - computed from today, never stored.</summary>
    Public ReadOnly Property Status As String
        Get
            Return StatusService.GetExpiryStatus(If(Certificate Is Nothing, CType(Nothing, Date?), Certificate.ExpiryDate))
        End Get
    End Property

    ''' <summary>Days until expiry (negative = expired), Nothing when there is no certificate.</summary>
    Public ReadOnly Property DaysLeft As Integer?
        Get
            If Certificate Is Nothing Then Return Nothing
            Return StatusService.DaysUntil(Certificate.ExpiryDate)
        End Get
    End Property

    ''' <summary>True for Expiring Soon, Expired and No Record (these appear in the "Renew Now" list).</summary>
    Public ReadOnly Property NeedsRenewal As Boolean
        Get
            Return Status <> StatusService.Valid
        End Get
    End Property
End Class

''' <summary>The numbers on the summary cards.</summary>
Public Class HealthSummary
    Public Property Total As Integer
    Public Property Valid As Integer
    Public Property ExpiringSoon As Integer
    Public Property Expired As Integer
    Public Property NoRecord As Integer
    ''' <summary>Valid / Total as a whole percent (0 when there are no employees).</summary>
    Public Property CompliancePercent As Integer
End Class

''' <summary>Result of renewing several employees at once.</summary>
Public Class RenewResult
    Public Property Issued As New List(Of HealthCertificate)
    ''' <summary>"Name: reason" for every employee that was not renewed.</summary>
    Public Property Skipped As New List(Of String)
    ''' <summary>Set when nothing could be saved (e.g. not allowed, invalid date, DB error).</summary>
    Public Property ErrorMessage As String
End Class

''' <summary>
''' Health certificate rules. Screens call this class; it calls the repositories.
'''   - A certificate is valid hc_validity_months (12) from its issue date.
'''   - Its status (Valid / Expiring Soon / Expired) is computed by StatusService.
'''   - Only Health and Admin can add employees or issue certificates; Owners can only view their own business.
''' Every change is written to audit_log.
''' </summary>
Public NotInheritable Class HealthCertificateService

    Private Sub New()
    End Sub

    Public Shared ReadOnly Categories As String() = {"Food Handler", "Non-Food"}

    Private Const EmployeesTable As String = "employees"
    Private Const CertificatesTable As String = "health_certificates"

    ' ==================== Reading ====================

    ''' <summary>Active employees of a business with their current certificate, sorted by name.</summary>
    Public Shared Function GetRoster(businessId As Integer) As List(Of EmployeeHealth)
        If Not AccessService.CanSeeBusiness(businessId) Then Return New List(Of EmployeeHealth)
        Dim certs = HealthCertificateRepository.GetLatestByBusinessId(businessId).
                    ToDictionary(Function(c) c.EmployeeId)
        Return EmployeeRepository.GetByBusinessId(businessId).
               Select(Function(e) New EmployeeHealth With {
                   .Employee = e,
                   .Certificate = If(certs.ContainsKey(e.EmployeeId), certs(e.EmployeeId), Nothing)}).
               ToList()
    End Function

    ''' <summary>Counts for the summary cards (all computed through StatusService).</summary>
    Public Shared Function GetSummary(roster As IEnumerable(Of EmployeeHealth)) As HealthSummary
        Dim list = roster.ToList()
        Dim counts = StatusService.CountByExpiryStatus(
            list.Select(Function(r) If(r.Certificate Is Nothing, CType(Nothing, Date?), r.Certificate.ExpiryDate)))
        Dim summary As New HealthSummary With {
            .Total = list.Count,
            .Valid = counts(StatusService.Valid),
            .ExpiringSoon = counts(StatusService.ExpiringSoon),
            .Expired = counts(StatusService.Expired),
            .NoRecord = counts(StatusService.NoRecord)
        }
        If summary.Total > 0 Then
            summary.CompliancePercent = CInt(Math.Round(summary.Valid * 100D / summary.Total, MidpointRounding.AwayFromZero))
        End If
        Return summary
    End Function

    ''' <summary>The "Renew Now" list: expired first, then no record, then expiring soonest.</summary>
    Public Shared Function GetRenewalList(roster As IEnumerable(Of EmployeeHealth)) As List(Of EmployeeHealth)
        Return roster.Where(Function(r) r.NeedsRenewal).
               OrderBy(Function(r) If(r.Status = StatusService.Expired, 0, If(r.Status = StatusService.NoRecord, 1, 2))).
               ThenBy(Function(r) If(r.DaysLeft, 0)).
               ThenBy(Function(r) r.Employee.FullName).ToList()
    End Function

    ''' <summary>Certificate history of one employee, newest first.</summary>
    Public Shared Function GetHistory(employee As Employee) As List(Of HealthCertificate)
        If employee Is Nothing OrElse Not AccessService.CanSeeBusiness(employee.BusinessId) Then Return New List(Of HealthCertificate)
        Return HealthCertificateRepository.GetByEmployeeId(employee.EmployeeId)
    End Function

    ''' <summary>Suggested "Issued by": the doctor on the newest certificate of the business.</summary>
    Public Shared Function GetDefaultIssuer(businessId As Integer) As String
        Dim latest = HealthCertificateRepository.GetLatestByBusinessId(businessId).
                     Where(Function(c) c.IssuedBy <> "").
                     OrderByDescending(Function(c) c.IssueDate).ThenByDescending(Function(c) c.CertId).FirstOrDefault()
        Return If(latest?.IssuedBy, "")
    End Function

    ' ==================== Employees ====================

    ''' <summary>
    ''' Checks an employee before saving. Returns field -> message (empty = valid).
    ''' Keys: "name", "position", "category".
    ''' </summary>
    Public Shared Function ValidateEmployee(e As Employee) As Dictionary(Of String, String)
        Dim errors As New Dictionary(Of String, String)
        Dim name = If(e.FullName, "").Trim()
        Dim position = If(e.Position, "").Trim()
        If name = "" Then
            errors("name") = "Enter the employee's full name."
        ElseIf name.Length < 3 Then
            errors("name") = "The name is too short."
        ElseIf name.Length > 150 Then
            errors("name") = "The name can be at most 150 characters."
        ElseIf EmployeeRepository.GetByBusinessId(e.BusinessId).Any(
                Function(x) x.EmployeeId <> e.EmployeeId AndAlso String.Equals(x.FullName.Trim(), name, StringComparison.OrdinalIgnoreCase)) Then
            errors("name") = "An employee with this name is already on the list."
        End If
        If position = "" Then
            errors("position") = "Enter the position, e.g. Baker or Cashier."
        ElseIf position.Length > 100 Then
            errors("position") = "The position can be at most 100 characters."
        End If
        If Not Categories.Contains(e.Category) Then errors("category") = "Choose Food Handler or Non-Food."
        Return errors
    End Function

    ''' <summary>Adds an employee. Returns the new employee_id, or -1.</summary>
    Public Shared Function AddEmployee(e As Employee) As Integer
        If Not CanChange(e.BusinessId) OrElse ValidateEmployee(e).Count > 0 Then Return -1
        e.IsActive = True
        Dim newId = EmployeeRepository.Insert(e)
        If newId <= 0 Then Return -1
        AuditService.LogInsert(EmployeesTable, newId, "Added employee " & e.FullName.Trim() & " (" & e.Category & ")")
        Return newId
    End Function

    Public Shared Function UpdateEmployee(e As Employee) As Boolean
        If Not CanChange(e.BusinessId) OrElse Not e.IsActive OrElse ValidateEmployee(e).Count > 0 Then Return False
        If Not EmployeeRepository.Update(e) Then Return False
        AuditService.LogUpdate(EmployeesTable, e.EmployeeId, "Edited employee " & e.FullName.Trim())
        Return True
    End Function

    ''' <summary>Soft delete: the employee leaves the list but the certificate history is kept.</summary>
    Public Shared Function DeactivateEmployee(e As Employee) As Boolean
        If Not CanChange(e.BusinessId) OrElse Not e.IsActive Then Return False
        If Not EmployeeRepository.Deactivate(e.EmployeeId) Then Return False
        AuditService.LogDelete(EmployeesTable, e.EmployeeId, "Deactivated employee " & e.FullName & " (soft delete)")
        Return True
    End Function

    ' ==================== Certificates ====================

    ''' <summary>
    ''' Why a certificate cannot be issued to this employee right now (Nothing = it can).
    ''' A certificate that is still Valid is not renewed early, to avoid duplicates.
    ''' </summary>
    Public Shared Function GetIssueBlocker(row As EmployeeHealth) As String
        If row Is Nothing Then Return "Select an employee first."
        If Not row.Employee.IsActive Then Return row.Employee.FullName & " is no longer active."
        If row.Status = StatusService.Valid Then
            Return row.Employee.FullName & "'s certificate is valid until " & row.Certificate.ExpiryDate.ToString("MMM d, yyyy", Globalization.CultureInfo.InvariantCulture) &
                   ". It can be renewed within " & StatusService.WarningDays & " days of expiry."
        End If
        Return Nothing
    End Function

    ''' <summary>Earliest issue date allowed: the new certificate must not already be expired.</summary>
    Public Shared Function GetMinIssueDate() As Date
        Dim months = SettingsRepository.GetInt("hc_validity_months", 12)
        Return Date.Today.AddMonths(-months).AddDays(1)
    End Function

    ''' <summary>
    ''' The next "count" certificate numbers for the issue date's year, e.g. HC-2026-00013, HC-2026-00014.
    ''' (They continue from the highest number already used that year.)
    ''' </summary>
    Public Shared Function PreviewNumbers(issueDate As Date, count As Integer) As List(Of String)
        Dim year = issueDate.Year
        Dim nextSeq = ReferenceNoRepository.GetLastSequence(ReferenceNoService.HealthCertificate, year) + 1
        Return Enumerable.Range(0, Math.Max(1, count)).
               Select(Function(i) ReferenceNoService.FormatNumber(ReferenceNoService.HealthCertificate, year, nextSeq + i)).ToList()
    End Function

    ''' <summary>Checks the issue form. Keys: "issue", "by".</summary>
    Public Shared Function ValidateIssue(issueDate As Date, issuedBy As String) As Dictionary(Of String, String)
        Dim errors As New Dictionary(Of String, String)
        If issueDate.Date > Date.Today Then
            errors("issue") = "The issue date cannot be in the future."
        ElseIf issueDate.Date < GetMinIssueDate() Then
            errors("issue") = "A certificate issued that early would already be expired."
        End If
        Dim by = If(issuedBy, "").Trim()
        If by = "" Then
            errors("by") = "Enter the name of the physician / health officer."
        ElseIf by.Length > 150 Then
            errors("by") = "The name can be at most 150 characters."
        End If
        Return errors
    End Function

    ''' <summary>
    ''' Issues (or renews) the certificate of one employee: number HC-YYYY-xxxxx,
    ''' expiry = issue date + hc_validity_months. Returns an error message, or Nothing on success.
    ''' </summary>
    Public Shared Function IssueCertificate(employeeId As Integer, issueDate As Date, issuedBy As String,
                                            Optional ByRef issued As HealthCertificate = Nothing) As String
        Dim result = RenewMany({employeeId}, issueDate, issuedBy)
        If result.ErrorMessage IsNot Nothing Then Return result.ErrorMessage
        If result.Issued.Count = 0 Then
            Dim reason = result.Skipped.FirstOrDefault()
            Return If(reason Is Nothing, "The certificate could not be issued.", reason.Substring(reason.IndexOf(": ", StringComparison.Ordinal) + 2))
        End If
        issued = result.Issued(0)
        Return Nothing
    End Function

    ''' <summary>
    ''' Issues certificates to several employees in one transaction (Renew Selected).
    ''' Employees whose certificate is still valid are skipped and listed in Skipped.
    ''' </summary>
    Public Shared Function RenewMany(employeeIds As IEnumerable(Of Integer), issueDate As Date, issuedBy As String) As RenewResult
        Dim result As New RenewResult
        If Not AccessService.CanManage(AppScreen.HealthCertificates) Then
            result.ErrorMessage = "You are not allowed to issue health certificates."
            Return result
        End If
        Dim errors = ValidateIssue(issueDate, issuedBy)
        If errors.Count > 0 Then
            result.ErrorMessage = errors.Values.First()
            Return result
        End If

        ' Decide who gets a certificate
        Dim targets As New List(Of Employee)
        For Each id In employeeIds.Distinct()
            Dim emp = EmployeeRepository.GetById(id)
            If emp Is Nothing OrElse Not AccessService.CanSeeBusiness(emp.BusinessId) Then Continue For
            Dim current = HealthCertificateRepository.GetByEmployeeId(id).FirstOrDefault()
            Dim blocker = GetIssueBlocker(New EmployeeHealth With {.Employee = emp, .Certificate = current})
            If blocker Is Nothing Then
                targets.Add(emp)
            Else
                result.Skipped.Add(emp.FullName & ": " & blocker)
            End If
        Next
        If targets.Count = 0 Then Return result

        Dim numbers = PreviewNumbers(issueDate, targets.Count)
        Dim expiry = StatusService.GetHealthCertificateExpiry(issueDate)
        Dim certs = targets.Select(Function(emp, i) New HealthCertificate With {
            .EmployeeId = emp.EmployeeId,
            .CertificateNo = numbers(i),
            .IssueDate = issueDate.Date, .ExpiryDate = expiry, .IssuedBy = issuedBy.Trim()}).ToList()

        If Not HealthCertificateRepository.InsertMany(certs) Then
            result.ErrorMessage = "The certificates could not be saved."
            Return result
        End If
        For i = 0 To certs.Count - 1
            AuditService.LogInsert(CertificatesTable, certs(i).CertId,
                "Issued " & certs(i).CertificateNo & " to " & targets(i).FullName & " (valid until " & expiry.ToString("yyyy-MM-dd") & ")")
        Next
        result.Issued.AddRange(certs)
        Return result
    End Function

    ' ==================== Helpers ====================

    Private Shared Function CanChange(businessId As Integer) As Boolean
        Return AccessService.CanManage(AppScreen.HealthCertificates) AndAlso AccessService.CanSeeBusiness(businessId)
    End Function

End Class
