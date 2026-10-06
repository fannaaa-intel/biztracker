''' <summary>The screens reachable from the sidebar.</summary>
Public Enum AppScreen
    Dashboard
    BusinessPermits
    SanitaryPermits
    RealPropertyTax
    HealthCertificates
    AnnualInspections
    ConstructionPermits
    Settings
End Enum

''' <summary>
''' Role-based access (the "Roles and access" table in CLAUDE.md).
''' The sidebar hides every screen a role cannot open, and MainForm checks again before opening one.
''' </summary>
Public NotInheritable Class AccessService

    Private Sub New()
    End Sub

    Private Shared ReadOnly AccessMap As New Dictionary(Of String, AppScreen()) From {
        {Roles.Admin, CType([Enum].GetValues(GetType(AppScreen)), AppScreen())},
        {Roles.BPLO, {AppScreen.Dashboard, AppScreen.BusinessPermits}},
        {Roles.Health, {AppScreen.SanitaryPermits, AppScreen.HealthCertificates}},
        {Roles.Assessor, {AppScreen.RealPropertyTax}},
        {Roles.Building, {AppScreen.ConstructionPermits}},
        {Roles.Inspector, {AppScreen.AnnualInspections}},
        {Roles.Owner, {AppScreen.Dashboard, AppScreen.BusinessPermits, AppScreen.SanitaryPermits,
                       AppScreen.RealPropertyTax, AppScreen.HealthCertificates,
                       AppScreen.AnnualInspections, AppScreen.ConstructionPermits}}
    }

    Public Shared Function CanAccess(role As String, screen As AppScreen) As Boolean
        Dim screens As AppScreen() = Nothing
        Return role IsNot Nothing AndAlso AccessMap.TryGetValue(role, screens) AndAlso screens.Contains(screen)
    End Function

    ''' <summary>Checks the logged-in user.</summary>
    Public Shared Function CanAccess(screen As AppScreen) As Boolean
        Return CanAccess(Session.Role, screen)
    End Function

    ''' <summary>Screens the role can open, in sidebar order.</summary>
    Public Shared Function GetScreens(role As String) As List(Of AppScreen)
        Return [Enum].GetValues(GetType(AppScreen)).Cast(Of AppScreen)().
               Where(Function(s) CanAccess(role, s)).ToList()
    End Function

    ''' <summary>
    ''' True if the role may change data (status, endorsements, ...).
    ''' Owners are read-only except uploading requirements and filing applications.
    ''' </summary>
    Public Shared Function IsStaffRole(role As String) As Boolean
        Return role IsNot Nothing AndAlso role <> Roles.Owner
    End Function

End Class
