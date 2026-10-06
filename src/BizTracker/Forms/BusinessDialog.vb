''' <summary>
''' Add / Edit a business in the registry (Admin screen, Businesses tab).
''' All rules and saving are done by BusinessService.
''' </summary>
Public Class BusinessDialog
    Inherits ModernDialog

    Private ReadOnly isNew As Boolean
    Private ReadOnly biz As Business

    Private ReadOnly txtName As New TextBox()
    Private ReadOnly cboType As New ComboBox()
    Private ReadOnly txtOwner As New TextBox()
    Private ReadOnly txtLine As New TextBox()
    Private ReadOnly txtAddress As New TextBox()
    Private ReadOnly txtBarangay As New TextBox()
    Private ReadOnly txtDti As New TextBox()
    Private ReadOnly txtTin As New TextBox()
    Private ReadOnly txtContact As New TextBox()
    Private ReadOnly txtEmail As New TextBox()
    Private ReadOnly chkFood As New CheckBox()
    Private ReadOnly errors As New Dictionary(Of String, Label)

    ''' <summary>The saved business (after OK).</summary>
    Public ReadOnly Property SavedBusiness As Business
        Get
            Return biz
        End Get
    End Property

    ''' <param name="existing">The business to edit (Nothing to register a new one).</param>
    Public Sub New(Optional existing As Business = Nothing)
        isNew = existing Is Nothing
        biz = If(existing, New Business())
        ClientSize = New Size(560, 76 + 18 + 5 * 78 + 10 + 64)
        If isNew Then
            SetTitle("Add Business", "Register a business in the municipality")
        Else
            SetTitle("Edit Business", biz.BusinessName)
        End If

        ' Row 1: name + type
        StyleText(txtName, biz.BusinessName, "e.g. Cristan's Bakeshop", 150)
        errors("name") = AddField("Business name", txtName, 24, 18, 296)
        cboType.DropDownStyle = ComboBoxStyle.DropDownList
        cboType.FlatStyle = FlatStyle.Flat
        cboType.Font = Theme.InputFont
        cboType.Items.AddRange(BusinessService.BusinessTypes)
        cboType.SelectedItem = biz.BusinessType
        If cboType.SelectedIndex < 0 Then cboType.SelectedIndex = 0
        UiHelper.StyleComboBox(cboType)
        errors("type") = AddField("Type", UiHelper.CreateComboBox(cboType, 29), 336, 18, 200)

        ' Row 2: owner + line of business
        StyleText(txtOwner, biz.OwnerName, "Registered owner", 150)
        errors("owner") = AddField("Owner's name", txtOwner, 24, 96, 246)
        StyleText(txtLine, biz.LineOfBusiness, "e.g. Bakery and Pastry Shop", 150)
        errors("line") = AddField("Line of business", txtLine, 290, 96, 246)

        ' Row 3: address + barangay
        StyleText(txtAddress, biz.Address, "House no., street", 255)
        errors("address") = AddField("Address", txtAddress, 24, 174, 296)
        StyleText(txtBarangay, biz.Barangay, "e.g. Centro 01", 100)
        errors("barangay") = AddField("Barangay", txtBarangay, 336, 174, 200)

        ' Row 4: DTI/SEC, TIN, contact (optional)
        StyleText(txtDti, biz.DtiSecNo, "Optional", 50)
        errors("dti") = AddField("DTI / SEC no.", txtDti, 24, 252, 160)
        StyleText(txtTin, biz.Tin, "123-456-789-000", 20)
        errors("tin") = AddField("TIN (optional)", txtTin, 200, 252, 160)
        StyleText(txtContact, biz.ContactNo, "09171234567", 20)
        errors("contact") = AddField("Contact no. (optional)", txtContact, 376, 252, 160)

        ' Row 5: e-mail + food business
        StyleText(txtEmail, biz.Email, "Optional", 150)
        errors("email") = AddField("E-mail (optional)", txtEmail, 24, 330, 296)
        chkFood.Text = "Food business"
        chkFood.Font = Theme.BodyFont
        chkFood.ForeColor = Theme.TextDark
        chkFood.AutoSize = True
        chkFood.Checked = biz.IsFoodBusiness
        chkFood.Location = New Point(336, 356)
        Tips.SetToolTip(chkFood, "Food businesses need a Food sanitary permit and food handler health certificates")
        Body.Controls.Add(chkFood)

        ' --- Buttons ---
        Dim save = AddFooterButton(If(isNew, "Add Business", "Save Changes"), "primary")
        Dim cancel = AddFooterButton("Cancel")
        AddHandler save.Click, AddressOf Save_Click
        AddHandler cancel.Click, Sub() DialogResult = DialogResult.Cancel
        AcceptButton = save
        CancelButton = cancel

        For Each pair In {New KeyValuePair(Of String, Control)("name", txtName), New KeyValuePair(Of String, Control)("owner", txtOwner),
                          New KeyValuePair(Of String, Control)("line", txtLine), New KeyValuePair(Of String, Control)("address", txtAddress),
                          New KeyValuePair(Of String, Control)("barangay", txtBarangay), New KeyValuePair(Of String, Control)("dti", txtDti),
                          New KeyValuePair(Of String, Control)("tin", txtTin), New KeyValuePair(Of String, Control)("contact", txtContact),
                          New KeyValuePair(Of String, Control)("email", txtEmail)}
            Dim key = pair.Key
            AddHandler pair.Value.TextChanged, Sub() errors(key).Text = ""
        Next
        AddHandler cboType.SelectedIndexChanged, Sub() errors("type").Text = ""
    End Sub

    Private Shared Sub StyleText(box As TextBox, value As String, placeholder As String, maxLength As Integer)
        box.Font = Theme.InputFont
        box.BorderStyle = BorderStyle.FixedSingle
        box.MaxLength = maxLength
        box.Text = value
        box.PlaceholderText = placeholder
    End Sub

    Private Sub Save_Click(sender As Object, e As EventArgs)
        For Each lbl In errors.Values
            lbl.Text = ""
        Next
        Dim candidate As New Business With {
            .BusinessId = biz.BusinessId, .BusinessName = txtName.Text.Trim(), .OwnerName = txtOwner.Text.Trim(),
            .Address = txtAddress.Text.Trim(), .Barangay = txtBarangay.Text.Trim(),
            .BusinessType = If(cboType.SelectedItem?.ToString(), ""), .LineOfBusiness = txtLine.Text.Trim(),
            .IsFoodBusiness = chkFood.Checked, .DtiSecNo = txtDti.Text.Trim(), .Tin = txtTin.Text.Trim(),
            .ContactNo = txtContact.Text.Trim(), .Email = txtEmail.Text.Trim(), .IsActive = biz.IsActive
        }
        Dim problems = BusinessService.ValidateBusiness(candidate)
        If problems.Count > 0 Then
            For Each kv In problems
                Dim lbl As Label = Nothing
                If errors.TryGetValue(kv.Key, lbl) Then
                    lbl.Text = kv.Value
                    Tips.SetToolTip(lbl, kv.Value)
                End If
            Next
            Return
        End If

        Cursor = Cursors.WaitCursor
        Dim problem = If(isNew, BusinessService.AddBusiness(candidate), BusinessService.UpdateBusiness(candidate))
        Cursor = Cursors.Default
        If problem IsNot Nothing Then
            UiHelper.ShowWarning(problem)
            Return
        End If
        biz.BusinessId = candidate.BusinessId
        biz.BusinessName = candidate.BusinessName
        biz.OwnerName = candidate.OwnerName
        biz.IsActive = candidate.IsActive
        DialogResult = DialogResult.OK
    End Sub

End Class
