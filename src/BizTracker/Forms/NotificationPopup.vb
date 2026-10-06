''' <summary>
''' The drop-down that opens under the bell:
'''   header : "Notifications", unread count, "Mark all read"
'''   tabs   : Unread | All
'''   list   : one row per alert (wheel scrolling, no scrollbar); click = open the module, ✓ = mark read
''' Closes when the user clicks outside it or presses Esc. No SQL here: everything goes through AlertService.
''' </summary>
Public Class NotificationPopup
    Inherits Form

    Private ReadOnly s As Single
    Private ReadOnly lblTitle As New Label()
    Private ReadOnly badgeCount As New StatusBadge()
    Private ReadOnly lnkMarkAll As New LinkLabel()
    Private ReadOnly tabs As New SegmentedTabs()
    Private ReadOnly list As New WheelScrollPanel()
    Private ReadOnly lblEmpty As New Label()
    Private ReadOnly tips As New ToolTip()
    Private items As New List(Of Notification)

    ''' <summary>A row was clicked: MainForm marks it read and opens its module.</summary>
    Public Event NotificationOpened(n As Notification)
    ''' <summary>Something was marked read (MainForm refreshes the bell count).</summary>
    Public Event ReadChanged As EventHandler

    Public Sub New()
        AutoScaleMode = AutoScaleMode.None      ' sizes below are already scaled by s
        FormBorderStyle = FormBorderStyle.None
        ShowInTaskbar = False
        StartPosition = FormStartPosition.Manual
        KeyPreview = True
        BackColor = Color.White
        Font = Theme.BodyFont
        Text = "Notifications"
        s = DeviceDpi / 96.0F
        Dim pad = CInt(18 * s)
        Size = New Size(CInt(460 * s), CInt(540 * s))
        Padding = New Padding(pad, CInt(14 * s), pad, CInt(12 * s))

        ' Header
        Dim header As New Panel With {.Dock = DockStyle.Top, .Height = CInt(40 * s), .BackColor = Color.White}
        lblTitle.Text = "Notifications"
        lblTitle.Font = Theme.SubtitleFont
        lblTitle.ForeColor = Theme.TextDark
        lblTitle.AutoSize = True
        lblTitle.BackColor = Color.White
        badgeCount.Height = CInt(22 * s)
        lnkMarkAll.Text = "Mark all read"
        lnkMarkAll.AutoSize = True
        lnkMarkAll.Font = Theme.SmallBoldFont
        lnkMarkAll.LinkColor = Theme.SidebarBlue
        lnkMarkAll.ActiveLinkColor = Theme.SidebarActive
        lnkMarkAll.DisabledLinkColor = Theme.TextMuted
        lnkMarkAll.LinkBehavior = LinkBehavior.HoverUnderline
        lnkMarkAll.BackColor = Color.White
        AddHandler lnkMarkAll.LinkClicked, Sub() MarkAllRead()
        header.Controls.AddRange({lblTitle, badgeCount, lnkMarkAll})
        AddHandler header.Layout,
            Sub()
                lblTitle.Location = New Point(0, (header.Height - lblTitle.Height) \ 2)
                badgeCount.Location = New Point(lblTitle.Right + CInt(8 * s), (header.Height - badgeCount.Height) \ 2)
                lnkMarkAll.Location = New Point(header.ClientSize.Width - lnkMarkAll.Width, (header.Height - lnkMarkAll.Height) \ 2)
                badgeCount.Visible = badgeCount.Text <> "" AndAlso badgeCount.Right + CInt(8 * s) <= lnkMarkAll.Left
            End Sub

        tabs.Dock = DockStyle.Top
        tabs.Tabs = {"Unread", "All"}
        AddHandler tabs.SelectedIndexChanged, Sub() LoadRows()
        Dim gap As New Panel With {.Dock = DockStyle.Top, .Height = CInt(8 * s), .BackColor = Color.White}

        list.Dock = DockStyle.Fill
        list.BackColor = Color.White
        lblEmpty.Dock = DockStyle.Fill
        lblEmpty.TextAlign = ContentAlignment.MiddleCenter
        lblEmpty.Font = Theme.BodyFont
        lblEmpty.ForeColor = Theme.TextMuted
        lblEmpty.BackColor = Color.White
        lblEmpty.Visible = False

        Controls.Add(lblEmpty)
        Controls.Add(list)
        Controls.Add(gap)
        Controls.Add(tabs)
        Controls.Add(header)
        UiHelper.DisableMnemonics(Me)
    End Sub

    ''' <summary>Drop shadow around the borderless window.</summary>
    Protected Overrides ReadOnly Property CreateParams As CreateParams
        Get
            Dim cp = MyBase.CreateParams
            cp.ClassStyle = cp.ClassStyle Or &H20000     ' CS_DROPSHADOW
            Return cp
        End Get
    End Property

    ' ---------- For tests ----------

    Public ReadOnly Property Rows As List(Of NotificationRow)
        Get
            Return list.Controls.OfType(Of NotificationRow)().ToList()
        End Get
    End Property

    Public Property SelectedTab As Integer
        Get
            Return tabs.SelectedIndex
        End Get
        Set(value As Integer)
            tabs.SelectedIndex = value
        End Set
    End Property

    ' ---------- Showing ----------

    ''' <summary>Opens under the given screen point (the bell's bottom-right corner), kept inside the owner window.</summary>
    Public Sub ShowUnder(owner As Form, anchorScreen As Point)
        Dim area = owner.RectangleToScreen(owner.ClientRectangle)
        Height = Math.Max(CInt(260 * s), Math.Min(CInt(540 * s), area.Bottom - anchorScreen.Y - CInt(16 * s)))
        Location = New Point(Math.Max(area.Left + CInt(8 * s), Math.Min(anchorScreen.X - Width, area.Right - Width - CInt(8 * s))),
                             anchorScreen.Y + CInt(4 * s))
        RefreshData()
        Show(owner)
    End Sub

    Public Sub RefreshData()
        PerformLayout()      ' the list needs its final width before the rows are measured
        Dim unread = AlertService.GetUnreadCount()
        tabs.Tabs = {"Unread (" & unread & ")", "All"}
        badgeCount.Text = If(unread = 0, "", unread & " new")
        lnkMarkAll.Visible = unread > 0      ' nothing to mark: the link is hidden
        tips.SetToolTip(lnkMarkAll, "Mark every notification as read")
        lblTitle.Parent?.PerformLayout()
        LoadRows()
    End Sub

    Private Sub LoadRows()
        items = AlertService.GetNotifications(unreadOnly:=tabs.SelectedIndex = 0)
        Dim rowWidth = list.ClientSize.Width
        Dim rows As New List(Of Control)
        For Each n In items
            Dim row As New NotificationRow(n, rowWidth)
            tips.SetToolTip(row, n.Message & vbCrLf & row.DetailLine & vbCrLf &
                                 "Click to open " & ModuleTitle(n) & If(n.IsRead, "", "  ·  " & ChrW(&H2713) & " marks it read"))
            AddHandler row.OpenRequested, Sub(sender, e) BeginInvoke(Sub() Open(DirectCast(sender, NotificationRow).Notification))
            AddHandler row.MarkReadRequested, Sub(sender, e) BeginInvoke(Sub() MarkRead(DirectCast(sender, NotificationRow).Notification))
            rows.Add(row)
        Next
        list.SetRows(rows)
        lblEmpty.Visible = items.Count = 0
        If items.Count = 0 Then
            lblEmpty.Text = If(tabs.SelectedIndex = 0,
                               "You're all caught up." & vbCrLf & "No unread notifications.",
                               "No notifications yet." & vbCrLf & "Expiry and due-date alerts appear here.")
            lblEmpty.BringToFront()
        End If
    End Sub

    Private Shared Function ModuleTitle(n As Notification) As String
        Return n.ModuleName
    End Function

    ' ---------- Actions ----------

    ''' <summary>Marks one alert read (the ✓ on a row).</summary>
    Public Sub MarkRead(n As Notification)
        If AlertService.MarkRead(n) Then
            RaiseEvent ReadChanged(Me, EventArgs.Empty)
            RefreshData()
        End If
    End Sub

    Public Sub MarkAllRead()
        If AlertService.MarkAllRead() > 0 Then RaiseEvent ReadChanged(Me, EventArgs.Empty)
        RefreshData()
    End Sub

    ''' <summary>Opens the alert's module (MainForm marks it read and navigates).</summary>
    Public Sub Open(n As Notification)
        Close()
        RaiseEvent NotificationOpened(n)
    End Sub

    ' ---------- Closing / painting ----------

    Protected Overrides Sub OnDeactivate(e As EventArgs)
        MyBase.OnDeactivate(e)
        If Not IsDisposed Then BeginInvoke(Sub() If Not IsDisposed Then Close())
    End Sub

    Protected Overrides Function ProcessCmdKey(ByRef msg As Message, keyData As Keys) As Boolean
        If keyData = Keys.Escape Then Close() : Return True
        Return MyBase.ProcessCmdKey(msg, keyData)
    End Function

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        Using pen As New Pen(Theme.Divider)
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1)
        End Using
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then tips.Dispose()
        MyBase.Dispose(disposing)
    End Sub

End Class

''' <summary>
''' One alert in the bell popup:
'''   (dot) Message (bold while unread, up to 2 lines)          ✓
'''         Business · Module · Oct 1, 2026 · 5 days ago
''' Dot: red Urgent, amber Warning, blue Info. Click = open; ✓ (unread only) = mark read.
''' </summary>
Public Class NotificationRow
    Inherits Control

    Private _hover As Boolean
    Private _hoverCheck As Boolean
    Private ReadOnly titleLines As Integer

    Public ReadOnly Property Notification As Notification
    Public Event OpenRequested As EventHandler
    Public Event MarkReadRequested As EventHandler

    Public Sub New(n As Notification, rowWidth As Integer)
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        Notification = n
        Text = n.Message
        Cursor = Cursors.Hand
        Dim sc = DeviceDpi / 96.0
        ' One or two title lines, measured for the width the row will get
        Dim lineH = TextRenderer.MeasureText("Ag", TitleFont).Height
        Dim need = TextRenderer.MeasureText(n.Message, TitleFont, New Size(Math.Max(50, TextWidth(rowWidth)), Integer.MaxValue),
                                            TextFormatFlags.WordBreak Or TextFormatFlags.NoPrefix).Height
        titleLines = If(need > lineH * 1.5, 2, 1)
        Height = lineH * titleLines + TextRenderer.MeasureText("Ag", Theme.SmallFont).Height + CInt(22 * sc)
    End Sub

    Private ReadOnly Property TitleFont As Font
        Get
            Return If(Notification.IsRead, Theme.BodyFont, Theme.BodyBoldFont)
        End Get
    End Property

    Private ReadOnly Property DpiFactor As Single
        Get
            Return DeviceDpi / 96.0F
        End Get
    End Property

    Private Function TextLeft() As Integer
        Return CInt(22 * DpiFactor)
    End Function

    Private Function CheckWidth() As Integer
        Return CInt(30 * DpiFactor)
    End Function

    Private Function TextWidth(rowWidth As Integer) As Integer
        Return rowWidth - TextLeft() - CheckWidth() - CInt(6 * DpiFactor)
    End Function

    ''' <summary>"Cristan's Bakeshop · Health Certificate · Oct 1, 2026 · 5 days ago" (no business for owners).</summary>
    Public ReadOnly Property DetailLine As String
        Get
            Return If(Session.IsOwner, "", Notification.BusinessName & " · ") & Notification.ModuleName & " · " & AlertService.DueText(Notification)
        End Get
    End Property

    Private Function CheckRect() As Rectangle
        Return New Rectangle(Width - CheckWidth(), 0, CheckWidth(), Height)
    End Function

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        Dim overCheck = Not Notification.IsRead AndAlso CheckRect().Contains(e.Location)
        If Not _hover OrElse overCheck <> _hoverCheck Then
            _hover = True : _hoverCheck = overCheck : Invalidate()
        End If
        MyBase.OnMouseMove(e)
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        _hover = False : _hoverCheck = False : Invalidate()
        MyBase.OnMouseLeave(e)
    End Sub

    Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
        MyBase.OnMouseClick(e)
        If Not Notification.IsRead AndAlso CheckRect().Contains(e.Location) Then
            RaiseEvent MarkReadRequested(Me, EventArgs.Empty)
        Else
            RaiseEvent OpenRequested(Me, EventArgs.Empty)
        End If
    End Sub

    ''' <summary>Same as clicking the row (for tests).</summary>
    Public Sub PerformOpen()
        RaiseEvent OpenRequested(Me, EventArgs.Empty)
    End Sub

    ''' <summary>Same as clicking the ✓ (for tests).</summary>
    Public Sub PerformMarkRead()
        If Not Notification.IsRead Then RaiseEvent MarkReadRequested(Me, EventArgs.Empty)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        Dim sc = DpiFactor
        g.Clear(If(_hover, Theme.SoftBackground, If(Notification.IsRead, Color.White, Color.FromArgb(247, 251, 253))))
        Dim dotColor = If(Notification.Priority = AlertService.Urgent, Theme.StatusRed,
                          If(Notification.Priority = AlertService.Warning, Theme.StatusAmber, Theme.SidebarBlue))
        If Notification.IsRead Then dotColor = ControlPaint.Light(dotColor, 0.9F)

        Dim lineH = TextRenderer.MeasureText("Ag", TitleFont).Height
        Dim detailH = TextRenderer.MeasureText("Ag", Theme.SmallFont).Height
        Dim top = (Height - lineH * titleLines - detailH - CInt(4 * sc)) \ 2
        Dim dot = CInt(10 * sc)
        g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
        Using brush As New SolidBrush(dotColor)
            g.FillEllipse(brush, CInt(4 * sc), top + (lineH - dot) \ 2, dot, dot)
        End Using
        g.SmoothingMode = Drawing2D.SmoothingMode.None

        Dim textW = TextWidth(Width)
        TextRenderer.DrawText(g, Notification.Message, TitleFont, New Rectangle(TextLeft(), top, textW, lineH * titleLines),
                              If(Notification.IsRead, Theme.TextMuted, Theme.TextDark),
                              TextFormatFlags.Left Or TextFormatFlags.WordBreak Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix)
        TextRenderer.DrawText(g, DetailLine, Theme.SmallFont, New Rectangle(TextLeft(), top + lineH * titleLines + CInt(4 * sc), textW, detailH),
                              If(Notification.Priority = AlertService.Urgent AndAlso Not Notification.IsRead, Theme.StatusRed, Theme.TextMuted),
                              TextFormatFlags.Left Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix Or TextFormatFlags.SingleLine)

        ' ✓ (mark read) for unread rows
        If Not Notification.IsRead Then
            Dim r = CheckRect()
            Dim circle As New Rectangle(r.X + (r.Width - CInt(26 * sc)) \ 2, (Height - CInt(26 * sc)) \ 2, CInt(26 * sc), CInt(26 * sc))
            If _hoverCheck Then
                g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
                Using brush As New SolidBrush(Theme.AccentSoft)
                    g.FillEllipse(brush, circle)
                End Using
                g.SmoothingMode = Drawing2D.SmoothingMode.None
            End If
            TextRenderer.DrawText(g, Icons.CheckMark, Theme.IconFont(9.0F), circle, If(_hoverCheck, Theme.SidebarBlue, Theme.TextMuted),
                                  TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
        End If

        Using pen As New Pen(Theme.Divider)
            g.DrawLine(pen, 0, Height - 1, Width, Height - 1)
        End Using
    End Sub

End Class
