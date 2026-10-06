''' <summary>
''' Admin screen, "Businesses" tab: the business registry (active and deactivated) with search,
''' and the selected business's details and owner account.
''' Toolbar: Add Business, Edit, Create Owner Account, Deactivate / Reactivate.
''' Deactivating is a soft delete that also stops the owner's login (BusinessService).
''' </summary>
Public Class AdminBusinessesPage
    Inherits AdminPage

    Private Class BusinessRow
        Public Property BusinessId As Integer
        Public Property Business As String
        Public Property Owner As String
        Public Property Barangay As String
        Public Property Account As String
        Public Property Status As String
    End Class

    Private ReadOnly btnAdd As Button
    Private ReadOnly btnEdit As Button
    Private ReadOnly btnOwner As Button
    Private ReadOnly btnActive As Button

    Private ReadOnly txtSearch As New TextBox()
    Private ReadOnly grid As New ModernGrid()
    Private ReadOnly lblEmpty As New Label()
    Private ReadOnly header As New DetailHeader()
    Private ReadOnly detailsList As New WheelScrollPanel()
    Private ReadOnly detailContent As New Panel()
    Private ReadOnly lblDetailEmpty As New Label()

    Private businesses As New List(Of Business)
    Private owners As New Dictionary(Of Integer, User)
    Private selected As Business
    Private loading As Boolean

    ''' <summary>Raised by "Open in Users": the Admin screen switches to the Users tab on that account.</summary>
    Public Event OpenUserRequested(userId As Integer)

    Public Sub New(bar As ModuleToolbar)
        MyBase.New(bar)
        btnAdd = AddAction("Add Business", "Add", "primary", AddressOf AddBusiness_Click, tip:="Register a new business")
        btnEdit = AddAction("Edit Business", "Edit", "secondary", AddressOf Edit_Click)
        btnOwner = AddAction("Create Owner Account", "Owner", "secondary", AddressOf Owner_Click)
        btnActive = AddAction("Deactivate", "Deactivate", "danger", AddressOf Active_Click)
        Controls.Add(TwoColumns(BuildListCard(), BuildDetailCard(), 52))
    End Sub

    Public ReadOnly Property SelectedBusiness As Business
        Get
            Return selected
        End Get
    End Property

    Private Function BuildListCard() As Control
        Dim card = CreateCard()
        Dim head = CreateListHeader("Business Registry", txtSearch, "Search businesses…")
        AddHandler txtSearch.TextChanged, Sub() BindGrid(selected?.BusinessId)

        UiHelper.StyleGrid(grid, "Status")
        grid.Dock = DockStyle.Fill
        UiHelper.AddGridColumn(grid, "Business", "BUSINESS", "Business", 170)
        UiHelper.AddGridColumn(grid, "Owner", "OWNER", "Owner", 140)
        UiHelper.AddGridColumn(grid, "Barangay", "BARANGAY", "Barangay", 100)
        UiHelper.AddGridColumn(grid, "Account", "LOGIN", "Account", 100)
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
        FitColumns(grid, {"Business", "Status", "Owner", "Account", "Barangay"})
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
        LoadBusinesses(selected?.BusinessId)
    End Sub

    Public Sub LoadBusinesses(selectId As Integer?)
        businesses = BusinessService.GetBusinesses()
        owners = businesses.Select(Function(b) New With {b.BusinessId, .Owner = BusinessService.GetOwnerAccount(b.BusinessId)}).
                            Where(Function(x) x.Owner IsNot Nothing).ToDictionary(Function(x) x.BusinessId, Function(x) x.Owner)
        BindGrid(selectId)
    End Sub

    Private Function OwnerOf(b As Business) As User
        Dim u As User = Nothing
        If b IsNot Nothing Then owners.TryGetValue(b.BusinessId, u)
        Return u
    End Function

    Private Sub BindGrid(selectId As Integer?)
        Dim term = txtSearch.Text.Trim().ToLowerInvariant()
        Dim rows = businesses.Where(Function(b) term = "" OrElse
                                        (b.BusinessName & " " & b.OwnerName & " " & b.Barangay & " " & b.LineOfBusiness & " " &
                                         b.BusinessType & " " & BusinessService.GetStatus(b) & " " & OwnerOf(b)?.Username).
                                        ToLowerInvariant().Contains(term)).
                              Select(Function(b) New BusinessRow With {
                                  .BusinessId = b.BusinessId, .Business = b.BusinessName, .Owner = b.OwnerName, .Barangay = b.Barangay,
                                  .Account = If(OwnerOf(b)?.Username, "None"), .Status = BusinessService.GetStatus(b)
                              }).ToList()
        loading = True
        grid.DataSource = rows
        MeasureColumns(grid, "Status")
        FitGrid()
        Dim index = If(selectId.HasValue, rows.FindIndex(Function(r) r.BusinessId = selectId.Value), -1)
        If index < 0 AndAlso rows.Count > 0 Then index = 0
        SelectRow(grid, index)
        loading = False

        lblEmpty.Visible = rows.Count = 0
        If rows.Count = 0 Then
            lblEmpty.Text = If(term <> "", "No businesses match your search." & vbCrLf & "Try a name, owner, barangay or line of business.",
                               "No businesses registered yet." & vbCrLf & "Click ""Add Business"" to register the first one.")
            lblEmpty.BringToFront()
        End If
        ShowSelected()
    End Sub

    Private Sub ShowSelected()
        Dim row = If(grid.SelectedRows.Count > 0, TryCast(grid.SelectedRows(0).DataBoundItem, BusinessRow), Nothing)
        selected = If(row Is Nothing, Nothing, businesses.FirstOrDefault(Function(b) b.BusinessId = row.BusinessId))
        ShowDetails()
        UpdateButtons()
    End Sub

    Private Sub UpdateButtons()
        Dim has = selected IsNot Nothing
        btnEdit.Enabled = has
        Toolbar.SetTip(btnEdit, If(has, "Change the details of " & selected?.BusinessName, "Select a business first"))

        Dim ownerBlocker = BusinessService.GetOwnerAccountBlocker(selected)
        btnOwner.Enabled = ownerBlocker Is Nothing
        Toolbar.SetTip(btnOwner, If(ownerBlocker, "Create the sign-in account for the owner of " & selected?.BusinessName))

        Dim reactivate = has AndAlso Not selected.IsActive
        Toolbar.SetActionText(btnActive, If(reactivate, "Reactivate", "Deactivate"), If(reactivate, "Reactivate", "Deactivate"))
        UiHelper.StyleDangerButton(btnActive)
        If reactivate Then UiHelper.StylePrimaryButton(btnActive)
        btnActive.Enabled = has
        Toolbar.SetTip(btnActive, If(Not has, "Select a business first",
                                     If(reactivate, "Make " & selected.BusinessName & " active again (its owner can sign in again)",
                                        "Close " & selected?.BusinessName & " (soft delete: its records are kept)")))
    End Sub

    Private Sub ShowDetails()
        Dim has = selected IsNot Nothing
        lblDetailEmpty.Visible = Not has
        detailContent.Visible = has
        If Not has Then
            lblDetailEmpty.Text = If(businesses.Count = 0, "Nothing to show yet.", "Select a business to see its details.")
            Return
        End If

        Dim b = selected
        header.SetText(b.BusinessName, BusinessService.GetStatus(b), b.LineOfBusiness & " · " & b.Barangay)
        Dim rows As New List(Of Control) From {
            InfoRow(Tips, "Owner", b.OwnerName, "Type", b.BusinessType),
            InfoRow(Tips, "Address", b.Address, "Barangay", b.Barangay),
            InfoRow(Tips, "Line of business", b.LineOfBusiness, "Food business", If(b.IsFoodBusiness, "Yes", "No")),
            InfoRow(Tips, "DTI / SEC no.", Dash(b.DtiSecNo), "TIN", Dash(b.Tin)),
            InfoRow(Tips, "Contact no.", Dash(b.ContactNo), "E-mail", Dash(b.Email)),
            InfoRow(Tips, "Registered", UiHelper.FormatDate(b.CreatedAt), "Status", BusinessService.GetStatus(b)),
            HeadingRow("Owner account")
        }
        Dim owner = OwnerOf(b)
        If owner IsNot Nothing Then
            Dim id = owner.UserId
            rows.Add(ListRow(Tips, owner.Username, owner.FullName & " · last sign-in " &
                             If(owner.LastLogin.HasValue, UiHelper.FormatDate(owner.LastLogin), "never"),
                             UserService.GetStatus(owner), {New RowLink("Open in Users", Theme.SidebarBlue, Sub() RaiseEvent OpenUserRequested(id))}))
        ElseIf b.IsActive Then
            rows.Add(ListRow(Tips, "No owner account yet", "The owner cannot sign in until one is created.", "",
                             {New RowLink("Create", Theme.SidebarBlue, Sub() Owner_Click(Nothing, EventArgs.Empty))}))
        Else
            rows.Add(ListRow(Tips, "No owner account", "Reactivate the business to create one."))
        End If
        If Not b.IsActive Then
            rows.Add(NoteRow(Tips, "Deactivated: hidden from the module screens and its owner cannot sign in. Its records are kept.", Theme.StatusRed))
        End If
        detailsList.SetRows(rows)
    End Sub

    Private Shared Function Dash(text As String) As String
        Return If(String.IsNullOrWhiteSpace(text), "—", text)
    End Function

    ' =====================================================================
    '  Actions
    ' =====================================================================

    Private Sub AddBusiness_Click(sender As Object, e As EventArgs)
        Using dlg As New BusinessDialog()
            If dlg.ShowDialog(OwnerWindow) <> DialogResult.OK Then Return
            Dim b = dlg.SavedBusiness
            txtSearch.Text = ""
            LoadBusinesses(b.BusinessId)
            OnDataChanged()
            If UiHelper.Confirm(b.BusinessName & " was added to the registry." & vbCrLf & vbCrLf &
                                "Next: create the owner's sign-in account so " & b.OwnerName & " can view the business and file applications.",
                                "Business registered", "Create Owner Account", "Later") Then
                Owner_Click(Nothing, EventArgs.Empty)
            End If
        End Using
    End Sub

    Private Sub Edit_Click(sender As Object, e As EventArgs)
        If selected Is Nothing Then Return
        Using dlg As New BusinessDialog(BusinessRepository.GetById(selected.BusinessId))
            If dlg.ShowDialog(OwnerWindow) <> DialogResult.OK Then Return
            LoadBusinesses(dlg.SavedBusiness.BusinessId)
            OnDataChanged()
            UiHelper.ShowSuccess(dlg.SavedBusiness.BusinessName & " was updated." & vbCrLf &
                                 "Next: the new details appear on documents printed from now on.", "Business saved")
        End Using
    End Sub

    Private Sub Owner_Click(sender As Object, e As EventArgs)
        Dim blocker = BusinessService.GetOwnerAccountBlocker(selected)
        If blocker IsNot Nothing Then
            UiHelper.ShowWarning(blocker, "Cannot create an owner account")
            Return
        End If
        Using dlg As New UserDialog(selected)
            If dlg.ShowDialog(OwnerWindow) <> DialogResult.OK Then Return
            LoadBusinesses(selected.BusinessId)
            OnDataChanged()
            AdminUsersPage.ShowCreated(dlg.SavedUser, dlg.Password)
        End Using
    End Sub

    Private Sub Active_Click(sender As Object, e As EventArgs)
        If selected Is Nothing Then Return
        Dim b = selected
        Dim reactivate = Not b.IsActive
        Dim owner = OwnerOf(b)
        If Not reactivate AndAlso Not UiHelper.Confirm(
                b.BusinessName & " will be hidden from the module screens" &
                If(owner IsNot Nothing, " and its owner login (" & owner.Username & ") will be deactivated", "") & "." & vbCrLf &
                "All its permits, payments and records are kept, and you can reactivate it later.",
                "Deactivate " & b.BusinessName & "?", "Deactivate", "Cancel", danger:=True) Then Return
        Dim problem = BusinessService.SetActive(b.BusinessId, reactivate)
        If problem IsNot Nothing Then
            UiHelper.ShowWarning(problem, If(reactivate, "Cannot reactivate", "Cannot deactivate"))
            Return
        End If
        LoadBusinesses(b.BusinessId)
        OnDataChanged()
        If reactivate Then
            UiHelper.ShowSuccess(b.BusinessName & " is active again" & If(owner IsNot Nothing, " and " & owner.Username & " can sign in", "") & "." & vbCrLf &
                                 "Next: it appears again in every module's business list.", "Business reactivated")
        Else
            UiHelper.ShowSuccess(b.BusinessName & " was deactivated" & If(owner IsNot Nothing, " and its owner can no longer sign in", "") & "." & vbCrLf &
                                 "Next: click ""Reactivate"" any time to restore it.", "Business deactivated")
        End If
    End Sub

End Class
