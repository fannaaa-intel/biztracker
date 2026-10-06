''' <summary>
''' Admin tools (sidebar "Settings", Admin only). Same layout as the module screens:
'''   Toolbar : tabs  Users | Businesses | Settings | Audit Log | Backup  + the open tab's actions
'''   Cards   : active users, active businesses, activity today, last backup
'''   Page    : the open tab (AdminUsersPage, AdminBusinessesPage, AdminSettingsPage, AdminAuditPage, AdminBackupPage)
''' Every rule and role check is in the services (UserService, BusinessService, SettingsService,
''' AuditLogService, BackupService). No SQL here.
''' </summary>
Public Class AdminView
    Inherits ModuleView

    Public Const UsersTab As Integer = 0
    Public Const BusinessesTab As Integer = 1
    Public Const SettingsTab As Integer = 2
    Public Const AuditTab As Integer = 3
    Public Const BackupTab As Integer = 4

    Private ReadOnly tabs As New SegmentedTabs()
    Private ReadOnly toolbar As ModuleToolbar

    Private ReadOnly cardUsers As New StatCard(Icons.People, "Active Users")
    Private ReadOnly cardBusinesses As New StatCard(Icons.City, "Active Businesses")
    Private ReadOnly cardActivity As New StatCard(Icons.History, "Activity Today")
    Private ReadOnly cardBackup As New StatCard(Icons.Save, "Last Backup")

    Private ReadOnly pageHost As New Panel()
    Private ReadOnly pages As AdminPage()
    Private ReadOnly lblDenied As New Label()

    Public ReadOnly Property UsersPage As AdminUsersPage
    Public ReadOnly Property BusinessesPage As AdminBusinessesPage
    Public ReadOnly Property SettingsPage As AdminSettingsPage
    Public ReadOnly Property AuditPage As AdminAuditPage
    Public ReadOnly Property BackupPage As AdminBackupPage

    Public Sub New()
        SuspendLayout()
        tabs.Tabs = {"Users", "Businesses", "Settings", "Audit Log", "Backup"}
        tabs.SetBounds(0, 3, 400, 40)
        toolbar = New ModuleToolbar(tabs, 380, 520)

        UsersPage = New AdminUsersPage(toolbar)
        BusinessesPage = New AdminBusinessesPage(toolbar)
        SettingsPage = New AdminSettingsPage(toolbar)
        AuditPage = New AdminAuditPage(toolbar)
        BackupPage = New AdminBackupPage(toolbar)
        pages = {CType(UsersPage, AdminPage), BusinessesPage, SettingsPage, AuditPage, BackupPage}
        For Each p In pages
            p.Visible = False
            AddHandler p.DataChanged, Sub() UpdateCards()
            pageHost.Controls.Add(p)
        Next
        AddHandler BusinessesPage.OpenUserRequested, Sub(id) OpenUser(id)
        AddHandler tabs.SelectedIndexChanged, Sub() ShowTab()

        pageHost.Dock = DockStyle.Fill
        pageHost.BackColor = Theme.ContentBackground
        pageHost.Margin = New Padding(0)

        Dim root As New TableLayoutPanel With {
            .Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 3, .BackColor = Theme.ContentBackground,
            .Margin = New Padding(0), .Padding = New Padding(0)
        }
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 56))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 110))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        root.Controls.Add(toolbar, 0, 0)
        root.Controls.Add(BuildCards(), 0, 1)
        root.Controls.Add(pageHost, 0, 2)
        Controls.Add(root)
        ResumeLayout(False)
    End Sub

    Public Overrides ReadOnly Property Subtitle As String
        Get
            Return "Users, business registry, settings, audit log and backup"
        End Get
    End Property

    ''' <summary>The tab bar (tests switch tabs through it).</summary>
    Public ReadOnly Property TabBar As SegmentedTabs
        Get
            Return tabs
        End Get
    End Property

    Private Function BuildCards() As Control
        Dim cards = {cardUsers, cardBusinesses, cardActivity, cardBackup}
        Dim row As New TableLayoutPanel With {
            .Dock = DockStyle.Fill, .ColumnCount = cards.Length, .RowCount = 1, .BackColor = Theme.ContentBackground,
            .Margin = New Padding(0), .Padding = New Padding(0)
        }
        row.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        For i = 0 To cards.Length - 1
            row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F / cards.Length))
            cards(i).Dock = DockStyle.Fill
            cards(i).Margin = New Padding(If(i = 0, 0, 8), 0, If(i = cards.Length - 1, 0, 8), 14)
            row.Controls.Add(cards(i), i, 0)
        Next
        Return row
    End Function

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        If Not Session.IsAdmin Then
            ' MainForm never opens this screen for other roles; this is a second line of defense.
            Controls.Clear()
            lblDenied.Text = "Only an Admin can open the admin tools."
            lblDenied.Dock = DockStyle.Fill
            lblDenied.TextAlign = ContentAlignment.MiddleCenter
            lblDenied.Font = Theme.SubtitleFont
            lblDenied.ForeColor = Theme.TextDark
            Controls.Add(lblDenied)
            Return
        End If
        UpdateCards()
        ShowTab()
    End Sub

    Public Overrides Sub RefreshData()
        If Not Session.IsAdmin Then Return
        UpdateCards()
        pages(tabs.SelectedIndex).RefreshData()
    End Sub

    Private Sub ShowTab()
        Dim index = tabs.SelectedIndex
        For i = 0 To pages.Length - 1
            pages(i).Visible = i = index
            pages(i).ShowActions(i = index)
        Next
        toolbar.Refit()
        pages(index).RefreshData()
    End Sub

    ''' <summary>Switches to the Users tab with an account selected (from the Businesses tab).</summary>
    Public Sub OpenUser(userId As Integer)
        tabs.SelectedIndex = UsersTab          ' reloads the list
        UsersPage.LoadUsers(userId)
    End Sub

    Private Sub UpdateCards()
        Dim users = UserService.GetUsers()
        Dim activeUsers = users.Where(Function(u) u.IsActive).Count()
        Dim inactiveUsers = users.Count - activeUsers
        cardUsers.SetValue(activeUsers.ToString(), "", If(inactiveUsers = 0, "all " & users.Count & " accounts active", inactiveUsers & " deactivated"))

        Dim businesses = BusinessService.GetBusinesses()
        Dim activeBiz = businesses.Where(Function(b) b.IsActive).Count()
        Dim inactiveBiz = businesses.Count - activeBiz
        cardBusinesses.SetValue(activeBiz.ToString(), "", If(inactiveBiz = 0, "none deactivated", inactiveBiz & " deactivated"))

        cardActivity.SetValue(AuditLogService.CountToday().ToString(), "", "audit log entries")

        Dim last = BackupService.GetHistory(1).FirstOrDefault()
        If last Is Nothing Then
            cardBackup.SetValue("Never", "", "back up regularly")
        Else
            Dim days = (Date.Today - last.CreatedAt.Date).Days
            cardBackup.SetValue(UiHelper.FormatDate(last.CreatedAt), "",
                                If(days = 0, "today", If(days = 1, "yesterday", days & " days ago")))
        End If
    End Sub

End Class
