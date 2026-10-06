''' <summary>Staff health-card numbers used by the "health-card sync" card of the sanitary permit.</summary>
Public Class HealthCardSync
    Public Property Total As Integer
    Public Property Valid As Integer
    Public Property ExpiringSoon As Integer
    ''' <summary>Food handlers whose certificate is Expired or missing (they block issuance).</summary>
    Public Property FoodHandlersNotCovered As New List(Of String)
    ''' <summary>Non-food staff whose certificate is Expired or missing (warning only).</summary>
    Public Property OthersNotCovered As New List(Of String)
End Class

''' <summary>
''' Sanitary permit workflow and rules (Municipal Health Office). Screens call this class.
''' Status flow: Submitted -> Lab Analysis -> For Inspection -> Issued (or Rejected).
'''   - Submitted -> Lab Analysis      : all prerequisite documents uploaded
'''   - Lab Analysis -> For Inspection : all prerequisite documents verified
'''   - For Inspection -> Issued       : inspection score >= sp_passing_score and every food handler
'''                                      has a valid health certificate
''' A permit is valid until December 31 of the year it is issued. Issuing it endorses the
''' Sanitary office on the business permit application. Every change is written to audit_log.
''' </summary>
Public NotInheritable Class SanitaryPermitService

    Private Sub New()
    End Sub

    Public Const Submitted As String = "Submitted"
    Public Const LabAnalysis As String = "Lab Analysis"
    Public Const ForInspection As String = "For Inspection"
    Public Const Issued As String = "Issued"
    Public Const Rejected As String = "Rejected"

    Public Shared ReadOnly StatusFlow As String() = {Submitted, LabAnalysis, ForInspection, Issued}
    Public Shared ReadOnly Categories As String() = {"Food", "Non-Food"}

    ''' <summary>The 4 steps of the workflow tracker.</summary>
    Public Shared ReadOnly TrackerSteps As String() = {"Intake", "Lab Analysis", "On-site Inspection", "Permit Issued"}

    ''' <summary>Prerequisite documents created with every application.</summary>
    Public Shared ReadOnly Prerequisites As String() =
        {"Microbiological Water Test", "Physico-Chemical Analysis", "Pest Control Certificate"}

    Private Const TableName As String = "sanitary_permits"

    ' ==================== Reading ====================

    Public Shared Function IsFinal(status As String) As Boolean
        Return status = Issued OrElse status = Rejected
    End Function

    Public Shared Function GetNextStatus(status As String) As String
        Dim i = Array.IndexOf(StatusFlow, status)
        If i < 0 OrElse i >= StatusFlow.Length - 1 Then Return Nothing
        Return StatusFlow(i + 1)
    End Function

    ''' <summary>Tracker position: 0-2 = step in progress, 4 = all done (Issued), -1 = Rejected.</summary>
    Public Shared Function GetTrackerStep(status As String) As Integer
        Select Case status
            Case Submitted : Return 0
            Case LabAnalysis : Return 1
            Case ForInspection : Return 2
            Case Issued : Return 4
            Case Else : Return -1
        End Select
    End Function

    ''' <summary>Minimum inspection score to issue (settings: sp_passing_score, default 75).</summary>
    Public Shared ReadOnly Property PassingScore As Integer
        Get
            Return SettingsRepository.GetInt("sp_passing_score", 75)
        End Get
    End Property

    ''' <summary>Applications of a business, newest first (empty if the user may not see it).</summary>
    Public Shared Function GetPermits(businessId As Integer) As List(Of SanitaryPermit)
        If Not AccessService.CanSeeBusiness(businessId) Then Return New List(Of SanitaryPermit)
        Return SanitaryRepository.GetByBusinessId(businessId)
    End Function

    ''' <summary>The newest Issued permit (the one in force or last in force), or Nothing.</summary>
    Public Shared Function GetCurrentPermit(permits As IEnumerable(Of SanitaryPermit)) As SanitaryPermit
        Return permits.Where(Function(p) p.Status = Issued).
               OrderByDescending(Function(p) p.ValidUntil).ThenByDescending(Function(p) p.SanitaryId).FirstOrDefault()
    End Function

    ''' <summary>Valid / Expiring Soon / Expired of an issued permit (computed), else its workflow status.</summary>
    Public Shared Function GetDisplayStatus(p As SanitaryPermit) As String
        If p.Status = Issued Then Return StatusService.GetExpiryStatus(p.ValidUntil)
        Return p.Status
    End Function

    ''' <summary>Health-card status of the business's active employees (from Health Certificates).</summary>
    Public Shared Function GetHealthCardSync(businessId As Integer) As HealthCardSync
        Dim sync As New HealthCardSync
        For Each r In HealthCertificateService.GetRoster(businessId)
            sync.Total += 1
            Select Case r.Status
                Case StatusService.Valid : sync.Valid += 1
                Case StatusService.ExpiringSoon : sync.Valid += 1 : sync.ExpiringSoon += 1
                Case Else
                    If r.Employee.Category = "Food Handler" Then
                        sync.FoodHandlersNotCovered.Add(r.Employee.FullName)
                    Else
                        sync.OthersNotCovered.Add(r.Employee.FullName)
                    End If
            End Select
        Next
        Return sync
    End Function

    ' ==================== Filing / editing ====================

    ''' <summary>
    ''' Why a new application cannot be filed for the business this year (Nothing = it can).
    ''' One application per business per year (Rejected ones do not count).
    ''' </summary>
    Public Shared Function GetFilingBlocker(businessId As Integer) As String
        Dim year = Date.Today.Year
        Dim existing = SanitaryRepository.GetByBusinessId(businessId).
                       FirstOrDefault(Function(p) p.PermitYear = year AndAlso p.Status <> Rejected)
        If existing Is Nothing Then Return Nothing
        Return If(existing.Status = Issued,
                  "The " & year & " sanitary permit " & existing.PermitNo & " is already issued (valid until Dec 31).",
                  "Application " & existing.PermitNo & " for " & year & " is still in progress.")
    End Function

    ''' <summary>Checks an application. Keys: "category", "filed".</summary>
    Public Shared Function Validate(p As SanitaryPermit) As Dictionary(Of String, String)
        Dim errors As New Dictionary(Of String, String)
        If Not Categories.Contains(p.Category) Then errors("category") = "Choose Food or Non-Food."
        If p.DateFiled.Date > Date.Today Then
            errors("filed") = "Date filed cannot be in the future."
        ElseIf p.DateFiled.Year <> p.PermitYear Then
            errors("filed") = "Date filed must be in " & p.PermitYear & "."
        End If
        Return errors
    End Function

    ''' <summary>
    ''' Files a sanitary permit application for this year: permit number SP-YYYY-xxxxx, status Submitted,
    ''' and the 3 prerequisite documents. Owners may file for their own business. Returns the new id or -1.
    ''' </summary>
    Public Shared Function FileApplication(p As SanitaryPermit) As Integer
        If Not AccessService.CanSeeBusiness(p.BusinessId) Then Return -1
        If Not Session.IsOwner AndAlso Not AccessService.CanManage(AppScreen.SanitaryPermits) Then Return -1
        p.PermitYear = Date.Today.Year
        If GetFilingBlocker(p.BusinessId) IsNot Nothing OrElse Validate(p).Count > 0 Then Return -1

        p.Status = Submitted
        p.PermitNo = ReferenceNoService.GetNext(ReferenceNoService.SanitaryPermit, p.PermitYear)
        p.InspectionDate = Nothing : p.InspectionScore = Nothing : p.InspectorName = "" : p.Findings = ""
        p.DateIssued = Nothing : p.ValidUntil = Nothing
        Dim newId = SanitaryRepository.Insert(p)
        If newId <= 0 Then Return -1
        For Each doc In Prerequisites
            RequirementRepository.Insert(New Requirement With {
                .BusinessId = p.BusinessId, .ModuleName = ModuleNames.SanitaryPermit,
                .RelatedId = newId, .DocumentName = doc, .Status = "Pending Upload"})
        Next
        p.SanitaryId = newId
        AuditService.LogInsert(TableName, newId, "Filed sanitary permit application " & p.PermitNo & " (" & p.Category & ")")
        Return newId
    End Function

    ''' <summary>Staff edit of category / date filed while the application is not final.</summary>
    Public Shared Function UpdateApplication(p As SanitaryPermit) As Boolean
        If Not AccessService.CanManage(AppScreen.SanitaryPermits) OrElse IsFinal(p.Status) Then Return False
        If Validate(p).Count > 0 Then Return False
        If Not SanitaryRepository.Update(p) Then Return False
        AuditService.LogUpdate(TableName, p.SanitaryId, "Edited sanitary permit application " & p.PermitNo)
        Return True
    End Function

    ' ==================== Inspection ====================

    ''' <summary>Checks the inspection form. Keys: "date", "score", "inspector", "findings".</summary>
    Public Shared Function ValidateInspection(p As SanitaryPermit, inspectionDate As Date, score As Integer,
                                              inspector As String, findings As String) As Dictionary(Of String, String)
        Dim errors As New Dictionary(Of String, String)
        If inspectionDate.Date > Date.Today Then
            errors("date") = "The inspection date cannot be in the future."
        ElseIf inspectionDate.Date < p.DateFiled.Date Then
            errors("date") = "The inspection cannot be before the date filed (" & p.DateFiled.ToString("MMM d, yyyy") & ")."
        End If
        If score < 0 OrElse score > 100 Then errors("score") = "The score must be from 0 to 100."
        If String.IsNullOrWhiteSpace(inspector) Then
            errors("inspector") = "Enter the sanitary inspector's name."
        ElseIf inspector.Trim().Length > 150 Then
            errors("inspector") = "The name can be at most 150 characters."
        End If
        If score < PassingScore AndAlso String.IsNullOrWhiteSpace(findings) Then
            errors("findings") = "Describe the deficiencies found (the score is below " & PassingScore & ")."
        End If
        Return errors
    End Function

    ''' <summary>Records the on-site inspection (Health / Admin, only while For Inspection). Returns an error or Nothing.</summary>
    Public Shared Function RecordInspection(p As SanitaryPermit, inspectionDate As Date, score As Integer,
                                            inspector As String, findings As String) As String
        If Not AccessService.CanManage(AppScreen.SanitaryPermits) Then Return "You are not allowed to record inspections."
        If p.Status <> ForInspection Then Return "Inspections are recorded when the application is For Inspection."
        Dim errors = ValidateInspection(p, inspectionDate, score, inspector, findings)
        If errors.Count > 0 Then Return errors.Values.First()

        p.InspectionDate = inspectionDate.Date
        p.InspectionScore = score
        p.InspectorName = inspector.Trim()
        p.Findings = If(findings, "").Trim()
        If Not SanitaryRepository.Update(p) Then Return "The inspection could not be saved."
        AuditService.LogUpdate(TableName, p.SanitaryId, "Inspection recorded for " & p.PermitNo & ": score " & score &
                               If(score >= PassingScore, " (passed)", " (failed)"))
        Return Nothing
    End Function

    ' ==================== Status workflow ====================

    ''' <summary>Why the application cannot move to the next status yet (Nothing = it can).</summary>
    Public Shared Function GetAdvanceBlocker(p As SanitaryPermit) As String
        Dim nextStatus = GetNextStatus(p.Status)
        If nextStatus Is Nothing Then Return "This application is already " & p.Status & "."
        Dim docs = RequirementRepository.GetByModule(p.BusinessId, ModuleNames.SanitaryPermit, p.SanitaryId)

        Select Case nextStatus
            Case LabAnalysis
                Dim missing = docs.Where(Function(r) r.Status = "Pending Upload" OrElse r.Status = "Rejected").ToList()
                If missing.Count > 0 Then
                    Return missing.Count & " of " & docs.Count & " prerequisite documents still need to be uploaded."
                End If
            Case ForInspection
                Dim notVerified = docs.Where(Function(r) r.Status <> "Verified").ToList()
                If notVerified.Count > 0 Then
                    Return notVerified.Count & " of " & docs.Count & " laboratory documents are not yet verified."
                End If
            Case Issued
                If Not p.InspectionScore.HasValue Then Return "Record the on-site inspection first."
                If p.InspectionScore.Value < PassingScore Then
                    Return "The inspection score " & p.InspectionScore.Value & " is below the passing score of " & PassingScore &
                           ". Record a re-inspection after the deficiencies are corrected."
                End If
                Dim sync = GetHealthCardSync(p.BusinessId)
                If sync.FoodHandlersNotCovered.Count > 0 Then
                    Return "Food handlers without a valid health certificate: " & String.Join(", ", sync.FoodHandlersNotCovered) & "."
                End If
        End Select
        Return Nothing
    End Function

    ''' <summary>
    ''' Moves the application to the next status. Issuing sets date issued = today, valid until Dec 31
    ''' of this year, and endorses the Sanitary office on the business permit. Returns an error or Nothing.
    ''' </summary>
    Public Shared Function Advance(p As SanitaryPermit) As String
        If Not AccessService.CanManage(AppScreen.SanitaryPermits) Then Return "You are not allowed to change the status."
        Dim blocker = GetAdvanceBlocker(p)
        If blocker IsNot Nothing Then Return blocker

        Dim oldStatus = p.Status
        Dim nextStatus = GetNextStatus(p.Status)
        If nextStatus = Issued Then
            p.DateIssued = Date.Today
            p.ValidUntil = StatusService.GetSanitaryPermitExpiry(Date.Today)
        End If
        p.Status = nextStatus
        If Not SanitaryRepository.Update(p) Then
            p.Status = oldStatus
            Return "The status could not be saved."
        End If
        AuditService.LogStatusChange(TableName, p.SanitaryId, oldStatus, nextStatus,
                                     If(nextStatus = Issued, "valid until " & p.ValidUntil.Value.ToString("yyyy-MM-dd"), ""))
        If nextStatus = Issued Then
            BusinessPermitService.AutoEndorse(p.BusinessId, EndorsementOffices.Sanitary, "Sanitary permit " & p.PermitNo & " issued")
        End If
        Return Nothing
    End Function

    ''' <summary>Rejects an application that is not final. The reason is kept in the findings.</summary>
    Public Shared Function Reject(p As SanitaryPermit, reason As String) As String
        If Not AccessService.CanManage(AppScreen.SanitaryPermits) Then Return "You are not allowed to change the status."
        If IsFinal(p.Status) Then Return "This application is already " & p.Status & "."
        If String.IsNullOrWhiteSpace(reason) Then Return "Please enter the reason for rejecting."
        Dim oldStatus = p.Status
        p.Status = Rejected
        p.Findings = ("Rejected: " & reason.Trim() & If(p.Findings <> "", vbCrLf & p.Findings, "")).Trim()
        If Not SanitaryRepository.Update(p) Then
            p.Status = oldStatus
            Return "The status could not be saved."
        End If
        AuditService.LogStatusChange(TableName, p.SanitaryId, oldStatus, Rejected, reason.Trim())
        Return Nothing
    End Function

    ''' <summary>The text shown in the green "ready" box of the status dialog.</summary>
    Public Shared Function GetReadyMessage(p As SanitaryPermit) As String
        Select Case GetNextStatus(p.Status)
            Case LabAnalysis : Return "All prerequisite documents are uploaded. Send the samples for laboratory analysis."
            Case ForInspection : Return "Laboratory results verified. Schedule the on-site sanitary inspection."
            Case Issued : Return "Inspection passed (" & p.InspectionScore & "/100). The permit will be valid until Dec 31, " & Date.Today.Year & "."
            Case Else : Return "Ready."
        End Select
    End Function

End Class
