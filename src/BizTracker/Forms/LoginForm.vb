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
    Private ReadOnly brandTips As New ToolTip()
    Private hoverService As Integer = -1      ' service card under the mouse (-1 = none)

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
        AddHandler brandPanel.MouseMove, AddressOf BrandPanel_MouseMove
        AddHandler brandPanel.MouseLeave, Sub()
                                              hoverService = -1
                                              brandTips.SetToolTip(brandPanel, "")
                                              brandPanel.Invalidate()
                                          End Sub
        ' Double-buffered so the hover highlight does not flicker
        GetType(Control).GetProperty("DoubleBuffered", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic).
            SetValue(brandPanel, True)
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
            g.FillEllipse(badge, x, 48 * s, 56 * s, 56 * s)
        End Using
        TextRenderer.DrawText(g, Icons.Document, Theme.IconFont(18.0F),
                              New Rectangle(x, CInt(48 * s), CInt(56 * s), CInt(56 * s)), Theme.SidebarBlue,
                              TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)

        TextRenderer.DrawText(g, "BizTracker", Theme.DisplayFont, New Point(x - 4, CInt(118 * s)), Color.White)
        TextRenderer.DrawText(g, "Business Permit and Regulatory" & vbCrLf & "Compliance System",
                              Theme.SubtitleFont, New Rectangle(x, CInt(164 * s), w - 2 * x, CInt(50 * s)),
                              Theme.SidebarTextMuted, TextFormatFlags.WordBreak)

        If lguName <> "" Then
            TextRenderer.DrawText(g, lguName & If(province <> "", ", " & province, ""), Theme.BodyFont,
                                  New Point(x, CInt(222 * s)), Color.White, TextFormatFlags.NoPrefix)   ' show "&" as typed
        End If

        ' The six services: one card each (icon, name, office); the card under the mouse lights up
        TextRenderer.DrawText(g, "SIX SERVICES IN ONE SYSTEM", Theme.SmallBoldFont,
                              New Point(x, ServiceRect(0).Top - CInt(24 * s)), Theme.SidebarTextMuted)
        Dim oneLine = TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix
        For i = 0 To Services.Length - 1
            Dim r = ServiceRect(i)
            Using path = UiHelper.RoundedRect(r, CInt(8 * s)),
                  fill As New SolidBrush(Color.FromArgb(If(i = hoverService, 52, 22), 255, 255, 255))
                g.FillPath(fill, path)
            End Using
            Dim iconBox As New Rectangle(r.X + CInt(8 * s), r.Y + (r.Height - CInt(24 * s)) \ 2, CInt(24 * s), CInt(24 * s))
            Using circle As New SolidBrush(Color.FromArgb(If(i = hoverService, 255, 230), 255, 255, 255))
                g.FillEllipse(circle, iconBox)
            End Using
            TextRenderer.DrawText(g, Services(i).Glyph, Theme.IconFont(9.0F), iconBox, Theme.SidebarBlue,
                                  TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
            ' Name on the left, office on the right (muted), with room between them
            Dim textLeft = iconBox.Right + CInt(10 * s)
            Dim officeW = TextRenderer.MeasureText(Services(i).Office, Theme.SmallFont).Width
            Dim officeRect As New Rectangle(r.Right - CInt(10 * s) - officeW, r.Y, officeW, r.Height)
            TextRenderer.DrawText(g, Services(i).Name, Theme.BodyBoldFont,
                                  New Rectangle(textLeft, r.Y, officeRect.Left - textLeft - CInt(8 * s), r.Height), Color.White, oneLine)
            TextRenderer.DrawText(g, Services(i).Office, Theme.SmallFont, officeRect, Theme.SidebarTextMuted, oneLine Or TextFormatFlags.Right)
        Next
        TextRenderer.DrawText(g, "RA 7160  ·  RA 11032 (Ease of Doing Business)", Theme.SmallFont,
                              New Point(x, CInt(h - 40 * s)), Theme.SidebarTextMuted)
    End Sub

    ' ==================== Service cards ====================

    Private Structure ServiceInfo
        Public Name As String
        Public Office As String
        Public Glyph As String
        Public Tip As String
    End Structure

    ''' <summary>The six services (same icons as the sidebar).</summary>
    Private Shared ReadOnly Services As ServiceInfo() = {
        New ServiceInfo With {.Name = "Business Permit", .Office = "BPLO", .Glyph = Icons.Document,
                              .Tip = "New and renewal Mayor's permits, with clearances from five offices"},
        New ServiceInfo With {.Name = "Sanitary Permit", .Office = "Health Office", .Glyph = Icons.CheckShield,
                              .Tip = "Yearly sanitary permit with inspection score (valid until Dec 31)"},
        New ServiceInfo With {.Name = "Real Property Tax", .Office = "Assessor", .Glyph = Icons.Bank,
                              .Tip = "Assessments and quarterly payments of land, buildings and machinery"},
        New ServiceInfo With {.Name = "Health Certificates", .Office = "Health Office", .Glyph = Icons.Health,
                              .Tip = "Food handler and non-food staff certificates (valid 1 year)"},
        New ServiceInfo With {.Name = "Annual Inspection", .Office = "Joint Team", .Glyph = Icons.Search,
                              .Tip = "Structural, electrical, mechanical and fire inspection"},
        New ServiceInfo With {.Name = "Construction Permit", .Office = "OBO · MPDO", .Glyph = Icons.Repair,
                              .Tip = "Locational clearance, building permit and occupancy"}
    }

    ''' <summary>Where service card i is drawn (one column, below the branding).</summary>
    Private Function ServiceRect(i As Integer) As Rectangle
        Dim s = DeviceDpi / 96.0F
        Dim x = CInt(48 * s)
        Dim rowH = CInt(34 * s), gap = CInt(6 * s)
        Dim top = CInt(300 * s)
        Return New Rectangle(x, top + i * (rowH + gap), brandPanel.ClientSize.Width - 2 * x, rowH)
    End Function

    Private Sub BrandPanel_MouseMove(sender As Object, e As MouseEventArgs)
        Dim found = -1
        For i = 0 To Services.Length - 1
            If ServiceRect(i).Contains(e.Location) Then found = i : Exit For
        Next
        If found = hoverService Then Return
        hoverService = found
        brandTips.SetToolTip(brandPanel, If(found >= 0, Services(found).Tip, ""))
        brandPanel.Invalidate()
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
        If disposing Then lockTimer.Dispose() : brandTips.Dispose()
        MyBase.Dispose(disposing)
    End Sub

End Class
