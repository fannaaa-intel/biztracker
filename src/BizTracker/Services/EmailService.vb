Imports System.IO
Imports System.Net
Imports System.Net.Mail
Imports System.Net.NetworkInformation
Imports System.Text.Json

''' <summary>
''' Sends plain-text e-mails through Gmail (smtp.gmail.com, port 587, SSL/STARTTLS) with the
''' built-in System.Net.Mail - no extra NuGet package.
''' The Gmail address and App Password come from email-settings.json next to the .exe
''' (never from App.config or any file in Git). Setup: docs/EMAIL_SETUP.md.
''' Every function returns a friendly error message, or Nothing - it never throws.
''' </summary>
Public NotInheritable Class EmailService

    Private Sub New()
    End Sub

    Public Const SettingsFileName As String = "email-settings.json"
    Private Const SmtpHost As String = "smtp.gmail.com"
    Private Const SmtpPort As Integer = 587
    Private Const SendTimeoutMilliseconds As Integer = 20000
    Private Const LookupTimeoutMilliseconds As Integer = 5000

    Public Const NoInternetMessage As String =
        "No internet connection. Please connect to the internet and try again."
    Private Const NotSetUpMessage As String =
        "E-mail is not set up on this computer yet, so codes cannot be sent." & vbCrLf &
        "Please ask the system administrator (setup guide: docs/EMAIL_SETUP.md)."

    ''' <summary>Full path of email-settings.json (in the program folder).</summary>
    Public Shared ReadOnly Property SettingsPath As String
        Get
            Return Path.Combine(AppContext.BaseDirectory, SettingsFileName)
        End Get
    End Property

    ''' <summary>Why e-mail cannot be used (missing or unfinished settings file), or Nothing when ready.</summary>
    Public Shared Function GetSetupProblem() As String
        Dim problem As String = Nothing
        LoadSettings(problem)
        Return problem
    End Function

    ''' <summary>
    ''' Quick check that this PC is online and can find Gmail's server (at most ~5 seconds).
    ''' Returns NoInternetMessage, or Nothing when online.
    ''' </summary>
    Public Shared Async Function CheckConnectionAsync() As Task(Of String)
        If Not NetworkInterface.GetIsNetworkAvailable() Then Return NoInternetMessage
        Try
            Dim lookup = Dns.GetHostAddressesAsync(SmtpHost)
            Dim finished = Await Task.WhenAny(lookup, Task.Delay(LookupTimeoutMilliseconds))
            If finished IsNot lookup OrElse lookup.IsFaulted OrElse lookup.Result.Length = 0 Then Return NoInternetMessage
            Return Nothing
        Catch ex As Exception
            Debug.WriteLine("E-mail connection check failed: " & ex.ToString())
            Return NoInternetMessage
        End Try
    End Function

    ''' <summary>
    ''' Sends one plain-text e-mail on a background thread (the window stays responsive).
    ''' Returns a friendly error message, or Nothing when Gmail accepted the e-mail.
    ''' </summary>
    Public Shared Async Function SendAsync(toAddress As String, subject As String, body As String) As Task(Of String)
        Dim problem As String = Nothing
        Dim settings = LoadSettings(problem)
        If problem IsNot Nothing Then Return problem
        If Not Validation.IsValidEmail(toAddress) Then Return "The e-mail address of this account is not valid."

        Try
            Using message As New MailMessage(),
                  client As New SmtpClient(SmtpHost, SmtpPort)
                message.From = New MailAddress(settings.GmailAddress, settings.SenderName)
                message.To.Add(toAddress.Trim())
                message.Subject = subject
                message.Body = body
                message.IsBodyHtml = False
                message.BodyEncoding = Text.Encoding.UTF8
                message.SubjectEncoding = Text.Encoding.UTF8

                client.EnableSsl = True                 ' STARTTLS on port 587
                client.DeliveryMethod = SmtpDeliveryMethod.Network
                client.UseDefaultCredentials = False
                client.Credentials = New NetworkCredential(settings.GmailAddress, settings.AppPassword)
                client.Timeout = SendTimeoutMilliseconds   ' only applies to Send (not SendMailAsync), hence Task.Run

                Await Task.Run(Sub() client.Send(message))
            End Using
            Return Nothing
        Catch ex As SmtpException
            Debug.WriteLine("E-mail sending failed: " & ex.ToString())
            Dim details = ex.Message & " " & If(ex.InnerException?.Message, "")
            If details.Contains("5.7.") OrElse details.IndexOf("authentication", StringComparison.OrdinalIgnoreCase) >= 0 Then
                Return "Gmail did not accept the sending account (wrong address or App Password in " & SettingsFileName & ")." &
                       vbCrLf & "Please ask the system administrator."
            End If
            If details.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0 Then
                Return "Gmail took too long to answer. Please check the internet connection and try again."
            End If
            Return "Gmail could not send the e-mail right now. Please try again in a few minutes." & vbCrLf &
                   "If this keeps happening, ask the system administrator to check " & SettingsFileName & "."
        Catch ex As Exception
            ' No internet, firewall, bad address... never crash, never show a stack trace.
            Debug.WriteLine("E-mail sending failed: " & ex.ToString())
            Return "The e-mail could not be sent. Please check the internet connection and try again."
        End Try
    End Function

    ' ---------------- helpers ----------------

    ''' <summary>Reads and checks email-settings.json. On a problem, sets "problem" and returns Nothing.</summary>
    Private Shared Function LoadSettings(ByRef problem As String) As EmailSettings
        problem = Nothing
        If Not File.Exists(SettingsPath) Then
            problem = NotSetUpMessage
            Return Nothing
        End If

        Dim settings As EmailSettings
        Try
            settings = JsonSerializer.Deserialize(Of EmailSettings)(File.ReadAllText(SettingsPath),
                New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True,
                                                .ReadCommentHandling = JsonCommentHandling.Skip,
                                                .AllowTrailingCommas = True})
        Catch ex As Exception When TypeOf ex Is JsonException OrElse TypeOf ex Is IOException OrElse
                                   TypeOf ex Is UnauthorizedAccessException
            Debug.WriteLine("email-settings.json could not be read: " & ex.ToString())
            problem = NotSetUpMessage
            Return Nothing
        End Try
        If settings Is Nothing Then
            problem = NotSetUpMessage
            Return Nothing
        End If

        ' Google shows the App Password as "abcd efgh ijkl mnop" - spaces are not part of it.
        settings.GmailAddress = If(settings.GmailAddress, "").Trim()
        settings.AppPassword = If(settings.AppPassword, "").Replace(" ", "").Trim()
        If String.IsNullOrWhiteSpace(settings.SenderName) Then settings.SenderName = "BizTracker"

        ' Still the placeholder values from email-settings.example.json?
        If Not Validation.IsValidEmail(settings.GmailAddress) OrElse settings.GmailAddress.Contains("PUT-") OrElse
           settings.AppPassword.Length <> 16 OrElse settings.AppPassword.Contains("PUT-") Then
            problem = NotSetUpMessage
            Return Nothing
        End If
        Return settings
    End Function

End Class
