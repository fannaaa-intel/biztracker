''' <summary>One unfinished application in the Admin "Pending Applications" queue (display only, not a table).</summary>
Public Class PendingApplication
    Public Property ModuleName As String = ""            ' ModuleNames.* (Business Permit, Sanitary Permit, ...)
    Public Property Screen As AppScreen                  ' where the record is handled
    Public Property RecordId As Integer
    Public Property BusinessId As Integer
    Public Property BusinessName As String = ""
    Public Property ReferenceNo As String = ""
    Public Property ApplicationType As String = ""       ' New / Renewal / Food / Re-inspection / Renovation ...
    Public Property Status As String = ""                ' the module's own status / stage
    Public Property DateFiled As Date
    ''' <summary>Simple / Complex / Highly Technical (RA 11032 classification).</summary>
    Public Property Classification As String = ""
    Public Property LimitDays As Integer
    Public Property DaysWaiting As Integer
    ''' <summary>True when the next step is the applicant's (e.g. paying the assessed tax): the LGU clock is stopped.</summary>
    Public Property WaitingOnApplicant As Boolean
    ''' <summary>On Time / Due Soon / Overdue / With Applicant.</summary>
    Public Property Timeliness As String = ""
End Class

''' <summary>
''' Admin "Pending Applications" queue (Revision 4C): every application that is not finished yet, from all modules,
''' oldest first, so the Admin knows who to serve first.
'''   Days Waiting = working days (Monday - Friday) after the filing day up to today. Holidays are not excluded.
'''   Processing limits (RA 11032 Ease of Doing Business, settings table):
'''     arta_simple_days (3)     - Business Permit Renewal
'''     arta_complex_days (7)    - Business Permit New, Sanitary Permit, Annual Inspection
'''     arta_technical_days (20) - Construction Permit
'''   Timeliness: Overdue (red) when Days Waiting is over the limit, Due Soon (amber) within 1 day of it,
'''   otherwise On Time (green). "With Applicant" (gray) when the next step is the applicant's.
''' Everything is computed from today, nothing is stored. Admin only.
''' </summary>
Public NotInheritable Class ApplicationQueueService

    Private Sub New()
    End Sub

    Public Const Simple As String = "Simple"
    Public Const Complex As String = "Complex"
    Public Const HighlyTechnical As String = "Highly Technical"

    Public Const OnTime As String = "On Time"
    Public Const WithApplicant As String = "With Applicant"

    ' ==================== Rules ====================

    ''' <summary>Working days (Mon - Fri) after fromDate up to and including toDate. Same day or earlier = 0.</summary>
    Public Shared Function CountWorkingDays(fromDate As Date, toDate As Date) As Integer
        Dim count = 0
        Dim d = fromDate.Date.AddDays(1)
        While d <= toDate.Date
            If d.DayOfWeek <> DayOfWeek.Saturday AndAlso d.DayOfWeek <> DayOfWeek.Sunday Then count += 1
            d = d.AddDays(1)
        End While
        Return count
    End Function

    ''' <summary>Processing limit in working days for a classification (settings, RA 11032 defaults 3 / 7 / 20).</summary>
    Public Shared Function GetLimitDays(classification As String) As Integer
        Select Case classification
            Case Simple : Return SettingsRepository.GetInt("arta_simple_days", 3)
            Case HighlyTechnical : Return SettingsRepository.GetInt("arta_technical_days", 20)
            Case Else : Return SettingsRepository.GetInt("arta_complex_days", 7)
        End Select
    End Function

    ''' <summary>On Time / Due Soon / Overdue / With Applicant.</summary>
    Public Shared Function GetTimeliness(daysWaiting As Integer, limitDays As Integer, waitingOnApplicant As Boolean) As String
        If waitingOnApplicant Then Return WithApplicant
        If daysWaiting > limitDays Then Return StatusService.Overdue
        If daysWaiting >= limitDays - 1 Then Return StatusService.DueSoon
        Return OnTime
    End Function

    ''' <summary>RA 11032 classification of a business permit application: Renewal = Simple, New = Complex.</summary>
    Public Shared Function ClassifyBusinessPermit(applicationType As String) As String
        Return If(applicationType = "Renewal", Simple, Complex)
    End Function

    ' ==================== The queue ====================

    ''' <summary>Every unfinished application of the active businesses, oldest first (empty for non-Admins).</summary>
    Public Shared Function GetPendingApplications(Optional today As Date? = Nothing) As List(Of PendingApplication)
        Dim list As New List(Of PendingApplication)
        If Not Session.IsAdmin Then Return list
        Dim onDate = If(today, Date.Today).Date
        ' (Each GetAll() already leaves out deactivated businesses.)

        ' Business permits: not Issued / Rejected. "Assessed" waits for the applicant's payment.
        For Each p In BusinessPermitRepository.GetAll()
            If p.Status = "Issued" OrElse p.Status = "Rejected" Then Continue For
            list.Add(Make(ModuleNames.BusinessPermit, AppScreen.BusinessPermits, p.PermitId, p.BusinessId, p.BusinessName,
                          p.ReferenceNo, p.ApplicationType, p.Status, p.DateFiled, ClassifyBusinessPermit(p.ApplicationType),
                          p.Status = "Assessed", onDate))
        Next

        ' Sanitary permits: not Issued / Rejected
        For Each s In SanitaryRepository.GetAll()
            If s.Status = "Issued" OrElse s.Status = "Rejected" Then Continue For
            list.Add(Make(ModuleNames.SanitaryPermit, AppScreen.SanitaryPermits, s.SanitaryId, s.BusinessId, s.BusinessName,
                          s.PermitNo, s.Category, s.Status, s.DateFiled, Complex, False, onDate))
        Next

        ' Annual inspections: Scheduled / In Progress / For Re-inspection (filed = the day it was created)
        For Each i In InspectionRepository.GetAll()
            If i.Status <> InspectionService.Scheduled AndAlso i.Status <> InspectionService.InProgress AndAlso
               i.Status <> InspectionService.ForReinspection Then Continue For
            list.Add(Make(ModuleNames.AnnualInspection, AppScreen.AnnualInspections, i.InspectionId, i.BusinessId, i.BusinessName,
                          i.ReferenceNo, If(i.Status = InspectionService.ForReinspection, "Re-inspection", "Annual"),
                          i.Status, i.CreatedAt, Complex, False, onDate))
        Next

        ' Construction: Active projects waiting for a clearance decision (Locational, Building Permit, Occupancy stages).
        ' In the Construction stage the owner is building, so nothing is pending at the LGU.
        For Each c In ConstructionRepository.GetAll()
            If c.Status <> ConstructionService.Active Then Continue For
            If c.CurrentStage = ConstructionService.StageConstruction OrElse c.CurrentStage = ConstructionService.StageCompleted Then Continue For
            list.Add(Make(ModuleNames.ConstructionPermit, AppScreen.ConstructionPermits, c.ProjectId, c.BusinessId, c.BusinessName,
                          c.ReferenceNo, c.ProjectType, c.CurrentStage & " stage", c.DateFiled, HighlyTechnical, False, onDate))
        Next

        Return list.OrderBy(Function(a) a.DateFiled).ThenByDescending(Function(a) a.DaysWaiting - a.LimitDays).
                    ThenBy(Function(a) a.ReferenceNo).ToList()
    End Function

    Private Shared Function Make(moduleName As String, screen As AppScreen, recordId As Integer, businessId As Integer,
                                 businessName As String, referenceNo As String, applicationType As String, status As String,
                                 dateFiled As Date, classification As String, waitingOnApplicant As Boolean,
                                 today As Date) As PendingApplication
        Dim days = CountWorkingDays(dateFiled, today)
        Dim limit = GetLimitDays(classification)
        Return New PendingApplication With {
            .ModuleName = moduleName, .Screen = screen, .RecordId = recordId, .BusinessId = businessId,
            .BusinessName = businessName, .ReferenceNo = referenceNo, .ApplicationType = applicationType,
            .Status = status, .DateFiled = dateFiled.Date, .Classification = classification,
            .LimitDays = limit, .DaysWaiting = days, .WaitingOnApplicant = waitingOnApplicant,
            .Timeliness = GetTimeliness(days, limit, waitingOnApplicant)}
    End Function

End Class
