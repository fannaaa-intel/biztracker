''' <summary>
''' "Forgot your password?" from the sign-in screen, in 3 steps:
'''   1. username or e-mail  -> a 6-digit code is e-mailed (same message whether or not the account exists)
'''   2. the 6-digit code    -> "Resend code" is available again after 60 seconds
'''   3. new password + confirm
''' Sending runs in the background so the window never freezes.
''' All rules are in PasswordResetService; this dialog only shows the steps.
''' </summary>
Public Class ForgotPasswordDialog
    Inherits ModernDialog

    Private Enum ResetStep
        FindAccount
        EnterCode
        NewPassword
    End Enum

    Private currentStep As ResetStep
    Private ReadOnly stepControls As New Dictionary(Of ResetStep, List(Of Control))

    ' Step 1
    Private ReadOnly txtLogin As New TextBox()
    Private ReadOnly errLogin As Label
    ' Step 2
    Private ReadOnly sentBox As New RoundedPanel()
    Private ReadOnly lblSent As New Label()
    Private ReadOnly txtCode As New TextBox()
    Private ReadOnly errCode As Label
    Private ReadOnly lnkResend As New LinkLabel()
    ' Step 3
    Private ReadOnly txtNew As New TextBox()
    Private ReadOnly txtConfirm As New TextBox()
    Private ReadOnly errNew As Label
    Private ReadOnly errConfirm As Label
    Private ReadOnly chkShow As New CheckBox()

    ' Footer
    Private ReadOnly btnSend As Button
    Private ReadOnly btnVerify As Button
    Private ReadOnly btnChange As Button
    Private ReadOnly btnBack As Button

    Private ReadOnly resendTimer As New Timer With {.Interval = 1000}
    Private resendAllowedAt As Date = Date.MinValue
    Private requestedLogin As String = ""         ' the username / e-mail the code was requested for
    Private busy As Boolean

    ''' <summary>The username or e-mail typed in step 1 (after OK, LoginForm puts a username in its box).</summary>
    Public ReadOnly Property Login As String
        Get
            Return requestedLogin
        End Get
    End Property

    Public Sub New(Optional username As String = "")
        ClientSize = New Size(480, 76 + 230 + 64)

        ' ---------- Step 1: find the account ----------
        Dim start = Body.Controls.Count
        Dim intro As New Label With {
            .Text = "Enter your username or the e-mail of your account. We will e-mail you a 6-digit code to reset your password.",
            .Font = Theme.BodyFont, .ForeColor = Theme.TextMuted, .AutoSize = False
        }
        intro.SetBounds(24, 18, 432, 44)
        Body.Controls.Add(intro)
        StyleText(txtLogin, username.Trim(), "e.g. jdelacruz or juan@gmail.com", Validation.MaxEmailLength)
        errLogin = AddField("Username or e-mail", txtLogin, 24, 74, 432)
        errLogin.Height = 36
        stepControls(ResetStep.FindAccount) = TakeControls(start)

        ' ---------- Step 2: enter the code ----------
        start = Body.Controls.Count
        sentBox.ShowShadow = False
        sentBox.Radius = 10
        sentBox.BackColor = Theme.StatusGreenSoft
        sentBox.Padding = New Padding(14, 0, 14, 0)
        sentBox.SetBounds(24, 16, 432, 48)
        lblSent.Dock = DockStyle.Fill
        lblSent.Font = Theme.SmallBoldFont
        lblSent.ForeColor = Theme.StatusGreen
        lblSent.BackColor = Theme.StatusGreenSoft
        lblSent.TextAlign = ContentAlignment.MiddleLeft
        sentBox.Controls.Add(lblSent)
        Body.Controls.Add(sentBox)

        StyleText(txtCode, "", "6 digits", PasswordResetService.CodeLength)
        txtCode.Font = Theme.SubtitleFont
        txtCode.TextAlign = HorizontalAlignment.Center
        errCode = AddField("6-digit code", txtCode, 24, 78, 180)
        errCode.Width = 432
        errCode.Height = 36
        lnkResend.Font = Theme.SmallBoldFont
        lnkResend.LinkColor = Theme.SidebarBlue
        lnkResend.ActiveLinkColor = Theme.SidebarActive
        lnkResend.DisabledLinkColor = Theme.TextMuted
        lnkResend.LinkBehavior = LinkBehavior.HoverUnderline
        lnkResend.AutoSize = False
        lnkResend.TextAlign = ContentAlignment.MiddleLeft
        lnkResend.SetBounds(220, 104, 236, 26)
        Body.Controls.Add(lnkResend)
        Dim codeNote As New Label With {
            .Text = "The code works for " & PasswordResetService.CodeValidMinutes & " minutes. Not in your inbox? Check the Spam folder.",
            .Font = Theme.SmallFont, .ForeColor = Theme.TextMuted, .AutoSize = False, .AutoEllipsis = True
        }
        codeNote.SetBounds(24, 178, 432, 36)
        Tips.SetToolTip(codeNote, codeNote.Text)
        Body.Controls.Add(codeNote)
        stepControls(ResetStep.EnterCode) = TakeControls(start)

        ' ---------- Step 3: new password ----------
        start = Body.Controls.Count
        StyleText(txtNew, "", "At least " & UserService.MinPasswordLength & " letters and numbers", 72)
        txtNew.UseSystemPasswordChar = True
        errNew = AddField("New password", txtNew, 24, 16, 432)
        StyleText(txtConfirm, "", "Type the new password again", 72)
        txtConfirm.UseSystemPasswordChar = True
        errConfirm = AddField("Confirm new password", txtConfirm, 24, 94, 432)
        chkShow.Text = "Show passwords"
        chkShow.Font = Theme.SmallFont
        chkShow.ForeColor = Theme.TextDark
        chkShow.AutoSize = True
        chkShow.Location = New Point(24, 172)
        Body.Controls.Add(chkShow)
        Dim pwNote As New Label With {
            .Text = "Use at least " & UserService.MinPasswordLength & " characters with letters and numbers.",
            .Font = Theme.SmallFont, .ForeColor = Theme.TextMuted, .AutoSize = False, .AutoEllipsis = True
        }
        pwNote.SetBounds(24, 198, 432, 22)
        Body.Controls.Add(pwNote)
        stepControls(ResetStep.NewPassword) = TakeControls(start)

        ' ---------- Footer (added right-to-left) ----------
        btnSend = AddFooterButton("Send Code", "primary")
        btnVerify = AddFooterButton("Verify Code", "primary")
        btnChange = AddFooterButton("Change Password", "primary")
        Dim cancel = AddFooterButton("Cancel")
        btnBack = AddFooterButton("Back")
        CancelButton = cancel

        AddHandler btnSend.Click, AddressOf SendCode_Click
        AddHandler btnVerify.Click, AddressOf Verify_Click
        AddHandler btnChange.Click, AddressOf Change_Click
        AddHandler btnBack.Click, Sub() ShowStep(ResetStep.FindAccount)
        AddHandler cancel.Click, Sub() DialogResult = DialogResult.Cancel
        AddHandler lnkResend.LinkClicked, AddressOf Resend_Click
        AddHandler resendTimer.Tick, Sub() UpdateResendLink()
        AddHandler txtLogin.TextChanged, Sub() errLogin.Text = ""
        AddHandler txtCode.TextChanged, Sub() errCode.Text = ""
        AddHandler txtNew.TextChanged, Sub() errNew.Text = ""
        AddHandler txtConfirm.TextChanged, Sub() errConfirm.Text = ""
        AddHandler txtCode.KeyPress, Sub(s, e) e.Handled = Not Char.IsDigit(e.KeyChar) AndAlso Not Char.IsControl(e.KeyChar)
        AddHandler chkShow.CheckedChanged, Sub()
                                               txtNew.UseSystemPasswordChar = Not chkShow.Checked
                                               txtConfirm.UseSystemPasswordChar = Not chkShow.Checked
                                           End Sub
        ShowStep(ResetStep.FindAccount)
    End Sub

    ' ==================== Steps ====================

    Private Sub ShowStep(target As ResetStep)
        currentStep = target
        For Each pair In stepControls
            For Each c In pair.Value
                c.Visible = (pair.Key = target)
            Next
        Next
        btnSend.Visible = (target = ResetStep.FindAccount)
        btnVerify.Visible = (target = ResetStep.EnterCode)
        btnChange.Visible = (target = ResetStep.NewPassword)
        btnBack.Visible = (target = ResetStep.EnterCode)

        Select Case target
            Case ResetStep.FindAccount
                SetTitle("Forgot Password", "Step 1 of 3  ·  Find your account")
                AcceptButton = btnSend
                txtLogin.Focus()
            Case ResetStep.EnterCode
                SetTitle("Forgot Password", "Step 2 of 3  ·  Enter the code from the e-mail")
                AcceptButton = btnVerify
                UpdateResendLink()
                txtCode.Focus()
            Case ResetStep.NewPassword
                SetTitle("Forgot Password", "Step 3 of 3  ·  Choose a new password")
                AcceptButton = btnChange
                txtNew.Focus()
        End Select
    End Sub

    ''' <summary>Step 1: request a code, then go to step 2 (the same message for every account).</summary>
    Private Async Sub SendCode_Click(sender As Object, e As EventArgs)
        If busy Then Return
        Dim login = txtLogin.Text.Trim()
        If login = "" Then
            errLogin.Text = "Enter your username or e-mail."
            txtLogin.Focus()
            Return
        End If
        ' Same account and a code was just requested: go back to the code instead of asking again
        If String.Equals(login, requestedLogin, StringComparison.OrdinalIgnoreCase) AndAlso Date.Now < resendAllowedAt Then
            ShowStep(ResetStep.EnterCode)
            Return
        End If

        Dim problem = Await RequestCodeAsync(login, btnSend, "Sending...")
        If IsDisposed Then Return
        If problem IsNot Nothing Then
            UiHelper.ShowWarning(problem, "Code not sent")
            Return
        End If
        txtCode.Clear()
        ShowStep(ResetStep.EnterCode)
    End Sub

    ''' <summary>Step 2: "Resend code" (after the 60-second wait).</summary>
    Private Async Sub Resend_Click(sender As Object, e As LinkLabelLinkClickedEventArgs)
        If busy OrElse Date.Now < resendAllowedAt Then Return
        lnkResend.Text = "Sending..."
        Dim problem = Await RequestCodeAsync(requestedLogin, btnVerify, Nothing)
        If IsDisposed Then Return
        UpdateResendLink()
        If problem IsNot Nothing Then
            UiHelper.ShowWarning(problem, "Code not sent")
            Return
        End If
        lblSent.Text = PasswordResetService.GenericSentMessage & "  (asked again at " & Date.Now.ToString("h:mm tt") & ")"
        txtCode.Clear()
        txtCode.Focus()
    End Sub

    ''' <summary>Shared by Send Code and Resend: busy state, the request, and the 60-second wait.</summary>
    Private Async Function RequestCodeAsync(login As String, button As Button, busyText As String) As Task(Of String)
        busy = True
        Dim oldText = button.Text
        If busyText IsNot Nothing Then button.Text = busyText
        button.Enabled = False
        txtLogin.Enabled = False
        UseWaitCursor = True
        Try
            Dim problem = Await PasswordResetService.RequestCodeAsync(login)
            If problem Is Nothing Then
                requestedLogin = login
                resendAllowedAt = Date.Now.AddSeconds(PasswordResetService.ResendCooldownSeconds)
                lblSent.Text = PasswordResetService.GenericSentMessage
            End If
            Return problem
        Finally
            If Not IsDisposed Then
                busy = False
                button.Text = oldText
                button.Enabled = True
                txtLogin.Enabled = True
                UseWaitCursor = False
            End If
        End Try
    End Function

    ''' <summary>"Resend code in 42s" while waiting, then a clickable "Resend code".</summary>
    Private Sub UpdateResendLink()
        Dim waitSeconds = (resendAllowedAt - Date.Now).TotalSeconds
        Dim seconds = If(waitSeconds > 0, CInt(Math.Ceiling(waitSeconds)), 0)
        If seconds > 0 Then
            lnkResend.Text = "Resend code in " & seconds & "s"
            lnkResend.Enabled = False
            resendTimer.Start()
        Else
            lnkResend.Text = "Resend code"
            lnkResend.Enabled = True
            resendTimer.Stop()
        End If
    End Sub

    ''' <summary>Step 2: check the code (a wrong one counts as an attempt).</summary>
    Private Sub Verify_Click(sender As Object, e As EventArgs)
        If busy Then Return
        Cursor = Cursors.WaitCursor
        Dim problem = PasswordResetService.VerifyCode(requestedLogin, CodeText)
        Cursor = Cursors.Default
        If problem IsNot Nothing Then
            errCode.Text = problem
            Tips.SetToolTip(errCode, problem)
            txtCode.SelectAll()
            txtCode.Focus()
            Return
        End If
        txtNew.Clear()
        txtConfirm.Clear()
        ShowStep(ResetStep.NewPassword)
    End Sub

    ''' <summary>Step 3: save the new password.</summary>
    Private Sub Change_Click(sender As Object, e As EventArgs)
        If busy Then Return
        errNew.Text = ""
        errConfirm.Text = ""
        Dim pwError = UserService.ValidatePassword(txtNew.Text)
        If pwError IsNot Nothing Then
            errNew.Text = pwError
            txtNew.Focus()
            Return
        End If
        If txtNew.Text <> txtConfirm.Text Then
            errConfirm.Text = "The two passwords do not match."
            txtConfirm.Focus()
            Return
        End If

        Cursor = Cursors.WaitCursor
        Dim problem = PasswordResetService.ResetPassword(requestedLogin, CodeText, txtNew.Text, txtConfirm.Text)
        Cursor = Cursors.Default
        If problem IsNot Nothing Then
            ' The password itself was fine, so the code stopped working (expired or replaced) in the meantime
            UiHelper.ShowWarning(problem, "Password not changed")
            ShowStep(ResetStep.EnterCode)
            Return
        End If
        UiHelper.ShowSuccess("Your password was changed." & vbCrLf & "Next: sign in with your new password.", "Password changed")
        DialogResult = DialogResult.OK
    End Sub

    ' ==================== Helpers ====================

    ''' <summary>The typed code without spaces (a pasted "123 456" also works).</summary>
    Private ReadOnly Property CodeText As String
        Get
            Return txtCode.Text.Replace(" ", "").Trim()
        End Get
    End Property

    ''' <summary>The controls added to Body since index "start" (one step's controls).</summary>
    Private Function TakeControls(start As Integer) As List(Of Control)
        Dim list As New List(Of Control)
        For i = start To Body.Controls.Count - 1
            list.Add(Body.Controls(i))
        Next
        Return list
    End Function

    Private Shared Sub StyleText(box As TextBox, value As String, placeholder As String, maxLength As Integer)
        box.Font = Theme.InputFont
        box.BorderStyle = BorderStyle.FixedSingle
        box.MaxLength = maxLength
        box.Text = value
        box.PlaceholderText = placeholder
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        resendTimer.Stop()
        MyBase.OnFormClosing(e)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then resendTimer.Dispose()
        MyBase.Dispose(disposing)
    End Sub

End Class
