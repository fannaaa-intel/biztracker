Imports System.Text.RegularExpressions

''' <summary>
''' Business registry (Admin only). Screens call this class; it checks the role itself.
''' Rules:
'''   - name, owner, address, barangay, type and line of business are required; business names are unique
'''   - optional TIN / contact number / e-mail must look valid
'''   - a business is never deleted: deactivating it is a soft delete (is_active = 0) that also
'''     deactivates its Owner login; reactivating restores both
'''   - one Owner account per business ("Create Owner Account")
''' Every change is written to audit_log.
''' </summary>
Public NotInheritable Class BusinessService

    Private Sub New()
    End Sub

    Public Const Active As String = "Active"
    Public Const Inactive As String = "Inactive"

    Public Shared ReadOnly BusinessTypes As String() = {"Sole Proprietorship", "Partnership", "Corporation", "Cooperative"}

    Private Const TableName As String = "businesses"
    Private Const NotAllowed As String = "Only an Admin can manage the business registry."

    Public Shared Function CanManage() As Boolean
        Return Session.IsAdmin
    End Function

    ''' <summary>Every business, active and inactive (empty for non-admins).</summary>
    Public Shared Function GetBusinesses() As List(Of Business)
        If Not CanManage() Then Return New List(Of Business)
        Return BusinessRepository.GetAll(includeInactive:=True)
    End Function

    Public Shared Function GetStatus(b As Business) As String
        Return If(b IsNot Nothing AndAlso b.IsActive, Active, Inactive)
    End Function

    ''' <summary>The business's Owner account (Nothing if it has none yet).</summary>
    Public Shared Function GetOwnerAccount(businessId As Integer) As User
        Return UserRepository.GetByBusinessId(businessId).FirstOrDefault(Function(u) u.Role = Roles.Owner)
    End Function

    ' ==================== Validation ====================

    ''' <summary>
    ''' Checks a business. Keys: "name", "owner", "address", "barangay", "type", "line",
    ''' "dti", "tin", "contact", "email".
    ''' </summary>
    Public Shared Function ValidateBusiness(b As Business) As Dictionary(Of String, String)
        Dim errors As New Dictionary(Of String, String)
        Required(errors, "name", b.BusinessName, 150, "Enter the business name.")
        Required(errors, "owner", b.OwnerName, 150, "Enter the owner's name.")
        Required(errors, "address", b.Address, 255, "Enter the address.")
        Required(errors, "barangay", b.Barangay, 100, "Enter the barangay.")
        Required(errors, "line", b.LineOfBusiness, 150, "Enter the line of business.")
        If Not BusinessTypes.Contains(b.BusinessType) Then errors("type") = "Choose the type of business."

        If Not errors.ContainsKey("name") Then
            Dim name = b.BusinessName.Trim()
            Dim duplicate = BusinessRepository.GetAll(includeInactive:=True).
                            FirstOrDefault(Function(x) x.BusinessId <> b.BusinessId AndAlso
                                                       String.Equals(x.BusinessName.Trim(), name, StringComparison.OrdinalIgnoreCase))
            If duplicate IsNot Nothing Then
                errors("name") = "A business with this name is already registered" & If(duplicate.IsActive, ".", " (deactivated).")
            End If
        End If

        Dim dti = If(b.DtiSecNo, "").Trim()
        If dti.Length > 50 Then errors("dti") = "At most 50 characters."
        Dim tin = If(b.Tin, "").Trim()
        If tin <> "" AndAlso Not Regex.IsMatch(tin, "^\d{3}-\d{3}-\d{3}(-\d{3,5})?$") Then
            errors("tin") = "Use 123-456-789-000."
        End If
        Dim contact = If(b.ContactNo, "").Trim()
        If contact <> "" AndAlso Not Regex.IsMatch(contact, "^\+?[0-9][0-9 -]{6,18}$") Then
            errors("contact") = "Enter a valid number."
        End If
        Dim email = If(b.Email, "").Trim()
        If email.Length > 150 Then
            errors("email") = "At most 150 characters."
        ElseIf email <> "" AndAlso Not Regex.IsMatch(email, "^[^@\s]+@[^@\s]+\.[^@\s]+$") Then
            errors("email") = "Enter a valid e-mail, e.g. name@email.com."
        End If
        Return errors
    End Function

    Private Shared Sub Required(errors As Dictionary(Of String, String), key As String, value As String, maxLength As Integer, message As String)
        If String.IsNullOrWhiteSpace(value) Then
            errors(key) = message
        ElseIf value.Trim().Length > maxLength Then
            errors(key) = "At most " & maxLength & " characters."
        End If
    End Sub

    ' ==================== Actions ====================

    ''' <summary>Registers a business (b.BusinessId is set). Returns an error message, or Nothing when saved.</summary>
    Public Shared Function AddBusiness(b As Business) As String
        If Not CanManage() Then Return NotAllowed
        Dim errors = ValidateBusiness(b)
        If errors.Count > 0 Then Return errors.Values.First()
        b.IsActive = True
        Dim id = BusinessRepository.Insert(b)
        If id <= 0 Then Return "The business could not be saved. Please try again."
        b.BusinessId = id
        AuditService.LogInsert(TableName, id, "Registered business '" & b.BusinessName.Trim() & "' (" & b.Barangay.Trim() & ")")
        Return Nothing
    End Function

    ''' <summary>Saves the business details (not the active flag). Returns an error message, or Nothing when saved.</summary>
    Public Shared Function UpdateBusiness(b As Business) As String
        If Not CanManage() Then Return NotAllowed
        Dim existing = BusinessRepository.GetById(b.BusinessId)
        If existing Is Nothing Then Return "This business no longer exists."
        Dim errors = ValidateBusiness(b)
        If errors.Count > 0 Then Return errors.Values.First()
        b.IsActive = existing.IsActive
        If Not BusinessRepository.Update(b) Then Return "The business could not be saved. Please try again."
        AuditService.LogUpdate(TableName, b.BusinessId, "Updated business '" & b.BusinessName.Trim() & "'" &
                               If(existing.BusinessName <> b.BusinessName.Trim(), " (was '" & existing.BusinessName & "')", ""))
        Return Nothing
    End Function

    ''' <summary>
    ''' Deactivates (soft delete) or reactivates a business together with its Owner login.
    ''' Returns an error message, or Nothing when done.
    ''' </summary>
    Public Shared Function SetActive(businessId As Integer, isActive As Boolean) As String
        If Not CanManage() Then Return NotAllowed
        Dim b = BusinessRepository.GetById(businessId)
        If b Is Nothing Then Return "This business no longer exists."
        If b.IsActive = isActive Then Return Nothing
        Dim owner = GetOwnerAccount(businessId)
        If Not BusinessRepository.SetActiveWithOwners(businessId, isActive) Then Return "The business could not be updated. Please try again."
        AuditService.LogStatusChange(TableName, businessId, GetStatus(b), If(isActive, Active, Inactive), b.BusinessName)
        If owner IsNot Nothing AndAlso owner.IsActive <> isActive Then
            AuditService.LogStatusChange("users", owner.UserId, UserService.GetStatus(owner), If(isActive, Active, Inactive),
                                         owner.Username & ", with business")
        End If
        Return Nothing
    End Function

    ''' <summary>Why an Owner account cannot be created for the business (Nothing = allowed).</summary>
    Public Shared Function GetOwnerAccountBlocker(b As Business) As String
        If b Is Nothing Then Return "Select a business first."
        If Not CanManage() Then Return NotAllowed
        If Not b.IsActive Then Return b.BusinessName & " is deactivated. Reactivate it first."
        Dim owner = GetOwnerAccount(b.BusinessId)
        If owner IsNot Nothing Then
            Return b.BusinessName & " already has an owner account (" & owner.Username & "). Each business has one."
        End If
        Return Nothing
    End Function

    ''' <summary>A new Owner account prefilled for the business (name = registered owner, suggested username).</summary>
    Public Shared Function NewOwnerAccount(b As Business) As User
        Return New User With {
            .Role = Roles.Owner, .BusinessId = b.BusinessId, .BusinessName = b.BusinessName,
            .FullName = b.OwnerName, .Username = UserService.SuggestUsername(b.BusinessName)
        }
    End Function

End Class
