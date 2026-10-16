''' <summary>
''' Admin screen, "Pending" tab (Revision 4C): every unfinished application from all modules in ONE list,
''' oldest first, so the Admin knows who to serve first (first come, first served - RA 11032).
''' Columns: date filed, reference, business, module, type, status, days waiting, processing limit, timeliness.
''' Filter by module + search. Double-click (or "Open in Module") opens the record's module on its business.
''' Everything comes from ApplicationQueueService. No SQL here.
''' </summary>
Public Class AdminQueuePage
    Inherits AdminPage

    Private Class QueueRow
        Public Property Index As Integer
        Public Property Filed As String
        Public Property ReferenceNo As String
        Public Property Business As String
        Public Property ModuleName As String
        Public Property Type As String
        Public Property Status As String
        Public Property Waiting As String
        Public Property Limit As String
        Public Property Timeliness As String
    End Class

    Private Const AllModules As String = "All modules"

    ''' <summary>Raised to open a module on a business (AdminView passes it on to MainForm).</summary>
    Public Event OpenRequested(screen As AppScreen, businessId As Integer)

    Private ReadOnly btnOpen As Button
    Private ReadOnly btnRefresh As Button
    Private ReadOnly txtSearch As New TextBox()
    Private ReadOnly cboModule As New ComboBox()
    Private ReadOnly lblCount As New Label()
    Private ReadOnly grid As New ModernGrid()
    Private ReadOnly lblEmpty As New Label()
    Private ReadOnly lblNote As New Label()

    Private items As New List(Of PendingApplication)
    Private shown As New List(Of PendingApplication)
    Private loading As Boolean

    Public Sub New(bar As ModuleToolbar)
        MyBase.New(bar)
        btnOpen = AddAction("Open in Module", "Open", "primary", AddressOf Open_Click,
                            tip:="Open the selected application in its module")
        btnRefresh = AddAction("Refresh", "Refresh", "secondary", Sub() RefreshData(), tip:="Reload the queue (F5)")
        Dim card = CreateCard()
        card.Controls.Add(lblEmpty)
        card.Controls.Add(grid)
        card.Controls.Add(BuildNote())
        card.Controls.Add(BuildHeader())
        StyleEmptyLabel(lblEmpty)
        BuildGrid()
        Controls.Add(card)
    End Sub

    ''' <summary>Applications currently listed (for tests).</summary>
    Public ReadOnly Property Applications As List(Of PendingApplication)
        Get
            Return shown
        End Get
    End Property

    ' =====================================================================
    '  Layout
    ' =====================================================================

    Private Function BuildHeader() As Control
        Dim header As New Panel With {.Dock = DockStyle.Top, .Height = Dpi(48), .BackColor = Color.White}
        Dim title As New Label With {.Text = "Pending Applications", .Font = Theme.SubtitleFont, .ForeColor = Theme.TextDark,
                                     .AutoSize = True, .Location = New Point(0, Dpi(10)), .BackColor = Color.White}
        lblCount.Font = Theme.SmallFont
        lblCount.ForeColor = Theme.TextMuted
        lblCount.BackColor = Color.White
        lblCount.AutoEllipsis = True
        lblCount.TextAlign = ContentAlignment.MiddleLeft

        cboModule.DropDownStyle = ComboBoxStyle.DropDownList
        cboModule.FlatStyle = FlatStyle.Flat
        cboModule.Font = Theme.InputFont
        UiHelper.StyleComboBox(cboModule)
        cboModule.Items.Add(AllModules)
        cboModule.Items.AddRange({ModuleNames.BusinessPermit, ModuleNames.SanitaryPermit,
                                  ModuleNames.AnnualInspection, ModuleNames.ConstructionPermit})
        cboModule.SelectedIndex = 0
        Dim moduleBox = UiHelper.CreateComboBox(cboModule, Dpi(38))
        AddHandler cboModule.SelectedIndexChanged, Sub() BindGrid()
        Tips.SetToolTip(cboModule, "Show one module only")

        Dim searchBox = UiHelper.CreateInputBox(txtSearch, "Search…", Icons.Search)
        searchBox.Height = Dpi(38)
        header.Controls.AddRange({title, lblCount, moduleBox, searchBox})
        AddHandler header.Resize,
            Sub()
                Dim w = Math.Max(Dpi(120), Math.Min(Dpi(220), (header.ClientSize.Width - title.Width - Dpi(24)) \ 2))
                searchBox.SetBounds(header.ClientSize.Width - w, Dpi(2), w, Dpi(38))
                Dim mw = Math.Max(Dpi(150), Math.Min(Dpi(200), w))
                moduleBox.SetBounds(searchBox.Left - mw - Dpi(10), Dpi(2), mw, Dpi(38))
                Dim countLeft = title.Right + Dpi(12)
                lblCount.SetBounds(countLeft, Dpi(12), Math.Max(0, moduleBox.Left - countLeft - Dpi(12)), Dpi(24))
                lblCount.Visible = lblCount.Width >= Dpi(90)
            End Sub
        AddHandler txtSearch.TextChanged, Sub() BindGrid()
        Return header
    End Function

    Private Function BuildNote() As Control
        lblNote.Dock = DockStyle.Bottom
        lblNote.Height = Dpi(40)
        lblNote.Font = Theme.SmallFont
        lblNote.ForeColor = Theme.TextMuted
        lblNote.BackColor = Color.White
        lblNote.TextAlign = ContentAlignment.BottomLeft
        lblNote.AutoEllipsis = True
        Return lblNote
    End Function

    Private Sub BuildGrid()
        UiHelper.StyleGrid(grid, "Timeliness")
        grid.Dock = DockStyle.Fill
        UiHelper.AddGridColumn(grid, "Filed", "DATE FILED", "Filed", 100)
        UiHelper.AddGridColumn(grid, "ReferenceNo", "REFERENCE", "ReferenceNo", 110)
        UiHelper.AddGridColumn(grid, "Business", "BUSINESS", "Business", 170)
        UiHelper.AddGridColumn(grid, "ModuleName", "MODULE", "ModuleName", 130)
        UiHelper.AddGridColumn(grid, "Type", "TYPE", "Type", 90)
        UiHelper.AddGridColumn(grid, "Status", "STATUS", "Status", 120)
        UiHelper.AddGridColumn(grid, "Waiting", "DAYS WAITING", "Waiting", 90)
        UiHelper.AddGridColumn(grid, "Limit", "LIMIT", "Limit", 80)
        UiHelper.AddGridColumn(grid, "Timeliness", "TIMELINESS", "Timeliness", 110)
        AddHandler grid.SelectionChanged, Sub() If Not loading Then UpdateButtons()
        AddHandler grid.CellDoubleClick, Sub(s, e) If e.RowIndex >= 0 Then Open_Click(Nothing, EventArgs.Empty)
        AddHandler grid.Resize, Sub() FitGrid()
    End Sub

    Private Sub FitGrid()
        FitColumns(grid, {"Filed", "ReferenceNo", "Timeliness", "Waiting", "Business", "ModuleName", "Status", "Limit", "Type"})
        grid.Invalidate()
    End Sub

    ' =====================================================================
    '  Data
    ' =====================================================================

    Public Overrides Sub RefreshData()
        items = ApplicationQueueService.GetPendingApplications()
        BindGrid()
    End Sub

    Private Sub BindGrid()
        Dim keep = SelectedApplication()
        Dim moduleFilter = If(cboModule.SelectedItem?.ToString(), AllModules)
        Dim term = txtSearch.Text.Trim().ToLowerInvariant()
        shown = items.Where(Function(a) (moduleFilter = AllModules OrElse a.ModuleName = moduleFilter) AndAlso
                                        (term = "" OrElse (a.ReferenceNo & " " & a.BusinessName & " " & a.ModuleName & " " &
                                                           a.ApplicationType & " " & a.Status & " " & a.Timeliness).ToLowerInvariant().Contains(term))).ToList()
        Dim rows = shown.Select(Function(a, i) New QueueRow With {
            .Index = i, .Filed = UiHelper.FormatDate(a.DateFiled), .ReferenceNo = a.ReferenceNo, .Business = a.BusinessName,
            .ModuleName = a.ModuleName, .Type = a.ApplicationType, .Status = a.Status,
            .Waiting = a.DaysWaiting & If(a.DaysWaiting = 1, " day", " days"),
            .Limit = a.LimitDays & " (" & a.Classification & ")", .Timeliness = a.Timeliness}).ToList()

        loading = True
        grid.DataSource = rows
        MeasureColumns(grid, "Timeliness", "Business")
        FitGrid()
        Dim index = If(keep Is Nothing, -1, shown.FindIndex(Function(a) a.Screen = keep.Screen AndAlso a.RecordId = keep.RecordId))
        SelectRow(grid, If(index < 0 AndAlso rows.Count > 0, 0, index))
        loading = False

        Dim overdue = shown.Where(Function(a) a.Timeliness = StatusService.Overdue).Count()
        lblCount.Text = shown.Count & If(shown.Count = 1, " application", " applications") &
                        If(overdue > 0, "  ·  " & overdue & " over the processing time", "")
        lblEmpty.Visible = rows.Count = 0
        If rows.Count = 0 Then
            lblEmpty.Text = If(items.Count = 0, "No pending applications. Everything has been processed.", "No applications match the filter.")
            lblEmpty.BringToFront()
        End If
        lblNote.Text = "Oldest first. Days waiting = working days (Mon-Fri) since filing. Limits per RA 11032: simple " &
                       ApplicationQueueService.GetLimitDays(ApplicationQueueService.Simple) & ", complex " &
                       ApplicationQueueService.GetLimitDays(ApplicationQueueService.Complex) & ", highly technical " &
                       ApplicationQueueService.GetLimitDays(ApplicationQueueService.HighlyTechnical) &
                       " working days (Settings). ""With Applicant"" = waiting for the applicant, e.g. payment."
        Tips.SetToolTip(lblNote, lblNote.Text)
        UpdateButtons()
    End Sub

    Private Function SelectedApplication() As PendingApplication
        If grid.SelectedRows.Count = 0 Then Return Nothing
        Dim row = TryCast(grid.SelectedRows(0).DataBoundItem, QueueRow)
        Return If(row Is Nothing OrElse row.Index >= shown.Count, Nothing, shown(row.Index))
    End Function

    Private Sub UpdateButtons()
        UiHelper.SetAvailable(btnOpen, If(SelectedApplication() Is Nothing, "Select an application in the list first.", Nothing))
    End Sub

    Private Sub Open_Click(sender As Object, e As EventArgs)
        Dim a = SelectedApplication()
        If a Is Nothing Then Return
        RaiseEvent OpenRequested(a.Screen, a.BusinessId)
    End Sub

End Class
