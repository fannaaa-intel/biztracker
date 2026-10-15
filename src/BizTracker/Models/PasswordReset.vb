''' <summary>
''' One row of the password_resets table: a 6-digit code e-mailed for Forgot Password.
''' Only the BCrypt hash of the code is stored.
''' </summary>
Public Class PasswordReset
    Public Property ResetId As Integer
    Public Property UserId As Integer
    Public Property CodeHash As String = ""
    Public Property ExpiresAt As Date
    Public Property Attempts As Integer             ' wrong guesses so far
    Public Property CreatedAt As Date
    Public Property UsedAt As Date?                 ' set when used or invalidated

    ''' <summary>Filled by the repository from NOW() on the database server (not the PC clock).</summary>
    Public Property IsExpired As Boolean
End Class
