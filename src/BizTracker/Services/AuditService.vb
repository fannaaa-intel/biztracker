''' <summary>
''' Writes rows to audit_log for important actions. The user comes from Session.
''' Example: AuditService.LogInsert("business_permits", newId, "Filed BP-2026-00004")
''' </summary>
Public NotInheritable Class AuditService

    Private Sub New()
    End Sub

    Private Const MaxDetailsLength As Integer = 255   ' audit_log.details is VARCHAR(255)

    ''' <summary>
    ''' General entry point. userId defaults to the logged-in user; pass it explicitly
    ''' for login events (the session is not started yet).
    ''' </summary>
    Public Shared Sub Log(action As String, tableName As String, recordId As Integer?, details As String,
                          Optional userId As Integer? = Nothing)
        Dim who = If(userId, Session.UserId)
        AuditLogRepository.Insert(who, action, tableName, recordId, Shorten(details))
    End Sub

    Public Shared Sub LogInsert(tableName As String, recordId As Integer, details As String)
        Log(AuditActions.Insert, tableName, recordId, details)
    End Sub

    Public Shared Sub LogUpdate(tableName As String, recordId As Integer, details As String)
        Log(AuditActions.Update, tableName, recordId, details)
    End Sub

    Public Shared Sub LogDelete(tableName As String, recordId As Integer, details As String)
        Log(AuditActions.Delete, tableName, recordId, details)
    End Sub

    ''' <summary>Records "old -> new", e.g. "Submitted -> Under Review".</summary>
    Public Shared Sub LogStatusChange(tableName As String, recordId As Integer, oldStatus As String,
                                      newStatus As String, Optional note As String = "")
        Dim details = oldStatus & " -> " & newStatus & If(String.IsNullOrWhiteSpace(note), "", " (" & note & ")")
        Log(AuditActions.StatusChange, tableName, recordId, details)
    End Sub

    ''' <summary>
    ''' Login attempts. userId is written exactly as given (Nothing for an unknown username) -
    ''' it never falls back to the session user, so a failed attempt is not blamed on anyone.
    ''' </summary>
    Public Shared Sub LogLogin(userId As Integer?, details As String)
        AuditLogRepository.Insert(userId, AuditActions.Login, "users", userId, Shorten(details))
    End Sub

    Public Shared Sub LogLogout()
        Log(AuditActions.Logout, "users", Session.UserId, "Logged out")
    End Sub

    Private Shared Function Shorten(text As String) As String
        If text Is Nothing OrElse text.Length <= MaxDetailsLength Then Return text
        Return text.Substring(0, MaxDetailsLength - 3) & "..."
    End Function

End Class
