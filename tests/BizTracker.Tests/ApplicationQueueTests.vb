Imports Xunit

''' <summary>Admin Pending Applications queue (Revision 4C): working days and RA 11032 timeliness.</summary>
Public Class ApplicationQueueTests

    Public Sub New()
        TestSettings.UseSeededRates()
    End Sub

    ' 2026-10-16 is a Friday
    <Theory>
    <InlineData("2026-10-16", "2026-10-16", 0)>   ' filed today
    <InlineData("2026-10-15", "2026-10-16", 1)>   ' Thu -> Fri
    <InlineData("2026-10-16", "2026-10-19", 1)>   ' Fri -> Mon: the weekend does not count
    <InlineData("2026-10-17", "2026-10-19", 1)>   ' filed on a Saturday -> Monday
    <InlineData("2026-10-09", "2026-10-16", 5)>   ' one calendar week = 5 working days
    <InlineData("2026-10-16", "2026-10-09", 0)>   ' future filing date
    Public Sub WorkingDays_SkipWeekends(filed As String, today As String, expected As Integer)
        Assert.Equal(expected, ApplicationQueueService.CountWorkingDays(Date.Parse(filed), Date.Parse(today)))
    End Sub

    <Theory>
    <InlineData(0, 3, "On Time")>
    <InlineData(1, 3, "On Time")>
    <InlineData(2, 3, "Due Soon")>      ' within 1 day of the limit
    <InlineData(3, 3, "Due Soon")>      ' on the last allowed day
    <InlineData(4, 3, "Overdue")>
    Public Sub Timeliness_AgainstTheLimit(days As Integer, limit As Integer, expected As String)
        Assert.Equal(expected, ApplicationQueueService.GetTimeliness(days, limit, False))
    End Sub

    <Fact>
    Public Sub WaitingOnApplicant_IsNeverOverdue()
        Assert.Equal(ApplicationQueueService.WithApplicant, ApplicationQueueService.GetTimeliness(30, 3, True))
    End Sub

    <Fact>
    Public Sub Classification_RenewalIsSimple_NewIsComplex()
        Assert.Equal(ApplicationQueueService.Simple, ApplicationQueueService.ClassifyBusinessPermit("Renewal"))
        Assert.Equal(ApplicationQueueService.Complex, ApplicationQueueService.ClassifyBusinessPermit("New"))
    End Sub

    <Fact>
    Public Sub Limits_ComeFromSettings_WithRa11032Defaults()
        Assert.Equal(3, ApplicationQueueService.GetLimitDays(ApplicationQueueService.Simple))
        Assert.Equal(7, ApplicationQueueService.GetLimitDays(ApplicationQueueService.Complex))
        Assert.Equal(20, ApplicationQueueService.GetLimitDays(ApplicationQueueService.HighlyTechnical))
    End Sub

End Class
