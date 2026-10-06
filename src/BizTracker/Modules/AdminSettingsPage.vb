''' <summary>
''' Admin screen, "Settings" tab: every row of the settings table (rates, deadlines, LGU name...)
''' with friendly names, and the selected value's rule. Toolbar: Edit Value.
''' Values are validated by SettingsService (whole numbers, rates 0-1, MM-DD dates, text).
''' </summary>
Public Class AdminSettingsPage
    Inherits AdminPage

    Private Class SettingRow
        Public Property Key As String
        Public Property Setting As String
        Public Property Value As String
    End Class

    Private ReadOnly btnEdit As Button
    Private ReadOnly txtSearch As New TextBox()
    Private ReadOnly grid As New ModernGrid()
    Private ReadOnly lblEmpty As New Label()
    Private ReadOnly header As New DetailHeader()
    Private ReadOnly detailsList As New WheelScrollPanel()
    Private ReadOnly detailContent As New Panel()
    Private ReadOnly lblDetailEmpty As New Label()

    Private settings As New List(Of Setting)
    Private selected As Setting
    Private loading As Boolean

    Public Sub New(bar As ModuleToolbar)
        MyBase.New(bar)
        btnEdit = AddAction("Edit Value", "Edit", "primary", AddressOf Edit_Click)
        Controls.Add(TwoColumns(BuildListCard(), BuildDetailCard(), 52))
    End Sub

    Public ReadOnly Property SelectedSetting As Setting
        Get
            Return selected
        End Get
    End Property

    Private Function BuildListCard() As Control
        Dim card = CreateCard()
        Dim head = CreateListHeader("System Settings", txtSearch, "Search settings…")
        AddHandler txtSearch.TextChanged, Sub() BindGrid(selected?.SettingKey)

        UiHelper.StyleGrid(grid)
        grid.Dock = DockStyle.Fill
        UiHelper.AddGridColumn(grid, "Setting", "SETTING", "Setting", 180)
        UiHelper.AddGridColumn(grid, "Value", "VALUE", "Value", 140)
        UiHelper.AddGridColumn(grid, "Key", "KEY", "Key", 150)
        AddHandler grid.SelectionChanged, Sub() If Not loading Then ShowSelected()
        AddHandler grid.CellDoubleClick, Sub(s, e) If e.RowIndex >= 0 Then Edit_Click(Nothing, EventArgs.Empty)
        AddHandler grid.Resize, Sub() FitGrid()

        StyleEmptyLabel(lblEmpty)
        card.Controls.Add(lblEmpty)
        card.Controls.Add(grid)
        card.Controls.Add(head)
        Return card
    End Function

    Private Sub FitGrid()
        FitColumns(grid, {"Setting", "Value", "Key"})
    End Sub

    Private Function BuildDetailCard() As Control
        Dim card = CreateCard(New Padding(18, 14, 18, 16))
        Dim gap As New Panel With {.Dock = DockStyle.Top, .Height = 8, .BackColor = Color.White}
        detailsList.Dock = DockStyle.Fill
        detailsList.BackColor = Color.White
        StyleEmptyLabel(lblDetailEmpty)
        detailContent.Dock = DockStyle.Fill
        detailContent.BackColor = Color.White
        detailContent.Controls.Add(detailsList)
        detailContent.Controls.Add(gap)
        detailContent.Controls.Add(header)
        card.Controls.Add(lblDetailEmpty)
        card.Controls.Add(detailContent)
        Return card
    End Function

    Public Overrides Sub RefreshData()
        settings = SettingsService.GetSettings()
        BindGrid(selected?.SettingKey)
    End Sub

    Private Sub BindGrid(selectKey As String)
        Dim term = txtSearch.Text.Trim().ToLowerInvariant()
        Dim rows = settings.OrderBy(Function(s) SettingsService.SortOrder(s.SettingKey)).ThenBy(Function(s) s.SettingKey).Select(Function(s) New SettingRow With {
                                       .Key = s.SettingKey, .Setting = SettingsService.GetInfo(s.SettingKey).Label,
                                       .Value = SettingsService.FormatValue(s.SettingKey, s.SettingValue)}).
                            Where(Function(r) term = "" OrElse (r.Setting & " " & r.Value & " " & r.Key).ToLowerInvariant().Contains(term)).
                            ToList()
        loading = True
        grid.DataSource = rows
        MeasureColumns(grid)
        FitGrid()
        Dim index = If(selectKey IsNot Nothing, rows.FindIndex(Function(r) r.Key = selectKey), -1)
        If index < 0 AndAlso rows.Count > 0 Then index = 0
        SelectRow(grid, index)
        loading = False

        lblEmpty.Visible = rows.Count = 0
        If rows.Count = 0 Then
            lblEmpty.Text = If(term <> "", "No settings match your search.",
                               "No settings found." & vbCrLf & "Import database/biztracker_db.sql to create the default settings.")
            lblEmpty.BringToFront()
        End If
        ShowSelected()
    End Sub

    Private Sub ShowSelected()
        Dim row = If(grid.SelectedRows.Count > 0, TryCast(grid.SelectedRows(0).DataBoundItem, SettingRow), Nothing)
        selected = If(row Is Nothing, Nothing, settings.FirstOrDefault(Function(s) s.SettingKey = row.Key))
        btnEdit.Enabled = selected IsNot Nothing
        Toolbar.SetTip(btnEdit, If(selected Is Nothing, "Select a setting first", "Change the value of """ & row?.Setting & """"))
        ShowDetails()
    End Sub

    Private Sub ShowDetails()
        Dim has = selected IsNot Nothing
        lblDetailEmpty.Visible = Not has
        detailContent.Visible = has
        If Not has Then
            lblDetailEmpty.Text = If(settings.Count = 0, "Nothing to show yet.", "Select a setting to see its rule.")
            Return
        End If
        Dim info = SettingsService.GetInfo(selected.SettingKey)
        header.SetText(info.Label, "", If(selected.Description = "", selected.SettingKey, selected.Description))
        Dim rows As New List(Of Control) From {
            InfoRow(Tips, "Current value", SettingsService.FormatValue(selected.SettingKey, selected.SettingValue),
                    "Stored as", selected.SettingValue),
            InfoRow(Tips, "Key", selected.SettingKey, "Kind", SettingsService.KindText(selected.SettingKey)),
            HeadingRow("Rule"),
            NoteRow(Tips, info.Hint, Theme.TextDark),
            NoteRow(Tips, "Changes apply right away. Records already saved keep their computed amounts.", Theme.SidebarBlue)
        }
        detailsList.SetRows(rows)
    End Sub

    Private Sub Edit_Click(sender As Object, e As EventArgs)
        If selected Is Nothing Then Return
        Dim info = SettingsService.GetInfo(selected.SettingKey)
        Using dlg As New SettingDialog(selected)
            If dlg.ShowDialog(OwnerWindow) <> DialogResult.OK Then Return
            Dim key = selected.SettingKey
            RefreshData()
            OnDataChanged()
            UiHelper.ShowSuccess(info.Label & " is now " & SettingsService.FormatValue(key, dlg.SavedValue) & "." & vbCrLf &
                                 "Next: the new value is used from now on (press F5 on an open module to refresh it).", "Setting saved")
        End Using
    End Sub

End Class
