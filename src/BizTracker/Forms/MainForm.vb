''' <summary>
''' The main application window (opens maximized):
'''   - left: blue sidebar with the screens the user's role can open
'''   - top:  page title, notification bell, signed-in user, logout
'''   - fill: content panel that hosts one ModuleView at a time
''' Shortcuts: Ctrl+1..8 switch pages, F5 refreshes the current page.
''' </summary>
Public Class MainForm
    Inherits Form

    ''' <summary>One sidebar entry: which screen, its label/icon/section, and how to create its page.</summary>
    Private Class NavItem
        Public Screen As AppScreen
        Public Title As String
        Public Glyph As String
        Public Section As String
        Public Create As Func(Of ModuleView)
    End Class

    Private ReadOnly navItems As New List(Of NavItem) From {
        New NavItem With {.Screen = AppScreen.Dashboard, .Title = "Dashboard", .Glyph = Icons.Home,
                          .Section = "OVERVIEW", .Create = Function() New DashboardView()},
        New NavItem With {.Screen = AppScreen.BusinessPermits, .Title = "Business Permits", .Glyph = Icons.Document,
                          .Section = "SERVICES", .Create = Function() New BusinessPermitView()},
        New NavItem With {.Screen = AppScreen.SanitaryPermits, .Title = "Sanitary Permits", .Glyph = Icons.CheckShield,
                          .Section = "SERVICES", .Create = Function() New SanitaryPermitView()},
        New NavItem With {.Screen = AppScreen.RealPropertyTax, .Title = "Real Property Tax", .Glyph = Icons.Bank,
                          .Section = "SERVICES", .Create = Function() New RealPropertyTaxView()},
        New NavItem With {.Screen = AppScreen.HealthCertificates, .Title = "Health Certificates", .Glyph = Icons.Health,
                          .Section = "SERVICES", .Create = Function() New HealthCertificateView()},
        New NavItem With {.Screen = AppScreen.AnnualInspections, .Title = "Annual Inspections", .Glyph = Icons.Search,
                          .Section = "SERVICES", .Create = Function() New AnnualInspectionView()},
        New NavItem With {.Screen = AppScreen.ConstructionPermits, .Title = "Construction Permit", .Glyph = Icons.Repair,
                          .Section = "SERVICES", .Create = Function() New ConstructionPermitView()},
        New NavItem With {.Screen = AppScreen.Settings, .Title = "Settings", .Glyph = Icons.Settings,
                          .Section = "ADMINISTRATION", .Create = Function() New SettingsView()}
    }

    Private ReadOnly sidebar As New Panel()
    Private ReadOnly navPanel As New FlowLayoutPanel()
    Private ReadOnly lblLgu As New Label()
    Private footer As Label
    Private ReadOnly topBar As New Panel()
    Private ReadOnly lblTitle As New Label()
    Private ReadOnly lblSubtitle As New Label()
    Private ReadOnly btnBell As New IconButton()
    Private ReadOnly avatar As New Avatar()
    Private ReadOnly lblUserName As New Label()
    Private ReadOnly lblUserRole As New Label()
    Private ReadOnly btnLogout As New IconButton()
    Private ReadOnly contentPanel As New Panel()
    Private ReadOnly tips As New ToolTip()

    Private ReadOnly navButtons As New List(Of NavButton)
    Private currentView As ModuleView

    ''' <summary>True when the user clicked Logout (Program then shows the login screen again).</summary>
    Public Property LoggedOut As Boolean

    Public Sub New()
        SuspendLayout()
        AutoScaleDimensions = New SizeF(96.0F, 96.0F)
        AutoScaleMode = AutoScaleMode.Dpi
        Text = "BizTracker"
        StartPosition = FormStartPosition.CenterScreen
        Size = New Size(1366, 800)
        MinimumSize = New Size(1100, 680)
        WindowState = FormWindowState.Maximized     ' full screen
        BackColor = Theme.ContentBackground
        Font = Theme.BodyFont
        KeyPreview = True

        BuildSidebar()
        BuildTopBar()
        contentPanel.Dock = DockStyle.Fill
        contentPanel.BackColor = Theme.ContentBackground
        contentPanel.Padding = New Padding(28, 24, 28, 24)

        ' Docking order: the last control added is docked first
        Controls.Add(contentPanel)   ' fills what is left
        Controls.Add(topBar)         ' top, right of the sidebar
        Controls.Add(sidebar)        ' full height on the left
        ResumeLayout(False)
    End Sub

    ' ==================== Sidebar ====================

    Private Sub BuildSidebar()
        sidebar.Dock = DockStyle.Left
        sidebar.Width = 260
        sidebar.BackColor = Theme.SidebarBlue

        ' --- Header: logo + app name + LGU ---
        Dim header As New Panel With {.Dock = DockStyle.Top, .Height = 92, .BackColor = Theme.SidebarBlue}
        Dim logo As New IconButton With {
            .Glyph = Icons.Document, .IconSize = 15.0F, .IconColor = Theme.SidebarBlue,
            .CircleColor = Color.White, .Cursor = Cursors.Default,
            .Size = New Size(44, 44), .Location = New Point(20, 24), .BackColor = Theme.SidebarBlue
        }
        Dim lblApp As New Label With {
            .Text = "BizTracker", .Font = Theme.BrandFont, .ForeColor = Color.White,
            .AutoSize = True, .Location = New Point(72, 22), .BackColor = Theme.SidebarBlue
        }
        lblLgu.Font = Theme.SmallFont
        lblLgu.ForeColor = Theme.SidebarTextMuted
        lblLgu.AutoSize = False
        lblLgu.AutoEllipsis = True
        lblLgu.SetBounds(74, 50, 176, 20)
        lblLgu.BackColor = Theme.SidebarBlue
        header.Controls.AddRange({logo, lblApp, lblLgu})

        Dim headerLine As New Panel With {.Dock = DockStyle.Top, .Height = 1, .BackColor = Theme.SidebarHover}

        ' --- Footer (shortcut hints; hidden automatically when the window is too short) ---
        footer = New Label With {
            .Dock = DockStyle.Bottom, .Height = 56, .Font = Theme.SmallFont, .ForeColor = Theme.SidebarTextMuted,
            .Text = "Ctrl+1–8 switch pages  ·  F5 refresh" & vbCrLf & "BizTracker v1.0",
            .TextAlign = ContentAlignment.MiddleLeft, .Padding = New Padding(22, 0, 0, 0),
            .BackColor = Theme.SidebarBlue
        }

        ' --- Navigation list (no scrollbars: everything fits even at the minimum window size) ---
        navPanel.Dock = DockStyle.Fill
        navPanel.FlowDirection = FlowDirection.TopDown
        navPanel.WrapContents = False
        navPanel.AutoScroll = False
        navPanel.Padding = New Padding(14, 4, 14, 4)
        navPanel.BackColor = Theme.SidebarBlue
        AddHandler navPanel.Resize, Sub() FitNavWidths()
        AddHandler sidebar.Resize, Sub() UpdateFooterVisibility()

        sidebar.Controls.Add(navPanel)     ' Fill (added first = docked last)
        sidebar.Controls.Add(footer)       ' Bottom
        sidebar.Controls.Add(headerLine)   ' Top, under header
        sidebar.Controls.Add(header)       ' Top
    End Sub

    ''' <summary>Adds the section titles and buttons for the screens this role can open.</summary>
    Private Sub BuildNavigation()
        navPanel.SuspendLayout()
        navPanel.Controls.Clear()
        navButtons.Clear()

        Dim lastSection = ""
        For Each item In navItems
            If Not AccessService.CanAccess(item.Screen) Then Continue For

            If item.Section <> lastSection Then
                navPanel.Controls.Add(New Label With {
                    .Text = item.Section, .Font = Theme.SmallBoldFont, .ForeColor = Theme.SidebarTextMuted,
                    .AutoSize = False, .Height = 30, .TextAlign = ContentAlignment.BottomLeft,
                    .Padding = New Padding(8, 0, 0, 5), .Margin = New Padding(0, 2, 0, 2),
                    .BackColor = Theme.SidebarBlue
                })
                lastSection = item.Section
            End If

            Dim btn As New NavButton With {.Text = item.Title, .Glyph = item.Glyph, .Screen = item.Screen, .Height = 42}
            AddHandler btn.Click, Sub(s, ev) ShowScreen(DirectCast(s, NavButton).Screen)
            navButtons.Add(btn)
            navPanel.Controls.Add(btn)
        Next
        navPanel.ResumeLayout()
        FitNavWidths()
        UpdateFooterVisibility()
    End Sub

    Private Sub FitNavWidths()
        Dim w = navPanel.ClientSize.Width - navPanel.Padding.Horizontal
        For Each c As Control In navPanel.Controls
            c.Width = w
        Next
    End Sub

    ''' <summary>Height the navigation items need (sections + buttons + spacing).</summary>
    Private Function NavContentHeight() As Integer
        Dim total = navPanel.Padding.Vertical
        For Each c As Control In navPanel.Controls
            total += c.Height + c.Margin.Vertical
        Next
        Return total
    End Function

    ''' <summary>
    ''' Hides the footer hints when the window is too short for footer + all nav items,
    ''' so the sidebar never needs a scrollbar.
    ''' </summary>
    Private Sub UpdateFooterVisibility()
        If footer Is Nothing Then Return
        ' Space below the header = nav panel + footer (when shown)
        Dim available = navPanel.Height + If(footer.Visible, footer.Height, 0)
        footer.Visible = available - footer.Height >= NavContentHeight()
    End Sub

    ' ==================== Top bar ====================

    Private Sub BuildTopBar()
        topBar.Dock = DockStyle.Top
        topBar.Height = 76
        topBar.BackColor = Theme.TopBarBackground
        ' Thin divider line along the bottom
        AddHandler topBar.Paint,
            Sub(s, e)
                Using pen As New Pen(Theme.Divider)
                    e.Graphics.DrawLine(pen, 0, topBar.Height - 1, topBar.Width, topBar.Height - 1)
                End Using
            End Sub

        lblTitle.Font = Theme.TitleFont
        lblTitle.ForeColor = Theme.TextDark
        lblTitle.AutoSize = True
        lblTitle.Location = New Point(26, 12)
        lblSubtitle.Font = Theme.BodyFont
        lblSubtitle.ForeColor = Theme.TextMuted
        lblSubtitle.AutoSize = True
        lblSubtitle.Location = New Point(28, 44)

        ' Right side, laid out right-to-left: logout | divider | user | bell
        Dim right As New FlowLayoutPanel With {
            .Dock = DockStyle.Right, .AutoSize = True, .WrapContents = False,
            .FlowDirection = FlowDirection.RightToLeft, .Padding = New Padding(0, 0, 20, 0),
            .BackColor = Theme.TopBarBackground
        }

        btnLogout.Glyph = Icons.SignOut
        btnLogout.Margin = New Padding(4, 18, 0, 0)
        btnLogout.HoverColor = Theme.StatusRedSoft
        btnLogout.IconColor = Theme.TextMuted
        tips.SetToolTip(btnLogout, "Log out")
        AddHandler btnLogout.Click, AddressOf BtnLogout_Click

        Dim divider As New Panel With {.Size = New Size(1, 36), .BackColor = Theme.Divider, .Margin = New Padding(12, 20, 12, 0)}

        Dim userBox As New Panel With {.Size = New Size(210, 76), .Margin = New Padding(0), .BackColor = Theme.TopBarBackground}
        avatar.Location = New Point(0, 19)
        lblUserName.Font = Theme.BodyBoldFont
        lblUserName.ForeColor = Theme.TextDark
        lblUserName.AutoSize = False
        lblUserName.AutoEllipsis = True
        lblUserName.SetBounds(48, 17, 162, 22)
        lblUserRole.Font = Theme.SmallFont
        lblUserRole.ForeColor = Theme.TextMuted
        lblUserRole.AutoSize = False
        lblUserRole.AutoEllipsis = True
        lblUserRole.SetBounds(48, 39, 162, 20)
        userBox.Controls.AddRange({avatar, lblUserName, lblUserRole})

        btnBell.Glyph = Icons.Bell
        btnBell.Margin = New Padding(0, 18, 8, 0)
        tips.SetToolTip(btnBell, "Notifications")
        AddHandler btnBell.Click, AddressOf BtnBell_Click

        right.Controls.AddRange({btnLogout, divider, userBox, btnBell})
        topBar.Controls.AddRange({lblTitle, lblSubtitle, right})
    End Sub

    ' ==================== Behaviour ====================

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)

        Dim lgu = SettingsRepository.GetValue("lgu_name")
        Dim province = SettingsRepository.GetValue("province")
        lblLgu.Text = lgu & If(province <> "", ", " & province, "")
        Text = "BizTracker  —  " & lgu

        avatar.Text = Session.FullName
        lblUserName.Text = Session.FullName
        lblUserRole.Text = Session.Role
        If Session.IsOwner AndAlso Session.BusinessId.HasValue Then
            Dim biz = BusinessRepository.GetById(Session.BusinessId.Value)
            If biz IsNot Nothing Then lblUserRole.Text = "Owner  ·  " & biz.BusinessName
        End If
        tips.SetToolTip(lblUserRole, lblUserRole.Text)

        BuildNavigation()
        Dim screens = AccessService.GetScreens(Session.Role)
        If screens.Count > 0 Then ShowScreen(screens(0))
    End Sub

    ''' <summary>Loads a page into the content area (only if the role may open it).</summary>
    Private Sub ShowScreen(screen As AppScreen)
        If Not AccessService.CanAccess(screen) Then Return
        Dim item = navItems.First(Function(n) n.Screen = screen)

        Cursor = Cursors.WaitCursor
        contentPanel.SuspendLayout()
        Dim oldView = currentView
        currentView = item.Create()
        currentView.Dock = DockStyle.Fill
        contentPanel.Controls.Add(currentView)
        If oldView IsNot Nothing Then
            contentPanel.Controls.Remove(oldView)
            oldView.Dispose()
        End If
        contentPanel.ResumeLayout()

        lblTitle.Text = item.Title
        lblSubtitle.Text = If(currentView.Subtitle <> "", currentView.Subtitle,
                              Date.Today.ToString("dddd, MMMM d, yyyy"))
        For Each btn In navButtons
            btn.IsActive = (btn.Screen = screen)
        Next
        RefreshNotificationCount()
        Cursor = Cursors.Default
    End Sub

    ''' <summary>Unread alerts: the owner's business only, or all businesses for staff.</summary>
    Private Sub RefreshNotificationCount()
        btnBell.BadgeCount = NotificationRepository.GetUnreadCount(If(Session.IsOwner, Session.BusinessId, Nothing))
    End Sub

    Private Sub BtnBell_Click(sender As Object, e As EventArgs)
        RefreshNotificationCount()
        Dim count = btnBell.BadgeCount
        UiHelper.ShowInfo(If(count = 0, "You have no unread notifications.",
                             "You have " & count & " unread notification" & If(count = 1, "", "s") & ".") &
                          vbCrLf & vbCrLf & "The full notification center arrives in Phase 12.", "Notifications")
    End Sub

    Private Sub BtnLogout_Click(sender As Object, e As EventArgs)
        If Not UiHelper.Confirm("Do you want to log out of BizTracker?", "Log out") Then Return
        LoggedOut = True
        AuthService.Logout()
        Close()
    End Sub

    ''' <summary>Closing the window (X) also ends the session and is written to the audit log.</summary>
    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        If Not LoggedOut Then AuthService.Logout()
        MyBase.OnFormClosing(e)
    End Sub

    ''' <summary>Keyboard shortcuts: Ctrl+1..8 = n-th sidebar page, F5 = refresh.</summary>
    Protected Overrides Function ProcessCmdKey(ByRef msg As Message, keyData As Keys) As Boolean
        If keyData = Keys.F5 Then
            currentView?.RefreshData()
            RefreshNotificationCount()
            Return True
        End If
        Dim key = keyData And Keys.KeyCode
        If (keyData And Keys.Control) = Keys.Control AndAlso key >= Keys.D1 AndAlso key <= Keys.D9 Then
            Dim index = key - Keys.D1
            If index < navButtons.Count Then
                ShowScreen(navButtons(index).Screen)
                Return True
            End If
        End If
        Return MyBase.ProcessCmdKey(msg, keyData)
    End Function

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then tips.Dispose()
        MyBase.Dispose(disposing)
    End Sub

End Class
