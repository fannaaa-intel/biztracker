''' <summary>
''' Add / Edit a user account (Admin screen, Users tab), or create the Owner account of a business
''' (Businesses tab). New accounts get a prefilled temporary password that the Admin can change.
''' All rules and saving are done by UserService.
''' </summary>
Public Class UserDialog
    Inherits ModernDialog

    Private ReadOnly isNew As Boolean
    Private ReadOnly ownerFor As Business       ' set when creating a business's owner account
    Private ReadOnly account As User

    Private ReadOnly txtUsername As New TextBox()
    Private ReadOnly txtName As New TextBox()
    Private ReadOnly cboRole As New ComboBox()
    Private ReadOnly cboBusiness As New ComboBox()
    Private ReadOnly txtPassword As New TextBox()
    Private ReadOnly errUsername As Label
    Private ReadOnly errName As Label
    Private ReadOnly errRole As Label
    Private ReadOnly errBusiness As Label
    Private ReadOnly errPassword As Label
    Private ReadOnly accessBox As New RoundedPanel()
    Private ReadOnly lblAccess As New Label()
    Private businesses As List(Of Business)

    ''' <summary>The saved account (after OK).</summary>
    Public ReadOnly Property SavedUser As User
        Get
            Return account
        End Get
    End Property

    ''' <summary>The password given to a new account (after OK).</summary>
    Public ReadOnly Property Password As String
        Get
            Return txtPassword.Text
        End Get
    End Property

    ''' <summary>Add a new account (any role).</summary>
    Public Sub New()
        Me.New(New User With {.Role = Roles.BPLO}, True, Nothing)
    End Sub

    ''' <summary>Edit an existing account.</summary>
    Public Sub New(existing As User)
        Me.New(existing, False, Nothing)
    End Sub

    ''' <summary>Create the Owner account of a business (role and business are fixed).</summary>
    Public Sub New(business As Business)
        Me.New(BusinessService.NewOwnerAccount(business), True, business)
    End Sub

    Private Sub New(u As User, adding As Boolean, business As Business)
        isNew = adding
        ownerFor = business
        account = New User With {
            .UserId = u.UserId, .Username = u.Username, .FullName = u.FullName, .Role = u.Role,
            .BusinessId = u.BusinessId, .BusinessName = u.BusinessName, .IsActive = u.IsActive
        }
        Dim bodyHeight = If(isNew, 322, 244)
        ClientSize = New Size(560, 76 + bodyHeight + 64)
        If ownerFor IsNot Nothing Then
            SetTitle("Create Owner Account", "Sign-in for the owner of " & ownerFor.BusinessName)
        ElseIf isNew Then
            SetTitle("Add User", "Create a staff or business owner account")
        Else
            SetTitle("Edit User", account.Username & "  ·  " & account.Role)
        End If

        ' --- Username + name ---
        StyleText(txtUsername, account.Username, "e.g. jdelacruz", 50)
        errUsername = AddField("Username", txtUsername, 24, 18, 246)
        If Not isNew Then
            txtUsername.ReadOnly = True
            txtUsername.BackColor = Theme.SoftBackground
            txtUsername.TabStop = False       ' cannot change: start (and tab) on Full name
            Tips.SetToolTip(txtUsername, "Usernames cannot be changed")
        End If
        StyleText(txtName, account.FullName, "e.g. Juan Dela Cruz", 150)
        errName = AddField("Full name", txtName, 290, 18, 246)

        ' --- Role + business ---
        StyleCombo(cboRole)
        cboRole.Items.AddRange(Roles.All)
        cboRole.SelectedItem = account.Role
        errRole = AddField("Role", UiHelper.CreateComboBox(cboRole, 29), 24, 96, 246)
        StyleCombo(cboBusiness)
        errBusiness = AddField("Business (owner accounts only)", UiHelper.CreateComboBox(cboBusiness, 29), 290, 96, 246)
        If ownerFor IsNot Nothing Then
            cboRole.Enabled = False
            Tips.SetToolTip(cboRole.Parent, "Owner accounts are linked to one business")
        ElseIf Not isNew AndAlso UserService.IsSelf(account) Then
            cboRole.Enabled = False
            Tips.SetToolTip(cboRole.Parent, "You cannot change your own role")
        End If

        ' --- Temporary password (new accounts only) ---
        If isNew Then
            StyleText(txtPassword, UserService.GenerateTemporaryPassword(), "At least 8 letters and numbers", 72)
            errPassword = AddField("Temporary password", txtPassword, 24, 174, 246)
            Dim note As New Label With {
                .Text = "Prefilled with a random password. It is shown again once after saving, so you can give it to the user.",
                .Font = Theme.SmallFont, .ForeColor = Theme.TextMuted, .AutoSize = False, .AutoEllipsis = True
            }
            note.SetBounds(290, 194, 246, 48)
            Tips.SetToolTip(note, note.Text)
            Body.Controls.Add(note)
        Else
            errPassword = New Label()
        End If

        ' --- What the role can open ---
        accessBox.ShowShadow = False
        accessBox.Radius = 10
        accessBox.BackColor = Theme.AccentSoft
        accessBox.Padding = New Padding(14, 0, 14, 0)
        accessBox.SetBounds(24, If(isNew, 252, 174), 512, 52)
        lblAccess.Dock = DockStyle.Fill
        lblAccess.Font = Theme.SmallBoldFont
        lblAccess.ForeColor = Theme.SidebarBlue
        lblAccess.BackColor = Theme.AccentSoft
        lblAccess.TextAlign = ContentAlignment.MiddleLeft
        lblAccess.AutoEllipsis = True
        accessBox.Controls.Add(lblAccess)
        Body.Controls.Add(accessBox)

        ' --- Buttons ---
        Dim save = AddFooterButton(If(ownerFor IsNot Nothing, "Create Account", If(isNew, "Add User", "Save Changes")), "primary")
        Dim cancel = AddFooterButton("Cancel")
        AddHandler save.Click, AddressOf Save_Click
        AddHandler cancel.Click, Sub() DialogResult = DialogResult.Cancel
        AcceptButton = save
        CancelButton = cancel

        AddHandler txtUsername.TextChanged, Sub() errUsername.Text = ""
        AddHandler txtName.TextChanged, Sub() errName.Text = ""
        AddHandler txtPassword.TextChanged, Sub() errPassword.Text = ""
        AddHandler cboBusiness.SelectedIndexChanged, Sub() errBusiness.Text = ""
        AddHandler cboRole.SelectedIndexChanged, Sub()
                                                     errRole.Text = ""
                                                     errBusiness.Text = ""
                                                     FillBusinesses()
                                                 End Sub
        FillBusinesses()
    End Sub

    Private Shared Sub StyleText(box As TextBox, value As String, placeholder As String, maxLength As Integer)
        box.Font = Theme.InputFont
        box.BorderStyle = BorderStyle.FixedSingle
        box.MaxLength = maxLength
        box.Text = value
        box.PlaceholderText = placeholder
    End Sub

    Private Shared Sub StyleCombo(combo As ComboBox)
        combo.DropDownStyle = ComboBoxStyle.DropDownList
        combo.FlatStyle = FlatStyle.Flat
        combo.Font = Theme.InputFont
        UiHelper.StyleComboBox(combo)
    End Sub

    Private ReadOnly Property SelectedRole As String
        Get
            Return If(cboRole.SelectedItem?.ToString(), "")
        End Get
    End Property

    ''' <summary>Owner: the active businesses (or the fixed one). Staff: "Not linked" and disabled.</summary>
    Private Sub FillBusinesses()
        cboBusiness.Items.Clear()
        If SelectedRole <> Roles.Owner Then
            cboBusiness.Items.Add("Not linked (staff account)")
            cboBusiness.SelectedIndex = 0
            cboBusiness.Parent.Enabled = False
            Tips.SetToolTip(cboBusiness.Parent, "Only Owner accounts are linked to a business")
        Else
            If businesses Is Nothing Then
                businesses = If(ownerFor IsNot Nothing, New List(Of Business) From {ownerFor}, BusinessRepository.GetAll())
            End If
            cboBusiness.Items.AddRange(businesses.Cast(Of Object)().ToArray())
            cboBusiness.SelectedIndex = businesses.FindIndex(Function(b) account.BusinessId.HasValue AndAlso b.BusinessId = account.BusinessId.Value)
            cboBusiness.Parent.Enabled = ownerFor Is Nothing
            Tips.SetToolTip(cboBusiness.Parent, If(ownerFor Is Nothing, "The business this owner can view", "Fixed: " & ownerFor.BusinessName))
        End If
        lblAccess.Text = "Access: " & UserService.DescribeRole(SelectedRole)
        Tips.SetToolTip(lblAccess, lblAccess.Text)
    End Sub

    Private Sub Save_Click(sender As Object, e As EventArgs)
        For Each errLabel In {errUsername, errName, errRole, errBusiness, errPassword}
            errLabel.Text = ""
        Next
        Dim candidate As New User With {
            .UserId = account.UserId, .Username = txtUsername.Text.Trim(), .FullName = txtName.Text.Trim(), .Role = SelectedRole,
            .BusinessId = If(SelectedRole = Roles.Owner, TryCast(cboBusiness.SelectedItem, Business)?.BusinessId, Nothing),
            .IsActive = account.IsActive
        }
        Dim errors = UserService.ValidateUser(candidate, isNew, If(isNew, txtPassword.Text, Nothing))
        If errors.Count > 0 Then
            If errors.ContainsKey("username") Then errUsername.Text = errors("username")
            If errors.ContainsKey("name") Then errName.Text = errors("name")
            If errors.ContainsKey("role") Then errRole.Text = errors("role")
            If errors.ContainsKey("business") Then errBusiness.Text = errors("business")
            If errors.ContainsKey("password") Then errPassword.Text = errors("password")
            For Each lbl In {errUsername, errName, errRole, errBusiness, errPassword}
                Tips.SetToolTip(lbl, lbl.Text)
            Next
            Return
        End If

        Cursor = Cursors.WaitCursor
        Dim problem = If(isNew, UserService.AddUser(candidate, txtPassword.Text), UserService.UpdateUser(candidate))
        Cursor = Cursors.Default
        If problem IsNot Nothing Then
            UiHelper.ShowWarning(problem)
            Return
        End If
        account.UserId = candidate.UserId
        account.Username = candidate.Username
        account.FullName = candidate.FullName
        account.Role = candidate.Role
        account.BusinessId = candidate.BusinessId
        DialogResult = DialogResult.OK
    End Sub

    ''' <summary>Puts text on the clipboard. Returns False if the clipboard is busy.</summary>
    Public Shared Function CopyToClipboard(text As String) As Boolean
        Try
            Clipboard.SetText(text)
            Return True
        Catch ex As Exception When TypeOf ex Is Runtime.InteropServices.ExternalException OrElse TypeOf ex Is Threading.ThreadStateException
            Return False
        End Try
    End Function

End Class
