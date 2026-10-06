''' <summary>
''' Base class for the tabs of the Admin screen (Users, Businesses, Settings, Audit Log, Backup).
''' Each page owns its toolbar buttons (shown only while the page is open) and builds the same
''' list card + detail card layout as the module screens, using the shared helpers below.
''' No SQL here: pages call the admin services.
''' </summary>
Public MustInherit Class AdminPage
    Inherits Panel

    Protected ReadOnly Toolbar As ModuleToolbar
    Protected ReadOnly Tips As New ToolTip()
    Private ReadOnly ownButtons As New List(Of Button)

    ''' <summary>Raised after the page saved something (the Admin screen refreshes its summary cards).</summary>
    Public Event DataChanged As EventHandler

    Protected Sub New(bar As ModuleToolbar)
        Toolbar = bar
        Dock = DockStyle.Fill
        BackColor = Theme.ContentBackground
        Margin = New Padding(0)
    End Sub

    ''' <summary>Reloads the page's data from the database (called each time the tab is opened, and on F5).</summary>
    Public MustOverride Sub RefreshData()

    ''' <summary>Adds a toolbar button that belongs to this page (hidden until the page is shown).</summary>
    Protected Function AddAction(fullText As String, shortText As String, style As String, handler As EventHandler,
                                 Optional tip As String = "") As Button
        Dim btn = Toolbar.AddAction(fullText, shortText, style, handler, isVisible:=False, tip:=tip)
        ownButtons.Add(btn)
        Return btn
    End Function

    ''' <summary>Shows or hides this page's toolbar buttons.</summary>
    Public Sub ShowActions(show As Boolean)
        For Each btn In ownButtons
            btn.Visible = show
        Next
    End Sub

    Protected Sub OnDataChanged()
        RaiseEvent DataChanged(Me, EventArgs.Empty)
    End Sub

    Protected ReadOnly Property OwnerWindow As IWin32Window
        Get
            Return FindForm()
        End Get
    End Property

    ' =====================================================================
    '  Layout helpers (same look as PermitVaultView / RealPropertyTaxView)
    ' =====================================================================

    ''' <summary>Two cards side by side (leftPercent of the width for the first one).</summary>
    Protected Function TwoColumns(leftCard As Control, rightCard As Control, leftPercent As Single) As TableLayoutPanel
        Dim row As New TableLayoutPanel With {
            .Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 1, .BackColor = Theme.ContentBackground,
            .Margin = New Padding(0), .Padding = New Padding(0)
        }
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, leftPercent))
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100 - leftPercent))
        row.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        leftCard.Margin = New Padding(0, 0, 8, 0)
        rightCard.Margin = New Padding(8, 0, 0, 0)
        row.Controls.Add(leftCard, 0, 0)
        row.Controls.Add(rightCard, 1, 0)
        Return row
    End Function

    Protected Shared Function CreateCard(Optional padding As Padding = Nothing) As RoundedPanel
        Return New RoundedPanel With {
            .Dock = DockStyle.Fill,
            .Padding = If(padding = Nothing, New Padding(16, 14, 16, 16), padding)
        }
    End Function

    ''' <summary>Card header: bold title on the left, search box on the right.</summary>
    Protected Function CreateListHeader(title As String, search As TextBox, Optional placeholder As String = "Search…") As Panel
        Dim header As New Panel With {.Dock = DockStyle.Top, .Height = Dpi(48), .BackColor = Color.White}
        Dim lblTitle As New Label With {.Text = title, .Font = Theme.SubtitleFont, .ForeColor = Theme.TextDark,
                                        .AutoSize = True, .Location = New Point(0, Dpi(10)), .BackColor = Color.White}
        Dim searchBox = UiHelper.CreateInputBox(search, placeholder, Icons.Search)
        searchBox.Height = Dpi(38)
        header.Controls.AddRange({lblTitle, searchBox})
        AddHandler header.Resize,
            Sub()
                Dim w = Math.Max(Dpi(120), Math.Min(Dpi(240), header.ClientSize.Width - lblTitle.Width - Dpi(16)))
                searchBox.SetBounds(header.ClientSize.Width - w, Dpi(2), w, Dpi(38))
            End Sub
        Return header
    End Function

    ''' <summary>Centered muted text for empty lists / no selection.</summary>
    Protected Shared Sub StyleEmptyLabel(lbl As Label)
        lbl.Dock = DockStyle.Fill
        lbl.TextAlign = ContentAlignment.MiddleCenter
        lbl.Font = Theme.BodyFont
        lbl.ForeColor = Theme.TextMuted
        lbl.BackColor = Color.White
        lbl.Visible = False
    End Sub

    ''' <summary>Selects a grid row (or none) without leaving a dotted focus cell.</summary>
    Protected Shared Sub SelectRow(grid As DataGridView, index As Integer)
        grid.ClearSelection()
        If index < 0 OrElse index >= grid.Rows.Count Then Return
        Dim cell = grid.Rows(index).Cells.Cast(Of DataGridViewCell)().FirstOrDefault(Function(c) c.Visible)
        If cell IsNot Nothing Then grid.CurrentCell = cell
        grid.Rows(index).Selected = True
        ' Grids have no scrollbars: scroll so the selected row is fully visible
        If grid.IsHandleCreated AndAlso grid.DisplayedRowCount(True) > 0 Then
            Dim first = grid.FirstDisplayedScrollingRowIndex
            Dim visible = Math.Max(1, grid.DisplayedRowCount(False))
            Dim target = If(index < first, index, If(index >= first + visible, index - visible + 1, first))
            If target <> first Then
                Try
                    grid.FirstDisplayedScrollingRowIndex = target
                Catch ex As InvalidOperationException
                    ' Still being laid out - nothing to scroll yet.
                End Try
            End If
        End If
    End Sub

    ' ---------------- responsive grid columns ----------------

    ''' <summary>
    ''' Sets each column's minimum width to what its header and cells need (text is never cut),
    ''' except the "free text" columns, which keep their own minimum and are shortened with "…".
    ''' </summary>
    Protected Shared Sub MeasureColumns(grid As DataGridView, Optional statusColumn As String = "", Optional freeText As String = "")
        For Each col As DataGridViewColumn In grid.Columns
            If col.Name = freeText Then Continue For
            Dim need = UiHelper.TextWidth(col.HeaderText, Theme.SmallBoldFont) + 20
            Dim isStatus = col.Name = statusColumn
            For Each row As DataGridViewRow In grid.Rows
                Dim text = If(row.Cells(col.Index).FormattedValue?.ToString(), "")
                Dim w = If(isStatus, UiHelper.TextWidth(text, Theme.SmallBoldFont) + 44,
                           UiHelper.TextWidth(text, Theme.BodyFont) + 18)
                If w > need Then need = w
            Next
            col.MinimumWidth = Math.Max(Dpi(60), need)
        Next
    End Sub

    ''' <summary>
    ''' Hides the least important columns (last in "priority") until the rest fit the grid width.
    ''' </summary>
    Protected Shared Sub FitColumns(grid As DataGridView, priority As String())
        For Each columnName In priority
            grid.Columns(columnName).Visible = True
        Next
        For i = priority.Length - 1 To 1 Step -1
            Dim total = grid.Columns.Cast(Of DataGridViewColumn)().Where(Function(c) c.Visible).Sum(Function(c) c.MinimumWidth)
            If total <= grid.ClientSize.Width - 4 Then Exit For
            grid.Columns(priority(i)).Visible = False
        Next
    End Sub

    ''' <summary>Text shortened with "…" so it fits maxWidth pixels (full text goes in the tooltip).</summary>
    Protected Shared Function FitText(text As String, textFont As Font, maxWidth As Integer) As String
        If text Is Nothing OrElse UiHelper.TextWidth(text, textFont) <= maxWidth Then Return text
        Dim low = 0, high = text.Length
        While low < high
            Dim mid = (low + high + 1) \ 2
            If UiHelper.TextWidth(text.Substring(0, mid).TrimEnd() & "…", textFont) <= maxWidth Then low = mid Else high = mid - 1
        End While
        Return text.Substring(0, low).TrimEnd() & "…"
    End Function

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then Tips.Dispose()
        MyBase.Dispose(disposing)
    End Sub

