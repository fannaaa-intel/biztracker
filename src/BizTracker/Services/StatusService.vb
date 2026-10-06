''' <summary>
''' Computes every date-based status from Date.Today. These statuses are NEVER stored in the database.
'''   - Valid / Expiring Soon / Expired  (health certificates, permits, sanitary permits)
'''   - Paid / Overdue / Due Soon / Not Yet Due  (RPT quarters and other due dates)
''' "Soon" means within the expiry_warning_days setting (default 30).
''' </summary>
Public NotInheritable Class StatusService

    Private Sub New()
    End Sub

    ' ---------- Expiry statuses ----------
    Public Const Valid As String = "Valid"
    Public Const ExpiringSoon As String = "Expiring Soon"
    Public Const Expired As String = "Expired"
    Public Const NoRecord As String = "No Record"

    ' ---------- Due-date statuses ----------
    Public Const Paid As String = "Paid"
    Public Const Overdue As String = "Overdue"
    Public Const DueSoon As String = "Due Soon"
    Public Const NotYetDue As String = "Not Yet Due"

    ''' <summary>Days before expiry to show "Expiring Soon" (settings: expiry_warning_days).</summary>
    Public Shared ReadOnly Property WarningDays As Integer
        Get
            Return SettingsRepository.GetInt("expiry_warning_days", 30)
        End Get
    End Property

    ''' <summary>Days from today to a date (negative = already passed).</summary>
    Public Shared Function DaysUntil(target As Date) As Integer
        Return (target.Date - Date.Today).Days
    End Function

    ' ==================== Valid / Expiring Soon / Expired ====================

    ''' <summary>
    ''' Expired if the date has passed, Expiring Soon if within WarningDays
    ''' (the expiry day itself still counts as valid), otherwise Valid.
    ''' No date = "No Record".
    ''' </summary>
    Public Shared Function GetExpiryStatus(expiryDate As Date?) As String
        If Not expiryDate.HasValue Then Return NoRecord
        Dim daysLeft = DaysUntil(expiryDate.Value)
        If daysLeft < 0 Then Return Expired
        If daysLeft <= WarningDays Then Return ExpiringSoon
        Return Valid
    End Function

    ''' <summary>
    ''' Counts how many dates fall in each expiry status.
    ''' Always contains the keys Valid, Expiring Soon, Expired and No Record.
    ''' </summary>
    Public Shared Function CountByExpiryStatus(expiryDates As IEnumerable(Of Date?)) As Dictionary(Of String, Integer)
        Dim counts As New Dictionary(Of String, Integer) From {
            {Valid, 0}, {ExpiringSoon, 0}, {Expired, 0}, {NoRecord, 0}
        }
        For Each d In expiryDates
            counts(GetExpiryStatus(d)) += 1
        Next
        Return counts
    End Function

    ' ==================== Due dates / Overdue ====================

    ''' <summary>True if the due date has passed (today is not overdue yet).</summary>
    Public Shared Function IsOverdue(dueDate As Date) As Boolean
        Return DaysUntil(dueDate) < 0
    End Function

    ''' <summary>Paid, or else Overdue / Due Soon / Not Yet Due based on the due date.</summary>
    Public Shared Function GetDueStatus(dueDate As Date, isPaid As Boolean) As String
        If isPaid Then Return Paid
        Dim daysLeft = DaysUntil(dueDate)
        If daysLeft < 0 Then Return Overdue
        If daysLeft <= WarningDays Then Return DueSoon
        Return NotYetDue
    End Function

    ''' <summary>RPT quarters are due at the end of each quarter: Mar 31, Jun 30, Sep 30, Dec 31.</summary>
    Public Shared Function GetQuarterDueDate(taxYear As Integer, quarter As Integer) As Date
        If quarter < 1 OrElse quarter > 4 Then Throw New ArgumentOutOfRangeException(NameOf(quarter))
        Return New Date(taxYear, quarter * 3, 1).AddMonths(1).AddDays(-1)
    End Function

    Public Shared Function GetQuarterStatus(taxYear As Integer, quarter As Integer, isPaid As Boolean) As String
        Return GetDueStatus(GetQuarterDueDate(taxYear, quarter), isPaid)
    End Function

    ' ==================== Validity rules (from CLAUDE.md business rules) ====================

    ''' <summary>Health certificates are valid hc_validity_months (default 12) from the issue date.</summary>
    Public Shared Function GetHealthCertificateExpiry(issueDate As Date) As Date
        Return issueDate.Date.AddMonths(SettingsRepository.GetInt("hc_validity_months", 12))
    End Function

    ''' <summary>Sanitary permits expire December 31 of the year issued.</summary>
    Public Shared Function GetSanitaryPermitExpiry(issueDate As Date) As Date
        Return New Date(issueDate.Year, 12, 31)
    End Function

End Class
