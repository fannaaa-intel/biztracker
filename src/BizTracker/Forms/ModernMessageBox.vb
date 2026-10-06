Imports System.Drawing.Drawing2D
Imports System.Runtime.InteropServices

''' <summary>The look of a ModernMessageBox (icon + accent color).</summary>
Public Enum MessageKind
    Info
    Success
    Warning
    [Error]
    Question
End Enum

''' <summary>
''' The app's own message box (replaces the standard Windows MessageBox):
''' a rounded white card with a colored icon, title, message and buttons.
''' It fades and slides in, can be dragged, Enter = main button, Esc = cancel.
''' Use UiHelper.ShowInfo / ShowWarning / ShowError / ShowSuccess / Confirm.
''' </summary>
Public Class ModernMessageBox
    Inherits Form

    Private ReadOnly kind As MessageKind
    Private ReadOnly animTimer As New Timer With {.Interval = 15}
    Private targetTop As Integer
    Private animStep As Integer

    Private Sub New(message As String, title As String, messageKind As MessageKind,
                    okText As String, cancelText As String, dangerOk As Boolean)
        kind = messageKind
        AutoScaleMode = AutoScaleMode.None      ' sizes below are already scaled by s
        FormBorderStyle = FormBorderStyle.None
        ShowInTaskbar = False
        KeyPreview = True
        BackColor = Color.White
        Font = Theme.BodyFont
        Text = title
        Opacity = 0

        Dim s = DeviceDpi / 96.0F
        Dim pad = CInt(24 * s)
        Dim iconSize = CInt(48 * s)
        Dim textLeft = pad + iconSize + CInt(16 * s)
        Dim width = CInt(460 * s)
        Dim textWidth = width - textLeft - pad

        ' Colored icon circle
        Dim icon As New IconButton With {
            .Glyph = GlyphFor(kind), .IconSize = 16.0F, .IconColor = AccentFor(kind),
            .CircleColor = Theme.StatusSoftColor(StatusWordFor(kind)), .Cursor = Cursors.Default,
            .BackColor = Color.White
        }
        If kind = MessageKind.Info OrElse kind = MessageKind.Question Then icon.CircleColor = Theme.AccentSoft
        icon.SetBounds(pad, pad, iconSize, iconSize)

        ' Title + message (message height grows with the text)
        Dim lblTitle As New Label With {
            .Text = title, .Font = Theme.SubtitleFont, .ForeColor = Theme.TextDark, .AutoSize = False,
            .AutoEllipsis = True, .BackColor = Color.White
        }
        lblTitle.SetBounds(textLeft, pad, textWidth, CInt(26 * s))
        Dim msgSize = TextRenderer.MeasureText(message, Theme.BodyFont, New Size(textWidth, Integer.MaxValue),
                                               TextFormatFlags.WordBreak Or TextFormatFlags.NoPrefix)
        Dim msgHeight = Math.Min(msgSize.Height + CInt(4 * s), CInt(320 * s))
        Dim lblMessage As New Label With {
            .Text = message, .Font = Theme.BodyFont, .ForeColor = Theme.TextMuted, .AutoSize = False,
            .AutoEllipsis = True, .UseMnemonic = False, .BackColor = Color.White
        }
        lblMessage.SetBounds(textLeft, lblTitle.Bottom + CInt(6 * s), textWidth, msgHeight)

        ' Buttons (right-aligned)
        Dim buttonsTop = Math.Max(icon.Bottom, lblMessage.Bottom) + CInt(22 * s)
        Dim btnHeight = CInt(38 * s)
        Dim right = width - pad
        Dim okButton As New Button With {.Text = okText}
        If dangerOk Then UiHelper.StyleDangerButton(okButton) Else UiHelper.StylePrimaryButton(okButton)
        okButton.SetBounds(0, buttonsTop, Math.Max(CInt(96 * s), TextRenderer.MeasureText(okText, Theme.BodyBoldFont).Width + CInt(36 * s)), btnHeight)
        okButton.Left = right - okButton.Width
        AddHandler okButton.Click, Sub() CloseWith(DialogResult.OK)
        Controls.Add(okButton)
        AcceptButton = okButton

        If cancelText <> "" Then
            Dim cancelButton As New Button With {.Text = cancelText}
            UiHelper.StyleSecondaryButton(cancelButton)
            cancelButton.SetBounds(0, buttonsTop, Math.Max(CInt(96 * s), TextRenderer.MeasureText(cancelText, Theme.BodyBoldFont).Width + CInt(36 * s)), btnHeight)
            cancelButton.Left = okButton.Left - cancelButton.Width - CInt(10 * s)
            AddHandler cancelButton.Click, Sub() CloseWith(DialogResult.Cancel)
            Controls.Add(cancelButton)
            Me.CancelButton = cancelButton
        End If

        Controls.AddRange({icon, lblTitle, lblMessage})
        ClientSize = New Size(width, buttonsTop + btnHeight + pad)

        ' Drag the card by any non-button area
        For Each c In New Control() {Me, icon, lblTitle, lblMessage}
            AddHandler c.MouseDown, AddressOf DragWindow
        Next
        AddHandler animTimer.Tick, AddressOf Animate
    End Sub

    ' ==================== Public API ====================

    ''' <summary>Shows a message with an OK button.</summary>
    Public Shared Sub Inform(message As String, title As String, kind As MessageKind, Optional okText As String = "OK")
        Using box As New ModernMessageBox(message, title, kind, okText, "", False)
            box.ShowCentered()
        End Using
    End Sub

    ''' <summary>Yes/No style question. Returns True if the main button was clicked.
    ''' warning = True shows the amber warning icon with a normal (blue) main button.</summary>
    Public Shared Function Ask(message As String, title As String, Optional yesText As String = "Yes",
                               Optional noText As String = "No", Optional danger As Boolean = False,
                               Optional warning As Boolean = False) As Boolean
        Using box As New ModernMessageBox(message, title, If(danger OrElse warning, MessageKind.Warning, MessageKind.Question),
                                          yesText, noText, danger)
            Return box.ShowCentered() = DialogResult.OK
        End Using
    End Function

    ' ==================== Behaviour ====================

    Private Function ShowCentered() As DialogResult
        Dim owner = Form.ActiveForm
        If owner IsNot Nothing AndAlso (owner Is Me OrElse Not owner.Visible) Then owner = Nothing
        Dim area = If(owner IsNot Nothing, owner.Bounds, Screen.PrimaryScreen.WorkingArea)
        StartPosition = FormStartPosition.Manual
        targetTop = area.Top + (area.Height - Height) \ 2
        Location = New Point(area.Left + (area.Width - Width) \ 2, targetTop + CInt(16 * DeviceDpi / 96.0F))
        If owner Is Nothing Then TopMost = True
        Return If(owner IsNot Nothing, ShowDialog(owner), ShowDialog())
    End Function

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        animStep = 0
        animTimer.Start()
        If AcceptButton IsNot Nothing Then DirectCast(AcceptButton, Button).Focus()
    End Sub

    ''' <summary>Fade + slide up (about 150 ms).</summary>
    Private Sub Animate(sender As Object, e As EventArgs)
        animStep += 1
        Dim t = Math.Min(1.0, animStep / 10.0)
        Dim eased = 1 - Math.Pow(1 - t, 3)                         ' ease-out
        Opacity = eased
        Top = targetTop + CInt((1 - eased) * 16 * DeviceDpi / 96.0F)
        If t >= 1 Then animTimer.Stop()
    End Sub

    Private Sub CloseWith(result As DialogResult)
        animTimer.Stop()
        DialogResult = result
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        If e.KeyCode = Keys.Escape Then CloseWith(If(CancelButton IsNot Nothing, DialogResult.Cancel, DialogResult.OK))
        MyBase.OnKeyDown(e)
    End Sub

    ''' <summary>Thin accent bar at the top + light border around the card.</summary>
    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        Dim s = DeviceDpi / 96.0F
        Using bar As New SolidBrush(AccentFor(kind))
            e.Graphics.FillRectangle(bar, 0, 0, Width, CInt(4 * s))
        End Using
        Using pen As New Pen(Theme.Divider)
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1)
        End Using
    End Sub

    ' ==================== Look per kind ====================

    Private Shared Function GlyphFor(k As MessageKind) As String
        Select Case k
            Case MessageKind.Success : Return Icons.CheckMark
            Case MessageKind.Warning : Return Icons.Warning
            Case MessageKind.Error : Return ChrW(&HEA39)      ' error badge
            Case MessageKind.Question : Return ChrW(&HE9CE)   ' help / question
            Case Else : Return Icons.Info
        End Select
    End Function

    Private Shared Function AccentFor(k As MessageKind) As Color
        Select Case k
            Case MessageKind.Success : Return Theme.StatusGreen
            Case MessageKind.Warning : Return Theme.StatusAmber
            Case MessageKind.Error : Return Theme.StatusRed
            Case Else : Return Theme.SidebarBlue
        End Select
    End Function

    Private Shared Function StatusWordFor(k As MessageKind) As String
        Select Case k
            Case MessageKind.Success : Return "valid"
            Case MessageKind.Warning : Return "pending"
            Case MessageKind.Error : Return "rejected"
            Case Else : Return ""
        End Select
    End Function

    ' ==================== Borderless window helpers ====================

    <DllImport("user32.dll")>
    Private Shared Function ReleaseCapture() As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function SendMessage(hWnd As IntPtr, msg As Integer, wParam As Integer, lParam As Integer) As IntPtr
    End Function

    <DllImport("dwmapi.dll")>
    Private Shared Function DwmSetWindowAttribute(hwnd As IntPtr, attribute As Integer, ByRef value As Integer, size As Integer) As Integer
    End Function

    Private Sub DragWindow(sender As Object, e As MouseEventArgs)
        If e.Button <> MouseButtons.Left Then Return
        ReleaseCapture()
        SendMessage(Handle, &HA1, 2, 0)    ' WM_NCLBUTTONDOWN, HTCAPTION
    End Sub

    Protected Overrides ReadOnly Property CreateParams As CreateParams
        Get
            Dim cp = MyBase.CreateParams
            cp.ClassStyle = cp.ClassStyle Or &H20000   ' CS_DROPSHADOW
            Return cp
        End Get
    End Property

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        Dim round As Integer = 2                        ' DWMWCP_ROUND (Windows 11)
        Try
            DwmSetWindowAttribute(Handle, 33, round, 4)
        Catch
        End Try
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then animTimer.Dispose()
        MyBase.Dispose(disposing)
    End Sub

End Class