End Class

''' <summary>
''' Header of a detail card: bold title, a status badge right after it, and a muted info line below.
''' </summary>
Public Class DetailHeader
    Inherits Panel

    Private ReadOnly lblTitle As New Label()
    Private ReadOnly badge As New StatusBadge()
    Private ReadOnly lblInfo As New Label()
    Private ReadOnly tips As New ToolTip()

    Public Sub New()
        Dock = DockStyle.Top
        Height = Dpi(52)
        BackColor = Color.White
        lblTitle.Font = Theme.SubtitleFont
        lblTitle.ForeColor = Theme.TextDark
        lblTitle.AutoSize = False
        lblTitle.AutoEllipsis = True
        lblTitle.BackColor = Color.White
        badge.Height = Dpi(24)
        lblInfo.Font = Theme.SmallFont
        lblInfo.ForeColor = Theme.TextMuted
        lblInfo.AutoEllipsis = True
        lblInfo.BackColor = Color.White
        Controls.AddRange({lblTitle, badge, lblInfo})
    End Sub

    ''' <summary>An empty status hides the badge.</summary>
    Public Sub SetText(title As String, status As String, info As String)
        lblTitle.Text = title
        badge.Text = status
        badge.Visible = status <> ""
        lblInfo.Text = info
        tips.SetToolTip(lblTitle, title)
        tips.SetToolTip(lblInfo, info)
        PlaceParts()
    End Sub

    Protected Overrides Sub OnLayout(levent As LayoutEventArgs)
        MyBase.OnLayout(levent)
        PlaceParts()
    End Sub

    Private Sub PlaceParts()
        If lblInfo Is Nothing Then Return
        Dim w = ClientSize.Width
        Dim badgeW = If(badge.Text <> "", badge.Width + Dpi(10), 0)
        Dim titleNeed = UiHelper.TextWidth(lblTitle.Text, lblTitle.Font) + 4
        Dim titleW = Math.Max(Dpi(40), Math.Min(titleNeed, w - badgeW))
        lblTitle.SetBounds(0, Dpi(2), titleW, Dpi(26))
        badge.Location = New Point(lblTitle.Right + Dpi(10), Dpi(4))
        badge.Visible = badge.Text <> "" AndAlso badge.Right <= w
        lblInfo.SetBounds(0, Dpi(30), w, Dpi(20))
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then tips.Dispose()
        MyBase.Dispose(disposing)
    End Sub

End Class
