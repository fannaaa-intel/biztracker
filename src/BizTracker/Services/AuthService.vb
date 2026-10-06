''' <summary>Result of a login attempt.</summary>
Public Class LoginResult
    Public Property Success As Boolean
    Public Property Message As String = ""
    Public Property User As User
    ''' <summary>Seconds until the username can try again (0 = not locked).</summary>
    Public Property LockedSeconds As Integer
End Class

''' <summary>
''' Login / logout. Passwords are checked with BCrypt.
''' After 5 failed attempts a username is locked for 1 minute (kept in memory while the app runs).
''' Every attempt is written to audit_log.
''' </summary>
Public NotInheritable Class AuthService

    Private Sub New()
    End Sub

    Public Const MaxFailedAttempts As Integer = 5
    Public Const LockoutSeconds As Integer = 60

    ''' <summary>Failed-attempt tracking per username (lower case).</summary>
    Private Class AttemptInfo
        Public FailedCount As Integer
        Public LockedUntil As Date = Date.MinValue
    End Class

    Private Shared ReadOnly Attempts As New Dictionary(Of String, AttemptInfo)

    Public Shared Function Login(username As String, password As String) As LoginResult
        username = If(username, "").Trim()
        If username = "" OrElse String.IsNullOrEmpty(password) Then
            Return Fail("Please enter your username and password.")
        End If

        Dim key = username.ToLowerInvariant()
        Dim locked = GetLockedSeconds(username)
        If locked > 0 Then
            Return New LoginResult With {.Message = LockedMessage(locked), .LockedSeconds = locked}
        End If

        Dim user = UserRepository.GetByUsername(username)
        If user Is Nothing OrElse Not PasswordMatches(password, user.PasswordHash) Then
            AuditService.LogLogin(user?.UserId,
                If(user Is Nothing, "Failed login: unknown username '" & username & "'", "Failed login: wrong password"))
            Return RegisterFailure(key)
        End If

        If Not user.IsActive Then
            AuditService.LogLogin(user.UserId, "Failed login: account is deactivated")
            Return Fail("This account is deactivated. Please contact the system administrator.")
        End If

        If user.Role = Roles.Owner AndAlso Not user.BusinessId.HasValue Then
            AuditService.LogLogin(user.UserId, "Failed login: owner account not linked to a business")
            Return Fail("This owner account is not linked to a business yet. Please contact the BPLO.")
        End If

        ' Success
        Attempts.Remove(key)
        UserRepository.UpdateLastLogin(user.UserId)
        Session.Start(user)
        AuditService.LogLogin(user.UserId, "Login successful (" & user.Role & ")")
        Return New LoginResult With {.Success = True, .User = user}
    End Function

    Public Shared Sub Logout()
        If Not Session.IsLoggedIn Then Return
        AuditService.LogLogout()
        Session.Clear()
    End Sub

    ''' <summary>Seconds left on a username's lockout (0 if not locked).</summary>
    Public Shared Function GetLockedSeconds(username As String) As Integer
        Dim info As AttemptInfo = Nothing
        If Not Attempts.TryGetValue(If(username, "").Trim().ToLowerInvariant(), info) Then Return 0
        Dim remaining = (info.LockedUntil - Date.Now).TotalSeconds
        Return If(remaining > 0, CInt(Math.Ceiling(remaining)), 0)
    End Function

    ''' <summary>Creates a BCrypt hash for a new or reset password.</summary>
    Public Shared Function HashPassword(password As String) As String
        Return BCrypt.Net.BCrypt.HashPassword(password)
    End Function

    ' ---------------- helpers ----------------

    Private Shared Function PasswordMatches(password As String, hash As String) As Boolean
        Try
            Return BCrypt.Net.BCrypt.Verify(password, hash)
        Catch ex As BCrypt.Net.SaltParseException
            Return False   ' stored hash is not valid BCrypt
        End Try
    End Function

    Private Shared Function RegisterFailure(key As String) As LoginResult
        Dim info As AttemptInfo = Nothing
        If Not Attempts.TryGetValue(key, info) Then
            info = New AttemptInfo()
            Attempts(key) = info
        End If
        info.FailedCount += 1

        If info.FailedCount >= MaxFailedAttempts Then
            info.FailedCount = 0
            info.LockedUntil = Date.Now.AddSeconds(LockoutSeconds)
            AuditService.LogLogin(Nothing, "Username '" & key & "' locked for " & LockoutSeconds & " seconds")
            Return New LoginResult With {.Message = LockedMessage(LockoutSeconds), .LockedSeconds = LockoutSeconds}
        End If

        Dim left = MaxFailedAttempts - info.FailedCount
        Dim msg = "Invalid username or password."
        If left <= 2 Then msg &= " " & left & If(left = 1, " attempt", " attempts") & " left before a 1-minute lock."
        Return Fail(msg)
    End Function

    Private Shared Function LockedMessage(seconds As Integer) As String
        Return "Too many failed attempts. Please try again in " & seconds & " seconds."
    End Function

    Private Shared Function Fail(message As String) As LoginResult
        Return New LoginResult With {.Message = message}
    End Function

End Class
