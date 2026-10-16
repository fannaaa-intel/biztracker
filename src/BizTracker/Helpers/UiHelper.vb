Imports System.Drawing.Drawing2D

''' <summary>
''' Shared UI helpers: consistent button / input styling, message boxes, and drawing utilities.
''' </summary>
Public Module UiHelper

    ' ==================== DPI ====================

    Private _dpiScale As Single = 0

    ''' <summary>
    ''' Screen scaling (1.0 at 100%, 1.5 at 150%). Module screens and their detail rows are built
    ''' in code after the window is scaled, so pixel sizes there must go through Dpi().
    ''' </summary>
    Public ReadOnly Property DpiScale As Single
        Get
            If _dpiScale = 0 Then
                Using g = Graphics.FromHwnd(IntPtr.Zero)
                    _dpiScale = Math.Max(1.0F, g.DpiX / 96.0F)
                End Using
            End If
            Return _dpiScale
        End Get
    End Property

    ''' <summary>A size in 100%-scale pixels converted for the current screen scaling.</summary>
    Public Function Dpi(value As Integer) As Integer
        Return CInt(Math.Round(value * DpiScale))
    End Function

    ' ==================== Labels ====================

    ''' <summary>
    ''' Labels normally treat "&amp;" as a keyboard-shortcut marker and hide it ("Renovation &amp; Extension"
    ''' would show "Renovation  Extension"). This turns that off for every label under root, including
    ''' labels added later (detail rows, dialogs fields...).
    ''' </summary>
    Public Sub DisableMnemonics(root As Control)
        If TypeOf root Is Label Then DirectCast(root, Label).UseMnemonic = False
        RemoveHandler root.ControlAdded, AddressOf Mnemonic_ControlAdded
        AddHandler root.ControlAdded, AddressOf Mnemonic_ControlAdded
        For Each child As Control In root.Controls
            DisableMnemonics(child)
        Next
    End Sub

    Private Sub Mnemonic_ControlAdded(sender As Object, e As ControlEventArgs)
        DisableMnemonics(e.Control)
    End Sub

    ' ==================== Buttons ====================

    ''' <summary>Solid blue button for the main action on a screen.</summary>
    Public Sub StylePrimaryButton(btn As Button)
        StyleButton(btn, Theme.ButtonPrimary, Theme.ButtonPrimaryText, Theme.ButtonPrimaryHover,
                    Theme.ButtonPrimaryPressed, 0)
    End Sub

    ''' <summary>White button with a gray border for secondary actions.</summary>
    Public Sub StyleSecondaryButton(btn As Button)
        StyleButton(btn, Color.White, Theme.TextDark, Theme.SoftBackground, Theme.Divider, 1)
    End Sub

    ''' <summary>Red button for destructive actions (delete, reject).</summary>
    Public Sub StyleDangerButton(btn As Button)
        StyleButton(btn, Theme.StatusRed, Color.White, ControlPaint.Light(Theme.StatusRed, 0.2F),
                    ControlPaint.Dark(Theme.StatusRed, 0.1F), 0)
    End Sub

    ''' <summary>Normal colors of each styled button, so the disabled look can be undone.</summary>
    Private ReadOnly buttonColors As New Runtime.CompilerServices.ConditionalWeakTable(Of Button, Color())

    Private Sub StyleButton(btn As Button, back As Color, fore As Color, hover As Color, down As Color, border As Integer)
        btn.FlatStyle = FlatStyle.Flat
        btn.FlatAppearance.BorderSize = border
        btn.FlatAppearance.BorderColor = Theme.InputBorder
        btn.FlatAppearance.MouseOverBackColor = hover
        btn.FlatAppearance.MouseDownBackColor = down
        btn.Font = Theme.BodyBoldFont
        btn.UseVisualStyleBackColor = False
        buttonColors.Remove(btn)
        buttonColors.Add(btn, {back, fore})
        RemoveHandler btn.EnabledChanged, AddressOf Button_EnabledChanged
        AddHandler btn.EnabledChanged, AddressOf Button_EnabledChanged
        RemoveHandler btn.Paint, AddressOf Button_Paint
        AddHandler btn.Paint, AddressOf Button_Paint
        RemoveHandler btn.ParentChanged, AddressOf Button_ParentChanged
        AddHandler btn.ParentChanged, AddressOf Button_ParentChanged
        WatchParent(btn.Parent)
        ApplyButtonState(btn)
    End Sub

    Private Sub Button_EnabledChanged(sender As Object, e As EventArgs)
        ApplyButtonState(DirectCast(sender, Button))
    End Sub

    Private Sub ApplyButtonState(btn As Button)
        Dim colors As Color() = Nothing
        If Not buttonColors.TryGetValue(btn, colors) Then Return
        btn.BackColor = colors(0)
        btn.ForeColor = colors(1)
        btn.Cursor = Cursors.Hand
        btn.Invalidate()       ' Button_Paint draws the unavailable look
    End Sub

    ' ==================== Unavailable buttons ====================
    ' A button that cannot be used yet is drawn faded (dashed border + lock icon). Windows sends no
    ' mouse events or tooltips to a disabled button, so its parent watches the mouse instead: hovering
    ' or clicking the button shows WHY it is unavailable (the reason given to SetAvailable).

    ''' <summary>Why each unavailable button cannot be used (set with SetAvailable).</summary>
    Private ReadOnly unavailableReasons As New Runtime.CompilerServices.ConditionalWeakTable(Of Button, String)
    Private ReadOnly watchedParents As New Runtime.CompilerServices.ConditionalWeakTable(Of Control, Object)
    Private ReadOnly reasonTip As New ToolTip With {.ToolTipTitle = "Not available yet", .ToolTipIcon = ToolTipIcon.Info}
    Private reasonShownFor As Button

    ''' <summary>
    ''' Turns a button on (reason = Nothing or "") or off. When off, hovering or clicking it shows the
    ''' reason, so always say what is missing, e.g. "Select an application first."
    ''' </summary>
    Public Sub SetAvailable(btn As Button, reason As String)
        unavailableReasons.Remove(btn)
        If Not String.IsNullOrEmpty(reason) Then unavailableReasons.Add(btn, reason)
        btn.Enabled = String.IsNullOrEmpty(reason)
        If reasonShownFor Is btn Then HideReason()
    End Sub

    ''' <summary>The reason shown for an unavailable button (a general one if none was given).</summary>
    Public Function GetUnavailableReason(btn As Button) As String
        Dim reason As String = Nothing
        If unavailableReasons.TryGetValue(btn, reason) Then Return reason
        Return "This action is not available for the current selection."
    End Function

    Private Sub Button_ParentChanged(sender As Object, e As EventArgs)
        WatchParent(DirectCast(sender, Control).Parent)
    End Sub

    ''' <summary>Listens to the mouse on a button's container (once per container).</summary>
    Private Sub WatchParent(parent As Control)
        If parent Is Nothing Then Return
        Dim seen As Object = Nothing
        If watchedParents.TryGetValue(parent, seen) Then Return
        watchedParents.Add(parent, True)
        AddHandler parent.MouseMove, AddressOf Parent_MouseMove
        AddHandler parent.MouseDown, AddressOf Parent_MouseDown
        AddHandler parent.MouseLeave, Sub(s, e)
                                          DirectCast(s, Control).Cursor = Cursors.Default
                                          HideReason()
                                      End Sub
    End Sub

    ''' <summary>The unavailable styled button under the mouse, or Nothing.</summary>
    Private Function UnavailableButtonAt(parent As Control, location As Point) As Button
        Dim btn = TryCast(parent.GetChildAtPoint(location, GetChildAtPointSkip.Invisible), Button)
        Dim colors As Color() = Nothing
        If btn Is Nothing OrElse btn.Enabled OrElse Not buttonColors.TryGetValue(btn, colors) Then Return Nothing
        Return btn
    End Function

    Private Sub Parent_MouseMove(sender As Object, e As MouseEventArgs)
        Dim parent = DirectCast(sender, Control)
        Dim btn = UnavailableButtonAt(parent, e.Location)
        parent.Cursor = If(btn Is Nothing, Cursors.Default, Cursors.No)
        If btn Is Nothing Then
            HideReason()
        ElseIf btn IsNot reasonShownFor Then
            ShowReason(btn)
        End If
    End Sub

    Private Sub Parent_MouseDown(sender As Object, e As MouseEventArgs)
        Dim btn = UnavailableButtonAt(DirectCast(sender, Control), e.Location)
        If btn IsNot Nothing Then ShowReason(btn)      ' a click shows the reason again
    End Sub

    ''' <summary>Shows the reason just under the button, wrapped to a readable width.</summary>
    Public Sub ShowReason(btn As Button)
        If btn.Parent Is Nothing Then Return
        reasonShownFor = btn
        reasonTip.Show(WrapText(GetUnavailableReason(btn), 60), btn.Parent, btn.Left, btn.Bottom + Dpi(4), 6000)
    End Sub

    Private Sub HideReason()
        If reasonShownFor Is Nothing Then Return
        If reasonShownFor.Parent IsNot Nothing AndAlso Not reasonShownFor.Parent.IsDisposed Then reasonTip.Hide(reasonShownFor.Parent)
        reasonShownFor = Nothing
    End Sub

    ''' <summary>Breaks a long sentence into lines of about maxChars (tooltips do not wrap by themselves).</summary>
    Public Function WrapText(text As String, maxChars As Integer) As String
        Dim lines As New List(Of String)
        For Each paragraph In text.Replace(vbCrLf, vbLf).Split(vbLf(0))
            Dim line = ""
            For Each word In paragraph.Split(" "c)
                If line <> "" AndAlso line.Length + 1 + word.Length > maxChars Then
                    lines.Add(line)
                    line = word
                Else
                    line = If(line = "", word, line & " " & word)
                End If
            Next
            lines.Add(line)
        Next
        Return String.Join(vbCrLf, lines)
    End Function

    ''' <summary>Mixes two colors (amount 0 = a, 1 = b).</summary>
    Private Function Mix(a As Color, b As Color, amount As Single) As Color
        Return Color.FromArgb(CInt(a.R + (CInt(b.R) - a.R) * amount), CInt(a.G + (CInt(b.G) - a.G) * amount),
                              CInt(a.B + (CInt(b.B) - a.B) * amount))
    End Function

    ''' <summary>
    ''' Unavailable look: the button fades into its background (so it no longer looks like a
    ''' white or blue button), with a dashed border and a lock icon in front of the label.
    ''' </summary>
    Private Sub Button_Paint(sender As Object, e As PaintEventArgs)
        Dim btn = DirectCast(sender, Button)
        If btn.Enabled Then Return
        Dim ground = If(btn.Parent IsNot Nothing, btn.Parent.BackColor, Color.White)
        Dim back = Mix(ground, Color.White, 0.35F)
        Dim fore = Mix(back, Theme.TextDark, 0.45F)
        Dim g = e.Graphics
        g.Clear(back)
        Using pen As New Pen(Mix(back, Theme.TextDark, 0.3F)) With {.DashStyle = DashStyle.Dash}
            g.DrawRectangle(pen, 0, 0, btn.Width - 1, btn.Height - 1)
        End Using

        Const flags = TextFormatFlags.NoPrefix Or TextFormatFlags.SingleLine Or TextFormatFlags.VerticalCenter
        Dim textWidth = TextRenderer.MeasureText(g, btn.Text, btn.Font, Size.Empty, flags).Width
        Dim lockFont = Theme.IconFont(9.0F)      ' cached by Theme - do not dispose
        Dim lockWidth = TextRenderer.MeasureText(g, Icons.Lock, lockFont, Size.Empty, flags Or TextFormatFlags.NoPadding).Width
        Dim gap = Dpi(5)
        Dim total = lockWidth + gap + textWidth
        If total > btn.Width - Dpi(8) Then
            ' Too narrow for the icon: label only
            TextRenderer.DrawText(g, btn.Text, btn.Font, btn.ClientRectangle, fore, flags Or TextFormatFlags.HorizontalCenter)
            Return
        End If
        Dim x = (btn.Width - total) \ 2
        TextRenderer.DrawText(g, Icons.Lock, lockFont, New Rectangle(x, 0, lockWidth, btn.Height), fore, flags Or TextFormatFlags.NoPadding)
        TextRenderer.DrawText(g, btn.Text, btn.Font, New Rectangle(x + lockWidth + gap, 0, textWidth, btn.Height), fore, flags)
    End Sub

    ''' <summary>
    ''' Clean look for a DropDownList combo box: no blue highlight on the selected text,
    ''' light-blue highlight inside the open list.
    ''' </summary>
    Public Sub StyleComboBox(combo As ComboBox)
        combo.DrawMode = DrawMode.OwnerDrawFixed
        combo.ItemHeight = TextRenderer.MeasureText("Ag", combo.Font).Height + 8
        AddHandler combo.DrawItem,
            Sub(sender, e)
                If e.Index < 0 Then Return
                Dim inList = (e.State And DrawItemState.ComboBoxEdit) = 0
                Dim highlighted = inList AndAlso (e.State And DrawItemState.Selected) <> 0
                Using brush As New SolidBrush(If(highlighted, Theme.AccentSoft, Color.White))
                    e.Graphics.FillRectangle(brush, e.Bounds)
                End Using
                Dim text = combo.GetItemText(combo.Items(e.Index))
                Dim r = e.Bounds
                r.Inflate(-6, 0)
                TextRenderer.DrawText(e.Graphics, text, combo.Font, r, Theme.TextDark,
                                      TextFormatFlags.VerticalCenter Or TextFormatFlags.Left Or
                                      TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix)
            End Sub
    End Sub

    ' ==================== Inputs ====================

    ''' <summary>
    ''' Puts a styled (flat, borderless) combo box inside a white bordered box of the given height,
    ''' so it lines up with the FixedSingle text boxes of a dialog. Add the returned panel to the form.
    ''' </summary>
    Public Function CreateComboBox(combo As ComboBox, height As Integer) As Panel
        Dim box As New Panel With {.BorderStyle = BorderStyle.FixedSingle, .BackColor = Color.White, .Height = height}
        combo.ItemHeight = Math.Max(16, box.ClientSize.Height - 6)
        box.Controls.Add(combo)
        ' Layout (not Resize) runs again after a dialog is DPI-scaled
        AddHandler box.Layout,
            Sub()
                Dim itemH = Math.Max(16, box.ClientSize.Height - 6)
                If combo.ItemHeight <> itemH Then combo.ItemHeight = itemH
                combo.Width = box.ClientSize.Width
                combo.Location = New Point(0, (box.ClientSize.Height - combo.Height) \ 2)
            End Sub
        AddHandler box.EnabledChanged, Sub() box.BackColor = If(box.Enabled, Color.White, Theme.SoftBackground)
        Return box
    End Function

    ''' <summary>
    ''' Wraps a TextBox in a rounded, bordered box that turns blue when focused.
    ''' Add the returned panel to the form (not the TextBox itself).
    ''' </summary>
    Public Function CreateInputBox(textBox As TextBox, Optional placeholder As String = "",
                                   Optional glyph As String = "") As RoundedPanel
        Dim hasIcon = glyph <> ""
        Dim box As New RoundedPanel With {
            .Radius = 8,
            .ShowShadow = False,
            .BorderColor = Theme.InputBorder,
            .BackColor = Color.White,
            .Height = 46,
            .Padding = New Padding(If(hasIcon, 44, 14), 0, 14, 0)
        }

        ' Optional icon on the left (e.g. a person for username, a lock for password)
        If hasIcon Then
            Dim icon As New Label With {
                .Text = glyph, .Font = Theme.IconFont(11.0F), .ForeColor = Theme.TextMuted,
                .BackColor = Color.White, .TextAlign = ContentAlignment.MiddleCenter,
                .AutoSize = False, .Location = New Point(12, 2), .Size = New Size(24, box.Height - 4)
            }
            box.Controls.Add(icon)
            ' Stay 2px inside the box so the icon never covers the rounded border
            ' (Layout, not Resize: Layout runs again after the window is DPI-scaled)
            AddHandler box.Layout, Sub() icon.SetBounds(12, 2, 24, box.ClientSize.Height - 4)
            AddHandler icon.Click, Sub() textBox.Focus()
        End If

        textBox.BorderStyle = BorderStyle.None
        textBox.Font = Theme.InputFont
        textBox.ForeColor = Theme.TextDark
        textBox.BackColor = Color.White
        textBox.PlaceholderText = placeholder
        box.Controls.Add(textBox)

        ' Keep the text box vertically centered and full width inside the box
        AddHandler box.Layout,
            Sub()
                textBox.Width = box.ClientSize.Width - box.Padding.Horizontal
                textBox.Location = New Point(box.Padding.Left, (box.ClientSize.Height - textBox.Height) \ 2)
            End Sub
        AddHandler textBox.Enter, Sub() box.BorderColor = Theme.InputFocusBorder
        AddHandler textBox.Leave, Sub() box.BorderColor = Theme.InputBorder
        ' Clicking the padding area focuses the text box
        AddHandler box.Click, Sub() textBox.Focus()
        Return box
    End Function

    ' ==================== Grids ====================

    ''' <summary>
    ''' Modern, read-only DataGridView look. Scrollbars are hidden; the mouse wheel and the
    ''' arrow keys still scroll. If statusColumn is given, that column is drawn as colored badges
    ''' (several columns: "Status,Schedule").
    ''' </summary>
    Public Sub StyleGrid(grid As DataGridView, Optional statusColumn As String = "")
        grid.BorderStyle = BorderStyle.None
        grid.BackgroundColor = Color.White
        grid.CellBorderStyle = DataGridViewCellBorderStyle.None   ' row lines are painted in PaintCell
        grid.GridColor = Theme.Divider
        grid.ReadOnly = True
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.AllowUserToResizeRows = False
        grid.AllowUserToResizeColumns = False
        grid.AllowUserToOrderColumns = False
        grid.RowHeadersVisible = False
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.MultiSelect = False
        grid.AutoGenerateColumns = False
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        grid.ScrollBars = ScrollBars.None
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
        grid.ColumnHeadersHeight = 40
        grid.ColumnHeadersDefaultCellStyle = New DataGridViewCellStyle With {
            .BackColor = Color.White, .ForeColor = Theme.TextMuted, .Font = Theme.SmallBoldFont,
            .SelectionBackColor = Color.White, .SelectionForeColor = Theme.TextMuted,
            .Padding = New Padding(8, 0, 0, 0), .Alignment = DataGridViewContentAlignment.MiddleLeft,
            .WrapMode = DataGridViewTriState.False
        }
        grid.DefaultCellStyle = New DataGridViewCellStyle With {
            .BackColor = Color.White, .ForeColor = Theme.TextDark, .Font = Theme.BodyFont,
            .SelectionBackColor = Theme.AccentSoft, .SelectionForeColor = Theme.TextDark,
            .Padding = New Padding(8, 0, 4, 0), .WrapMode = DataGridViewTriState.False
        }
        grid.RowTemplate.Height = 38

        ' Smooth repainting (DoubleBuffered is protected, so set it through reflection)
        GetType(DataGridView).GetProperty("DoubleBuffered",
            Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic).SetValue(grid, True)

        AddHandler grid.MouseWheel, AddressOf Grid_MouseWheel
        AddHandler grid.SelectionChanged, AddressOf Grid_KeepCurrentRowVisible
        AddHandler grid.CellPainting,
            Sub(sender, e) PaintCell(DirectCast(sender, DataGridView), e, statusColumn)
    End Sub

    ''' <summary>Mouse wheel scrolling for grids without scrollbars.</summary>
    Private Sub Grid_MouseWheel(sender As Object, e As MouseEventArgs)
        Dim grid = DirectCast(sender, DataGridView)
        If Not GridHasRoom(grid) Then Return
        Dim visible = Math.Max(1, grid.DisplayedRowCount(False))
        Dim maxFirst = Math.Max(0, grid.RowCount - visible)
        Dim target = grid.FirstDisplayedScrollingRowIndex - Math.Sign(e.Delta) * Math.Max(1, SystemInformation.MouseWheelScrollLines)
        target = Math.Max(0, Math.Min(maxFirst, target))
        SetFirstRow(grid, target)
    End Sub

    ''' <summary>Keeps the selected row on screen when moving with the arrow keys.</summary>
    Private Sub Grid_KeepCurrentRowVisible(sender As Object, e As EventArgs)
        Dim grid = DirectCast(sender, DataGridView)
        If grid.CurrentRow Is Nothing OrElse Not GridHasRoom(grid) Then Return
        Dim index = grid.CurrentRow.Index
        Dim first = grid.FirstDisplayedScrollingRowIndex
        Dim visible = Math.Max(1, grid.DisplayedRowCount(False))
        If index < first Then
            SetFirstRow(grid, index)
        ElseIf index >= first + visible Then
            SetFirstRow(grid, Math.Max(0, index - visible + 1))
        End If
    End Sub

    ''' <summary>False while the grid has no rows or no space to show any (e.g. still being laid out).</summary>
    Private Function GridHasRoom(grid As DataGridView) As Boolean
        Return grid.IsHandleCreated AndAlso grid.RowCount > 0 AndAlso grid.DisplayedRowCount(True) > 0
    End Function

    Private Sub SetFirstRow(grid As DataGridView, index As Integer)
        If index = grid.FirstDisplayedScrollingRowIndex Then Return
        Try
            grid.FirstDisplayedScrollingRowIndex = index
        Catch ex As InvalidOperationException
            ' The grid is being resized and has no room for rows yet - nothing to scroll.
        End Try
    End Sub

    ''' <summary>
    ''' Paints every cell: only a thin line under each row (no vertical lines, no dotted focus box),
    ''' and the status column as a colored pill (green / amber / red / gray).
    ''' </summary>
    Private Sub PaintCell(grid As DataGridView, e As DataGridViewCellPaintingEventArgs, statusColumn As String)
        If e.ColumnIndex < 0 Then Return
        Dim isStatus = e.RowIndex >= 0 AndAlso statusColumn <> "" AndAlso statusColumn.Split(","c).Contains(grid.Columns(e.ColumnIndex).Name)
        ' Fill the WHOLE cell first (including its edge pixels) so no old lines show through
        Dim isSelected = e.RowIndex >= 0 AndAlso (e.State And DataGridViewElementStates.Selected) <> 0
        ' Anti-aliasing OFF for fills/lines: otherwise cell edges are half-painted and dark lines appear
        e.Graphics.SmoothingMode = SmoothingMode.None
        Using back As New SolidBrush(If(isSelected, e.CellStyle.SelectionBackColor, e.CellStyle.BackColor))
            e.Graphics.FillRectangle(back, e.CellBounds)
        End Using
        If Not isStatus Then
            e.Paint(e.CellBounds, DataGridViewPaintParts.ContentForeground)
            DrawRowLine(e)
            e.Handled = True
            Return
        End If
        Dim text = If(e.FormattedValue?.ToString(), "")
        If text <> "" Then
            Dim g = e.Graphics
            g.SmoothingMode = SmoothingMode.AntiAlias
            Dim font = Theme.SmallBoldFont
            Dim size = TextRenderer.MeasureText(text, font)
            Dim h = Math.Min(e.CellBounds.Height - 10, size.Height + 8)
            Dim w = Math.Min(e.CellBounds.Width - 12, size.Width + h)
            Dim pill As New Rectangle(e.CellBounds.X + 8, e.CellBounds.Y + (e.CellBounds.Height - h) \ 2, w, h)
            Using path = RoundedRect(pill, h \ 2), brush As New SolidBrush(Theme.StatusSoftColor(text))
                g.FillPath(brush, path)
            End Using
            TextRenderer.DrawText(g, text, font, pill, Theme.StatusColor(text),
                                  TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or
                                  TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPadding)
        End If
        e.Graphics.SmoothingMode = SmoothingMode.None   ' reset after the rounded pill
        DrawRowLine(e)
        e.Handled = True
    End Sub

    Private Sub DrawRowLine(e As DataGridViewCellPaintingEventArgs)
        Using pen As New Pen(Theme.Divider)
            e.Graphics.DrawLine(pen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1)
        End Using
    End Sub

    ''' <summary>Adds a text column bound to a property.</summary>
    Public Function AddGridColumn(grid As DataGridView, name As String, header As String, propertyName As String,
                                  fillWeight As Single, Optional minWidth As Integer = 60,
                                  Optional format As String = "") As DataGridViewTextBoxColumn
        Dim col As New DataGridViewTextBoxColumn With {
            .Name = name, .HeaderText = header, .DataPropertyName = propertyName,
            .FillWeight = fillWeight, .MinimumWidth = minWidth, .SortMode = DataGridViewColumnSortMode.NotSortable
        }
        If format <> "" Then col.DefaultCellStyle.Format = format
        grid.Columns.Add(col)
        Return col
    End Function

    ' ==================== Measuring ====================

    ''' <summary>
    ''' Width of text as grids and labels draw it. TextRenderer normally treats "&amp;" as a keyboard-shortcut
    ''' marker and leaves it out of the measurement ("Renovation &amp; Extension" would come out too short),
    ''' so this measures with NoPrefix.
    ''' </summary>
    Public Function TextWidth(text As String, font As Font) As Integer
        Return TextRenderer.MeasureText(If(text, ""), font, Size.Empty, TextFormatFlags.NoPrefix).Width
    End Function

    ' ==================== Formatting ====================

    ''' <summary>₱ 1,234.50</summary>
    Public Function FormatMoney(amount As Decimal) As String
        Return "₱ " & amount.ToString("N2")
    End Function

    ''' <summary>Oct 6, 2026 - or "—" when there is no date.</summary>
    Public Function FormatDate(value As Date?) As String
        If Not value.HasValue Then Return "—"
        Return value.Value.ToString("MMM d, yyyy")
    End Function

    ' ==================== Messages ====================

    ' All messages use the app's own animated ModernMessageBox (never the plain Windows one).

    Public Sub ShowInfo(message As String, Optional title As String = "BizTracker")
        ModernMessageBox.Inform(message, title, MessageKind.Info)
    End Sub

    Public Sub ShowSuccess(message As String, Optional title As String = "Done")
        ModernMessageBox.Inform(message, title, MessageKind.Success)
    End Sub

    Public Sub ShowWarning(message As String, Optional title As String = "Please check")
        ModernMessageBox.Inform(message, title, MessageKind.Warning)
    End Sub

    Public Sub ShowError(message As String, Optional title As String = "Something went wrong")
        ModernMessageBox.Inform(message, title, MessageKind.Error)
    End Sub

    ''' <summary>
    ''' Yes/No question. Returns True if the user clicked the main button.
    ''' danger = True shows a red button (for delete / destructive actions).
    ''' </summary>
    Public Function Confirm(message As String, Optional title As String = "Please confirm",
                            Optional yesText As String = "Yes", Optional noText As String = "No",
                            Optional danger As Boolean = False, Optional warning As Boolean = False) As Boolean
        Return ModernMessageBox.Ask(message, title, yesText, noText, danger, warning)
    End Function

    ' ==================== Drawing utilities ====================

    ''' <summary>A rectangle with rounded corners, for custom painting.</summary>
    Public Function RoundedRect(rect As Rectangle, radius As Integer) As GraphicsPath
        Dim path As New GraphicsPath()
        Dim d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height))
        If d <= 0 Then
            path.AddRectangle(rect)
            Return path
        End If
        path.AddArc(rect.X, rect.Y, d, d, 180, 90)
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90)
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90)
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90)
        path.CloseFigure()
        Return path
    End Function

    ''' <summary>
    ''' The first non-transparent background color going up the parent chain
    ''' (custom-painted controls use it to paint their corners).
    ''' </summary>
    Public Function EffectiveBackColor(control As Control) As Color
        Dim c = control
        While c IsNot Nothing
            If c.BackColor.A = 255 Then Return c.BackColor
            c = c.Parent
        End While
        Return SystemColors.Control
    End Function

    ''' <summary>"Liza Ramirez" -> "LR", "Dr. Paolo Agustin" -> "PA" (titles like Dr./Engr. are skipped).</summary>
    Public Function GetInitials(name As String) As String
        Dim parts = If(name, "").Split({" "c}, StringSplitOptions.RemoveEmptyEntries).
                    Where(Function(p) Char.IsLetter(p(0)) AndAlso Not p.EndsWith(".")).ToList()
        If parts.Count = 0 Then Return "?"
        If parts.Count = 1 Then Return parts(0).Substring(0, 1).ToUpper()
        Return (parts.First().Substring(0, 1) & parts.Last().Substring(0, 1)).ToUpper()
    End Function

End Module
