''' <summary>
''' Admin screen, "Users" tab: every account with search, and the selected account's details.
''' Toolbar: Add User, Edit User, Reset Password, Deactivate / Activate.
''' All rules (unique username, one owner per business, cannot deactivate yourself, keep one Admin)
''' are in UserService.
''' </summary>
Public Class AdminUsersPage
    Inherits AdminPage

    Private Class UserRow
        Public Property UserId As Integer
        Public Property Username As String
        Public Property FullName As String
        Public Property Role As String
        Public Property Business As String
        Public Property Status As String
    End Class

    Private ReadOnly btnAdd As Button
    Private ReadOnly btnEdit As Button
    Private ReadOnly btnReset As Button
    Private ReadOnly btnActive As Button

    Private ReadOnly txtSearch As New TextBox()
    Private ReadOnly grid As New ModernGrid()
    Private ReadOnly lblEmpty As New Label()
    Private ReadOnly header As New DetailHeader()
    Private ReadOnly detailsList As New WheelScrollPanel()
    Private ReadOnly detailContent As New Panel()
    Private ReadOnly lblDetailEmpty As New Label()

    Private users As New List(Of User)
    Private selected As User
    Private loading As Boolean

    Public Sub New(bar As ModuleToolbar)
        MyBase.New(bar)
        btnAdd = AddAction("Add User", "Add", "primary", AddressOf AddUser_Click, tip:="Create a staff or owner account")
        btnEdit = AddAction("Edit User", "Edit", "secondary", AddressOf Edit_Click)
        btnReset = AddAction("Reset Password", "Reset", "secondary", AddressOf Reset_Click)
        btnActive = AddAction("Deactivate", "Deactivate", "danger", AddressOf Active_Click)
        Controls.Add(TwoColumns(BuildListCard(), BuildDetailCard(), 52))
    End Sub

    ''' <summary>The selected account (for tests).</summary>
    Public ReadOnly Property SelectedUser As User
        Get
            Return selected
        End Get
    End Property

    Private Function BuildListCard() As Control
        Dim card = CreateCard()
        Dim head = CreateListHeader("User Accounts", txtSearch, "Search users…")
        AddHandler txtSearch.TextChanged, Sub() BindGrid(selected?.UserId)

        UiHelper.StyleGrid(grid, "Status")
        grid.Dock = DockStyle.Fill
        UiHelper.AddGridColumn(grid, "Username", "USERNAME", "Username", 110)
        UiHelper.AddGridColumn(grid, "FullName", "NAME", "FullName", 150)
        UiHelper.AddGridColumn(grid, "Role", "ROLE", "Role", 90)
        UiHelper.AddGridColumn(grid, "Business", "BUSINESS", "Business", 150)
        UiHelper.AddGridColumn(grid, "Status", "STATUS", "Status", 90)
        AddHandler grid.SelectionChanged, Sub() If Not loading Then ShowSelected()
        AddHandler grid.CellDoubleClick, Sub(s, e) If e.RowIndex >= 0 AndAlso btnEdit.Enabled Then Edit_Click(Nothing, EventArgs.Empty)
        AddHandler grid.Resize, Sub() FitGrid()

        StyleEmptyLabel(lblEmpty)
        card.Controls.Add(lblEmpty)
        card.Controls.Add(grid)
        card.Controls.Add(head)
        Return card
    End Function

    Private Sub FitGrid()
        FitColumns(grid, {"Username", "Status", "Role", "FullName", "Business"})
    End Sub

    Private Function BuildDetailCard() As Control
        Dim card = CreateCard(New Padding(18, 14, 18, 16))
        Dim gap As New Panel With {.Dock = DockStyle.Top, .Height = 8, .BackColor = Color.White}
        detailsList.Dock = DockStyle.Fill
        detailsList.BackColor = Color.White
        StyleEmptyLabel(lblDetailEmpty)
        detailContent.Dock = DockStyle.Fill
        detailContent.BackColor = Color.White
        detailContent.Controls.Add(detailsList)
        detailContent.Controls.Add(gap)
        detailContent.Controls.Add(header)
        card.Controls.Add(lblDetailEmpty)
        card.Controls.Add(detailContent)
        Return card
    End Function

    ' =====================================================================
    '  Data
    ' =====================================================================

    Public Overrides Sub RefreshData()
        LoadUsers(selected?.UserId)
    End Sub

    ''' <summary>Reloads and selects an account (used by the Businesses tab's "Open in Users").</summary>
    Public Sub LoadUsers(selectId As Integer?)
        users = UserService.GetUsers()
        BindGrid(selectId)
    End Sub

    Private Sub BindGrid(selectId As Integer?)
        Dim term = txtSearch.Text.Trim().ToLowerInvariant()
        Dim rows = users.Where(Function(u) term = "" OrElse
                                   (u.Username & " " & u.FullName & " " & u.Role & " " & u.BusinessName & " " & UserService.GetStatus(u)).
                                   ToLowerInvariant().Contains(term)).
                         Select(Function(u) New UserRow With {
                             .UserId = u.UserId, .Username = u.Username, .FullName = u.FullName, .Role = u.Role,
                             .Business = If(u.BusinessName = "", "—", u.BusinessName), .Status = UserService.GetStatus(u)
                         }).ToList()
        loading = True
        grid.DataSource = rows
        MeasureColumns(grid, "Status")
        FitGrid()
        Dim index = If(selectId.HasValue, rows.FindIndex(Function(r) r.UserId = selectId.Value), -1)
        If index < 0 AndAlso rows.Count > 0 Then index = 0
        SelectRow(grid, index)
        loading = False

        lblEmpty.Visible = rows.Count = 0
        If rows.Count = 0 Then
            lblEmpty.Text = If(term <> "", "No accounts match your search." & vbCrLf & "Try a username, name, role or business.",
                               "No user accounts yet." & vbCrLf & "Click ""Add User"" to create one.")
            lblEmpty.BringToFront()
        End If
        ShowSelected()
    End Sub

    Private Sub ShowSelected()
        Dim row = If(grid.SelectedRows.Count > 0, TryCast(grid.SelectedRows(0).DataBoundItem, UserRow), Nothing)
        selected = If(row Is Nothing, Nothing, users.FirstOrDefault(Function(u) u.UserId = row.UserId))
        ShowDetails()
        UpdateButtons()
    End Sub

    Private Sub UpdateButtons()
        Dim has = selected IsNot Nothing
        btnEdit.Enabled = has
        btnReset.Enabled = has
        Toolbar.SetTip(btnEdit, If(has, "Change " & selected?.Username & "'s name, role or business", "Select an account first"))
        Toolbar.SetTip(btnReset, If(has, "Give " & selected?.Username & " a new temporary password", "Select an account first"))

        Dim activate = has AndAlso Not selected.IsActive
        Toolbar.SetActionText(btnActive, If(activate, "Activate", "Deactivate"), If(activate, "Activate", "Deactivate"))
        UiHelper.StyleDangerButton(btnActive)
        If activate Then UiHelper.StylePrimaryButton(btnActive)
        Dim blocker = UserService.GetActivationBlocker(selected)
        btnActive.Enabled = blocker Is Nothing
        Toolbar.SetTip(btnActive, If(blocker, If(activate, "Let " & selected.Username & " sign in again",
                                                 "Stop " & selected?.Username & " from signing in (the account and its history are kept)")))
    End Sub

    Private Sub ShowDetails()
        Dim has = selected IsNot Nothing
        lblDetailEmpty.Visible = Not has
        detailContent.Visible = has
        If Not has Then
            lblDetailEmpty.Text = If(users.Count = 0, "Nothing to show yet.", "Select an account to see its details.")
            Return
        End If

        Dim u = selected
        header.SetText(u.Username, UserService.GetStatus(u), u.FullName & " · " & u.Role)
        Dim rows As New List(Of Control) From {
            InfoRow(Tips, "Full name", u.FullName, "Role", u.Role),
            InfoRow(Tips, "Linked business", If(u.BusinessName = "", "None (staff)", u.BusinessName),
                    "Last sign-in", If(u.LastLogin.HasValue, FormatSignIn(u.LastLogin.Value), "Never")),
            InfoRow(Tips, "Account created", UiHelper.FormatDate(u.CreatedAt), "Status", UserService.GetStatus(u)),
            HeadingRow("Access"),
            NoteRow(Tips, UserService.DescribeRole(u.Role), Theme.TextDark)
        }
        If UserService.IsSelf(u) Then
            rows.Add(NoteRow(Tips, "This is your account: you cannot deactivate it or change its role.", Theme.StatusAmber))
        ElseIf Not u.IsActive Then
            rows.Add(NoteRow(Tips, "Deactivated: this account cannot sign in. Click ""Activate"" to allow it again.", Theme.StatusRed))
        Else
            Dim blocker = UserService.GetActivationBlocker(u)
            If blocker IsNot Nothing Then rows.Add(NoteRow(Tips, blocker, Theme.StatusAmber))
        End If
        detailsList.SetRows(rows)
    End Sub

    ''' <summary>"Oct 6, 11:06 PM" this year, "Oct 6, 2025" for older sign-ins (short enough for a narrow window).</summary>
    Private Shared Function FormatSignIn(value As Date) As String
        Return value.ToString(If(value.Year = Date.Today.Year, "MMM d, h:mm tt", "MMM d, yyyy"))
    End Function

    ' =====================================================================
    '  Actions
    ' =====================================================================

    Private Sub AddUser_Click(sender As Object, e As EventArgs)
        Using dlg As New UserDialog()
            If dlg.ShowDialog(OwnerWindow) <> DialogResult.OK Then Return
            LoadUsers(dlg.SavedUser.UserId)
            OnDataChanged()
            ShowCreated(dlg.SavedUser, dlg.Password)
        End Using
    End Sub

    ''' <summary>Success message for a new account: the temporary password is shown once (and copied).</summary>
    Public Shared Sub ShowCreated(u As User, password As String)
        Dim copied = UserDialog.CopyToClipboard(password)
        UiHelper.ShowSuccess("Account '" & u.Username & "' (" & u.Role & ") was created." & vbCrLf & vbCrLf &
                             "Temporary password:  " & password & If(copied, "   (copied to the clipboard)", "") & vbCrLf & vbCrLf &
                             "Next: give the username and password to " & u.FullName & ". It is shown only now - " &
                             "use ""Reset Password"" if it gets lost.", "Account created")
    End Sub

    Private Sub Edit_Click(sender As Object, e As EventArgs)
        If selected Is Nothing Then Return
        Using dlg As New UserDialog(selected)
            If dlg.ShowDialog(OwnerWindow) <> DialogResult.OK Then Return
            LoadUsers(selected.UserId)
            OnDataChanged()
            UiHelper.ShowSuccess("Account '" & dlg.SavedUser.Username & "' was updated." & vbCrLf &
                                 "The changes apply the next time " & dlg.SavedUser.FullName & " signs in.", "Account saved")
        End Using
    End Sub

    Private Sub Reset_Click(sender As Object, e As EventArgs)
        If selected Is Nothing Then Return
        Dim u = selected
        If Not UiHelper.Confirm(u.FullName & " will need the new temporary password to sign in. The old password stops working right away.",
                                "Reset the password of '" & u.Username & "'?", "Reset Password", "Cancel", warning:=True) Then Return
        Dim result = UserService.ResetPassword(u.UserId)
        If result.ErrorMessage IsNot Nothing Then
            UiHelper.ShowWarning(result.ErrorMessage, "Cannot reset the password")
            Return
        End If
        RefreshData()
        OnDataChanged()
        Dim copied = UserDialog.CopyToClipboard(result.TemporaryPassword)
        UiHelper.ShowSuccess("New temporary password for '" & u.Username & "':  " & result.TemporaryPassword &
                             If(copied, "   (copied to the clipboard)", "") & vbCrLf & vbCrLf &
                             "Next: give it to " & u.FullName & ". It is shown only now.", "Password reset")
    End Sub

    Private Sub Active_Click(sender As Object, e As EventArgs)
        If selected Is Nothing Then Return
        Dim u = selected
        Dim activate = Not u.IsActive
        If Not activate AndAlso Not UiHelper.Confirm(u.FullName & " will not be able to sign in. The account and its history are kept, " &
                                                     "and you can activate it again later.",
                                                     "Deactivate '" & u.Username & "'?", "Deactivate", "Cancel", danger:=True) Then Return
        Dim problem = UserService.SetActive(u.UserId, activate)
        If problem IsNot Nothing Then
            UiHelper.ShowWarning(problem, If(activate, "Cannot activate", "Cannot deactivate"))
            Return
        End If
        LoadUsers(u.UserId)
        OnDataChanged()
        UiHelper.ShowSuccess(If(activate, "'" & u.Username & "' can sign in again with the current password." & vbCrLf &
                                          "Next: use ""Reset Password"" if they no longer remember it.",
                                "'" & u.Username & "' was deactivated and can no longer sign in." & vbCrLf &
                                "Next: click ""Activate"" any time to allow it again."),
                             If(activate, "Account activated", "Account deactivated"))
    End Sub

End Class
