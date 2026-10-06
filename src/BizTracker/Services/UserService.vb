Imports System.Security.Cryptography
Imports System.Text.RegularExpressions

''' <summary>
''' User management (Admin only). Screens call this class; it checks the role itself.
''' Rules:
'''   - usernames are unique (3-50 letters, digits, dot, dash or underscore) and cannot be changed later
'''   - Owner accounts must be linked to an active business (one owner account per business);
'''     staff accounts are never linked to a business
'''   - passwords are stored as BCrypt hashes; a reset creates a temporary password shown once
'''   - you cannot deactivate yourself or change your own role, and at least one active Admin must remain
'''   - users are never deleted (deactivated instead) so the audit log stays valid
''' Every change is written to audit_log (passwords never are).
''' </summary>
Public NotInheritable Class UserService

    Private Sub New()
    End Sub

    Public Const Active As String = "Active"
    Public Const Inactive As String = "Inactive"
    Public Const MinPasswordLength As Integer = 8

    Private Const TableName As String = "users"
    Private Const NotAllowed As String = "Only an Admin can manage user accounts."

    ''' <summary>True if the logged-in user may manage accounts (Admin).</summary>
    Public Shared Function CanManage() As Boolean
        Return Session.IsAdmin
    End Function

    ''' <summary>All accounts (empty for non-admins), sorted by role then username.</summary>
    Public Shared Function GetUsers() As List(Of User)
        If Not CanManage() Then Return New List(Of User)
        Return UserRepository.GetAll()
    End Function

    Public Shared Function GetStatus(u As User) As String
        Return If(u IsNot Nothing AndAlso u.IsActive, Active, Inactive)
    End Function

    ''' <summary>What a role can open (the "Roles and access" table in CLAUDE.md).</summary>
    Public Shared Function DescribeRole(role As String) As String
        Select Case role
            Case Roles.Admin : Return "Everything, plus users, businesses, settings, audit log and backup."
            Case Roles.BPLO : Return "Dashboard, Business Permits and Permit Vault for every business."
            Case Roles.Health : Return "Sanitary Permits and Health Certificates."
            Case Roles.Assessor : Return "Real Property Tax."
            Case Roles.Building : Return "Construction Permits."
            Case Roles.Inspector : Return "Annual Inspections."
            Case Roles.Owner : Return "Read-only view of their own business; can upload requirements and file applications."
            Case Else : Return ""
        End Select
    End Function

    Public Shared Function IsSelf(u As User) As Boolean
        Return u IsNot Nothing AndAlso Session.UserId.HasValue AndAlso u.UserId = Session.UserId.Value
    End Function

    ' ==================== Validation ====================

    ''' <summary>
    ''' Checks an account. Keys: "username", "name", "role", "business", "password".
    ''' password is checked only for new accounts (pass Nothing when editing).
    ''' </summary>
    Public Shared Function ValidateUser(u As User, isNew As Boolean, Optional password As String = Nothing) As Dictionary(Of String, String)
        Dim errors As New Dictionary(Of String, String)
        Dim username = If(u.Username, "").Trim()
        If isNew Then
            If username = "" Then
                errors("username") = "Enter a username."
            ElseIf username.Length < 3 OrElse username.Length > 50 Then
                errors("username") = "Use 3 to 50 characters."
            ElseIf Not Regex.IsMatch(username, "^[A-Za-z0-9._-]+$") Then
                errors("username") = "Use letters, numbers, dot, dash or underscore only (no spaces)."
            ElseIf UserRepository.GetByUsername(username) IsNot Nothing Then
                errors("username") = "This username is already taken."
            End If
            Dim pwError = ValidatePassword(password)
            If pwError IsNot Nothing Then errors("password") = pwError
        End If

        If String.IsNullOrWhiteSpace(u.FullName) Then
            errors("name") = "Enter the person's full name."
        ElseIf u.FullName.Trim().Length > 150 Then
            errors("name") = "The name can be at most 150 characters."
        End If

        If Not Roles.All.Contains(u.Role) Then
            errors("role") = "Choose a role."
        ElseIf u.Role = Roles.Owner Then
            Dim businessError = GetOwnerBusinessError(u)
            If businessError IsNot Nothing Then errors("business") = businessError
        ElseIf u.BusinessId.HasValue Then
            errors("business") = "Only Owner accounts are linked to a business."
        End If

        If Not isNew AndAlso Not errors.ContainsKey("role") Then
            Dim roleError = GetRoleChangeBlocker(u)
            If roleError IsNot Nothing Then errors("role") = roleError
        End If
        Return errors
    End Function

    ''' <summary>Nothing when the password is acceptable.</summary>
    Public Shared Function ValidatePassword(password As String) As String
        password = If(password, "")
        If password.Trim() = "" Then Return "Enter a password."
        If password.Length < MinPasswordLength Then Return "Use at least " & MinPasswordLength & " characters."
        If password.Length > 72 Then Return "Use at most 72 characters."
        If Not password.Any(AddressOf Char.IsLetter) OrElse Not password.Any(AddressOf Char.IsDigit) Then
            Return "Use letters and numbers."
        End If
        Return Nothing
    End Function

    ''' <summary>An Owner must be linked to an active business that has no other owner account.</summary>
    Private Shared Function GetOwnerBusinessError(u As User) As String
        If Not u.BusinessId.HasValue Then Return "Choose the business this owner manages."
        Dim b = BusinessRepository.GetById(u.BusinessId.Value)
        If b Is Nothing Then Return "Choose the business this owner manages."
        If Not b.IsActive Then Return b.BusinessName & " is deactivated."
        Dim other = UserRepository.GetByBusinessId(b.BusinessId).
                    FirstOrDefault(Function(x) x.Role = Roles.Owner AndAlso x.UserId <> u.UserId)
        If other IsNot Nothing Then Return "Already has an owner account (" & other.Username & ")."
        Return Nothing
    End Function

    ''' <summary>Why the role of an existing account cannot change to u.Role (Nothing = allowed).</summary>
    Private Shared Function GetRoleChangeBlocker(u As User) As String
        Dim existing = UserRepository.GetById(u.UserId)
        If existing Is Nothing OrElse existing.Role = u.Role Then Return Nothing
        If IsSelf(existing) Then Return "You cannot change your own role."
        If existing.Role = Roles.Admin AndAlso existing.IsActive AndAlso UserRepository.CountActiveAdmins() <= 1 Then
            Return "This is the only active Admin. Make another Admin first."
        End If
        Return Nothing
    End Function

    ' ==================== Actions ====================

    ''' <summary>Creates an account (u.UserId is set). Returns an error message, or Nothing when saved.</summary>
    Public Shared Function AddUser(u As User, password As String) As String
        If Not CanManage() Then Return NotAllowed
        Dim errors = ValidateUser(u, True, password)
        If errors.Count > 0 Then Return errors.Values.First()
        u.Username = u.Username.Trim()
        u.FullName = u.FullName.Trim()
        u.PasswordHash = AuthService.HashPassword(password)
        u.IsActive = True
        If u.Role <> Roles.Owner Then u.BusinessId = Nothing
        Dim id = UserRepository.Insert(u)
        If id <= 0 Then Return "The account could not be saved. Please try again."
        u.UserId = id
        Dim business = If(u.BusinessId.HasValue, " for " & BusinessRepository.GetById(u.BusinessId.Value)?.BusinessName, "")
        AuditService.LogInsert(TableName, id, "Created " & u.Role & " account '" & u.Username & "' (" & u.FullName & ")" & business)
        Return Nothing
    End Function

    ''' <summary>Saves the name, role and linked business. Returns an error message, or Nothing when saved.</summary>
    Public Shared Function UpdateUser(u As User) As String
        If Not CanManage() Then Return NotAllowed
        Dim existing = UserRepository.GetById(u.UserId)
        If existing Is Nothing Then Return "This account no longer exists."
        Dim errors = ValidateUser(u, False)
        If errors.Count > 0 Then Return errors.Values.First()
        If u.Role <> Roles.Owner Then u.BusinessId = Nothing
        u.IsActive = existing.IsActive          ' activation has its own action
        If Not UserRepository.Update(u) Then Return "The account could not be saved. Please try again."

        Dim changes As New List(Of String)
        If existing.FullName <> u.FullName.Trim() Then changes.Add("name '" & existing.FullName & "' -> '" & u.FullName.Trim() & "'")
        If existing.Role <> u.Role Then changes.Add("role " & existing.Role & " -> " & u.Role)
        If Not Nullable.Equals(existing.BusinessId, u.BusinessId) Then
            changes.Add("business " & If(existing.BusinessId?.ToString(), "none") & " -> " & If(u.BusinessId?.ToString(), "none"))
        End If
        AuditService.LogUpdate(TableName, u.UserId, "Updated '" & existing.Username & "': " &
                               If(changes.Count = 0, "no changes", String.Join(", ", changes)))
        Return Nothing
    End Function

    ''' <summary>Result of ResetPassword: the temporary password to show ONCE.</summary>
    Public Class PasswordResetResult
        Public Property ErrorMessage As String
        Public Property TemporaryPassword As String = ""
    End Class

    ''' <summary>Gives an account a new random temporary password (BCrypt-hashed in the database).</summary>
    Public Shared Function ResetPassword(userId As Integer) As PasswordResetResult
        If Not CanManage() Then Return New PasswordResetResult With {.ErrorMessage = NotAllowed}
        Dim u = UserRepository.GetById(userId)
        If u Is Nothing Then Return New PasswordResetResult With {.ErrorMessage = "This account no longer exists."}
        Dim password = GenerateTemporaryPassword()
        If Not UserRepository.UpdatePassword(userId, AuthService.HashPassword(password)) Then
            Return New PasswordResetResult With {.ErrorMessage = "The password could not be reset. Please try again."}
        End If
        AuditService.LogUpdate(TableName, userId, "Password reset for '" & u.Username & "'")
        Return New PasswordResetResult With {.TemporaryPassword = password}
    End Function

    ''' <summary>Why an account cannot be activated/deactivated right now (Nothing = allowed).</summary>
    Public Shared Function GetActivationBlocker(u As User) As String
        If u Is Nothing Then Return "Select an account first."
        If Not CanManage() Then Return NotAllowed
        If u.IsActive Then
            If IsSelf(u) Then Return "You cannot deactivate your own account."
            If u.Role = Roles.Admin AndAlso UserRepository.CountActiveAdmins() <= 1 Then
                Return "This is the only active Admin, so it cannot be deactivated."
            End If
        ElseIf u.Role = Roles.Owner AndAlso u.BusinessId.HasValue Then
            Dim b = BusinessRepository.GetById(u.BusinessId.Value)
            If b IsNot Nothing AndAlso Not b.IsActive Then
                Return b.BusinessName & " is deactivated. Reactivate the business first (Businesses tab)."
            End If
        End If
        Return Nothing
    End Function

    ''' <summary>Activates or deactivates an account. Returns an error message, or Nothing when done.</summary>
    Public Shared Function SetActive(userId As Integer, isActive As Boolean) As String
        Dim u = UserRepository.GetById(userId)
        If u Is Nothing Then Return "This account no longer exists."
        If u.IsActive = isActive Then Return Nothing
        Dim blocker = GetActivationBlocker(u)
        If blocker IsNot Nothing Then Return blocker
        If Not UserRepository.SetActive(userId, isActive) Then Return "The account could not be updated. Please try again."
        AuditService.LogStatusChange(TableName, userId, GetStatus(u), If(isActive, Active, Inactive), u.Username)
        Return Nothing
    End Function

    ' ==================== Helpers ====================

    ''' <summary>
    ''' A random, easy-to-read temporary password such as "Kfmt-4827"
    ''' (no look-alike characters such as l/1 or O/0).
    ''' </summary>
    Public Shared Function GenerateTemporaryPassword() As String
        Const letters As String = "abcdefghjkmnpqrstuvwxyz"
        Const digits As String = "23456789"
        Dim chars As New Text.StringBuilder()
        For i = 1 To 4
            Dim c = letters(RandomNumberGenerator.GetInt32(letters.Length))
            chars.Append(If(i = 1, Char.ToUpperInvariant(c), c))
        Next
        chars.Append("-"c)
        For i = 1 To 4
            chars.Append(digits(RandomNumberGenerator.GetInt32(digits.Length)))
        Next
        Return chars.ToString()
    End Function

    ''' <summary>A free username based on some text, e.g. "Cristan's Bakeshop" -> "cristans" (or "cristans2").</summary>
    Public Shared Function SuggestUsername(text As String) As String
        Dim firstWord = If(text, "").Split({" "c}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
        Dim baseName = New String(If(firstWord, "").ToLowerInvariant().Where(Function(c) c >= "a"c AndAlso c <= "z"c OrElse Char.IsDigit(c)).ToArray())
        If baseName.Length < 3 Then baseName &= "owner"
        If baseName.Length > 40 Then baseName = baseName.Substring(0, 40)
        Dim candidate = baseName
        Dim n = 2
        While UserRepository.GetByUsername(candidate) IsNot Nothing
            candidate = baseName & n
            n += 1
        End While
        Return candidate
    End Function

End Class
