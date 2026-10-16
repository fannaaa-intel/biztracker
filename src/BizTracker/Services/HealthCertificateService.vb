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

''' <summary>
''' One row of the Employees Summary: who still works at a business (Revision 4B).
''' The certificate counts are for ACTIVE employees only (separated ones no longer need one).
''' </summary>
Public Class BusinessEmployeeSummary
    Public Property Business As Business
    Public Property Total As Integer
    Public Property Active As Integer
    Public Property Separated As Integer
    Public Property Valid As Integer
    Public Property ExpiringSoon As Integer
    Public Property Expired As Integer
    Public Property NoCertificate As Integer
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
'''   - An employee who leaves is SEPARATED (is_active = 0 + date_separated), never deleted. Separated employees
'''     are not on the roster, so they are not counted on the cards, the dashboard or in alerts.
''' Every change is written to audit_log.
''' </summary>
Public NotInheritable Class HealthCertificateService

    Private Sub New()
    End Sub

    Public Shared ReadOnly Categories As String() = {"Food Handler", "Non-Food"}

    ' Employment status (from is_active)
    Public Const EmploymentActive As String = "Active"
    Public Const EmploymentSeparated As String = "Separated"

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

    ' ==================== Employees Summary (Revision 4B) ====================

    Public Shared Function GetEmploymentStatus(e As Employee) As String
        Return If(e.IsActive, EmploymentActive, EmploymentSeparated)
    End Function

    ''' <summary>
    ''' Active + separated employees per business the user may see (Owner: their own only), by business name.
    ''' </summary>
    Public Shared Function GetEmployeeSummaries() As List(Of BusinessEmployeeSummary)
        Dim list As New List(Of BusinessEmployeeSummary)
        For Each b In BusinessRepository.GetAll().Where(Function(x) AccessService.CanSeeBusiness(x.BusinessId))
            Dim all = EmployeeRepository.GetByBusinessId(b.BusinessId, includeInactive:=True)
            Dim health = GetSummary(GetRoster(b.BusinessId))
            list.Add(New BusinessEmployeeSummary With {
                .Business = b, .Total = all.Count,
                .Active = all.AsEnumerable().Count(Function(e) e.IsActive),
                .Separated = all.AsEnumerable().Count(Function(e) Not e.IsActive),
                .Valid = health.Valid, .ExpiringSoon = health.ExpiringSoon,
                .Expired = health.Expired, .NoCertificate = health.NoRecord})
        Next
        Return list.OrderBy(Function(x) x.Business.BusinessName).ToList()
    End Function

    ''' <summary>
    ''' Every employee of a business (active first, then separated, by name) with their latest certificate.
    ''' For separated employees the certificate is history only.
    ''' </summary>
    Public Shared Function GetAllEmployees(businessId As Integer) As List(Of EmployeeHealth)
        If Not AccessService.CanSeeBusiness(businessId) Then Return New List(Of EmployeeHealth)
        Return EmployeeRepository.GetByBusinessId(businessId, includeInactive:=True).
               Select(Function(e) New EmployeeHealth With {
                   .Employee = e, .Certificate = HealthCertificateRepository.GetByEmployeeId(e.EmployeeId).FirstOrDefault()}).
               OrderBy(Function(r) If(r.Employee.IsActive, 0, 1)).ThenBy(Function(r) r.Employee.FullName).ToList()
    End Function

    ''' <summary>"3 yrs 4 mos" of service, up to the separation date (or today). Empty when the hire date is unknown.</summary>
    Public Shared Function GetLengthOfService(e As Employee, Optional today As Date? = Nothing) As String
        If Not e.DateHired.HasValue Then Return ""
        Dim until = If(e.DateSeparated, If(today, Date.Today)).Date
        Dim hired = e.DateHired.Value.Date
        Dim months = (until.Year - hired.Year) * 12 + until.Month - hired.Month
        If until.Day < hired.Day Then months -= 1
        months = Math.Max(0, months)
        If months = 0 Then Return "less than a month"
        Dim years = months \ 12
        Dim rest = months Mod 12
        Dim parts As New List(Of String)
        If years > 0 Then parts.Add(years & If(years = 1, " yr", " yrs"))
        If rest > 0 Then parts.Add(rest & If(rest = 1, " mo", " mos"))
        Return String.Join(" ", parts)
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
    ''' Keys: "name", "position", "category", "hired".
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
        If e.DateHired.HasValue Then
            If e.DateHired.Value.Date > Date.Today Then
                errors("hired") = "The hire date cannot be in the future."
            ElseIf e.DateHired.Value.Year < 1950 Then
                errors("hired") = "Enter a valid hire date."
            End If
        End If
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

    ''' <summary>Why the employee cannot be separated on this date (Nothing = OK).</summary>
    Public Shared Function ValidateSeparation(e As Employee, separatedOn As Date) As String
        If separatedOn.Date > Date.Today Then Return "The separation date cannot be in the future."
        If e.DateHired.HasValue AndAlso separatedOn.Date < e.DateHired.Value.Date Then
            Return "The separation date cannot be before the hire date (" & UiHelper.FormatDate(e.DateHired.Value) & ")."
        End If
        Return Nothing
    End Function

    ''' <summary>
    ''' Soft delete: the employee no longer works there (resigned / ended) as of separatedOn.
    ''' They leave the roster, but the certificate history is kept. Returns an error or Nothing.
    ''' </summary>
    Public Shared Function DeactivateEmployee(e As Employee, separatedOn As Date, Optional reason As String = "") As String
        If e Is Nothing OrElse Not CanChange(e.BusinessId) Then Return "You are not allowed to change this employee."
        If Not e.IsActive Then Return e.FullName & " is already separated."
        Dim problem = ValidateSeparation(e, separatedOn)
        If problem IsNot Nothing Then Return problem
        If Not EmployeeRepository.Deactivate(e.EmployeeId, separatedOn) Then Return "The employee could not be saved."
        AuditService.LogDelete(EmployeesTable, e.EmployeeId, "Separated employee " & e.FullName & " on " &
                               separatedOn.ToString("yyyy-MM-dd") & If(String.IsNullOrWhiteSpace(reason), "", ": " & reason.Trim()) &
                               " (soft delete)")
        Return Nothing
    End Function

    ''' <summary>Rehires a separated employee (active again, separation date cleared). Returns an error or Nothing.</summary>
    Public Shared Function ReactivateEmployee(e As Employee) As String
        If e Is Nothing OrElse Not CanChange(e.BusinessId) Then Return "You are not allowed to change this employee."
        If e.IsActive Then Return e.FullName & " is already active."
        If EmployeeRepository.GetByBusinessId(e.BusinessId).Any(
                Function(x) String.Equals(x.FullName.Trim(), e.FullName.Trim(), StringComparison.OrdinalIgnoreCase)) Then
            Return "An active employee with the same name is already on the list."
        End If
        If Not EmployeeRepository.Reactivate(e.EmployeeId) Then Return "The employee could not be saved."
        AuditService.LogUpdate(EmployeesTable, e.EmployeeId, "Reactivated employee " & e.FullName & " (rehired)")
        Return Nothing
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
