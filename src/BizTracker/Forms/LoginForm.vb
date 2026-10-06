Imports System.Drawing.Drawing2D
Imports System.Runtime.InteropServices

''' <summary>
''' Sign-in window: a borderless, rounded window with a blue brand panel on the left
''' and the sign-in card on the right. Returns DialogResult.OK when login succeeds
''' (Session is then set by AuthService).
''' </summary>
Public Class LoginForm
    Inherits Form

    Private ReadOnly brandPanel As New Panel()
    Private ReadOnly formPanel As New Panel()
    Private ReadOnly txtUsername As New TextBox()
    Private ReadOnly txtPassword As New TextBox()
    Private ReadOnly chkShowPassword As New CheckBox()
    Private ReadOnly btnLogin As New Button()
    Private ReadOnly errorBox As New RoundedPanel()
    Private ReadOnly lblError As New Label()
    Private ReadOnly lockTimer As New Timer With {.Interval = 1000}

    ' Read from the settings table when the form loads
    Private lguName As String = ""
    Private province As String = ""

    Public Sub New()
        SuspendLayout()
        AutoScaleDimensions = New SizeF(96.0F, 96.0F)
        AutoScaleMode = AutoScaleMode.Dpi
        FormBorderStyle = FormBorderStyle.None
        StartPosition = FormStartPosition.CenterScreen
        ClientSize = New Size(960, 600)
        BackColor = Color.White
        Font = Theme.BodyFont
        Text = "BizTracker - Sign in"

        BuildBrandPanel()
        BuildFormPanel()
        ' Docking order: the last control added is docked first
        Controls.Add(formPanel)    ' Fill
        Controls.Add(brandPanel)   ' Left

        AcceptButton = btnLogin    ' Enter key signs in
        AddHandler lockTimer.Tick, AddressOf LockTimer_Tick
        UiHelper.DisableMnemonics(Me)     ' show "&" in data as-is
        ResumeLayout(False)
    End Sub

    ' ==================== Layout ====================

    Private Sub BuildBrandPanel()
        brandPanel.Dock = DockStyle.Left
        brandPanel.Width = 420
        brandPanel.BackColor = Theme.SidebarBlue
        AddHandler brandPanel.Paint, AddressOf BrandPanel_Paint
        AddHandler brandPanel.Resize, Sub() brandPanel.Invalidate()
        AddHandler brandPanel.MouseDown, AddressOf DragWindow
    End Sub

    Private Sub BuildFormPanel()
        formPanel.Dock = DockStyle.Fill
        formPanel.BackColor = Color.White
        AddHandler formPanel.MouseDown, AddressOf DragWindow

        Const x0 As Integer = 70
        Const w0 As Integer = 400

        Dim btnClose As New IconButton With {
            .Glyph = Icons.Close, .IconSize = 10.0F, .IconColor = Theme.TextMuted,
            .Size = New Size(36, 36), .Location = New Point(540 - 48, 12)
        }
        AddHandler btnClose.Click, Sub()
                                       DialogResult = DialogResult.Cancel
                                       Close()
                                   End Sub
        Call New ToolTip().SetToolTip(btnClose, "Close")

        Dim lblWelcome As New Label With {
            .Text = "Welcome back", .Font = Theme.HeadingFont, .ForeColor = Theme.TextDark,
            .AutoSize = True, .Location = New Point(x0 - 3, 96)
        }
        Dim lblHint As New Label With {
            .Text = "Sign in to your BizTracker account to continue.", .Font = Theme.BodyFont,
            .ForeColor = Theme.TextMuted, .AutoSize = True, .Location = New Point(x0, 140)
        }

        Dim lblUser As New Label With {
            .Text = "Username", .Font = Theme.SmallBoldFont, .ForeColor = Theme.TextDark,
            .AutoSize = True, .Location = New Point(x0, 190)
        }
        Dim userBox = UiHelper.CreateInputBox(txtUsername, "Enter your username", Icons.Contact)
        userBox.SetBounds(x0, 212, w0, 46)
        txtUsername.MaxLength = 50

        Dim lblPass As New Label With {
            .Text = "Password", .Font = Theme.SmallBoldFont, .ForeColor = Theme.TextDark,
            .AutoSize = True, .Location = New Point(x0, 274)
        }
        Dim passBox = UiHelper.CreateInputBox(txtPassword, "Enter your password", Icons.Lock)
        passBox.SetBounds(x0, 296, w0, 46)
        txtPassword.UseSystemPasswordChar = True
        txtPassword.MaxLength = 100

        chkShowPassword.Text = "Show password"
        chkShowPassword.Font = Theme.SmallFont
        chkShowPassword.ForeColor = Theme.TextMuted
        chkShowPassword.AutoSize = True
        chkShowPassword.Cursor = Cursors.Hand
        chkShowPassword.Location = New Point(x0, 352)
        AddHandler chkShowPassword.CheckedChanged,
            Sub() txtPassword.UseSystemPasswordChar = Not chkShowPassword.Checked

        ' Error message: soft red rounded box, hidden until needed
        errorBox.SetBounds(x0, 384, w0, 44)
        errorBox.Radius = 8
        errorBox.ShowShadow = False
        errorBox.BackColor = Theme.StatusRedSoft
        errorBox.Padding = New Padding(12, 0, 12, 0)
        errorBox.Visible = False
        lblError.Dock = DockStyle.Fill
        lblError.Font = Theme.SmallBoldFont
        lblError.ForeColor = Theme.StatusRed
        lblError.TextAlign = ContentAlignment.MiddleLeft
        lblError.BackColor = Theme.StatusRedSoft
        errorBox.Controls.Add(lblError)

        btnLogin.Text = "Sign in"
        btnLogin.SetBounds(x0, 440, w0, 48)
        UiHelper.StylePrimaryButton(btnLogin)
        AddHandler btnLogin.Click, AddressOf BtnLogin_Click

        Dim lblFooter As New Label With {
            .Text = "Forgot your password? Please contact the system administrator.",
            .Font = Theme.SmallFont, .ForeColor = Theme.TextMuted,
            .AutoSize = False, .TextAlign = ContentAlignment.MiddleCenter
        }
        lblFooter.SetBounds(x0, 520, w0, 24)

        ' Tab order: username -> password -> show password -> sign in
        userBox.TabIndex = 0 : passBox.TabIndex = 1 : chkShowPassword.TabIndex = 2 : btnLogin.TabIndex = 3

        formPanel.Controls.AddRange({btnClose, lblWelcome, lblHint, lblUser, userBox, lblPass, passBox,
                                     chkShowPassword, errorBox, btnLogin, lblFooter})
    End Sub

    ''' <summary>Gradient background, decorative circles, logo and LGU branding.</summary>
    Private Sub BrandPanel_Paint(sender As Object, e As PaintEventArgs)
        Dim g = e.Graphics
        Dim w = brandPanel.ClientSize.Width
        Dim h = brandPanel.ClientSize.Height
        If w <= 0 OrElse h <= 0 Then Return
        Dim s = DeviceDpi / 96.0F   ' DPI scale for hand-placed drawing
        g.SmoothingMode = SmoothingMode.AntiAlias

        Using bg As New LinearGradientBrush(brandPanel.ClientRectangle, Theme.SidebarBlue, Theme.SidebarBlueDark, 60.0F)
            g.FillRectangle(bg, brandPanel.ClientRectangle)
        End Using

        ' Soft decorative circles
        Using circle As New SolidBrush(Color.FromArgb(18, 255, 255, 255))
            g.FillEllipse(circle, w - 180 * s, -120 * s, 340 * s, 340 * s)
            g.FillEllipse(circle, -140 * s, h - 200 * s, 320 * s, 320 * s)
        End Using

        ' Logo badge
        Dim x = CInt(48 * s)
        Using badge As New SolidBrush(Color.White)
            g.FillEllipse(badge, x, 64 * s, 56 * s, 56 * s)
        End Using
        TextRenderer.DrawText(g, Icons.Document, Theme.IconFont(18.0F),
                              New Rectangle(x, CInt(64 * s), CInt(56 * s), CInt(56 * s)), Theme.SidebarBlue,
                              TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)

        TextRenderer.DrawText(g, "BizTracker", Theme.DisplayFont, New Point(x - 4, CInt(140 * s)), Color.White)
        TextRenderer.DrawText(g, "Business Permit and Regulatory" & vbCrLf & "Compliance System",
                              Theme.SubtitleFont, New Rectangle(x, CInt(188 * s), w - 2 * x, CInt(60 * s)),
                              Theme.SidebarTextMuted, TextFormatFlags.WordBreak)

        If lguName <> "" Then
            TextRenderer.DrawText(g, lguName & If(province <> "", ", " & province, ""), Theme.BodyFont,
                                  New Point(x, CInt(258 * s)), Color.White, TextFormatFlags.NoPrefix)   ' show "&" as typed
        End If

        ' The six services, as a checklist
        Dim services = {"Business Permit", "Sanitary Permit", "Real Property Tax",
                        "Health Certificates", "Annual Inspection", "Construction Permit"}
        Dim y = CInt(h - 250 * s)
        For Each service In services
            TextRenderer.DrawText(g, Icons.CheckMark, Theme.IconFont(9.0F), New Point(x, y + CInt(3 * s)), Theme.SidebarTextMuted)
            TextRenderer.DrawText(g, service, Theme.BodyFont, New Point(x + CInt(24 * s), y), Color.White)
            y += CInt(28 * s)
        Next
        TextRenderer.DrawText(g, "RA 7160  ·  RA 11032 (Ease of Doing Business)", Theme.SmallFont,
                              New Point(x, CInt(h - 48 * s)), Theme.SidebarTextMuted)
    End Sub

    ' ==================== Behaviour ====================

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        lguName = SettingsRepository.GetValue("lgu_name")
        province = SettingsRepository.GetValue("province")
        brandPanel.Invalidate()
        ActiveControl = txtUsername
    End Sub

    Private Sub BtnLogin_Click(sender As Object, e As EventArgs)
        HideError()
        If txtUsername.Text.Trim() = "" Then
            ShowError("Please enter your username.") : txtUsername.Focus() : Return
        End If
        If txtPassword.Text = "" Then
            ShowError("Please enter your password.") : txtPassword.Focus() : Return
        End If

        Cursor = Cursors.WaitCursor
        btnLogin.Enabled = False
        btnLogin.Text = "Signing in..."
        btnLogin.Refresh()

        Dim result = AuthService.Login(txtUsername.Text, txtPassword.Text)

        Cursor = Cursors.Default
        btnLogin.Text = "Sign in"
        btnLogin.Enabled = True

        If result.Success Then
            DialogResult = DialogResult.OK
            Close()
            Return
        End If

        ShowError(result.Message)
        txtPassword.Clear()
        If result.LockedSeconds > 0 Then
            StartLockCountdown()
        Else
            txtPassword.Focus()
        End If
    End Sub

    Private Sub StartLockCountdown()
        LockTimer_Tick(Nothing, EventArgs.Empty)
        lockTimer.Start()
    End Sub

    ''' <summary>Every second while locked: show the countdown on the button.</summary>
    Private Sub LockTimer_Tick(sender As Object, e As EventArgs)
        Dim seconds = AuthService.GetLockedSeconds(txtUsername.Text)
        If seconds > 0 Then
            btnLogin.Enabled = False
            btnLogin.Text = "Try again in " & seconds & "s"
            ShowError("Too many failed attempts. Please wait " & seconds & " seconds.")
        Else
            lockTimer.Stop()
            btnLogin.Enabled = True
            btnLogin.Text = "Sign in"
            HideError()
            txtPassword.Focus()
        End If
    End Sub

    Private Sub ShowError(message As String)
        lblError.Text = message
        errorBox.Visible = True
    End Sub

    Private Sub HideError()
        errorBox.Visible = False
    End Sub

    ' ==================== Borderless window helpers ====================

    <DllImport("user32.dll")>
    Private Shared Function ReleaseCapture() As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function SendMessage(hWnd As IntPtr, msg As Integer, wParam As Integer, lParam As Integer) As IntPtr
    End Function

    <DllImport("dwmapi.dll")>
    Private Shared Function DwmSetWindowAttribute(hwnd As IntPtr, attribute As Integer, ByRef value As Integer, size As Integer) As Integer
    End Function

    ''' <summary>Lets the user drag the borderless window by its background.</summary>
    Private Sub DragWindow(sender As Object, e As MouseEventArgs)
        If e.Button <> MouseButtons.Left Then Return
        Const WM_NCLBUTTONDOWN As Integer = &HA1
        Const HTCAPTION As Integer = 2
        ReleaseCapture()
        SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0)
    End Sub

    ''' <summary>Drop shadow around the borderless window.</summary>
    Protected Overrides ReadOnly Property CreateParams As CreateParams
        Get
            Const CS_DROPSHADOW As Integer = &H20000
            Dim cp = MyBase.CreateParams
            cp.ClassStyle = cp.ClassStyle Or CS_DROPSHADOW
            Return cp
        End Get
    End Property

    ''' <summary>Rounded window corners on Windows 11 (ignored on Windows 10).</summary>
    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        Const DWMWA_WINDOW_CORNER_PREFERENCE As Integer = 33
        Dim DWMWCP_ROUND As Integer = 2
        Try
            DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUND, 4)
        Catch
            ' Older Windows - square corners are fine
        End Try
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then lockTimer.Dispose()
        MyBase.Dispose(disposing)
    End Sub

End Class
