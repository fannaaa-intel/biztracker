Imports System.Text.RegularExpressions

''' <summary>
''' Small input checks shared by several services.
''' E-mail addresses are always stored and looked up trimmed and in lower case,
''' so "  Juan@Gmail.com " and "juan@gmail.com" are the same address.
''' </summary>
Public NotInheritable Class Validation

    Private Sub New()
    End Sub

    Public Const MaxEmailLength As Integer = 150     ' users.email / businesses.email are VARCHAR(150)

    ''' <summary>Trimmed, lower-case e-mail, or Nothing when empty (stored as NULL).</summary>
    Public Shared Function NormalizeEmail(email As String) As String
        If String.IsNullOrWhiteSpace(email) Then Return Nothing
        Return email.Trim().ToLowerInvariant()
    End Function

    ''' <summary>Simple shape check: something@something.something, no spaces.</summary>
    Public Shared Function IsValidEmail(email As String) As Boolean
        Return email IsNot Nothing AndAlso Regex.IsMatch(email.Trim(), "^[^@\s]+@[^@\s]+\.[^@\s]+$")
    End Function

End Class
