Imports System.Drawing.Drawing2D

' Controls used only by the dashboard: the blue welcome banner and the module summary cards.
' Both lay themselves out from their current size and DeviceDpi, so they look the same at 100% and 150%.

''' <summary>
''' Blue gradient banner: "Welcome, name" with a sub line on the left and the
''' "Action Required: N items need attention" pill on the right (green when nothing is due).
''' </summary>
Public Class WelcomeBanner
    Inherits Control

    Private _heading As String = ""
    Private _subText As String = ""
    Private _actionText As String = ""
    Private _actionCount As Integer
    Private _urgentCount As Integer

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        Height = 96
    End Sub

    ''' <summary>"Welcome, Cristan Dela Cruz"</summary>
    Public ReadOnly Property Heading As String
        Get
            Return _heading
        End Get
    End Property

    ''' <summary>"Action Required: 4 items need attention"</summary>
    Public ReadOnly Property ActionText As String
        Get
            Return _actionText
        End Get
    End Property

    Public Sub SetContent(heading As String, subText As String, actionText As String, actionCount As Integer, urgentCount As Integer)
        _heading = If(heading, "")
        _subText = If(subText, "")
        _actionText = If(actionText, "")
        _actionCount = actionCount
        _urgentCount = urgentCount
        Text = _heading
        Invalidate()
    End Sub

    ''' <summary>Pill colors: red when something is expired/overdue, amber for warnings only, green when all clear.</summary>
    Private ReadOnly Property PillColor As Color
        Get
            If _actionCount = 0 Then Return Theme.StatusGreen
            Return If(_urgentCount > 0, Theme.StatusRed, Theme.StatusAmber)
        End Get
    End Property

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.Clear(UiHelper.EffectiveBackColor(Parent))
        If Width < 10 OrElse Height < 10 Then Return
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim s = DeviceDpi / 96.0F
        Dim box As New Rectangle(0, 0, Width - 1, Height - 1)

        ' Background: rounded blue gradient with two soft decorative circles
        Using path = UiHelper.RoundedRect(box, CInt(Theme.CardCornerRadius * s)),
              brush As New LinearGradientBrush(box, Theme.SidebarBlue, Theme.SidebarBlueDark, LinearGradientMode.Horizontal)
            g.FillPath(brush, path)
            g.SetClip(path)
            Using soft As New SolidBrush(Color.FromArgb(22, Color.White))
                g.FillEllipse(soft, Width - CInt(220 * s), -CInt(90 * s), CInt(260 * s), CInt(260 * s))
                g.FillEllipse(soft, Width - CInt(420 * s), Height - CInt(40 * s), CInt(150 * s), CInt(150 * s))
            End Using
            g.ResetClip()
        End Using

        Dim padX = CInt(26 * s)

        ' Right: the action pill (white, with a colored dot)
        Dim pillFont = Theme.BodyBoldFont
        Dim textSize = TextRenderer.MeasureText(_actionText, pillFont)
        Dim pillH = textSize.Height + CInt(16 * s)
        Dim dot = CInt(10 * s)
        Dim pillW = Math.Min(textSize.Width + dot + CInt(44 * s), Width \ 2)
        Dim pill As New Rectangle(Width - padX - pillW, (Height - pillH) \ 2, pillW, pillH)
        If _actionText <> "" Then
            Using path = UiHelper.RoundedRect(pill, pillH \ 2), brush As New SolidBrush(Color.White)
                g.FillPath(brush, path)
            End Using
            Using brush As New SolidBrush(PillColor)
                g.FillEllipse(brush, pill.X + CInt(16 * s), pill.Y + (pillH - dot) \ 2, dot, dot)
            End Using
            Dim textRect As New Rectangle(pill.X + CInt(16 * s) + dot + CInt(8 * s), pill.Y, pill.Width - dot - CInt(36 * s), pillH)
            TextRenderer.DrawText(g, _actionText, pillFont, textRect, Theme.TextDark,
                                  TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or
                                  TextFormatFlags.NoPrefix Or TextFormatFlags.SingleLine)
        End If

        ' Left: heading + sub line (ellipsized before the pill)
        Dim textW = If(_actionText <> "", pill.X, Width - padX) - padX - CInt(16 * s)
        Dim headH = TextRenderer.MeasureText("Ag", Theme.TitleFont).Height
        Dim subH = TextRenderer.MeasureText("Ag", Theme.BodyFont).Height
        Dim top = (Height - headH - subH - CInt(4 * s)) \ 2
        TextRenderer.DrawText(g, _heading, Theme.TitleFont, New Rectangle(padX, top, textW, headH), Color.White,
                              TextFormatFlags.Left Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix Or TextFormatFlags.SingleLine)
        TextRenderer.DrawText(g, _subText, Theme.BodyFont, New Rectangle(padX, top + headH + CInt(4 * s), textW, subH), Theme.SidebarTextMuted,
                              TextFormatFlags.Left Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix Or TextFormatFlags.SingleLine)
        g.SmoothingMode = SmoothingMode.None
    End Sub

End Class


''' <summary>
''' One module card on the dashboard:
'''   [icon] Title ............ [Open ›]
'''   Value (reference / amount / count)
'''   [badge] note
'''   detail line
'''   progress caption ....... note
'''   ====== progress bar ======
''' Narrow card: the icon hides first, then the short title is used, then "Open ›" becomes "›",
''' so the title is never cut. The detail line and the progress bar only show when they fit.
''' </summary>
Public Class ModuleCard
    Inherits RoundedPanel

    Private ReadOnly icon As New IconButton()
    Private ReadOnly lblTitle As New Label()
    Private ReadOnly lblValue As New Label()
    Private ReadOnly badge As New StatusBadge()
    Private ReadOnly lblNote As New Label()
    Private ReadOnly lblDetail As New Label()
    Private ReadOnly progress As New ProgressStrip()
    Private ReadOnly tips As New ToolTip()
    Private ReadOnly fullTitle As String
    Private ReadOnly shortTitle As String
    Private hasBadge As Boolean
    Private hasProgress As Boolean

    Private Const OpenText As String = "Open  " & ChrW(&H203A)
    Private Const OpenTextShort As String = ChrW(&H203A)

    ''' <summary>The "Open" button (disabled with a tooltip when the role cannot open the module).</summary>
    Public ReadOnly Property OpenButton As New Button()

    ''' <summary>The module this card describes.</summary>
    Public Property Screen As AppScreen

    Public Sub New(glyph As String, title As String, compactTitle As String)
        fullTitle = title
        shortTitle = compactTitle
        icon.Glyph = glyph
        icon.IconSize = 13.0F
        icon.IconColor = Theme.SidebarBlue
        icon.CircleColor = Theme.AccentSoft
        icon.Cursor = Cursors.Default

        lblTitle.Text = title
        lblTitle.Font = Theme.BodyBoldFont
        lblTitle.ForeColor = Theme.TextDark
        lblTitle.AutoEllipsis = True
        lblTitle.TextAlign = ContentAlignment.MiddleLeft

        lblValue.Font = Theme.SubtitleFont
        lblValue.ForeColor = Theme.TextDark
        lblValue.AutoEllipsis = True
        lblValue.TextAlign = ContentAlignment.MiddleLeft

        badge.Visible = False
        For Each lbl In {lblNote, lblDetail}
            lbl.Font = Theme.SmallFont
            lbl.ForeColor = Theme.TextMuted
            lbl.AutoEllipsis = True
            lbl.TextAlign = ContentAlignment.MiddleLeft
        Next
        progress.Visible = False

        UiHelper.StyleSecondaryButton(OpenButton)
        OpenButton.Font = Theme.SmallBoldFont
        OpenButton.Text = OpenText

        Controls.AddRange({icon, lblTitle, OpenButton, lblValue, badge, lblNote, lblDetail, progress})
        tips.SetToolTip(lblTitle, title)
    End Sub

    ' ---------- Read-only views of what is shown (used by the tests) ----------

    Public ReadOnly Property ValueText As String
        Get
            Return lblValue.Text
        End Get
    End Property

    Public ReadOnly Property StatusText As String
        Get
            Return If(hasBadge, badge.Text, "")
        End Get
    End Property

    Public ReadOnly Property NoteText As String
        Get
            Return lblNote.Text
        End Get
    End Property

    Public ReadOnly Property DetailText As String
        Get
            Return lblDetail.Text
        End Get
    End Property

    Public ReadOnly Property ProgressText As String
        Get
            Return If(hasProgress, progress.Text & "|" & progress.Note, "")
        End Get
    End Property

    Public Sub SetValue(m As ModuleSummary)
        SetValue(m.Value, m.Status, m.Note, m.Detail)
        hasProgress = m.ProgressText <> ""
        progress.SetValue(m.ProgressText, m.Progress, m.ProgressNote,
                          If(m.Progress >= 1, Theme.StatusGreen,
                             If(Theme.StatusColor(m.Status) = Theme.StatusRed, Theme.StatusRed, Theme.SidebarBlue)))
        tips.SetToolTip(progress, m.ProgressText & ": " & m.ProgressNote)
        PerformLayout()
    End Sub

    Public Sub SetValue(value As String, status As String, note As String, detail As String)
        lblValue.Text = value
        badge.Text = If(status, "")
        hasBadge = badge.Text <> ""
        badge.Visible = hasBadge
        lblNote.Text = note
        lblDetail.Text = detail
        hasProgress = False
        tips.SetToolTip(lblValue, value)
        tips.SetToolTip(lblNote, note)
        tips.SetToolTip(lblDetail, detail)
        PerformLayout()
    End Sub

    ''' <summary>Enables "Open", or disables it and explains why in the tooltip.</summary>
    Public Sub SetOpenState(enabled As Boolean, tip As String)
        OpenButton.Enabled = enabled
        tips.SetToolTip(OpenButton, tip)
    End Sub

    Protected Overrides Sub OnLayout(e As LayoutEventArgs)
        MyBase.OnLayout(e)
        If progress Is Nothing Then Return
        Dim s = DeviceDpi / 96.0
        Dim pad = CInt(16 * s)
        Dim w = ClientSize.Width, h = ClientSize.Height
        Dim iconSize = CInt(36 * s)
        Dim gap = CInt(10 * s)

        ' Row 1: pick the widest header that fits (icon? / full or short title / full or short button)
        Dim btnH = TextRenderer.MeasureText("Ag", OpenButton.Font).Height + CInt(12 * s)
        Dim chosen = False
        For Each mode In {(True, fullTitle, OpenText), (False, fullTitle, OpenText), (False, shortTitle, OpenText), (False, shortTitle, OpenTextShort)}
            Dim btnW = TextRenderer.MeasureText(mode.Item3, OpenButton.Font).Width + CInt(26 * s)
            Dim titleW = TextRenderer.MeasureText(mode.Item2, lblTitle.Font).Width + 2
            Dim need = pad + If(mode.Item1, iconSize + gap, 0) + titleW + gap + btnW + pad
            If need <= w OrElse mode.Item3 = OpenTextShort Then
                icon.Visible = mode.Item1
                lblTitle.Text = mode.Item2
                OpenButton.Text = mode.Item3
                OpenButton.SetBounds(w - pad - btnW, pad + (iconSize - btnH) \ 2, btnW, btnH)
                chosen = True
                Exit For
            End If
        Next
        If Not chosen Then Return
        icon.SetBounds(pad, pad, iconSize, iconSize)
        Dim titleLeft = If(icon.Visible, pad + iconSize + gap, pad)
        lblTitle.SetBounds(titleLeft, pad, Math.Max(10, OpenButton.Left - gap - titleLeft), iconSize)

        ' Row 2: value
        Dim valueH = TextRenderer.MeasureText("Ag", lblValue.Font).Height + CInt(4 * s)
        Dim y = pad + iconSize + CInt(8 * s)
        lblValue.SetBounds(pad, y, Math.Max(10, w - 2 * pad), valueH)
        y += valueH + CInt(4 * s)

        ' Row 3: badge + note
        Dim rowH = CInt(24 * s)
        badge.SetBounds(pad, y, TextRenderer.MeasureText(badge.Text, badge.Font).Width + rowH, rowH)
        badge.Visible = hasBadge AndAlso badge.Right <= w - pad
        Dim noteLeft = If(badge.Visible, badge.Right + CInt(8 * s), pad)
        lblNote.SetBounds(noteLeft, y, Math.Max(10, w - pad - noteLeft), rowH)
        ' A note that does not fit completely is hidden (never cut); the badge tooltip shows it
        lblNote.Visible = TextRenderer.MeasureText(lblNote.Text, lblNote.Font).Width <= lblNote.Width
        tips.SetToolTip(badge, If(lblNote.Text <> "", badge.Text & " · " & lblNote.Text, badge.Text))
        y += rowH + CInt(6 * s)

        ' Row 4: detail (only when it fits)
        Dim detailH = TextRenderer.MeasureText("Ag", lblDetail.Font).Height + CInt(2 * s)
        lblDetail.SetBounds(pad, y, Math.Max(10, w - 2 * pad), detailH)
        lblDetail.Visible = y + detailH <= h - CInt(8 * s)
        If lblDetail.Visible Then y += detailH + CInt(10 * s)

        ' Bottom: progress strip (only when it fits below the text)
        Dim stripH = progress.PreferredHeight
        progress.SetBounds(pad, h - pad - stripH, Math.Max(10, w - 2 * pad), stripH)
        progress.Visible = hasProgress AndAlso lblDetail.Visible AndAlso progress.Top >= y
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then tips.Dispose()
        MyBase.Dispose(disposing)
    End Sub

End Class

''' <summary>A caption, a note on the right, and a thin rounded progress bar underneath.</summary>
Public Class ProgressStrip
    Inherits Control

    Private _fraction As Double
    Private _note As String = ""
    Private _color As Color = Theme.SidebarBlue

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        Font = Theme.SmallFont
    End Sub

    Public ReadOnly Property Note As String
        Get
            Return _note
        End Get
    End Property

    ''' <summary>Text line + gap + bar (DPI aware).</summary>
    Public ReadOnly Property PreferredHeight As Integer
        Get
            Dim s = DeviceDpi / 96.0
            Return TextRenderer.MeasureText("Ag", Theme.SmallBoldFont).Height + CInt(6 * s) + CInt(8 * s)
        End Get
    End Property

    Public Sub SetValue(caption As String, fraction As Double, note As String, barColor As Color)
        Text = If(caption, "")
        _fraction = Math.Max(0, Math.Min(1, fraction))
        _note = If(note, "")
        _color = barColor
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.Clear(UiHelper.EffectiveBackColor(Parent))
        Dim s = DeviceDpi / 96.0F
        Dim textH = TextRenderer.MeasureText("Ag", Theme.SmallBoldFont).Height
        Dim noteW = Math.Min(Width \ 2, TextRenderer.MeasureText(_note, Theme.SmallBoldFont).Width + 4)
        TextRenderer.DrawText(g, Text, Font, New Rectangle(0, 0, Width - noteW - CInt(8 * s), textH), Theme.TextMuted,
                              TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix)
        TextRenderer.DrawText(g, _note, Theme.SmallBoldFont, New Rectangle(Width - noteW, 0, noteW, textH), Theme.TextDark,
                              TextFormatFlags.Right Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix)

        Dim barH = CInt(8 * s)
        Dim track As New Rectangle(0, textH + CInt(6 * s), Width - 1, barH)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Using path = UiHelper.RoundedRect(track, barH \ 2), brush As New SolidBrush(Theme.SoftBackground), pen As New Pen(Theme.Divider)
            g.FillPath(brush, path)
            g.DrawPath(pen, path)
        End Using
        Dim fillW = CInt(track.Width * _fraction)
        If fillW >= barH Then
            Using path = UiHelper.RoundedRect(New Rectangle(track.X, track.Y, fillW, barH), barH \ 2), brush As New SolidBrush(_color)
                g.FillPath(brush, path)
            End Using
        End If
        g.SmoothingMode = SmoothingMode.None
    End Sub

End Class

''' <summary>
''' One row of the dashboard's Action Required list:
'''   (dot) Title ......................................... ›
'''         Status · detail
''' The dot and status are colored (red urgent, amber warning). The whole row is clickable when the
''' user may open the module (hand cursor, hover tint, chevron); otherwise it is information only.
''' </summary>
Public Class ActionRow
    Inherits Control

    Private _hover As Boolean

    Public ReadOnly Property Item As ActionItem
    Public ReadOnly Property CanOpen As Boolean

    ''' <summary>Raised when a clickable row is clicked.</summary>
    Public Event OpenRequested As EventHandler

    Public Sub New(actionItem As ActionItem, openAllowed As Boolean)
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        Item = actionItem
        CanOpen = openAllowed
        Text = actionItem.Title
        Cursor = If(openAllowed, Cursors.Hand, Cursors.Default)
        Height = RowHeight(DeviceDpi)
    End Sub

    ''' <summary>Two text lines + padding.</summary>
    Public Shared Function RowHeight(dpi As Integer) As Integer
        Dim s = dpi / 96.0
        Return TextRenderer.MeasureText("Ag", Theme.BodyBoldFont).Height + TextRenderer.MeasureText("Ag", Theme.SmallFont).Height + CInt(20 * s)
    End Function

    ''' <summary>"Expired · Health certificate HC-2025-00031 · 5 days ago (Oct 1, 2026)"</summary>
    Public ReadOnly Property DetailLine As String
        Get
            Return Item.Status & "  ·  " & Item.Detail
        End Get
    End Property

    ''' <summary>Opens the item's module (same as clicking the row).</summary>
    Public Sub OpenItem()
        If CanOpen Then RaiseEvent OpenRequested(Me, EventArgs.Empty)
    End Sub

    Protected Overrides Sub OnClick(e As EventArgs)
        MyBase.OnClick(e)
        OpenItem()
    End Sub

    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        _hover = CanOpen : Invalidate() : MyBase.OnMouseEnter(e)
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        _hover = False : Invalidate() : MyBase.OnMouseLeave(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        Dim back = If(_hover, Theme.SoftBackground, UiHelper.EffectiveBackColor(Parent))
        g.Clear(back)
        Dim s = DeviceDpi / 96.0F
        Dim statusColor = If(Item.IsUrgent, Theme.StatusRed, Theme.StatusColor(Item.Status))
        If statusColor = Theme.StatusGray AndAlso Not Item.IsUrgent Then statusColor = Theme.StatusAmber

        ' Dot
        Dim dot = CInt(10 * s)
        Dim titleH = TextRenderer.MeasureText("Ag", Theme.BodyBoldFont).Height
        Dim detailH = TextRenderer.MeasureText("Ag", Theme.SmallFont).Height
        Dim top = (Height - titleH - detailH - CInt(2 * s)) \ 2
        g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
        Using brush As New SolidBrush(statusColor)
            g.FillEllipse(brush, CInt(4 * s), top + (titleH - dot) \ 2, dot, dot)
        End Using
        g.SmoothingMode = Drawing2D.SmoothingMode.None

        ' Chevron (clickable rows only)
        Dim chevronW = If(CanOpen, CInt(22 * s), 0)
        If CanOpen Then
            TextRenderer.DrawText(g, ChrW(&H203A), Theme.SubtitleFont, New Rectangle(Width - chevronW, 0, chevronW, Height),
                                  If(_hover, Theme.SidebarBlue, Theme.TextMuted),
                                  TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPrefix)
        End If

        ' Title + status/detail line
        Dim left = CInt(22 * s)
        Dim textW = Width - left - chevronW - CInt(6 * s)
        TextRenderer.DrawText(g, Item.Title, Theme.BodyBoldFont, New Rectangle(left, top, textW, titleH), Theme.TextDark,
                              TextFormatFlags.Left Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix Or TextFormatFlags.SingleLine)
        Dim statusText = Item.Status & "  ·  "
        Dim statusW = Math.Min(textW, TextRenderer.MeasureText(g, statusText, Theme.SmallBoldFont, Size.Empty,
                                                                TextFormatFlags.NoPadding Or TextFormatFlags.NoPrefix).Width)
        Dim y2 = top + titleH + CInt(2 * s)
        TextRenderer.DrawText(g, statusText, Theme.SmallBoldFont, New Rectangle(left, y2, statusW, detailH), statusColor,
                              TextFormatFlags.Left Or TextFormatFlags.NoPadding Or TextFormatFlags.NoPrefix Or TextFormatFlags.SingleLine)
        ' The full detail when it fits, else the short one (narrow window)
        Dim detailW = Math.Max(0, textW - statusW)
        Dim detail = Item.Detail
        If Item.ShortDetail <> "" AndAlso TextRenderer.MeasureText(g, detail, Theme.SmallFont, Size.Empty,
                                                                     TextFormatFlags.NoPadding Or TextFormatFlags.NoPrefix).Width > detailW Then
            detail = Item.ShortDetail
        End If
        TextRenderer.DrawText(g, detail, Theme.SmallFont, New Rectangle(left + statusW, y2, detailW, detailH),
                              If(Item.IsUrgent, Theme.StatusRed, Theme.TextMuted),
                              TextFormatFlags.Left Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix Or TextFormatFlags.SingleLine)

        ' Divider
        Using pen As New Pen(Theme.Divider)
            g.DrawLine(pen, 0, Height - 1, Width, Height - 1)
        End Using
    End Sub

End Class
