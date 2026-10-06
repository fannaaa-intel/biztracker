Imports System.Drawing.Drawing2D

' Small custom-painted controls that give the app its modern look.
' All sizes inside OnPaint are relative to the control size so they scale with Windows DPI.

''' <summary>
''' A white card with rounded corners, an optional soft shadow and an optional border.
''' Put child controls inside it like a normal Panel (Padding = inner spacing).
''' </summary>
Public Class RoundedPanel
    Inherits Panel

    Private _radius As Integer = Theme.CardCornerRadius
    Private _borderColor As Color = Color.Empty
    Private _showShadow As Boolean = True

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        BackColor = Theme.CardBackground
        Padding = New Padding(20)
    End Sub

    Public Property Radius As Integer
        Get
            Return _radius
        End Get
        Set(value As Integer)
            _radius = value : Invalidate()
        End Set
    End Property

    ''' <summary>Color.Empty = no border.</summary>
    Public Property BorderColor As Color
        Get
            Return _borderColor
        End Get
        Set(value As Color)
            _borderColor = value : Invalidate()
        End Set
    End Property

    Public Property ShowShadow As Boolean
        Get
            Return _showShadow
        End Get
        Set(value As Boolean)
            _showShadow = value : Invalidate()
        End Set
    End Property

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        Dim g = e.Graphics
        g.Clear(UiHelper.EffectiveBackColor(Parent))
        g.SmoothingMode = SmoothingMode.AntiAlias

        Dim r = ScaledRadius()
        Dim cardRect As New Rectangle(0, 0, Width - 1, Height - 1)
        If _showShadow Then
            ' Soft shadow: two translucent layers just below the card
            Dim shadowRect As New Rectangle(0, 3, Width - 1, Height - 4)
            Using path = UiHelper.RoundedRect(shadowRect, r), brush As New SolidBrush(Color.FromArgb(22, 0, 40, 60))
                g.FillPath(brush, path)
            End Using
            cardRect.Height -= 3
        End If

        Using path = UiHelper.RoundedRect(cardRect, r), brush As New SolidBrush(BackColor)
            g.FillPath(brush, path)
            If _borderColor <> Color.Empty Then
                Using pen As New Pen(_borderColor, 1.0F)
                    g.DrawPath(pen, path)
                End Using
            End If
        End Using
    End Sub

    Private Function ScaledRadius() As Integer
        Return CInt(_radius * DeviceDpi / 96.0)
    End Function

End Class

''' <summary>
''' One item in the blue sidebar: icon + text, hover highlight, and an active state with an accent bar.
''' </summary>
Public Class NavButton
    Inherits Control

    Private _isActive As Boolean
    Private _isHover As Boolean

    Public Property Glyph As String = ""

    ''' <summary>Which screen this button opens (set by MainForm).</summary>
    Public Property Screen As AppScreen

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        BackColor = Theme.SidebarBlue
        ForeColor = Theme.TextOnBlue
        Font = Theme.SidebarFont
        Cursor = Cursors.Hand
        Height = 44
        Margin = New Padding(0, 2, 0, 2)
    End Sub

    Public Property IsActive As Boolean
        Get
            Return _isActive
        End Get
        Set(value As Boolean)
            _isActive = value : Invalidate()
        End Set
    End Property

    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        _isHover = True : Invalidate() : MyBase.OnMouseEnter(e)
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        _isHover = False : Invalidate() : MyBase.OnMouseLeave(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias

        ' Rounded highlight "pill" for hover / active
        If _isActive OrElse _isHover Then
            Dim fill = If(_isActive, Theme.SidebarActive, Theme.SidebarHover)
            Using path = UiHelper.RoundedRect(New Rectangle(0, 0, Width - 1, Height - 1), Height \ 4),
                  brush As New SolidBrush(fill)
                g.FillPath(brush, path)
            End Using
        End If

        ' White accent bar on the left of the active item
        If _isActive Then
            Dim barHeight = Height \ 2
            Using path = UiHelper.RoundedRect(New Rectangle(0, (Height - barHeight) \ 2, Math.Max(3, Height \ 12), barHeight), 2),
                  brush As New SolidBrush(Color.White)
                g.FillPath(brush, path)
            End Using
        End If

        Dim textColor = If(_isActive OrElse _isHover, Color.White, Theme.SidebarTextMuted)
        Dim iconBox As New Rectangle(Height \ 3, 0, Height \ 2, Height)
        TextRenderer.DrawText(g, Glyph, Theme.IconFont(12.0F), iconBox, textColor,
                              TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)

        Dim textLeft = iconBox.Right + Height \ 4
        Dim textBox As New Rectangle(textLeft, 0, Width - textLeft - 6, Height)
        TextRenderer.DrawText(g, Text, If(_isActive, Theme.SidebarActiveFont, Font), textBox, textColor,
                              TextFormatFlags.VerticalCenter Or TextFormatFlags.Left Or
                              TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix)
    End Sub

End Class

''' <summary>
''' A round icon button (e.g. notification bell, logout) with a hover circle
''' and an optional red count badge.
''' </summary>
Public Class IconButton
    Inherits Control

    Private _isHover As Boolean
    Private _badgeCount As Integer

    Public Property Glyph As String = ""
    Public Property HoverColor As Color = Theme.SoftBackground
    Public Property IconColor As Color = Theme.TextDark
    Public Property IconSize As Single = 13.0F
    ''' <summary>If set, a filled circle is always drawn behind the icon (decorative icon badge).</summary>
    Public Property CircleColor As Color = Color.Empty

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        Size = New Size(40, 40)
        Cursor = Cursors.Hand
        BackColor = Color.White
    End Sub

    ''' <summary>0 hides the badge; above 99 shows "99+".</summary>
    Public Property BadgeCount As Integer
        Get
            Return _badgeCount
        End Get
        Set(value As Integer)
            _badgeCount = value : Invalidate()
        End Set
    End Property

    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        _isHover = True : Invalidate() : MyBase.OnMouseEnter(e)
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        _isHover = False : Invalidate() : MyBase.OnMouseLeave(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.Clear(UiHelper.EffectiveBackColor(Me))
        g.SmoothingMode = SmoothingMode.AntiAlias

        If CircleColor <> Color.Empty Then
            Using brush As New SolidBrush(CircleColor)
                g.FillEllipse(brush, 0, 0, Width - 1, Height - 1)
            End Using
        ElseIf _isHover Then
            Using brush As New SolidBrush(HoverColor)
                g.FillEllipse(brush, 0, 0, Width - 1, Height - 1)
            End Using
        End If

        TextRenderer.DrawText(g, Glyph, Theme.IconFont(IconSize), ClientRectangle, IconColor,
                              TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)

        If _badgeCount > 0 Then
            Dim text = If(_badgeCount > 99, "99+", _badgeCount.ToString())
            Dim size = Math.Max(Height \ 2 - 2, 14)
            Dim badgeWidth = Math.Max(size, TextRenderer.MeasureText(text, Theme.SmallBoldFont).Width)
            Dim badge As New Rectangle(Width - badgeWidth, 0, badgeWidth, size)
            Using path = UiHelper.RoundedRect(badge, size \ 2), brush As New SolidBrush(Theme.StatusRed)
                g.FillPath(brush, path)
            End Using
            TextRenderer.DrawText(g, text, Theme.SmallBoldFont, badge, Color.White,
                                  TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
        End If
    End Sub

End Class

''' <summary>A circle with the user's initials (e.g. "LR" for Liza Ramirez).</summary>
Public Class Avatar
    Inherits Control

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        Size = New Size(38, 38)
        BackColor = Theme.SidebarBlue
        ForeColor = Color.White
        Font = Theme.BodyBoldFont
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.Clear(UiHelper.EffectiveBackColor(Parent))
        g.SmoothingMode = SmoothingMode.AntiAlias
        Using brush As New SolidBrush(BackColor)
            g.FillEllipse(brush, 0, 0, Width - 1, Height - 1)
        End Using
        TextRenderer.DrawText(g, UiHelper.GetInitials(Text), Font, ClientRectangle, ForeColor,
                              TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
    End Sub

    Protected Overrides Sub OnTextChanged(e As EventArgs)
        MyBase.OnTextChanged(e) : Invalidate()
    End Sub

End Class

''' <summary>A colored rounded "pill" label for statuses (green / amber / red / gray from Theme).</summary>
Public Class StatusBadge
    Inherits Control

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        Font = Theme.SmallBoldFont
        Size = New Size(110, 26)
    End Sub

    Protected Overrides Sub OnTextChanged(e As EventArgs)
        MyBase.OnTextChanged(e)
        ' Grow to fit the text
        Dim textWidth = TextRenderer.MeasureText(Text, Font).Width
        Width = textWidth + Height
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.Clear(UiHelper.EffectiveBackColor(Parent))
        g.SmoothingMode = SmoothingMode.AntiAlias
        Using path = UiHelper.RoundedRect(New Rectangle(0, 0, Width - 1, Height - 1), Height \ 2),
              brush As New SolidBrush(Theme.StatusSoftColor(Text))
            g.FillPath(brush, path)
        End Using
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, Theme.StatusColor(Text),
                              TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
    End Sub

End Class
