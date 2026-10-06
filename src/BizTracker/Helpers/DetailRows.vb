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
        Public Sub New(linkText As String, linkColor As Color, linkAction As Action)
            Text = linkText : Color = linkColor : Action = linkAction
        End Sub
    End Class

    ''' <summary>A row with two caption/value pairs side by side.</summary>
    Public Function InfoRow(tips As ToolTip, caption1 As String, value1 As String,
                            Optional caption2 As String = "", Optional value2 As String = "") As Control
        Dim row As New TableLayoutPanel With {.Height = 48, .ColumnCount = 2, .RowCount = 1, .BackColor = Color.White,
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
                                   .Dock = DockStyle.Top, .Height = 20, .AutoEllipsis = True}
        Dim val As New Label With {.Text = value, .Font = Theme.BodyBoldFont, .ForeColor = Theme.TextDark,
                                   .Dock = DockStyle.Top, .Height = 22, .AutoEllipsis = True}
        tips.SetToolTip(val, value)
        pair.Controls.Add(val)
        pair.Controls.Add(cap)
        Return pair
    End Function

    ''' <summary>A list row: bold title, muted detail (colored when detailColor is given), badge and links.</summary>
    Public Function ListRow(tips As ToolTip, title As String, detail As String, Optional status As String = "",
                            Optional links As IEnumerable(Of RowLink) = Nothing,
                            Optional detailColor As Color = Nothing) As Control
        Dim row As New Panel With {.Height = 52, .BackColor = Color.White}
        AddHandler row.Paint,
            Sub(s, e)
                Using pen As New Pen(Theme.Divider)
                    e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1)
                End Using
            End Sub

        Dim right As New FlowLayoutPanel With {
            .Dock = DockStyle.Right, .AutoSize = True, .WrapContents = False,
            .FlowDirection = FlowDirection.RightToLeft, .BackColor = Color.White, .Padding = New Padding(0, 13, 0, 0)
        }
        If links IsNot Nothing Then
            For Each l In links.Reverse()
                Dim info = l
                Dim link As New LinkLabel With {
                    .Text = info.Text, .AutoSize = True, .Font = Theme.SmallBoldFont, .LinkColor = info.Color,
                    .ActiveLinkColor = Theme.SidebarActive, .LinkBehavior = LinkBehavior.HoverUnderline,
                    .Margin = New Padding(10, 4, 0, 0), .BackColor = Color.White
                }
                ' Deferred: the action usually rebuilds this list, which disposes the clicked link
                AddHandler link.LinkClicked, Sub() row.BeginInvoke(info.Action)
                right.Controls.Add(link)
            Next
        End If
        If status <> "" Then right.Controls.Add(New StatusBadge With {.Text = status, .Height = 24, .Margin = New Padding(8, 1, 4, 0)})

        Dim left As New Panel With {.Dock = DockStyle.Fill, .BackColor = Color.White}
        Dim lblTitle As New Label With {
            .Text = title, .Font = Theme.BodyBoldFont, .ForeColor = Theme.TextDark,
            .Dock = DockStyle.Top, .Height = 26, .AutoEllipsis = True, .TextAlign = ContentAlignment.BottomLeft
        }
        Dim lblDetail As New Label With {
            .Text = detail, .Font = Theme.SmallFont, .Dock = DockStyle.Top, .Height = 20, .AutoEllipsis = True,
            .ForeColor = If(detailColor = Nothing, Theme.TextMuted, detailColor)
        }
        tips.SetToolTip(lblTitle, title)
        tips.SetToolTip(lblDetail, detail)
        left.Controls.Add(lblDetail)
        left.Controls.Add(lblTitle)

        row.Controls.Add(left)
        row.Controls.Add(right)
        Return row
    End Function

    ''' <summary>A colored note (up to two lines, ellipsized; full text in the tooltip).</summary>
    Public Function NoteRow(tips As ToolTip, text As String, color As Color) As Control
        Dim note As New Label With {
            .Height = 44, .Font = Theme.SmallBoldFont, .ForeColor = color, .BackColor = Color.White,
            .TextAlign = ContentAlignment.MiddleLeft, .AutoEllipsis = True, .Text = text
        }
        tips.SetToolTip(note, text)
        Return note
    End Function

    ''' <summary>A small bold section heading inside a list.</summary>
    Public Function HeadingRow(text As String) As Control
        Return New Label With {
            .Height = 30, .Font = Theme.SmallBoldFont, .ForeColor = Theme.TextMuted, .BackColor = Color.White,
            .TextAlign = ContentAlignment.BottomLeft, .AutoEllipsis = True, .Text = text.ToUpperInvariant()
        }
    End Function

End Module
