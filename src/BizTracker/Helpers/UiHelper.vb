Imports System.Drawing.Drawing2D

''' <summary>
''' Shared UI helpers: consistent button / input styling, message boxes, and drawing utilities.
''' </summary>
Public Module UiHelper

    ' ==================== Buttons ====================

    ''' <summary>Solid blue button for the main action on a screen.</summary>
    Public Sub StylePrimaryButton(btn As Button)
        btn.FlatStyle = FlatStyle.Flat
        btn.FlatAppearance.BorderSize = 0
        btn.FlatAppearance.MouseOverBackColor = Theme.ButtonPrimaryHover
        btn.FlatAppearance.MouseDownBackColor = Theme.ButtonPrimaryPressed
        btn.BackColor = Theme.ButtonPrimary
        btn.ForeColor = Theme.ButtonPrimaryText
        btn.Font = Theme.BodyBoldFont
        btn.Cursor = Cursors.Hand
        btn.UseVisualStyleBackColor = False
    End Sub

    ''' <summary>White button with a gray border for secondary actions.</summary>
    Public Sub StyleSecondaryButton(btn As Button)
        btn.FlatStyle = FlatStyle.Flat
        btn.FlatAppearance.BorderSize = 1
        btn.FlatAppearance.BorderColor = Theme.InputBorder
        btn.FlatAppearance.MouseOverBackColor = Theme.SoftBackground
        btn.FlatAppearance.MouseDownBackColor = Theme.Divider
        btn.BackColor = Color.White
        btn.ForeColor = Theme.TextDark
        btn.Font = Theme.BodyBoldFont
        btn.Cursor = Cursors.Hand
        btn.UseVisualStyleBackColor = False
    End Sub

    ''' <summary>Red button for destructive actions (delete, reject).</summary>
    Public Sub StyleDangerButton(btn As Button)
        StylePrimaryButton(btn)
        btn.BackColor = Theme.StatusRed
        btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(Theme.StatusRed, 0.2F)
        btn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(Theme.StatusRed, 0.1F)
    End Sub

    ' ==================== Inputs ====================

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
            AddHandler box.Resize, Sub() icon.SetBounds(12, 2, 24, box.ClientSize.Height - 4)
            AddHandler icon.Click, Sub() textBox.Focus()
        End If

        textBox.BorderStyle = BorderStyle.None
        textBox.Font = Theme.InputFont
        textBox.ForeColor = Theme.TextDark
        textBox.BackColor = Color.White
        textBox.PlaceholderText = placeholder
        textBox.Anchor = AnchorStyles.Left Or AnchorStyles.Right
        box.Controls.Add(textBox)

        ' Keep the text box vertically centered and full width inside the box
        AddHandler box.Resize,
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

    ' ==================== Messages ====================

    Public Sub ShowInfo(message As String, Optional title As String = "BizTracker")
        MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Public Sub ShowWarning(message As String, Optional title As String = "BizTracker")
        MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning)
    End Sub

    Public Sub ShowError(message As String, Optional title As String = "BizTracker")
        MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error)
    End Sub

    ''' <summary>Yes/No question. Returns True if the user clicked Yes.</summary>
    Public Function Confirm(message As String, Optional title As String = "Please confirm") As Boolean
        Return MessageBox.Show(message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                               MessageBoxDefaultButton.Button2) = DialogResult.Yes
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
