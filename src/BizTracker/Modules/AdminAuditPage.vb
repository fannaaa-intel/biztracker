''' <summary>
''' Admin screen, "Audit Log" tab: read-only list of every logged action, newest first.
''' Filters: date range (From / To, each can be switched off), user, table and action, plus a search box
''' over the loaded rows. The selected entry's full details are shown under the list.
''' Toolbar: Clear Filters, Refresh. Data comes from AuditLogService (at most 500 newest rows).
''' </summary>
Public Class AdminAuditPage
    Inherits AdminPage

    Private Class EntryRow
        Public Property LogId As Integer
        Public Property [When] As String
        Public Property User As String
        Public Property Action As String
        Public Property Table As String
        Public Property Record As String
        Public Property Details As String
    End Class

    ''' <summary>A combo item with a display text and a filter value.</summary>
    Private Class FilterItem
        Public Property Text As String
        Public Property Value As Object
        Public Overrides Function ToString() As String
            Return Text
        End Function
    End Class

    Public Const DefaultDays As Integer = 30

    Private ReadOnly btnClear As Button
    Private ReadOnly btnRefresh As Button

    Private ReadOnly txtSearch As New TextBox()
    Private ReadOnly lblCount As New Label()
    Private ReadOnly dtpFrom As New DateTimePicker()
    Private ReadOnly dtpTo As New DateTimePicker()
    Private ReadOnly cboUser As New ComboBox()
    Private ReadOnly cboTable As New ComboBox()
    Private ReadOnly cboAction As New ComboBox()
    Private ReadOnly grid As New ModernGrid()
    Private ReadOnly lblEmpty As New Label()
    Private ReadOnly strip As New RoundedPanel()
    Private ReadOnly lblSelected As New Label()

    Private result As New AuditLogResult()
    Private loading As Boolean
    Private filtersReady As Boolean

    Public Sub New(bar As ModuleToolbar)
        MyBase.New(bar)
        btnClear = AddAction("Clear Filters", "Clear", "secondary", AddressOf Clear_Click,
                             tip:="Show the last " & DefaultDays & " days for every user, table and action")
        btnRefresh = AddAction("Refresh", "Refresh", "primary", Sub() RefreshData(), tip:="Load the newest entries (F5)")
        Dim card = CreateCard()
        card.Controls.Add(lblEmpty)
        card.Controls.Add(grid)
        card.Controls.Add(BuildStrip())
        card.Controls.Add(BuildFilters())
        card.Controls.Add(BuildHeader())
        StyleEmptyLabel(lblEmpty)
        BuildGrid()
        Controls.Add(card)
    End Sub

    ''' <summary>Rows currently listed (for tests).</summary>
    Public ReadOnly Property Entries As List(Of AuditLogEntry)
        Get
            Return result.Entries
        End Get
    End Property

    ' =====================================================================
    '  Layout
    ' =====================================================================

    Private Function BuildHeader() As Control
        Dim header As New Panel With {.Dock = DockStyle.Top, .Height = Dpi(48), .BackColor = Color.White}
        Dim title As New Label With {.Text = "Audit Log", .Font = Theme.SubtitleFont, .ForeColor = Theme.TextDark,
                                     .AutoSize = True, .Location = New Point(0, Dpi(10)), .BackColor = Color.White}
        lblCount.Font = Theme.SmallFont
        lblCount.ForeColor = Theme.TextMuted
        lblCount.BackColor = Color.White
        lblCount.AutoEllipsis = True
        lblCount.TextAlign = ContentAlignment.MiddleLeft
        Dim searchBox = UiHelper.CreateInputBox(txtSearch, "Search the list…", Icons.Search)
        searchBox.Height = Dpi(38)
        header.Controls.AddRange({title, lblCount, searchBox})
        AddHandler header.Resize,
            Sub()
                Dim w = Math.Max(Dpi(120), Math.Min(Dpi(240), header.ClientSize.Width - title.Width - Dpi(16)))
                searchBox.SetBounds(header.ClientSize.Width - w, Dpi(2), w, Dpi(38))
                Dim countLeft = title.Right + Dpi(12)
                lblCount.SetBounds(countLeft, Dpi(12), Math.Max(0, searchBox.Left - countLeft - Dpi(12)), Dpi(24))
                lblCount.Visible = lblCount.Width >= Dpi(90)
            End Sub
        AddHandler txtSearch.TextChanged, Sub() BindGrid()
        Return header
    End Function

    Private Function BuildFilters() As Control
        Dim row As New TableLayoutPanel With {
            .Dock = DockStyle.Top, .Height = Dpi(66), .ColumnCount = 5, .RowCount = 1, .BackColor = Color.White,
            .Margin = New Padding(0), .Padding = New Padding(0, 0, 0, Dpi(8))
        }
        For Each pct In {21.0F, 21.0F, 20.0F, 20.0F, 18.0F}
            row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, pct))
        Next
        row.RowStyles.Add(New RowStyle(SizeType.Percent, 100))

        For Each dtp In {dtpFrom, dtpTo}
            dtp.Format = DateTimePickerFormat.Custom
            dtp.CustomFormat = "MMM dd, yyyy"
            dtp.Font = Theme.InputFont
            dtp.ShowCheckBox = True
            dtp.MaxDate = Date.Today.AddYears(1)
            AddHandler dtp.ValueChanged, Sub() If filtersReady Then RefreshData()
            ' Narrow window: "10/07/26" instead of a cut "Oct 07, 202"
            Dim picker = dtp
            AddHandler picker.Resize,
                Sub()
                    Dim need = TextRenderer.MeasureText("Sep 30, 2026", picker.Font).Width + Dpi(60)   ' + check box + drop-down button
                    picker.CustomFormat = If(picker.Width >= need, "MMM dd, yyyy", "MM/dd/yy")
                End Sub
        Next
        Tips.SetToolTip(dtpFrom, "Untick to include everything before the ""To"" date")
        Tips.SetToolTip(dtpTo, "Untick to include everything after the ""From"" date")

        For Each combo In {cboUser, cboTable, cboAction}
            combo.DropDownStyle = ComboBoxStyle.DropDownList
            combo.FlatStyle = FlatStyle.Flat
            combo.Font = Theme.InputFont
            UiHelper.StyleComboBox(combo)
            AddHandler combo.SelectedIndexChanged, Sub() If filtersReady Then RefreshData()
        Next

        row.Controls.Add(FilterCell("From", dtpFrom, False), 0, 0)
        row.Controls.Add(FilterCell("To", dtpTo, False), 1, 0)
        row.Controls.Add(FilterCell("User", cboUser, True), 2, 0)
        row.Controls.Add(FilterCell("Table", cboTable, True), 3, 0)
        row.Controls.Add(FilterCell("Action", cboAction, True), 4, 0)
        Return row
    End Function

    ''' <summary>Caption above an input, filling one column of the filter row.</summary>
    Private Function FilterCell(caption As String, input As Control, isCombo As Boolean) As Control
        Dim cell As New Panel With {.Dock = DockStyle.Fill, .BackColor = Color.White, .Margin = New Padding(0, 0, Dpi(10), 0)}
        Dim lbl As New Label With {.Text = caption, .Font = Theme.SmallBoldFont, .ForeColor = Theme.TextMuted,
                                   .AutoSize = False, .AutoEllipsis = True, .BackColor = Color.White}
        Dim box As Control = If(isCombo, UiHelper.CreateComboBox(DirectCast(input, ComboBox), dtpFrom.PreferredHeight), input)
        cell.Controls.AddRange({lbl, box})
        AddHandler cell.Layout,
            Sub()
                lbl.SetBounds(0, 0, cell.ClientSize.Width, Dpi(20))
                ' Lists match the height of the date pickers
                box.SetBounds(0, Dpi(22), cell.ClientSize.Width, dtpFrom.Height)
            End Sub
        Return cell
    End Function

    Private Function BuildStrip() As Control
        Dim holder As New Panel With {.Dock = DockStyle.Bottom, .Height = Dpi(52), .BackColor = Color.White,
                                      .Padding = New Padding(0, Dpi(8), 0, 0)}
        strip.Dock = DockStyle.Fill
        strip.ShowShadow = False
        strip.Radius = 10
        strip.BackColor = Theme.SoftBackground
        strip.Padding = New Padding(Dpi(12), 0, Dpi(12), 0)
        lblSelected.Dock = DockStyle.Fill
        lblSelected.Font = Theme.SmallFont
        lblSelected.ForeColor = Theme.TextDark
        lblSelected.BackColor = Theme.SoftBackground
        lblSelected.TextAlign = ContentAlignment.MiddleLeft
        lblSelected.AutoEllipsis = True
        strip.Controls.Add(lblSelected)
        holder.Controls.Add(strip)
        Return holder
    End Function

    Private Sub BuildGrid()
        UiHelper.StyleGrid(grid)
        grid.Dock = DockStyle.Fill
        grid.ShowCellToolTips = True
        UiHelper.AddGridColumn(grid, "When", "WHEN", "When", 120)
        UiHelper.AddGridColumn(grid, "User", "USER", "User", 80)
        UiHelper.AddGridColumn(grid, "Action", "ACTION", "Action", 80)
        UiHelper.AddGridColumn(grid, "Table", "TABLE", "Table", 100)
        UiHelper.AddGridColumn(grid, "Record", "RECORD", "Record", 60)
        UiHelper.AddGridColumn(grid, "Details", "DETAILS", "Details", 300, Dpi(160))
        AddHandler grid.SelectionChanged, Sub() If Not loading Then ShowSelected()
        AddHandler grid.Resize, Sub() FitGrid()
        ' Long details are shortened to the column width with "…"; the full text is in the tooltip and the strip below
        AddHandler grid.CellFormatting,
            Sub(s, e)
                If e.RowIndex < 0 OrElse grid.Columns(e.ColumnIndex).Name <> "Details" OrElse e.Value Is Nothing Then Return
                e.Value = FitText(e.Value.ToString(), Theme.BodyFont, grid.Columns(e.ColumnIndex).Width - 18)
                e.FormattingApplied = True
            End Sub
        AddHandler grid.CellToolTipTextNeeded,
            Sub(s, e)
                If e.RowIndex < 0 OrElse e.ColumnIndex < 0 OrElse grid.Columns(e.ColumnIndex).Name <> "Details" Then Return
                e.ToolTipText = TryCast(grid.Rows(e.RowIndex).DataBoundItem, EntryRow)?.Details
            End Sub
    End Sub

    Private Sub FitGrid()
        FitColumns(grid, {"When", "Action", "Details", "User", "Table", "Record"})
        grid.Invalidate()
    End Sub

    ' =====================================================================
    '  Data
    ' =====================================================================

    ''' <summary>Fills the filter lists once (default: last 30 days, everyone, every table and action).</summary>
    Private Sub PrepareFilters()
        filtersReady = False
        cboUser.Items.Clear()
        cboUser.Items.Add(New FilterItem With {.Text = "All users"})
        For Each u In UserService.GetUsers().OrderBy(Function(x) x.Username)
            cboUser.Items.Add(New FilterItem With {.Text = u.Username, .Value = u.UserId})
        Next
        cboTable.Items.Clear()
        cboTable.Items.Add(New FilterItem With {.Text = "All tables"})
        For Each t In AuditLogService.GetTableNames()
            cboTable.Items.Add(New FilterItem With {.Text = t, .Value = t})
        Next
        cboAction.Items.Clear()
        cboAction.Items.Add(New FilterItem With {.Text = "All actions"})
        For Each a In AuditActions.All
            cboAction.Items.Add(New FilterItem With {.Text = AuditLogService.ActionLabel(a), .Value = a})
        Next
        ResetFilterValues()
        filtersReady = True
    End Sub

    Private Sub ResetFilterValues()
        dtpFrom.Value = Date.Today.AddDays(-DefaultDays)
        dtpFrom.Checked = True
        dtpTo.Value = Date.Today
        dtpTo.Checked = True
        cboUser.SelectedIndex = 0
        cboTable.SelectedIndex = 0
        cboAction.SelectedIndex = 0
    End Sub

    ''' <summary>The filter chosen on screen.</summary>
    Public Function CurrentFilter() As AuditLogFilter
        Return New AuditLogFilter With {
            .FromDate = If(dtpFrom.Checked, dtpFrom.Value.Date, CType(Nothing, Date?)),
            .ToDate = If(dtpTo.Checked, dtpTo.Value.Date, CType(Nothing, Date?)),
            .UserId = CType(TryCast(cboUser.SelectedItem, FilterItem)?.Value, Integer?),
            .TableName = If(TryCast(cboTable.SelectedItem, FilterItem)?.Value?.ToString(), ""),
            .Action = If(TryCast(cboAction.SelectedItem, FilterItem)?.Value?.ToString(), "")
        }
    End Function

    ''' <summary>Sets the filters from code (tests) and reloads.</summary>
    Public Sub ApplyFilter(fromDate As Date?, toDate As Date?, Optional username As String = "", Optional tableName As String = "",
                           Optional action As String = "")
        filtersReady = False
        dtpFrom.Checked = fromDate.HasValue
        If fromDate.HasValue Then dtpFrom.Value = fromDate.Value
        dtpTo.Checked = toDate.HasValue
        If toDate.HasValue Then dtpTo.Value = toDate.Value
        cboUser.SelectedIndex = Math.Max(0, cboUser.Items.Cast(Of FilterItem)().ToList().FindIndex(Function(i) i.Text = username))
        cboTable.SelectedIndex = Math.Max(0, cboTable.Items.Cast(Of FilterItem)().ToList().FindIndex(Function(i) i.Text = tableName))
        cboAction.SelectedIndex = Math.Max(0, cboAction.Items.Cast(Of FilterItem)().ToList().FindIndex(Function(i) Equals(i.Value, action)))
        filtersReady = True
        RefreshData()
    End Sub

    Public Overrides Sub RefreshData()
        If cboUser.Items.Count = 0 Then PrepareFilters()
        Dim filter = CurrentFilter()
        Dim problem = AuditLogService.ValidateFilter(filter)
        If problem IsNot Nothing Then
            result = New AuditLogResult()
            BindGrid(problem & " Change one of the dates.")
            Return
        End If
        result = AuditLogService.Search(filter)
        BindGrid()
    End Sub

    Private Sub BindGrid(Optional filterProblem As String = Nothing)
        Dim term = txtSearch.Text.Trim().ToLowerInvariant()
        Dim rows = result.Entries.
            Select(Function(a) New EntryRow With {
                .LogId = a.LogId, .When = a.CreatedAt.ToString("MMM d, yyyy h:mm tt"), .User = AuditLogService.UserLabel(a),
                .Action = AuditLogService.ActionLabel(a.Action), .Table = If(a.TableName = "", "—", a.TableName),
                .Record = If(a.RecordId.HasValue, "#" & a.RecordId.Value, "—"), .Details = a.Details}).
            Where(Function(r) term = "" OrElse (r.When & " " & r.User & " " & r.Action & " " & r.Table & " " & r.Record & " " & r.Details).
                                                ToLowerInvariant().Contains(term)).
            ToList()
        loading = True
        grid.DataSource = rows
        MeasureColumns(grid, freeText:="Details")
        FitGrid()
        SelectRow(grid, If(rows.Count > 0, 0, -1))
        loading = False

        Dim shown = If(term = "", result.Entries.Count & " of " & result.TotalCount, rows.Count & " of " & result.Entries.Count & " loaded")
        lblCount.Text = shown & " entr" & If(result.TotalCount = 1 AndAlso term = "", "y", "ies") &
                        If(result.TotalCount > AuditLogService.MaxRows AndAlso term = "", " (newest " & AuditLogService.MaxRows & ")", "")
        Tips.SetToolTip(lblCount, lblCount.Text)

        lblEmpty.Visible = rows.Count = 0
        If rows.Count = 0 Then
            lblEmpty.Text = If(filterProblem, If(term <> "", "No entries match your search.",
                                                 "No activity matches these filters." & vbCrLf & "Widen the dates or click ""Clear Filters""."))
            lblEmpty.ForeColor = If(filterProblem Is Nothing, Theme.TextMuted, Theme.StatusRed)
            lblEmpty.BringToFront()
        End If
        ShowSelected()
    End Sub

    Private Sub ShowSelected()
        Dim row = If(grid.SelectedRows.Count > 0, TryCast(grid.SelectedRows(0).DataBoundItem, EntryRow), Nothing)
        If row Is Nothing Then
            lblSelected.ForeColor = Theme.TextMuted
            lblSelected.Text = If(result.Entries.Count = 0, "Every sign-in, change, upload, print and backup is recorded here.",
                                  "Select an entry to see its full details.")
        Else
            lblSelected.ForeColor = Theme.TextDark
            lblSelected.Text = row.When & "  ·  " & row.User & "  ·  " & row.Action & " " & row.Table &
                               If(row.Record = "—", "", " " & row.Record) & "  —  " & If(row.Details = "", "(no details)", row.Details)
        End If
        Tips.SetToolTip(lblSelected, lblSelected.Text)
    End Sub

    Private Sub Clear_Click(sender As Object, e As EventArgs)
        filtersReady = False
        txtSearch.Text = ""
        ResetFilterValues()
        filtersReady = True
        RefreshData()
    End Sub

End Class
