''' <summary>Outcome of InspectionService.RecordResult.</summary>
Public Class InspectionResultOutcome
    ''' <summary>Nothing when the result was saved.</summary>
    Public Property ErrorMessage As String
    ''' <summary>The inspection status after the result (In Progress / For Re-inspection / Completed).</summary>
    Public Property NewStatus As String = ""
    ''' <summary>The certificate number when this result completed the inspection (else empty).</summary>
    Public Property CertificateNo As String = ""
    ''' <summary>Reference no. of the business permit whose Fire endorsement was set to Endorsed (or Nothing).</summary>
    Public Property EndorsedReference As String
End Class

''' <summary>
''' Annual joint inspection rules (Structural, Electrical, Mechanical, Fire). Screens call this class.
''' Status: Scheduled -> In Progress -> (For Re-inspection) -> Completed, or Cancelled.
'''   - One active inspection per business per year; the schedule date cannot be in the past.
'''   - Each department records Passed / Failed with the inspector's name (findings required when Failed).
'''   - Any department Failed (and none Pending) -> For Re-inspection, overall Failed.
'''   - All 4 Passed -> Completed, overall Passed, certificate AIC-YYYY-xxxxx.
'''   - Fire Passed -> the Fire endorsement on the business permit application is set to Endorsed.
''' Inspector and Admin make changes; an Owner sees only their own business (read-only). Every change is audited.
''' </summary>
Public NotInheritable Class InspectionService

    Private Sub New()
    End Sub

    Public Const Scheduled As String = "Scheduled"
    Public Const InProgress As String = "In Progress"
    Public Const ForReinspection As String = "For Re-inspection"
    Public Const Completed As String = "Completed"
    Public Const Cancelled As String = "Cancelled"

    Public Const Pending As String = "Pending"
    Public Const Passed As String = "Passed"
    Public Const Failed As String = "Failed"

    Public Const FireDepartment As String = "Fire"

    ''' <summary>The 4 steps of the progress tracker.</summary>
    Public Shared ReadOnly TrackerSteps As String() = {"Scheduled", "Inspection", "Re-inspection", "Certified"}

    Private Const TableName As String = "inspections"
    Private Const ItemsTable As String = "inspection_items"

    ' ==================== Reading ====================

    Public Shared Function IsFinal(status As String) As Boolean
        Return status = Completed OrElse status = Cancelled
    End Function

    ''' <summary>Tracker position: 0 = scheduled, 1 = inspecting, 2 = re-inspection, 4 = certified, -1 = cancelled.</summary>
    Public Shared Function GetTrackerStep(status As String) As Integer
        Select Case status
            Case Scheduled : Return 0
            Case InProgress : Return 1
            Case ForReinspection : Return 2
            Case Completed : Return 4
            Case Else : Return -1
        End Select
    End Function

    ''' <summary>Inspections of a business, newest first (empty if the user may not see it).</summary>
    Public Shared Function GetInspections(businessId As Integer) As List(Of Inspection)
        If Not AccessService.CanSeeBusiness(businessId) Then Return New List(Of Inspection)
        Return InspectionRepository.GetByBusinessId(businessId)
    End Function

    ''' <summary>The 4 department items of an inspection (empty if the user may not see it).</summary>
    Public Shared Function GetItems(i As Inspection) As List(Of InspectionItem)
        If i Is Nothing OrElse Not AccessService.CanSeeBusiness(i.BusinessId) Then Return New List(Of InspectionItem)
        Return InspectionRepository.GetItems(i.InspectionId)
    End Function

    ''' <summary>The inspection that matters now: the newest one that is not cancelled.</summary>
    Public Shared Function GetCurrentInspection(inspections As IEnumerable(Of Inspection)) As Inspection
        Return inspections.Where(Function(x) x.Status <> Cancelled).
               OrderByDescending(Function(x) x.InspectionYear).ThenByDescending(Function(x) x.ScheduleDate).FirstOrDefault()
    End Function

    Public Shared Function GetPassedCount(items As IEnumerable(Of InspectionItem)) As Integer
        Return items.Count(Function(x) x.Result = Passed)
    End Function

    ''' <summary>"3 of 4 Depts Passed"</summary>
    Public Shared Function GetEndorsementText(items As IEnumerable(Of InspectionItem)) As String
        Return GetPassedCount(items) & " of " & InspectionDepartments.All.Length & " Depts Passed"
    End Function

    ''' <summary>The next date the inspectors visit: the re-inspection date while For Re-inspection, else the schedule.</summary>
    Public Shared Function GetNextVisit(i As Inspection) As Date?
        If i Is Nothing OrElse IsFinal(i.Status) Then Return Nothing
        If i.Status = ForReinspection Then Return i.ReinspectionDate
        Return i.ScheduleDate
    End Function

    ''' <summary>Why the certificate is not available yet (Nothing = it is issued).</summary>
    Public Shared Function GetCertificateBlocker(i As Inspection, items As IEnumerable(Of InspectionItem)) As String
        If i Is Nothing Then Return "No inspection yet."
        If i.Status = Completed AndAlso i.CertificateNo <> "" Then Return Nothing
        If i.Status = Cancelled Then Return "This inspection was cancelled."
        Return "Locked - all 4 departments must pass (" & GetPassedCount(items) & " of 4 passed)."
    End Function

    ' ==================== Scheduling ====================

    ''' <summary>Why a new inspection cannot be scheduled for the business in that year (Nothing = it can).</summary>
    Public Shared Function GetScheduleBlocker(businessId As Integer, year As Integer) As String
        Dim existing = InspectionRepository.GetByBusinessId(businessId).
                       FirstOrDefault(Function(x) x.InspectionYear = year AndAlso x.Status <> Cancelled)
        If existing Is Nothing Then Return Nothing
        If existing.Status = Completed Then
            Return "The " & year & " inspection " & existing.ReferenceNo & " is already completed (" & existing.CertificateNo & ")."
        End If
        Return "Inspection " & existing.ReferenceNo & " for " & year & " is still open (" & existing.Status & ")."
    End Function

    ''' <summary>Checks a schedule date/time. Key: "date". allowToday = False requires a date after today.</summary>
    Public Shared Function ValidateSchedule(visitAt As Date, Optional allowToday As Boolean = True) As Dictionary(Of String, String)
        Dim errors As New Dictionary(Of String, String)
        If allowToday AndAlso visitAt.Date < Date.Today Then
            errors("date") = "The date cannot be in the past."
        ElseIf Not allowToday AndAlso visitAt.Date <= Date.Today Then
            errors("date") = "Choose a date after today."
        ElseIf visitAt.Date > Date.Today.AddYears(1) Then
            errors("date") = "Choose a date within the next 12 months."
        ElseIf visitAt.Hour < 7 OrElse visitAt.Hour > 17 Then
            errors("date") = "Inspections are held between 7:00 AM and 5:00 PM."
        End If
        Return errors
    End Function

    ''' <summary>
    ''' Schedules the annual inspection (Inspector / Admin): reference INS-YYYY-xxxxx and the 4 Pending
    ''' department items. Returns an error or Nothing (i.InspectionId is set).
    ''' </summary>
    Public Shared Function ScheduleInspection(i As Inspection) As String
        If Not AccessService.CanManage(AppScreen.AnnualInspections) Then Return "You are not allowed to schedule inspections."
        If Not AccessService.CanSeeBusiness(i.BusinessId) Then Return "Business not found."
        Dim errors = ValidateSchedule(i.ScheduleDate)
        If errors.Count > 0 Then Return errors.Values.First()
        i.InspectionYear = i.ScheduleDate.Year
        Dim blocker = GetScheduleBlocker(i.BusinessId, i.InspectionYear)
        If blocker IsNot Nothing Then Return blocker

        i.ReferenceNo = ReferenceNoService.GetNext(ReferenceNoService.Inspection, i.InspectionYear)
        i.Status = Scheduled
        i.OverallResult = Pending
        i.ReinspectionDate = Nothing
        i.CertificateNo = ""
        Dim newId = InspectionRepository.Insert(i)
        If newId <= 0 Then Return "The inspection could not be saved."
        i.InspectionId = newId
        AuditService.LogInsert(TableName, newId, "Scheduled inspection " & i.ReferenceNo & " on " & i.ScheduleDate.ToString("yyyy-MM-dd HH:mm"))
        Return Nothing
    End Function

    ''' <summary>Moves a Scheduled inspection to another date/time (same year). Returns an error or Nothing.</summary>
    Public Shared Function Reschedule(i As Inspection, visitAt As Date) As String
        If Not AccessService.CanManage(AppScreen.AnnualInspections) Then Return "You are not allowed to change inspections."
        If i.Status <> Scheduled Then Return "Only a Scheduled inspection can be rescheduled."
        Dim errors = ValidateSchedule(visitAt)
        If errors.Count > 0 Then Return errors.Values.First()
        If visitAt.Year <> i.InspectionYear Then Return "Keep the inspection within " & i.InspectionYear & "."
        Dim old = i.ScheduleDate
        i.ScheduleDate = visitAt
        If Not InspectionRepository.Update(i) Then
            i.ScheduleDate = old
            Return "The inspection could not be saved."
        End If
        AuditService.LogUpdate(TableName, i.InspectionId, "Rescheduled " & i.ReferenceNo & ": " & old.ToString("yyyy-MM-dd HH:mm") &
                               " -> " & visitAt.ToString("yyyy-MM-dd HH:mm"))
        Return Nothing
    End Function

    ''' <summary>Sets the re-inspection date of an inspection that is For Re-inspection (a date after today).</summary>
    Public Shared Function ScheduleReinspection(i As Inspection, visitAt As Date) As String
        If Not AccessService.CanManage(AppScreen.AnnualInspections) Then Return "You are not allowed to change inspections."
        If i.Status <> ForReinspection Then Return "A re-inspection is scheduled only after a department fails."
        Dim errors = ValidateSchedule(visitAt, allowToday:=False)
        If errors.Count > 0 Then Return errors.Values.First()
        Dim old = i.ReinspectionDate
        i.ReinspectionDate = visitAt
        If Not InspectionRepository.Update(i) Then
            i.ReinspectionDate = old
            Return "The inspection could not be saved."
        End If
        AuditService.LogUpdate(TableName, i.InspectionId, "Re-inspection of " & i.ReferenceNo & " set to " & visitAt.ToString("yyyy-MM-dd HH:mm"))
        Return Nothing
    End Function

    ''' <summary>Cancels an inspection that is not Completed (reason required; kept in the audit log).</summary>
    Public Shared Function Cancel(i As Inspection, reason As String) As String
        If Not AccessService.CanManage(AppScreen.AnnualInspections) Then Return "You are not allowed to change inspections."
        If IsFinal(i.Status) Then Return "This inspection is already " & i.Status & "."
        If String.IsNullOrWhiteSpace(reason) Then Return "Please enter the reason for cancelling."
        Dim old = i.Status
        i.Status = Cancelled
        If Not InspectionRepository.Update(i) Then
            i.Status = old
            Return "The inspection could not be saved."
        End If
        AuditService.LogStatusChange(TableName, i.InspectionId, old, Cancelled, reason.Trim())
        Return Nothing
    End Function

    ' ==================== Department results ====================

    ''' <summary>Why results cannot be recorded for this inspection now (Nothing = they can).</summary>
    Public Shared Function GetRecordBlocker(i As Inspection) As String
        If i Is Nothing Then Return "Select an inspection first."
        If i.Status = Completed Then Return "This inspection is completed - all 4 departments passed."
        If i.Status = Cancelled Then Return "This inspection was cancelled."
        If Date.Today < i.ScheduleDate.Date Then
            Return "Results can be recorded from the inspection date (" & i.ScheduleDate.ToString("MMM d, yyyy") & ")."
        End If
        Return Nothing
    End Function

    ''' <summary>Departments that can still be recorded (Pending or Failed - Passed is final).</summary>
    Public Shared Function GetRecordableItems(items As IEnumerable(Of InspectionItem)) As List(Of InspectionItem)
        Return items.Where(Function(x) x.Result <> Passed).ToList()
    End Function

    ''' <summary>Checks a department result. Keys: "result", "inspector", "findings".</summary>
    Public Shared Function ValidateResult(result As String, findings As String, inspector As String) As Dictionary(Of String, String)
        Dim errors As New Dictionary(Of String, String)
        If result <> Passed AndAlso result <> Failed Then errors("result") = "Choose Passed or Failed."
        If String.IsNullOrWhiteSpace(inspector) Then
            errors("inspector") = "Enter the inspector's name."
        ElseIf inspector.Trim().Length > 150 Then
            errors("inspector") = "The name can be at most 150 characters."
        End If
        If result = Failed AndAlso String.IsNullOrWhiteSpace(findings) Then
            errors("findings") = "Describe the deficiency found (required when Failed)."
        ElseIf If(findings, "").Trim().Length > 1000 Then
            errors("findings") = "Findings can be at most 1,000 characters."
        End If
        Return errors
    End Function

    ''' <summary>
    ''' Records one department's result (Inspector / Admin), then updates the inspection:
    ''' any Pending -> In Progress; all done with a Failed -> For Re-inspection (overall Failed);
    ''' all 4 Passed -> Completed (overall Passed) with certificate AIC-YYYY-xxxxx.
    ''' A department that failed before keeps its earlier findings under the new ones.
    ''' When Fire passes, the Fire endorsement on the business permit application is set to Endorsed.
    ''' </summary>
    Public Shared Function RecordResult(i As Inspection, department As String, result As String,
                                        findings As String, inspector As String) As InspectionResultOutcome
        Dim outcome As New InspectionResultOutcome
        If Not AccessService.CanManage(AppScreen.AnnualInspections) Then
            outcome.ErrorMessage = "You are not allowed to record inspection results."
            Return outcome
        End If
        outcome.ErrorMessage = GetRecordBlocker(i)
        If outcome.ErrorMessage IsNot Nothing Then Return outcome
        Dim items = InspectionRepository.GetItems(i.InspectionId)
        Dim item = items.FirstOrDefault(Function(x) x.Department = department)
        If item Is Nothing Then
            outcome.ErrorMessage = "Choose a department."
            Return outcome
        End If
        If item.Result = Passed Then
            outcome.ErrorMessage = "The " & department & " inspection already passed."
            Return outcome
        End If
        Dim errors = ValidateResult(result, findings, inspector)
        If errors.Count > 0 Then
            outcome.ErrorMessage = errors.Values.First()
            Return outcome
        End If

        ' Keep the earlier failed findings as history under the new text
        Dim newFindings = If(findings, "").Trim()
        If item.Result = Failed AndAlso item.Findings <> "" Then
            newFindings = (If(newFindings = "", "Re-inspected: " & result & ".", newFindings) & vbCrLf &
                           "Earlier (" & If(item.InspectedAt.HasValue, item.InspectedAt.Value.ToString("MMM d, yyyy"), "before") & "): " & item.Findings).Trim()
            If newFindings.Length > 2000 Then newFindings = newFindings.Substring(0, 2000)
        End If
        Dim wasFailed = item.Result = Failed
        item.Result = result
        item.Findings = newFindings
        item.InspectorName = inspector.Trim()
        item.InspectedAt = Date.Now
        If Not InspectionRepository.UpdateItem(item) Then
            outcome.ErrorMessage = "The result could not be saved."
            Return outcome
        End If
        AuditService.LogUpdate(ItemsTable, item.ItemId, department & " inspection " & result & If(wasFailed, " (re-inspection)", "") &
                               " - " & i.ReferenceNo & If(result = Failed, ": " & findings.Trim(), ""))

        ' Recompute the inspection status
        Dim oldStatus = i.Status
        If items.Any(Function(x) x.Result = Pending) Then
            i.Status = InProgress
            i.OverallResult = Pending
        ElseIf items.Any(Function(x) x.Result = Failed) Then
            i.Status = ForReinspection
            i.OverallResult = Failed
        Else
            i.Status = Completed
            i.OverallResult = Passed
            i.CertificateNo = ReferenceNoService.GetNext(ReferenceNoService.InspectionCertificate, i.InspectionYear)
            outcome.CertificateNo = i.CertificateNo
        End If
        If Not InspectionRepository.Update(i) Then
            outcome.ErrorMessage = "The inspection status could not be saved."
            Return outcome
        End If
        If i.Status <> oldStatus Then
            AuditService.LogStatusChange(TableName, i.InspectionId, oldStatus, i.Status,
                                         If(i.Status = Completed, "certificate " & i.CertificateNo, GetEndorsementText(items)))
        End If
        outcome.NewStatus = i.Status

        If department = FireDepartment AndAlso result = Passed Then
            outcome.EndorsedReference = BusinessPermitService.AutoEndorse(i.BusinessId, EndorsementOffices.Fire,
                                                                          "Fire safety inspection passed (" & i.ReferenceNo & ")")
        End If
        Return outcome
    End Function

End Class
