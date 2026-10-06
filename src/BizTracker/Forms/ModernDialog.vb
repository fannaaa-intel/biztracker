''' <summary>
''' Base class for every Add/Edit dialog: a white header with title + subtitle,
''' a body for the fields, and a gray footer with right-aligned buttons.
''' Subclasses add fields with AddField() and buttons with AddFooterButton().
''' </summary>
Public Class ModernDialog
    Inherits Form

    Protected ReadOnly Header As New Panel()
    Protected ReadOnly Body As New Panel()
    Protected ReadOnly Footer As New FlowLayoutPanel()
    Private ReadOnly lblTitle As New Label()
    Private ReadOnly lblSubtitle As New Label()
    Protected ReadOnly Tips As New ToolTip()

    Public Sub New()
        AutoScaleDimensions = New SizeF(96.0F, 96.0F)
        AutoScaleMode = AutoScaleMode.Dpi
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ShowInTaskbar = False
        StartPosition = FormStartPosition.CenterParent
        BackColor = Color.White
        Font = Theme.BodyFont
        ClientSize = New Size(560, 420)

        Header.Dock = DockStyle.Top
        Header.Height = 76
        Header.BackColor = Color.White
        lblTitle.Font = Theme.TitleFont
        lblTitle.ForeColor = Theme.TextDark
        lblTitle.AutoSize = True
        lblTitle.Location = New Point(22, 14)
        lblSubtitle.Font = Theme.BodyFont
        lblSubtitle.ForeColor = Theme.TextMuted
        lblSubtitle.AutoSize = False
        lblSubtitle.AutoEllipsis = True
        lblSubtitle.SetBounds(24, 46, 510, 22)
        Header.Controls.AddRange({lblTitle, lblSubtitle})
        AddHandler Header.Resize, Sub() lblSubtitle.Width = Math.Max(50, Header.ClientSize.Width - 48)
        AddHandler Header.Paint,
            Sub(s, e)
                Using pen As New Pen(Theme.Divider)
                    e.Graphics.DrawLine(pen, 0, Header.Height - 1, Header.Width, Header.Height - 1)
                End Using
            End Sub

        Body.Dock = DockStyle.Fill
        Body.BackColor = Color.White

        Footer.Dock = DockStyle.Bottom
        Footer.Height = 64
        Footer.FlowDirection = FlowDirection.RightToLeft
        Footer.WrapContents = False
        Footer.Padding = New Padding(16, 14, 8, 0)
        Footer.BackColor = Theme.SoftBackground

        Controls.Add(Body)
        Controls.Add(Footer)
        Controls.Add(Header)
    End Sub

    Protected Sub SetTitle(title As String, subtitle As String)
        Text = title
        lblTitle.Text = title
        lblSubtitle.Text = subtitle
    End Sub

    ''' <summary>Adds a footer button (added right-to-left: add the main action first).</summary>
    Protected Function AddFooterButton(text As String, Optional style As String = "secondary") As Button
        Dim btn As New Button With {.Text = text, .Height = 38, .Margin = New Padding(8, 0, 0, 0)}
        btn.Width = Math.Max(100, TextRenderer.MeasureText(text, Theme.BodyBoldFont).Width + 36)
        Select Case style
            Case "primary" : UiHelper.StylePrimaryButton(btn)
            Case "danger" : UiHelper.StyleDangerButton(btn)
            Case Else : UiHelper.StyleSecondaryButton(btn)
        End Select
        Footer.Controls.Add(btn)
        Return btn
    End Function

    ''' <summary>
    ''' Adds "caption" above an input control at (left, top) with the given width,
    ''' plus a red error label under it. Returns the error label (set .Text to show an error).
    ''' </summary>
    Protected Function AddField(caption As String, input As Control, left As Integer, top As Integer,
                                width As Integer) As Label
        Dim lbl As New Label With {
            .Text = caption, .Font = Theme.SmallBoldFont, .ForeColor = Theme.TextDark,
            .AutoSize = False, .AutoEllipsis = True
        }
        lbl.SetBounds(left, top, width, 20)
        input.SetBounds(left, top + 22, width, input.Height)
        Dim err As New Label With {
            .Font = Theme.SmallFont, .ForeColor = Theme.StatusRed, .AutoSize = False, .AutoEllipsis = True
        }
        err.SetBounds(left, input.Bottom + 2, width, 18)
        Body.Controls.AddRange({lbl, input, err})
        Return err
    End Function

    ''' <summary>A read-only info line: small muted caption and a bold value.</summary>
    Protected Sub AddInfo(parent As Control, caption As String, value As String, left As Integer, top As Integer, width As Integer)
        Dim cap As New Label With {.Text = caption, .Font = Theme.SmallFont, .ForeColor = Theme.TextMuted,
                                   .AutoSize = False, .AutoEllipsis = True, .BackColor = parent.BackColor}
        cap.SetBounds(left, top, width, 18)
        Dim val As New Label With {.Text = value, .Font = Theme.BodyBoldFont, .ForeColor = Theme.TextDark,
                                   .AutoSize = False, .AutoEllipsis = True, .BackColor = parent.BackColor}
        val.SetBounds(left, top + 18, width, 22)
        Tips.SetToolTip(val, value)
        parent.Controls.AddRange({cap, val})
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then Tips.Dispose()
        MyBase.Dispose(disposing)
    End Sub

End Class

''' <summary>Small dialog that asks for one line/paragraph of text (e.g. a rejection reason).</summary>
Public Class PromptDialog
    Inherits ModernDialog

    Private ReadOnly txtValue As New TextBox()
    Private ReadOnly lblError As Label
    Private ReadOnly required As Boolean

    Private Sub New(title As String, message As String, caption As String, isRequired As Boolean, okText As String,
                    okStyle As String)
        required = isRequired
        ClientSize = New Size(480, 300)
        SetTitle(title, message)
        txtValue.Multiline = True
        txtValue.Height = 84
        txtValue.Font = Theme.InputFont
        txtValue.MaxLength = 255
        txtValue.BorderStyle = BorderStyle.FixedSingle
        lblError = AddField(caption, txtValue, 24, 18, 430)

        Dim ok = AddFooterButton(okText, okStyle)
        Dim cancel = AddFooterButton("Cancel")
        AddHandler ok.Click, AddressOf Ok_Click
        AddHandler cancel.Click, Sub() DialogResult = DialogResult.Cancel
        CancelButton = cancel
    End Sub

    Private Sub Ok_Click(sender As Object, e As EventArgs)
        If required AndAlso txtValue.Text.Trim() = "" Then
            lblError.Text = "This field is required."
            txtValue.Focus()
            Return
        End If
        DialogResult = DialogResult.OK
    End Sub

    ''' <summary>Shows the prompt. Returns the text, or Nothing if cancelled.</summary>
    Public Shared Function Ask(owner As IWin32Window, title As String, message As String, caption As String,
                               Optional required As Boolean = True, Optional okText As String = "OK",
                               Optional okStyle As String = "primary") As String
        Using dlg As New PromptDialog(title, message, caption, required, okText, okStyle)
            If dlg.ShowDialog(owner) <> DialogResult.OK Then Return Nothing
            Return dlg.txtValue.Text.Trim()
        End Using
    End Function

End Class
