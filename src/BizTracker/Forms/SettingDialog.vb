''' <summary>
''' Edit one value of the settings table (Admin screen, Settings tab).
''' Shows the current value and the rule for its kind; SettingsService validates and saves.
''' </summary>
Public Class SettingDialog
    Inherits ModernDialog

    Private ReadOnly setting As Setting
    Private ReadOnly txtValue As New TextBox()
    Private ReadOnly errValue As Label

    ''' <summary>The value as stored after saving.</summary>
    Public Property SavedValue As String = ""

    Public Sub New(s As Setting)
        setting = s
        Dim info = SettingsService.GetInfo(s.SettingKey)
        ClientSize = New Size(520, 76 + 250 + 64)
        SetTitle("Edit Setting", info.Label)

        Dim box As New RoundedPanel With {.BackColor = Theme.AccentSoft, .ShowShadow = False, .Radius = 10}
        box.SetBounds(24, 18, 472, 64)
        AddInfo(box, "Current value", SettingsService.FormatValue(s.SettingKey, s.SettingValue), 16, 10, 260)
        AddInfo(box, "Key", s.SettingKey, 290, 10, 170)
        Body.Controls.Add(box)

        txtValue.Font = Theme.InputFont
        txtValue.BorderStyle = BorderStyle.FixedSingle
        txtValue.MaxLength = 255
        txtValue.Text = s.SettingValue
        errValue = AddField("New value", txtValue, 24, 98, 472)

        Dim hint As New Label With {
            .Text = info.Hint, .Font = Theme.SmallFont, .ForeColor = Theme.TextMuted,
            .AutoSize = False, .AutoEllipsis = True
        }
        hint.SetBounds(24, 172, 472, 40)
        Tips.SetToolTip(hint, info.Hint)
        Body.Controls.Add(hint)

        Dim note As New Label With {
            .Text = "The new value is used right away (existing records keep their saved amounts).",
            .Font = Theme.SmallFont, .ForeColor = Theme.SidebarBlue, .AutoSize = False, .AutoEllipsis = True
        }
        note.SetBounds(24, 212, 472, 22)
        Body.Controls.Add(note)

        Dim save = AddFooterButton("Save Value", "primary")
        Dim cancel = AddFooterButton("Cancel")
        AddHandler save.Click, AddressOf Save_Click
        AddHandler cancel.Click, Sub() DialogResult = DialogResult.Cancel
        AcceptButton = save
        CancelButton = cancel
        AddHandler txtValue.TextChanged, Sub() errValue.Text = ""
    End Sub

    ''' <summary>Types a value and clicks Save (used by the UI tests).</summary>
    Public Sub TrySave(value As String)
        txtValue.Text = value
        Save_Click(Nothing, EventArgs.Empty)
    End Sub

    Private Sub Save_Click(sender As Object, e As EventArgs)
        Dim normalized As String = Nothing
        Dim problem = SettingsService.ValidateValue(setting.SettingKey, txtValue.Text, normalized)
        If problem IsNot Nothing Then
            errValue.Text = problem
            txtValue.Focus()
            Return
        End If
        problem = SettingsService.UpdateSetting(setting.SettingKey, txtValue.Text)
        If problem IsNot Nothing Then
            UiHelper.ShowWarning(problem)
            Return
        End If
        SavedValue = normalized
        DialogResult = DialogResult.OK
    End Sub

End Class
