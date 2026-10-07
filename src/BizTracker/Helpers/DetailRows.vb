''' <summary>
''' Row builders shared by the module detail tabs (put the rows in a WheelScrollPanel):
'''   InfoRow  - two caption/value pairs side by side
'''   ListRow  - bold title, muted detail, optional status badge and action links
'''   NoteRow  - one colored line of text
''' Long text is ellipsized and the full text is shown in a tooltip.
''' </summary>
Public Module DetailRows

    ''' <summary>A clickable action shown at the right of a ListRow.</summary>
    Public Class RowLink
        Public Property Text As String
        Public Property Color As Color
        Public Property Action As Action
        ''' <summary>Shorter label used when the row is narrow ("Pay" for "Record Payment"); empty = keep Text.</summary>
        Public Property ShortText As String
        Public Sub New(linkText As String, linkColor As Color, linkAction As Action, Optional shortLinkText As String = "")
            Text = linkText : Color = linkColor : Action = linkAction : ShortText = shortLinkText
        End Sub
    End Class

    ''' <summary>A row with two caption/value pairs side by side.</summary>
    Public Function InfoRow(tips As ToolTip, caption1 As String, value1 As String,
                            Optional caption2 As String = "", Optional value2 As String = "") As Control
        Dim row As New TableLayoutPanel With {.Height = Dpi(48), .ColumnCount = 2, .RowCount = 1, .BackColor = Color.White,
                                              .Margin = New Padding(0), .Padding = New Padding(0)}
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        row.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        row.Controls.Add(InfoPair(tips, caption1, value1), 0, 0)
        If caption2 <> "" Then row.Controls.Add(InfoPair(tips, caption2, value2), 1, 0)
        Return row
    End Function

    Private Function InfoPair(tips As ToolTip, caption As String, value As String) As Control
        Dim pair As New Panel With {.Dock = DockStyle.Fill, .BackColor = Color.White, .Margin = New Padding(0)}
        Dim cap As New Label With {.Text = caption, .Font = Theme.SmallFont, .ForeColor = Theme.TextMuted,
                                   .Dock = DockStyle.Top, .Height = Dpi(20), .AutoEllipsis = True}
        Dim val As New Label With {.Text = value, .Font = Theme.BodyBoldFont, .ForeColor = Theme.TextDark,
                                   .Dock = DockStyle.Top, .Height = Dpi(22), .AutoEllipsis = True}
        tips.SetToolTip(val, value)
        pair.Controls.Add(val)
        pair.Controls.Add(cap)
        Return pair
    End Function

    ''' <summary>
    ''' A list row: bold title, muted detail (colored when detailColor is given), badge and links.
    ''' linkColumnWidth > 0 gives the links a fixed-width column, so the badges of every row in a list
    ''' line up even when rows have different links (e.g. "Endorse  Reject" vs "Reset").
    ''' </summary>
    Public Function ListRow(tips As ToolTip, title As String, detail As String, Optional status As String = "",
                            Optional links As IEnumerable(Of RowLink) = Nothing,
                            Optional detailColor As Color = Nothing,
                            Optional linkColumnWidth As Integer = 0) As Control
        ' Title and detail sit in the middle with a small gap between them (not glued together)
        Dim row As New Panel With {.Height = Dpi(62), .BackColor = Color.White}
        AddHandler row.Paint,
            Sub(s, e)
                Using pen As New Pen(Theme.Divider)
                    e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1)
                End Using
            End Sub

        Dim right As New FlowLayoutPanel With {
            .Dock = DockStyle.Right, .AutoSize = True, .WrapContents = False,
            .FlowDirection = FlowDirection.RightToLeft, .BackColor = Color.White, .Padding = New Padding(0, Dpi(19), 0, 0)
        }
        ' Links: straight into the row, or into a fixed-width column so the badges line up
        Dim linkHost As FlowLayoutPanel = right
        If linkColumnWidth > 0 Then
            linkHost = New FlowLayoutPanel With {
                .AutoSize = False, .Size = New Size(linkColumnWidth, Dpi(26)), .WrapContents = False,
                .FlowDirection = FlowDirection.RightToLeft, .BackColor = Color.White, .Margin = New Padding(0)
            }
            right.Controls.Add(linkHost)
        End If
        If links IsNot Nothing Then
            For Each l In links.Reverse()
                Dim info = l
                Dim link As New LinkLabel With {
                    .Text = info.Text, .AutoSize = True, .Font = Theme.SmallBoldFont, .LinkColor = info.Color,
                    .ActiveLinkColor = Theme.SidebarActive, .LinkBehavior = LinkBehavior.HoverUnderline,
                    .Margin = New Padding(Dpi(12), Dpi(4), 0, 0), .BackColor = Color.White
                }
                ' Deferred: the action usually rebuilds this list, which disposes the clicked link
                AddHandler link.LinkClicked, Sub() row.BeginInvoke(info.Action)
                linkHost.Controls.Add(link)
                If info.ShortText <> "" Then
                    tips.SetToolTip(link, info.Text)
                    ' Narrow row: the short label, so the title is not squeezed
                    AddHandler row.Resize, Sub() link.Text = If(row.Width < Dpi(560), info.ShortText, info.Text)
                End If
            Next
        End If
        If linkHost IsNot right Then
            ' Narrow window: the column shrinks to this row's own links so the title keeps its room
            AddHandler row.Resize,
                Sub()
                    linkHost.Width = If(row.Width < Dpi(560),
                                        linkHost.Controls.Cast(Of Control)().Sum(Function(c) c.PreferredSize.Width + c.Margin.Horizontal),
                                        linkColumnWidth)
                End Sub
        End If
        If status <> "" Then right.Controls.Add(New StatusBadge With {.Text = status, .Height = Dpi(24), .Margin = New Padding(Dpi(12), Dpi(1), Dpi(4), 0)})

        Dim left As New Panel With {.Dock = DockStyle.Fill, .BackColor = Color.White, .Padding = New Padding(0, Dpi(9), Dpi(8), 0)}
        Dim lblTitle As New Label With {
            .Text = title, .Font = Theme.BodyBoldFont, .ForeColor = Theme.TextDark,
            .Dock = DockStyle.Top, .Height = Dpi(24), .AutoEllipsis = True, .TextAlign = ContentAlignment.MiddleLeft
        }
        Dim gap As New Panel With {.Dock = DockStyle.Top, .Height = Dpi(2), .BackColor = Color.White}
        Dim lblDetail As New Label With {
            .Text = detail, .Font = Theme.SmallFont, .Dock = DockStyle.Top, .Height = Dpi(20), .AutoEllipsis = True,
            .TextAlign = ContentAlignment.MiddleLeft,
            .ForeColor = If(detailColor = Nothing, Theme.TextMuted, detailColor)
        }
        tips.SetToolTip(lblTitle, title)
        tips.SetToolTip(lblDetail, detail)
        left.Controls.Add(lblDetail)
        left.Controls.Add(gap)
        left.Controls.Add(lblTitle)

        row.Controls.Add(left)
        row.Controls.Add(right)
        Return row
    End Function

    ''' <summary>Width for ListRow's linkColumnWidth that fits these links side by side (the widest set a row can have).</summary>
    Public Function LinkColumnWidth(ParamArray linkTexts() As String) As Integer
        Return linkTexts.Sum(Function(t) TextRenderer.MeasureText(t, Theme.SmallBoldFont).Width + Dpi(14))
    End Function

    ''' <summary>A colored note (up to two lines, ellipsized; full text in the tooltip).</summary>
    Public Function NoteRow(tips As ToolTip, text As String, color As Color) As Control
        Dim note As New Label With {
            .Height = Dpi(44), .Font = Theme.SmallBoldFont, .ForeColor = color, .BackColor = Color.White,
            .TextAlign = ContentAlignment.MiddleLeft, .AutoEllipsis = True, .Text = text
        }
        tips.SetToolTip(note, text)
        Return note
    End Function

    ''' <summary>A small bold section heading inside a list.</summary>
    Public Function HeadingRow(text As String) As Control
        Return New Label With {
            .Height = Dpi(30), .Font = Theme.SmallBoldFont, .ForeColor = Theme.TextMuted, .BackColor = Color.White,
            .TextAlign = ContentAlignment.BottomLeft, .AutoEllipsis = True, .Text = text.ToUpperInvariant()
        }
    End Function

End Module
