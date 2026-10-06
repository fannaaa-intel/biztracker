''' <summary>
''' Dashboard (Admin, BPLO and Owner only - CLAUDE.md roles table).
'''   Banner : "Welcome, name" and "Action Required: N items need attention"
'''   Left   : six module cards (live status, key reference, Open button)
'''   Right  : the Action Required list; each row opens its module on the right business
''' Owner = their own business. Admin / BPLO = totals across all businesses.
''' No SQL here: every number comes from DashboardService (which uses the module services).
''' </summary>
Public Class DashboardView
    Inherits ModuleView

    Private ReadOnly banner As New WelcomeBanner()
    Private ReadOnly cards As New List(Of ModuleCard)
    Private ReadOnly lblListCount As New StatusBadge()
    Private ReadOnly actionList As New WheelScrollPanel()
    Private ReadOnly lblEmpty As New Label()
    Private ReadOnly tips As New ToolTip()

    ''' <summary>Module order and icons (same as the sidebar).</summary>
    Private Shared ReadOnly ModuleIcons As New Dictionary(Of AppScreen, String) From {
        {AppScreen.BusinessPermits, Icons.Document},
        {AppScreen.SanitaryPermits, Icons.CheckShield},
        {AppScreen.RealPropertyTax, Icons.Bank},
        {AppScreen.HealthCertificates, Icons.Health},
        {AppScreen.AnnualInspections, Icons.Search},
        {AppScreen.ConstructionPermits, Icons.Repair}
    }

    Private Shared ReadOnly ModuleTitles As New Dictionary(Of AppScreen, String) From {
        {AppScreen.BusinessPermits, "Business Permit"},
        {AppScreen.SanitaryPermits, "Sanitary Permit"},
        {AppScreen.RealPropertyTax, "Real Property Tax"},
        {AppScreen.HealthCertificates, "Health Certificates"},
        {AppScreen.AnnualInspections, "Annual Inspection"},
        {AppScreen.ConstructionPermits, "Construction Permit"}
    }

    ''' <summary>Titles used when a card is narrow.</summary>
    Private Shared ReadOnly ShortTitles As New Dictionary(Of AppScreen, String) From {
        {AppScreen.BusinessPermits, "Business"},
        {AppScreen.SanitaryPermits, "Sanitary"},
        {AppScreen.RealPropertyTax, "RPT"},
        {AppScreen.HealthCertificates, "Health Cards"},
        {AppScreen.AnnualInspections, "Inspection"},
        {AppScreen.ConstructionPermits, "Construction"}
    }

    ''' <summary>The items currently listed (for tests and the banner).</summary>
    Public ReadOnly Property ActionItems As New List(Of ActionItem)

    Public Sub New()
        SuspendLayout()
        Dim root As New TableLayoutPanel With {
            .Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2, .BackColor = Theme.ContentBackground,
            .Margin = New Padding(0), .Padding = New Padding(0)
        }
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, Dpi(100)))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))

        banner.Dock = DockStyle.Fill
        banner.Margin = New Padding(0, 0, 0, Dpi(14))
        root.Controls.Add(banner, 0, 0)
        root.Controls.Add(BuildMain(), 0, 1)
        Controls.Add(root)
        ResumeLayout(False)
    End Sub

    Public Overrides ReadOnly Property Subtitle As String
        Get
            Return If(Session.IsOwner, "Your compliance overview across all six services",
                      "Compliance overview across all registered businesses")
        End Get
    End Property

    ' =====================================================================
    '  Layout
    ' =====================================================================

    Private Function BuildMain() As Control
        Dim row As New TableLayoutPanel With {
            .Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 1, .BackColor = Theme.ContentBackground,
            .Margin = New Padding(0), .Padding = New Padding(0)
        }
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 58))
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 42))
        row.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        row.Controls.Add(BuildCards(), 0, 0)
        row.Controls.Add(BuildActionCard(), 1, 0)
        Return row
    End Function

    ''' <summary>2 columns x 3 rows of module cards.</summary>
    Private Function BuildCards() As Control
        Dim grid As New TableLayoutPanel With {
            .Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 3, .BackColor = Theme.ContentBackground,
            .Margin = New Padding(0, 0, Dpi(8), 0), .Padding = New Padding(0)
        }
        grid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        grid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        For r = 0 To 2
            grid.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F / 3))
        Next
        Dim i = 0
        For Each kv In ModuleIcons
            Dim card As New ModuleCard(kv.Value, ModuleTitles(kv.Key), ShortTitles(kv.Key)) With {.Screen = kv.Key, .Dock = DockStyle.Fill}
            Dim col = i Mod 2, rowNo = i \ 2
            card.Margin = New Padding(If(col = 0, 0, Dpi(7)), If(rowNo = 0, 0, Dpi(7)), If(col = 0, Dpi(7), 0), If(rowNo = 2, 0, Dpi(7)))
            Dim target = kv.Key
            AddHandler card.OpenButton.Click, Sub() OpenModule(target, Nothing)
            cards.Add(card)
            grid.Controls.Add(card, col, rowNo)
            i += 1
        Next
        Return grid
    End Function

    Private Function BuildActionCard() As Control
        Dim card As New RoundedPanel With {.Dock = DockStyle.Fill, .Margin = New Padding(Dpi(8), 0, 0, 0), .Padding = New Padding(18, 14, 18, 16)}

        Dim header As New Panel With {.Dock = DockStyle.Top, .Height = Dpi(44), .BackColor = Color.White}
        Dim title As New Label With {.Text = "Action Required", .Font = Theme.SubtitleFont, .ForeColor = Theme.TextDark,
                                     .AutoSize = True, .Location = New Point(0, Dpi(6)), .BackColor = Color.White}
        lblListCount.Height = Dpi(24)
        header.Controls.AddRange({title, lblListCount})
        AddHandler header.Layout, Sub()
                                      lblListCount.Location = New Point(title.Right + Dpi(10), title.Top + (title.Height - lblListCount.Height) \ 2)
                                      lblListCount.Visible = lblListCount.Text <> "" AndAlso lblListCount.Right <= header.ClientSize.Width
                                  End Sub

        actionList.Dock = DockStyle.Fill
        actionList.BackColor = Color.White

        lblEmpty.Dock = DockStyle.Fill
        lblEmpty.TextAlign = ContentAlignment.MiddleCenter
        lblEmpty.Font = Theme.BodyFont
        lblEmpty.ForeColor = Theme.TextMuted
        lblEmpty.BackColor = Color.White
        lblEmpty.Visible = False

        card.Controls.Add(lblEmpty)
        card.Controls.Add(actionList)
        card.Controls.Add(header)
        Return card
    End Function

    ' =====================================================================
    '  Data
    ' =====================================================================

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        RefreshData()
    End Sub

    Public Overrides Sub RefreshData()
        ActionItems.Clear()
        ActionItems.AddRange(DashboardService.GetActionItems())
        ShowBanner()
        ShowCards()
        ShowActionList()
    End Sub

    Private Sub ShowBanner()
        Dim businesses = DashboardService.GetBusinesses()
        Dim subText As String
        If Session.IsOwner Then
            subText = If(businesses.Count = 0, "No business is linked to your account", businesses(0).BusinessName)
        Else
            subText = Session.Role & "  ·  " & businesses.Count & " business" & If(businesses.Count = 1, "", "es")
        End If
        subText &= "  ·  " & Date.Today.ToString("ddd, MMM d, yyyy")
        Dim urgent = ActionItems.AsEnumerable().Count(Function(x) x.IsUrgent)
        banner.SetContent("Welcome, " & Session.FullName, subText,
                          DashboardService.GetActionHeadline(ActionItems.Count), ActionItems.Count, urgent)
        tips.SetToolTip(banner, If(ActionItems.Count = 0, "Everything is in order.",
                                   urgent & " urgent (expired, overdue or failed) · " & (ActionItems.Count - urgent) & " to watch"))
    End Sub

    Private Sub ShowCards()
        Dim summaries = DashboardService.GetModuleSummaries()
        For Each card In cards
            Dim m = summaries.FirstOrDefault(Function(x) x.Screen = card.Screen)
            If m Is Nothing Then
                card.SetValue("—", "", "No business linked", "")
            Else
                card.SetValue(m)
            End If
            If AccessService.CanAccess(card.Screen) Then
                card.SetOpenState(m IsNot Nothing, If(m Is Nothing, "No business is linked to your account.",
                                                      "Open " & ModuleTitles(card.Screen)))
            Else
                card.SetOpenState(False, "Your role (" & Session.Role & ") cannot open " & ModuleTitles(card.Screen) &
                                         ". It is handled by its own office.")
            End If
        Next
    End Sub

    Private Sub ShowActionList()
        lblListCount.Text = If(ActionItems.Count = 0, "", ActionItems.Count.ToString())
        lblListCount.Parent?.PerformLayout()

        Dim rows As New List(Of Control)
        Dim groups = ActionItems.GroupBy(Function(x) x.BusinessId).ToList()
        For Each grp In groups
            ' Staff see every business: a heading per business
            If Not Session.IsOwner Then rows.Add(HeadingRow(grp.First().BusinessName & "  ·  " & grp.Count()))
            For Each item In grp
                Dim row As New ActionRow(item, AccessService.CanAccess(item.Screen))
                tips.SetToolTip(row, item.Title & vbCrLf & row.DetailLine & If(row.CanOpen, vbCrLf & "Click to open " & ModuleTitles(item.Screen), ""))
                ' Deferred: opening a module disposes this list (and the clicked row)
                AddHandler row.OpenRequested, Sub(s, ev)
                                                  Dim r = DirectCast(s, ActionRow)
                                                  BeginInvoke(Sub() OpenModule(r.Item.Screen, r.Item.BusinessId))
                                              End Sub
                rows.Add(row)
            Next
        Next
        If ActionItems.Any(Function(x) Not AccessService.CanAccess(x.Screen)) Then
            rows.Add(NoteRow(tips, "Rows without an arrow are handled by other offices.", Theme.TextMuted))
        End If
        actionList.SetRows(rows)

        lblEmpty.Visible = ActionItems.Count = 0
        If ActionItems.Count = 0 Then
            lblEmpty.Text = If(DashboardService.GetBusinesses().Count = 0, "No business to show yet.",
                               "Nothing needs attention right now." & vbCrLf &
                               "Permits, certificates, taxes and inspections are all in order.")
            lblEmpty.BringToFront()
        End If
    End Sub

    ''' <summary>Asks MainForm to open a module (on the given business when one is known).</summary>
    Private Sub OpenModule(screen As AppScreen, businessId As Integer?)
        If Not AccessService.CanAccess(screen) Then Return
        RequestNavigate(screen, businessId)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then tips.Dispose()
        MyBase.Dispose(disposing)
    End Sub

End Class
