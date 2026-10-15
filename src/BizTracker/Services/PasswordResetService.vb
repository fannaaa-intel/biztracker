Imports System.Security.Cryptography
Imports System.Text.RegularExpressions

''' <summary>
''' Forgot Password with a 6-digit code sent by e-mail. ForgotPasswordDialog calls this class.
''' Rules:
'''   - the code is 6 random digits (secure random generator), stored only as a BCrypt hash
'''   - it expires after 10 minutes and works once; a new code cancels the older unused ones
'''   - 5 wrong guesses cancel the code (a new one must be requested)
'''   - a new code can be requested 60 seconds after the last one
'''   - inactive accounts and accounts without an e-mail never get a code
'''   - the screen never tells whether an account exists: step 1 always shows GenericSentMessage,
'''     and unknown accounts get the same "wrong code / expired / too many" answers as real ones
'''     (tracked in memory as "decoys")
''' On success: new BCrypt password, code closed, login lockout cleared, audit_log row,
''' and a "your password was changed" e-mail. Functions return an error message, or Nothing.
''' </summary>
Public NotInheritable Class PasswordResetService

    Private Sub New()
    End Sub

    Public Const CodeLength As Integer = 6
    Public Const CodeValidMinutes As Integer = 10
    Public Const MaxAttempts As Integer = 5
    Public Const ResendCooldownSeconds As Integer = 60

    ''' <summary>Shown after every request, whether or not the account exists.</summary>
    Public Const GenericSentMessage As String = "If this account exists and has an email, a code was sent."

    Private Const TableName As String = "password_resets"
    Private Const NoCodeMessage As String = "This code is not valid. Click ""Resend code"" to get a new one."
    Private Const ExpiredMessage As String = "This code has expired (codes work for 10 minutes). Click ""Resend code"" to get a new one."
    Private Const TooManyMessage As String = "Too many wrong codes, so this code was cancelled. Click ""Resend code"" to get a new one."
    Private Const NotSentMessage As String = "The code could not be sent."

    ' ==================== Decoys (accounts that get no code) ====================

    ''' <summary>A pretend code for an unknown / inactive / no-e-mail account, so its answers look real.</summary>
    Private Class Decoy
        Public RequestedAt As Date
        Public Attempts As Integer
    End Class

    Private Shared ReadOnly Decoys As New Dictionary(Of String, Decoy)
    Private Shared ReadOnly DecoyLock As New Object()

    ''' <summary>A BCrypt hash to check decoy guesses against, so they take as long as real ones.</summary>
    Private Shared ReadOnly DummyHash As New Lazy(Of String)(Function() AuthService.HashPassword("decoy-code"))

    ' ==================== Step 1: request a code ====================

    ''' <summary>
    ''' Sends a new code to the account's e-mail. Returns Nothing when the screen should show
    ''' GenericSentMessage (also for unknown accounts), or a friendly error (no internet, e-mail not set up,
    ''' or Gmail failed for a real account).
    ''' </summary>
    Public Shared Async Function RequestCodeAsync(login As String) As Task(Of String)
        login = If(login, "").Trim()
        If login = "" Then Return "Enter your username or e-mail."

        ' These two checks do not depend on the account, so they reveal nothing about it.
        Dim problem = EmailService.GetSetupProblem()
        If problem IsNot Nothing Then Return problem
        problem = Await EmailService.CheckConnectionAsync()
        If problem IsNot Nothing Then Return problem

        Dim u = FindAccount(login)
        Dim skipReason = GetSkipReason(u)
        If skipReason IsNot Nothing Then
            StartDecoy(login)
            AuditService.Log(AuditActions.Insert, TableName, Nothing,
                             "Forgot Password for '" & login & "': no code sent (" & skipReason & ")", u?.UserId)
            Return Nothing
        End If

        If PasswordResetRepository.HasRecentCode(u.UserId, ResendCooldownSeconds) Then
            AuditService.Log(AuditActions.Insert, TableName, Nothing,
                             "Forgot Password for '" & u.Username & "': no code sent (asked again within " &
                             ResendCooldownSeconds & " seconds)", u.UserId)
            Return Nothing
        End If

        Dim code = NewCode()
        Dim resetId = PasswordResetRepository.InsertReplacingOld(u.UserId, AuthService.HashPassword(code), CodeValidMinutes)
        If resetId <= 0 Then Return NotSentMessage & " Please try again."

        Dim sendError = Await EmailService.SendAsync(u.Email, "Your BizTracker password reset code", CodeEmailBody(u, code))
        If sendError IsNot Nothing Then
            PasswordResetRepository.Delete(resetId)     ' it never arrived, so it must not block a retry
            AuditService.Log(AuditActions.Insert, TableName, Nothing,
                             "Forgot Password for '" & u.Username & "': the code e-mail could not be sent", u.UserId)
            Return NotSentMessage & vbCrLf & sendError
        End If

        AuditService.Log(AuditActions.Insert, TableName, resetId,
                         "Forgot Password: code e-mailed to '" & u.Username & "' (valid " & CodeValidMinutes & " minutes)", u.UserId)
        Return Nothing
    End Function

    ' ==================== Step 2: check the code ====================

    ''' <summary>
    ''' Checks the code without using it up (step 3 checks it again and uses it).
    ''' A wrong code counts as an attempt. Returns an error message, or Nothing when correct.
    ''' </summary>
    Public Shared Function VerifyCode(login As String, code As String) As String
        login = If(login, "").Trim()
        code = If(code, "").Trim()
        If Not Regex.IsMatch(code, "^\d{" & CodeLength & "}$") Then Return "Enter the 6-digit code from the e-mail."

        Dim u = FindAccount(login)
        If GetSkipReason(u) IsNot Nothing Then Return CheckDecoy(login)

        Dim r = PasswordResetRepository.GetLatest(u.UserId)
        If r Is Nothing Then Return NoCodeMessage
        If r.UsedAt.HasValue Then Return If(r.Attempts >= MaxAttempts, TooManyMessage, NoCodeMessage)
        If r.IsExpired Then Return ExpiredMessage
        If CodeMatches(code, r.CodeHash) Then Return Nothing

        Dim attempts = PasswordResetRepository.AddAttempt(r.ResetId)
        If attempts < 0 Then Return "The code could not be checked. Please try again."
        If attempts >= MaxAttempts Then
            PasswordResetRepository.Close(r.ResetId)
            AuditService.Log(AuditActions.Update, TableName, r.ResetId,
                             "Forgot Password: code for '" & u.Username & "' cancelled after " & MaxAttempts & " wrong attempts", u.UserId)
            Return TooManyMessage
        End If
        Return WrongCodeMessage(MaxAttempts - attempts)
    End Function

    ' ==================== Step 3: set the new password ====================

    ''' <summary>
    ''' Checks the new password and the code again, then saves the password and closes the code
    ''' (one transaction), clears the login lockout, writes audit_log and e-mails a notice.
    ''' Returns an error message, or Nothing when the password was changed.
    ''' </summary>
    Public Shared Function ResetPassword(login As String, code As String, newPassword As String, confirmPassword As String) As String
        Dim pwError = UserService.ValidatePassword(newPassword)
        If pwError IsNot Nothing Then Return pwError
        If newPassword <> confirmPassword Then Return "The two passwords do not match."

        Dim codeError = VerifyCode(login, code)
        If codeError IsNot Nothing Then Return codeError

        Dim u = FindAccount(login)
        Dim r = PasswordResetRepository.GetLatest(u.UserId)
        If r Is Nothing OrElse Not PasswordResetRepository.CompleteReset(r.ResetId, u.UserId, AuthService.HashPassword(newPassword)) Then
            Return NoCodeMessage
        End If

        AuthService.ClearLockout(u.Username)
        AuditService.Log(AuditActions.Update, "users", u.UserId,
                         "Password changed with Forgot Password (e-mail code) for '" & u.Username & "'", u.UserId)

        ' The notice is sent in the background; if it fails, the new password still works.
        Dim notice = EmailService.SendAsync(u.Email, "Your BizTracker password was changed", ChangedEmailBody(u))
        notice.ContinueWith(Sub(t) If t.Result IsNot Nothing Then Debug.WriteLine("Password-changed notice not sent: " & t.Result))
        Return Nothing
    End Function

    ' ==================== Helpers ====================

    ''' <summary>"@" in the text = look up by e-mail, otherwise by username.</summary>
    Private Shared Function FindAccount(login As String) As User
        If login.Contains("@"c) Then Return UserRepository.GetByEmail(login)
        Return UserRepository.GetByUsername(login)
    End Function

    ''' <summary>Why no code is sent to this account (for the audit log only), or Nothing if it can get one.</summary>
    Private Shared Function GetSkipReason(u As User) As String
        If u Is Nothing Then Return "unknown account"
        If Not u.IsActive Then Return "account is deactivated"
        If String.IsNullOrWhiteSpace(u.Email) Then Return "account has no e-mail"
        Return Nothing
    End Function

    ''' <summary>6 random digits from the secure generator, e.g. "048213".</summary>
    Private Shared Function NewCode() As String
        Return RandomNumberGenerator.GetInt32(0, 1000000).ToString("D6")
    End Function

    Private Shared Function CodeMatches(code As String, hash As String) As Boolean
        Try
            Return BCrypt.Net.BCrypt.Verify(code, hash)
        Catch ex As BCrypt.Net.SaltParseException
            Return False
        End Try
    End Function

    Private Shared Function WrongCodeMessage(attemptsLeft As Integer) As String
        Return "Wrong code. " & attemptsLeft & If(attemptsLeft = 1, " attempt", " attempts") & " left."
    End Function

    ''' <summary>A request for an account that gets no code: start (or keep, within the cooldown) a decoy.</summary>
    Private Shared Sub StartDecoy(login As String)
        Dim key = login.ToLowerInvariant()
        SyncLock DecoyLock
            Dim d As Decoy = Nothing
            If Decoys.TryGetValue(key, d) AndAlso (Date.Now - d.RequestedAt).TotalSeconds < ResendCooldownSeconds Then Return
            Decoys(key) = New Decoy With {.RequestedAt = Date.Now}
        End SyncLock
    End Sub

    ''' <summary>Answers a guess for a decoy exactly like a real wrong guess (the code is never right).</summary>
    Private Shared Function CheckDecoy(login As String) As String
        CodeMatches("000000", DummyHash.Value)       ' same delay as a real check
        Dim key = login.ToLowerInvariant()
        SyncLock DecoyLock
            Dim d As Decoy = Nothing
            If Not Decoys.TryGetValue(key, d) Then Return NoCodeMessage
            If d.Attempts >= MaxAttempts Then Return TooManyMessage
            If Date.Now > d.RequestedAt.AddMinutes(CodeValidMinutes) Then Return ExpiredMessage
            d.Attempts += 1
            If d.Attempts >= MaxAttempts Then Return TooManyMessage
            Return WrongCodeMessage(MaxAttempts - d.Attempts)
        End SyncLock
    End Function

    Private Shared Function LguName() As String
        Return SettingsRepository.GetValue("lgu_name", "BizTracker")
    End Function

    Private Shared Function CodeEmailBody(u As User, code As String) As String
        Return "Hello " & u.FullName & "," & vbCrLf & vbCrLf &
               "Your BizTracker password reset code is:" & vbCrLf & vbCrLf &
               "    " & code & vbCrLf & vbCrLf &
               "Type it in the Forgot Password window. It works for " & CodeValidMinutes & " minutes and only once." & vbCrLf &
               "Username: " & u.Username & vbCrLf & vbCrLf &
               "If you did not ask for this, ignore this e-mail - your password stays the same." & vbCrLf & vbCrLf &
               LguName() & " - BizTracker"
    End Function

    Private Shared Function ChangedEmailBody(u As User) As String
        Return "Hello " & u.FullName & "," & vbCrLf & vbCrLf &
               "The password of your BizTracker account (" & u.Username & ") was changed on " &
               Date.Now.ToString("MMM d, yyyy 'at' h:mm tt") & " using Forgot Password." & vbCrLf & vbCrLf &
               "If this was you, nothing else to do." & vbCrLf &
               "If it was NOT you, contact the system administrator right away." & vbCrLf & vbCrLf &
               LguName() & " - BizTracker"
    End Function

End Class
