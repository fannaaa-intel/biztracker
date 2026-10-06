''' <summary>
''' Who is logged in right now. Set by AuthService after a successful login,
''' cleared on logout. Every screen reads the role and business_id from here.
''' </summary>
Public NotInheritable Class Session

    Private Sub New()
    End Sub

    Public Shared Property CurrentUser As User

    Public Shared ReadOnly Property IsLoggedIn As Boolean
        Get
            Return CurrentUser IsNot Nothing
        End Get
    End Property

    ''' <summary>Nothing when no one is logged in (e.g. failed login attempts in the audit log).</summary>
    Public Shared ReadOnly Property UserId As Integer?
        Get
            If CurrentUser Is Nothing Then Return Nothing
            Return CurrentUser.UserId
        End Get
    End Property

    Public Shared ReadOnly Property FullName As String
        Get
            Return If(CurrentUser?.FullName, "")
        End Get
    End Property

    Public Shared ReadOnly Property Role As String
        Get
            Return If(CurrentUser?.Role, "")
        End Get
    End Property

    ''' <summary>Set only for Owner accounts - every Owner query must filter by this.</summary>
    Public Shared ReadOnly Property BusinessId As Integer?
        Get
            Return CurrentUser?.BusinessId
        End Get
    End Property

    Public Shared ReadOnly Property IsAdmin As Boolean
        Get
            Return Role = Roles.Admin
        End Get
    End Property

    Public Shared ReadOnly Property IsOwner As Boolean
        Get
            Return Role = Roles.Owner
        End Get
    End Property

    ''' <summary>Any LGU staff role (everyone except Owner).</summary>
    Public Shared ReadOnly Property IsStaff As Boolean
        Get
            Return IsLoggedIn AndAlso Not IsOwner
        End Get
    End Property

    Public Shared Sub Start(user As User)
        CurrentUser = user
    End Sub

    Public Shared Sub Clear()
        CurrentUser = Nothing
    End Sub

End Class
