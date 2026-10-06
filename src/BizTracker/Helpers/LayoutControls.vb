Imports System.Drawing.Drawing2D

' More custom controls used by the module screens: progress tracker, tabs,
' a wheel-only scrolling list (no scrollbars), and summary stat cards.

''' <summary>
''' Horizontal progress tracker: numbered circles joined by a line, with labels underneath.
''' CurrentStep: 0..n-1 = step in progress, n = all done, -1 = stopped (e.g. Rejected - all gray).
''' </summary>
Public Class StepTracker
    Inherits Control

    Private _steps As String() = {}
    Private _current As Integer

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        Height = 72
        Font = Theme.SmallFont
    End Sub

    Public Property Steps As String()
        Get
            Return _steps
        End Get
        Set(value As String())
            _steps = If(value, {}) : Invalidate()
        End Set
    End Property

    Public Property CurrentStep As Integer
        Get
            Return _current
        End Get
        Set(value As Integer)
            _current = value : Invalidate()
        End Set
    End Property

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.Clear(UiHelper.EffectiveBackColor(Parent))
        Dim n = _steps.Length
        If n = 0 Then Return
        g.SmoothingMode = SmoothingMode.AntiAlias

        Dim s = DeviceDpi / 96.0F
        Dim colW = Width / CSng(n)
        Dim d = CInt(Math.Min(28 * s, colW - 8))
        Dim cy = d \ 2 + 2

        ' Connecting lines (green when the step on the right has been reached)
        For i = 1 To n - 1
            Dim x1 = colW * (i - 1) + colW / 2
            Dim x2 = colW * i + colW / 2
            Dim reached = _current >= 0 AndAlso i <= _current
            Using pen As New Pen(If(reached, Theme.StatusGreen, Theme.Divider), Math.Max(2.0F, 2 * s))
                g.DrawLine(pen, x1, cy, x2, cy)
            End Using
        Next

        For i = 0 To n - 1
            Dim cx = colW * i + colW / 2
            Dim circle As New Rectangle(CInt(cx - d / 2), 2, d, d)
            Dim done = _current >= 0 AndAlso i < _current
            Dim active = _current >= 0 AndAlso i = _current

            Using brush As New SolidBrush(If(done, Theme.StatusGreen, If(active, Theme.SidebarBlue, Color.White)))
                g.FillEllipse(brush, circle)
            End Using
            If Not done AndAlso Not active Then
                Using pen As New Pen(Theme.InputBorder, Math.Max(1.5F, 1.5F * s))
                    g.DrawEllipse(pen, circle)
                End Using
            End If

            If done Then
                TextRenderer.DrawText(g, Icons.CheckMark, Theme.IconFont(9.0F), circle, Color.White,
                                      TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
            Else
                TextRenderer.DrawText(g, (i + 1).ToString(), Theme.SmallBoldFont, circle,
                                      If(active, Color.White, Theme.TextMuted),
                                      TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
            End If

            Dim top = d + CInt(8 * s)
            Dim labelRect As New Rectangle(CInt(colW * i + 2), top, CInt(colW - 4), Height - top)
            TextRenderer.DrawText(g, _steps(i), If(active, Theme.SmallBoldFont, Font), labelRect,
                                  If(done OrElse active, Theme.TextDark, Theme.TextMuted),
                                  TextFormatFlags.HorizontalCenter Or TextFormatFlags.Top Or TextFormatFlags.WordBreak Or
                                  TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix)
        Next
    End Sub

End Class

''' <summary>Pill-style tab switcher (e.g. Endorsements | Requirements | Assessment).</summary>
Public Class SegmentedTabs
    Inherits Control

    Private _tabs As String() = {}
    Private _selected As Integer
    Private _hover As Integer = -1

    Public Event SelectedIndexChanged As EventHandler

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        Height = 36
        Font = Theme.SmallBoldFont
        Cursor = Cursors.Hand
    End Sub

    Public Property Tabs As String()
        Get
            Return _tabs
        End Get
        Set(value As String())
            _tabs = If(value, {}) : Invalidate()
        End Set
    End Property

    Public Property SelectedIndex As Integer
        Get
            Return _selected
        End Get
        Set(value As Integer)
            If value = _selected OrElse value < 0 OrElse value >= _tabs.Length Then Return
            _selected = value
            Invalidate()
            RaiseEvent SelectedIndexChanged(Me, EventArgs.Empty)
        End Set
    End Property

    Private Function IndexAt(x As Integer) As Integer
        If _tabs.Length = 0 Then Return -1
        Return Math.Max(0, Math.Min(_tabs.Length - 1, x * _tabs.Length \ Math.Max(1, Width)))
    End Function

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        Dim i = IndexAt(e.X)
        If i <> _hover Then
            _hover = i
            Invalidate()
        End If
        MyBase.OnMouseMove(e)
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        _hover = -1
        Invalidate()
        MyBase.OnMouseLeave(e)
    End Sub

    Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
        SelectedIndex = IndexAt(e.X)
        MyBase.OnMouseClick(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.Clear(UiHelper.EffectiveBackColor(Parent))
        If _tabs.Length = 0 Then Return
        g.SmoothingMode = SmoothingMode.AntiAlias

        Using path = UiHelper.RoundedRect(New Rectangle(0, 0, Width - 1, Height - 1), Height \ 2),
              brush As New SolidBrush(Theme.SoftBackground)
            g.FillPath(brush, path)
        End Using

        Dim segW = Width / CSng(_tabs.Length)
        For i = 0 To _tabs.Length - 1
            Dim r As New Rectangle(CInt(segW * i) + 3, 3, CInt(segW) - 6, Height - 7)
            If i = _selected Then
                Using path = UiHelper.RoundedRect(r, r.Height \ 2), brush As New SolidBrush(Color.White),
                      pen As New Pen(Theme.Divider)
                    g.FillPath(brush, path)
                    g.DrawPath(pen, path)
                End Using
            End If
            Dim textColor = If(i = _selected, Theme.SidebarBlue, If(i = _hover, Theme.TextDark, Theme.TextMuted))
            TextRenderer.DrawText(g, _tabs(i), Font, r, textColor,
                                  TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or
                                  TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix)
        Next
    End Sub

End Class

''' <summary>
''' A list of row controls stacked top-to-bottom that scrolls with the MOUSE WHEEL only
''' (a scrollbar is never shown). Fill it with SetRows().
''' </summary>
Public Class WheelScrollPanel
    Inherits Panel

    Private ReadOnly _rows As New List(Of Control)
    Private _offset As Integer
    ''' <summary>"Scroll for more" strip shown at the bottom while rows are hidden below.</summary>
    Private ReadOnly hint As New Label()

    Public Event Scrolled As EventHandler

    Public Sub New()
        AutoScroll = False
        DoubleBuffered = True
        hint.Text = "Scroll for more  " & ChrW(&H25BE)
        hint.Font = Theme.SmallBoldFont
        hint.ForeColor = Theme.SidebarBlue
        hint.BackColor = Theme.SoftBackground
        hint.TextAlign = ContentAlignment.MiddleCenter
        hint.AutoSize = False
        hint.Visible = False
        AddHandler hint.MouseWheel, AddressOf Child_MouseWheel
        AddHandler hint.Click, Sub() ScrollBy(CInt(96 * DeviceDpi / 96.0))
        Controls.Add(hint)
    End Sub

    ''' <summary>True while the "Scroll for more" strip is showing.</summary>
    Public ReadOnly Property HintVisible As Boolean
        Get
            Return hint.Visible
        End Get
    End Property

    Private ReadOnly Property HintHeight As Integer
        Get
            Return CInt(22 * DeviceDpi / 96.0)
        End Get
    End Property

    ''' <summary>Visible height for rows (smaller when the hint strip is needed).</summary>
    Private ReadOnly Property Viewport As Integer
        Get
            Return If(ContentHeight > ClientSize.Height, ClientSize.Height - HintHeight, ClientSize.Height)
        End Get
    End Property

    Private ReadOnly Property MaxOffset As Integer
        Get
            Return Math.Max(0, ContentHeight - Viewport)
        End Get
    End Property

    ''' <summary>Replaces all rows (the old rows are disposed).</summary>
    Public Sub SetRows(rows As IEnumerable(Of Control))
        SuspendLayout()
        Dim oldRows = _rows.ToList()
        _rows.Clear()
        For Each old In oldRows
            Controls.Remove(old)
            old.Dispose()
        Next
        _offset = 0
        For Each r In rows
            _rows.Add(r)
            Controls.Add(r)
            HookWheel(r)
        Next
        LayoutRows()
        ResumeLayout()
        RaiseEvent Scrolled(Me, EventArgs.Empty)
    End Sub

    Public ReadOnly Property ContentHeight As Integer
        Get
            Return _rows.Sum(Function(r) r.Height)
        End Get
    End Property

    ''' <summary>True when some rows are hidden below the visible area.</summary>
    Public ReadOnly Property HasMoreBelow As Boolean
        Get
            Return _offset < MaxOffset
        End Get
    End Property

    Public Sub ScrollBy(pixels As Integer)
        Dim newOffset = Math.Max(0, Math.Min(MaxOffset, _offset + pixels))
        If newOffset = _offset Then Return
        _offset = newOffset
        LayoutRows()
        RaiseEvent Scrolled(Me, EventArgs.Empty)
    End Sub

    Private Sub LayoutRows()
        Dim y = -_offset
        For Each r In _rows
            r.SetBounds(0, y, ClientSize.Width, r.Height)
            y += r.Height
        Next
        hint.SetBounds(0, ClientSize.Height - HintHeight, ClientSize.Width, HintHeight)
        hint.Visible = HasMoreBelow
        If hint.Visible Then hint.BringToFront()
    End Sub

    Private Sub HookWheel(c As Control)
        AddHandler c.MouseWheel, AddressOf Child_MouseWheel
        For Each child As Control In c.Controls
            HookWheel(child)
        Next
    End Sub

    Private Sub Child_MouseWheel(sender As Object, e As MouseEventArgs)
        ScrollBy(-Math.Sign(e.Delta) * CInt(48 * DeviceDpi / 96.0))
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        Child_MouseWheel(Me, e)
        MyBase.OnMouseWheel(e)
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        If _offset > MaxOffset Then _offset = MaxOffset
        LayoutRows()
        RaiseEvent Scrolled(Me, EventArgs.Empty)
    End Sub

End Class

''' <summary>
''' Summary card: icon circle, small caption, big value, and an optional status badge + note.
''' </summary>
Public Class StatCard
    Inherits RoundedPanel

    Private ReadOnly icon As New IconButton()
    Private ReadOnly lblCaption As New Label()
    Private ReadOnly lblValue As New Label()
    Private ReadOnly badge As New StatusBadge()
    Private ReadOnly lblNote As New Label()
    Private ReadOnly tips As New ToolTip()

    Public Sub New(glyph As String, caption As String)
        Height = 96
        Padding = New Padding(16)
        icon.Glyph = glyph
        icon.IconSize = 14.0F
        icon.IconColor = Theme.SidebarBlue
        icon.CircleColor = Theme.AccentSoft
        icon.Cursor = Cursors.Default
        icon.SetBounds(16, 24, 44, 44)

        lblCaption.Text = caption
        lblCaption.Font = Theme.SmallFont
        lblCaption.ForeColor = Theme.TextMuted
        lblCaption.AutoEllipsis = True
        lblCaption.SetBounds(72, 12, 100, 20)

        lblValue.Font = Theme.SubtitleFont
        lblValue.ForeColor = Theme.TextDark
        lblValue.AutoEllipsis = True
        lblValue.SetBounds(72, 31, 100, 26)

        badge.Location = New Point(72, 61)
        badge.Height = 22
        badge.Visible = False
        lblNote.Font = Theme.SmallFont
        lblNote.ForeColor = Theme.TextMuted
        lblNote.AutoEllipsis = True
        lblNote.SetBounds(72, 62, 100, 20)

        Controls.AddRange({icon, lblCaption, lblValue, badge, lblNote})
    End Sub

    ''' <summary>Updates the card. An empty status hides the badge.</summary>
    Public Sub SetValue(value As String, Optional status As String = "", Optional note As String = "")
        lblValue.Text = value
        badge.Text = status
        badge.Visible = status <> ""
        lblNote.Text = note
        tips.SetToolTip(lblValue, value)
        tips.SetToolTip(lblNote, note)
        LayoutParts()
    End Sub

    Protected Overrides Sub OnResize(eventargs As EventArgs)
        MyBase.OnResize(eventargs)
        LayoutParts()
    End Sub

    Private Sub LayoutParts()
        If lblValue Is Nothing Then Return
        Dim right = ClientSize.Width - CInt(12 * DeviceDpi / 96.0)
        lblCaption.Width = Math.Max(10, right - lblCaption.Left)
        lblValue.Width = Math.Max(10, right - lblValue.Left)
        Dim noteLeft = If(badge.Visible, badge.Right + CInt(8 * DeviceDpi / 96.0), lblValue.Left)
        lblNote.Left = noteLeft
        lblNote.Width = Math.Max(10, right - noteLeft)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then tips.Dispose()
        MyBase.Dispose(disposing)
    End Sub

End Class
