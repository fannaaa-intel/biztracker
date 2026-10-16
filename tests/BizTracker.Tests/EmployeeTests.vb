Imports Xunit

''' <summary>Employees still working? (Revision 4B): length of service and separation dates.</summary>
Public Class EmployeeTests

    Private Shared Function Emp(hired As Date?, Optional separated As Date? = Nothing) As Employee
        Return New Employee With {.FullName = "Paolo Lim", .IsActive = Not separated.HasValue,
                                  .DateHired = hired, .DateSeparated = separated}
    End Function

    <Theory>
    <InlineData("2023-06-15", "2026-10-16", "3 yrs 4 mos")>
    <InlineData("2025-10-16", "2026-10-16", "1 yr")>
    <InlineData("2026-09-17", "2026-10-16", "less than a month")>
    <InlineData("2026-08-16", "2026-10-16", "2 mos")>
    <InlineData("2026-08-17", "2026-10-16", "1 mo")>       ' one day short of 2 months
    Public Sub LengthOfService_WholeMonths(hired As String, today As String, expected As String)
        Assert.Equal(expected, HealthCertificateService.GetLengthOfService(Emp(Date.Parse(hired)), Date.Parse(today)))
    End Sub

    <Fact>
    Public Sub LengthOfService_StopsAtTheSeparationDate()
        Dim e = Emp(New Date(2024, 1, 10), New Date(2025, 1, 10))
        Assert.Equal("1 yr", HealthCertificateService.GetLengthOfService(e, New Date(2026, 10, 16)))
    End Sub

    <Fact>
    Public Sub LengthOfService_UnknownHireDate_IsEmpty()
        Assert.Equal("", HealthCertificateService.GetLengthOfService(Emp(Nothing)))
    End Sub

    <Fact>
    Public Sub Separation_NotInTheFuture_NorBeforeHire()
        Dim e = Emp(Date.Today.AddYears(-1))
        Assert.Null(HealthCertificateService.ValidateSeparation(e, Date.Today))
        Assert.NotNull(HealthCertificateService.ValidateSeparation(e, Date.Today.AddDays(1)))
        Assert.NotNull(HealthCertificateService.ValidateSeparation(e, Date.Today.AddYears(-2)))
        Assert.Null(HealthCertificateService.ValidateSeparation(Emp(Nothing), Date.Today.AddYears(-2)))   ' hire date unknown
    End Sub

    <Fact>
    Public Sub EmploymentStatus_FollowsIsActive()
        Assert.Equal("Active", HealthCertificateService.GetEmploymentStatus(Emp(Nothing)))
        Assert.Equal("Separated", HealthCertificateService.GetEmploymentStatus(Emp(Nothing, Date.Today)))
    End Sub

End Class
