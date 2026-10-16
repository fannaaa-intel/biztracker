Imports System.IO
Imports System.Net
Imports System.Text

''' <summary>
''' Terms and Conditions on login (Revision 4E).
'''   - The text is in Templates/terms.html ({{lgu_name}}, {{terms_version}} ... are filled from the settings).
'''   - settings.terms_version is the current version. A user whose users.terms_accepted_version is different
'''     (or empty) must accept before using the system; accepting saves the version + time and is audit-logged.
'''   - Declining logs the user out (back to the login screen).
'''   - When the Admin changes terms_version in Settings, every user must accept again on the next login.
''' </summary>
Public NotInheritable Class TermsService

    Private Sub New()
    End Sub

    Private Const TableName As String = "users"

    ''' <summary>The current version (settings.terms_version, default 1.0).</summary>
    Public Shared ReadOnly Property CurrentVersion As String
        Get
            Return SettingsRepository.GetValue("terms_version", "1.0").Trim()
        End Get
    End Property

    ''' <summary>True when the user has not accepted the current version yet.</summary>
    Public Shared Function NeedsAcceptance(user As User) As Boolean
        Return NeedsAcceptance(user?.TermsAcceptedVersion, CurrentVersion)
    End Function

    ''' <summary>The rule itself: accepted version must equal the current one (spaces ignored).</summary>
    Public Shared Function NeedsAcceptance(acceptedVersion As String, currentVersion As String) As Boolean
        If String.IsNullOrWhiteSpace(acceptedVersion) Then Return True     ' never accepted
        Return Not String.Equals(acceptedVersion.Trim(), If(currentVersion, "").Trim(), StringComparison.OrdinalIgnoreCase)
    End Function

    ''' <summary>The full Terms page (HTML).</summary>
    Public Shared Function BuildHtml() As String
        Dim page = File.ReadAllText(Path.Combine(ReportService.GetTemplateFolder(), "terms.html"), Encoding.UTF8)
        Return PolicyService.FillPlaceholders(page, Function(key) SettingsRepository.GetValue(key))
    End Function

    ''' <summary>Saves the logged-in user's acceptance of the current version. Returns an error or Nothing.</summary>
    Public Shared Function Accept() As String
        If Not Session.IsLoggedIn Then Return "Please log in again."
        Dim version = CurrentVersion
        If Not UserRepository.SetTermsAccepted(Session.UserId.Value, version) Then Return "Your acceptance could not be saved."
        Session.CurrentUser.TermsAcceptedVersion = version
        Session.CurrentUser.TermsAcceptedAt = Date.Now
        AuditService.LogUpdate(TableName, Session.UserId.Value, "Accepted terms v" & version)
        Return Nothing
    End Function

    ''' <summary>The user declined: log it and log them out.</summary>
    Public Shared Sub Decline()
        If Not Session.IsLoggedIn Then Return
        AuditService.LogUpdate(TableName, Session.UserId.Value, "Declined terms v" & CurrentVersion & " (logged out)")
        AuthService.Logout()
    End Sub

End Class
