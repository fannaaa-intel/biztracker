Imports Xunit

''' <summary>Construction schedule (Revision 4A): computed schedule status, duration and date validation.</summary>
Public Class ConstructionScheduleTests

    Private Shared ReadOnly Start As New Date(2026, 3, 1)
    Private Shared ReadOnly Finish As New Date(2026, 8, 31)

    <Theory>
    <InlineData("2026-02-28", "Not Started")>
    <InlineData("2026-03-01", "On Schedule")>      ' the start day itself
    <InlineData("2026-06-15", "On Schedule")>
    <InlineData("2026-08-31", "On Schedule")>      ' the completion day is still on schedule
    <InlineData("2026-09-01", "Behind Schedule")>
    Public Sub ActiveProject_ScheduleFollowsToday(today As String, expected As String)
        Assert.Equal(expected, StatusService.GetScheduleStatus(Start, Finish, "Active", Date.Parse(today)))
    End Sub

    <Fact>
    Public Sub OnHoldProject_PastCompletion_IsStillBehindSchedule()
        Assert.Equal(StatusService.BehindSchedule, StatusService.GetScheduleStatus(Start, Finish, "On Hold", New Date(2026, 10, 1)))
    End Sub

    <Fact>
    Public Sub CompletedProject_IsNeverBehindSchedule()
        Assert.Equal("Completed", StatusService.GetScheduleStatus(Start, Finish, "Completed", New Date(2027, 1, 1)))
    End Sub

    <Fact>
    Public Sub RejectedOrNoDates_IsNoSchedule()
        Assert.Equal(StatusService.NoSchedule, StatusService.GetScheduleStatus(Start, Finish, "Rejected", New Date(2026, 9, 1)))
        Assert.Equal(StatusService.NoSchedule, StatusService.GetScheduleStatus(Nothing, Nothing, "Active", New Date(2026, 9, 1)))
    End Sub

    <Fact>
    Public Sub Duration_CountsBothDays()
        Assert.Equal(1, StatusService.GetDurationDays(Start, Start))
        Assert.Equal(184, StatusService.GetDurationDays(Start, Finish))
        Assert.Equal("184 days (about 6 months)", ConstructionService.GetDurationText(Start, Finish))
        Assert.Equal("30 days", ConstructionService.GetDurationText(Start, Start.AddDays(29)))
        Assert.Equal("Not given", ConstructionService.GetDurationText(Start, Nothing))
    End Sub

    Private Shared Function Project(estStart As Date?, estEnd As Date?) As ConstructionProject
        Return New ConstructionProject With {.ProjectTitle = "Store Front Renovation", .ProjectType = "Renovation",
                                             .EstStartDate = estStart, .EstEndDate = estEnd}
    End Function

    <Fact>
    Public Sub Validate_DatesAreOptional()
        Assert.Empty(ConstructionService.ValidateProject(Project(Nothing, Nothing)))
        Assert.Empty(ConstructionService.ValidateProject(Project(Start, Finish)))
        Assert.Empty(ConstructionService.ValidateProject(Project(Start, Start)))    ' one-day job
    End Sub

    <Fact>
    Public Sub Validate_NeedsBothDates()
        Assert.True(ConstructionService.ValidateProject(Project(Start, Nothing)).ContainsKey("end"))
        Assert.True(ConstructionService.ValidateProject(Project(Nothing, Finish)).ContainsKey("start"))
    End Sub

    <Fact>
    Public Sub Validate_CompletionNotBeforeStart_AndAtMostTenYears()
        Assert.True(ConstructionService.ValidateProject(Project(Finish, Start)).ContainsKey("end"))
        Assert.True(ConstructionService.ValidateProject(Project(Start, Start.AddYears(10).AddDays(1))).ContainsKey("end"))
        Assert.Empty(ConstructionService.ValidateProject(Project(Start, Start.AddYears(10))))
    End Sub

End Class
