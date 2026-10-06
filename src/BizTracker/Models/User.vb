''' <summary>One row of the users table.</summary>
Public Class User
    Public Property UserId As Integer
    Public Property Username As String = ""
    Public Property PasswordHash As String = ""      ' BCrypt hash - never show in the UI
    Public Property FullName As String = ""
    Public Property Role As String = ""              ' see Roles constants
    Public Property BusinessId As Integer?           ' only set for Owner accounts
    Public Property IsActive As Boolean = True
    Public Property LastLogin As Date?
    Public Property CreatedAt As Date
End Class
