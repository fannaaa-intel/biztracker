''' <summary>
''' Add / Edit an employee of a business (Health Certificates module).
''' All rules and saving are done by HealthCertificateService.
''' </summary>
Public Class EmployeeDialog
    Inherits ModernDialog

    Private ReadOnly isNew As Boolean
    Private ReadOnly employee As Employee

    Private ReadOnly txtName As New TextBox()
    Private ReadOnly txtPosition As New TextBox()
    Private ReadOnly cboCategory As New ComboBox()
    Private ReadOnly errName As Label
    Private ReadOnly errPosition As Label
    Private ReadOnly errCategory As Label

    ''' <summary>The saved employee id (after OK).</summary>
    Public ReadOnly Property EmployeeId As Integer
        Get
            Return employee.EmployeeId
        End Get
    End Property

    ''' <param name="existing">The employee to edit (Nothing to add a new one).</param>
    ''' <param name="currentStatus">Certificate status shown in the summary box (edit only).</param>
    Public Sub New(business As Business, Optional existing As Employee = Nothing, Optional currentStatus As String = "")
        isNew = existing Is Nothing
        employee = If(existing, New Employee With {.BusinessId = business.BusinessId, .Category = "Food Handler"})
        ClientSize = New Size(560, 430)
        If isNew Then
            SetTitle("Add Employee", "Add a staff member of " & business.BusinessName)
        Else
            SetTitle("Edit Employee", employee.FullName & "  ·  " & business.BusinessName)
        End If

        ' --- Summary box ---
        Dim info As New RoundedPanel With {.BackColor = Theme.AccentSoft, .ShowShadow = False, .Radius = 10}
        info.SetBounds(24, 18, 512, 64)
        AddInfo(info, "Business", business.BusinessName, 16, 10, 300)
        AddInfo(info, "Health certificate", If(isNew, "Not yet issued", currentStatus), 330, 10, 170)
        Body.Controls.Add(info)

        ' --- Fields ---
        txtName.Font = Theme.InputFont
        txtName.BorderStyle = BorderStyle.FixedSingle
        txtName.MaxLength = 150
        txtName.Text = employee.FullName
        txtName.PlaceholderText = "e.g. Juan Dela Cruz"
        errName = AddField("Full name", txtName, 24, 98, 512)

        txtPosition.Font = Theme.InputFont
        txtPosition.BorderStyle = BorderStyle.FixedSingle
        txtPosition.MaxLength = 100
        txtPosition.Text = employee.Position
        txtPosition.PlaceholderText = "e.g. Baker"
        errPosition = AddField("Position", txtPosition, 24, 176, 246)

        cboCategory.DropDownStyle = ComboBoxStyle.DropDownList
        cboCategory.FlatStyle = FlatStyle.Flat
        cboCategory.Font = Theme.InputFont
        cboCategory.Items.AddRange(HealthCertificateService.Categories)
        cboCategory.SelectedItem = employee.Category
        If cboCategory.SelectedIndex < 0 Then cboCategory.SelectedIndex = 0
        UiHelper.StyleComboBox(cboCategory)
        ' A flat combo has no border of its own: put it in a bordered box as tall as the text boxes
        errCategory = AddField("Category", UiHelper.CreateComboBox(cboCategory, txtPosition.Height), 290, 176, 246)

        Dim note As New Label With {
            .Text = "Food Handlers prepare or serve food. Everyone else (cashier, guard, driver...) is Non-Food.",
            .Font = Theme.SmallFont, .ForeColor = Theme.TextMuted, .AutoSize = False, .AutoEllipsis = True
        }
        note.SetBounds(24, 254, 512, 20)
        Tips.SetToolTip(note, note.Text)
        Body.Controls.Add(note)

        ' --- Buttons ---
        Dim save = AddFooterButton(If(isNew, "Add Employee", "Save Changes"), "primary")
        Dim cancel = AddFooterButton("Cancel")
        AddHandler save.Click, AddressOf Save_Click
        AddHandler cancel.Click, Sub() DialogResult = DialogResult.Cancel
        AcceptButton = save
        CancelButton = cancel

        ' Clear a field's error as soon as the user changes it
        AddHandler txtName.TextChanged, Sub() errName.Text = ""
        AddHandler txtPosition.TextChanged, Sub() errPosition.Text = ""
        AddHandler cboCategory.SelectedIndexChanged, Sub() errCategory.Text = ""
    End Sub

    Private Sub Save_Click(sender As Object, e As EventArgs)
        errName.Text = "" : errPosition.Text = "" : errCategory.Text = ""

        ' Work on a copy so a failed save does not change the caller's object
        Dim candidate As New Employee With {
            .EmployeeId = employee.EmployeeId, .BusinessId = employee.BusinessId, .IsActive = employee.IsActive,
            .FullName = txtName.Text.Trim(), .Position = txtPosition.Text.Trim(),
            .Category = If(cboCategory.SelectedItem?.ToString(), "")
        }
        Dim errors = HealthCertificateService.ValidateEmployee(candidate)
        If errors.Count > 0 Then
            If errors.ContainsKey("name") Then errName.Text = errors("name")
            If errors.ContainsKey("position") Then errPosition.Text = errors("position")
            If errors.ContainsKey("category") Then errCategory.Text = errors("category")
            If errors.ContainsKey("name") Then txtName.Focus() Else If errors.ContainsKey("position") Then txtPosition.Focus()
            Return
        End If

        Cursor = Cursors.WaitCursor
        Dim ok As Boolean
        If isNew Then
            Dim newId = HealthCertificateService.AddEmployee(candidate)
            ok = newId > 0
            If ok Then candidate.EmployeeId = newId
        Else
            ok = HealthCertificateService.UpdateEmployee(candidate)
        End If
        Cursor = Cursors.Default
        If Not ok Then
            UiHelper.ShowError("The employee could not be saved. Please check the details and try again.")
            Return
        End If

        employee.EmployeeId = candidate.EmployeeId
        DialogResult = DialogResult.OK
    End Sub

End Class
