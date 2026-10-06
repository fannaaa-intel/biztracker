''' <summary>Outcome of ConstructionService.ApproveClearance.</summary>
Public Class ClearanceOutcome
    ''' <summary>Nothing when the clearance was approved.</summary>
    Public Property ErrorMessage As String
    Public Property ClearanceNo As String = ""
    ''' <summary>Reference no. of the business permit whose Zoning endorsement was set to Endorsed (or Nothing).</summary>
    Public Property EndorsedReference As String
End Class

''' <summary>
''' Construction permit rules (OBO / MPDO). Screens call this class.
''' Stages: Locational -> Building Permit -> Construction -> Occupancy -> Completed (one step at a time).
'''   - Locational -> Building Permit : Locational / Zoning clearance Approved
'''   - Building Permit -> Construction : Building clearance Approved (which needs FSEC Approved and
'''                                       every technical document Verified)
'''   - Construction -> Occupancy       : construction finished (no clearance)
'''   - Occupancy -> Completed          : Occupancy clearance Approved
''' A clearance can be decided only when its stage is reached. Approving the Locational clearance
''' endorses Zoning on the business permit application. Status: Active / On Hold / Completed / Rejected.
''' Building and Admin make changes; an Owner may file a project and upload documents for their own business.
''' Every change is written to audit_log.
''' </summary>
Public NotInheritable Class ConstructionService

    Private Sub New()
    End Sub

    ' Stages
    Public Const StageLocational As String = "Locational"
    Public Const StageBuilding As String = "Building Permit"
    Public Const StageConstruction As String = "Construction"
    Public Const StageOccupancy As String = "Occupancy"
    Public Const StageCompleted As String = "Completed"
    Public Shared ReadOnly Stages As String() = {StageLocational, StageBuilding, StageConstruction, StageOccupancy, StageCompleted}

    ' Project statuses
    Public Const Active As String = "Active"
    Public Const OnHold As String = "On Hold"
    Public Const Completed As String = "Completed"
    Public Const Rejected As String = "Rejected"

    ' Clearance statuses
    Public Const Pending As String = "Pending"
    Public Const Approved As String = "Approved"

    Public Shared ReadOnly ProjectTypes As String() =
        {"New Construction", "Renovation", "Extension", "Renovation & Extension", "Demolition"}

    ''' <summary>Technical documents created with every project (module "Construction Permit").</summary>
    Public Shared ReadOnly DefaultDocuments As String() =
        {"Architectural & Structural Plans", "Electrical Plans", "Plumbing / Sanitary Plans", "Lot Title & Tax Declaration"}

    Private Const TableName As String = "construction_projects"
    Private Const ClearanceTable As String = "construction_clearances"

    ' ==================== Reading ====================

    Public Shared Function IsFinal(status As String) As Boolean
        Return status = Completed OrElse status = Rejected
    End Function

    Public Shared Function GetNextStage(stage As String) As String
        Dim i = Array.IndexOf(Stages, stage)
        If i < 0 OrElse i >= Stages.Length - 1 Then Return Nothing
        Return Stages(i + 1)
    End Function

    ''' <summary>Tracker position: 0-3 = stage in progress, 5 = completed, -1 = rejected.</summary>
    Public Shared Function GetTrackerStep(p As ConstructionProject) As Integer
        If p.Status = Rejected Then Return -1
        If p.CurrentStage = StageCompleted Then Return Stages.Length
        Return Math.Max(0, Array.IndexOf(Stages, p.CurrentStage))
    End Function

    ''' <summary>Long display name of a clearance type.</summary>
    Public Shared Function GetClearanceTitle(clearanceType As String) As String
        Select Case clearanceType
            Case ClearanceTypes.Locational : Return "Locational / Zoning Clearance"
            Case ClearanceTypes.FSEC : Return "Fire Safety (FSEC)"
            Case ClearanceTypes.Building : Return "Building Permit"
            Case ClearanceTypes.Occupancy : Return "Certificate of Occupancy"
            Case Else : Return clearanceType
        End Select
    End Function

    ''' <summary>The stage at which a clearance is decided.</summary>
    Public Shared Function GetClearanceStage(clearanceType As String) As String
        Select Case clearanceType
            Case ClearanceTypes.Locational : Return StageLocational
            Case ClearanceTypes.Occupancy : Return StageOccupancy
            Case Else : Return StageBuilding      ' FSEC and Building
        End Select
    End Function

    ''' <summary>The clearance that must be Approved to leave a stage (Nothing = none).</summary>
    Public Shared Function GetRequiredClearance(stage As String) As String
        Select Case stage
            Case StageLocational : Return ClearanceTypes.Locational
            Case StageBuilding : Return ClearanceTypes.Building
            Case StageOccupancy : Return ClearanceTypes.Occupancy
            Case Else : Return Nothing
        End Select
    End Function

    ''' <summary>Projects of a business, newest first (empty if the user may not see it).</summary>
    Public Shared Function GetProjects(businessId As Integer) As List(Of ConstructionProject)
        If Not AccessService.CanSeeBusiness(businessId) Then Return New List(Of ConstructionProject)
        Return ConstructionRepository.GetByBusinessId(businessId)
    End Function

    ''' <summary>The 4 clearances of a project (empty if the user may not see it).</summary>
    Public Shared Function GetClearances(p As ConstructionProject) As List(Of ConstructionClearance)
        If p Is Nothing OrElse Not AccessService.CanSeeBusiness(p.BusinessId) Then Return New List(Of ConstructionClearance)
        Return ConstructionRepository.GetClearances(p.ProjectId)
    End Function

    ''' <summary>The project's technical documents.</summary>
    Public Shared Function GetDocuments(p As ConstructionProject) As List(Of Requirement)
        If p Is Nothing OrElse Not AccessService.CanSeeBusiness(p.BusinessId) Then Return New List(Of Requirement)
        Return RequirementRepository.GetByModule(p.BusinessId, ModuleNames.ConstructionPermit, p.ProjectId)
    End Function

    ''' <summary>"1 of 4 approved"</summary>
    Public Shared Function GetClearanceSummary(clearances As IEnumerable(Of ConstructionClearance)) As String
        Return clearances.AsEnumerable().Count(Function(c) c.Status = Approved) & " of " & ClearanceTypes.All.Length & " approved"
    End Function

    ' ==================== Filing / editing ====================

    ''' <summary>Checks a project. Keys: "title", "type", "cost".</summary>
    Public Shared Function ValidateProject(p As ConstructionProject) As Dictionary(Of String, String)
        Dim errors As New Dictionary(Of String, String)
        If String.IsNullOrWhiteSpace(p.ProjectTitle) Then
            errors("title") = "Enter the project title."
        ElseIf p.ProjectTitle.Trim().Length > 200 Then
            errors("title") = "The title can be at most 200 characters."
        End If
        If Not ProjectTypes.Contains(p.ProjectType) Then errors("type") = "Choose the type of project."
        If p.EstimatedCost.HasValue Then
            If p.EstimatedCost.Value <= 0 Then
                errors("cost") = "The estimated cost must be greater than zero (or leave it blank)."
            ElseIf p.EstimatedCost.Value > 999999999999D Then
                errors("cost") = "The estimated cost is too large."
            End If
        End If
        Return errors
    End Function

    ''' <summary>
    ''' Files a construction project: reference CP-YYYY-xxxxx, stage Locational, status Active, the 4 clearances
    ''' (Pending) and the 4 technical documents. Owners may file for their own business. Returns an error or Nothing.
    ''' </summary>
    Public Shared Function FileProject(p As ConstructionProject) As String
        If Not AccessService.CanSeeBusiness(p.BusinessId) Then Return "Business not found."
        If Not Session.IsOwner AndAlso Not AccessService.CanManage(AppScreen.ConstructionPermits) Then
            Return "You are not allowed to file construction projects."
        End If
        Dim errors = ValidateProject(p)
        If errors.Count > 0 Then Return errors.Values.First()

        p.ReferenceNo = ReferenceNoService.GetNext(ReferenceNoService.ConstructionProject)
        p.CurrentStage = StageLocational
        p.Status = Active
        p.DateFiled = Date.Today
        Dim newId = ConstructionRepository.Insert(p)
        If newId <= 0 Then Return "The project could not be saved."
        p.ProjectId = newId
        For Each doc In DefaultDocuments
            RequirementRepository.Insert(New Requirement With {
                .BusinessId = p.BusinessId, .ModuleName = ModuleNames.ConstructionPermit,
                .RelatedId = newId, .DocumentName = doc, .Status = "Pending Upload"})
        Next
        AuditService.LogInsert(TableName, newId, "Filed construction project " & p.ReferenceNo & ": " & p.ProjectTitle.Trim())
        Return Nothing
    End Function

    ''' <summary>Staff edit of title / type / cost while the project is not Completed or Rejected.</summary>
    Public Shared Function UpdateProject(p As ConstructionProject) As String
        If Not AccessService.CanManage(AppScreen.ConstructionPermits) Then Return "You are not allowed to edit projects."
        Dim existing = ConstructionRepository.GetById(p.ProjectId)
        If existing Is Nothing OrElse existing.BusinessId <> p.BusinessId Then Return "Project not found."
        If IsFinal(existing.Status) Then Return "This project is already " & existing.Status & "."
        Dim errors = ValidateProject(p)
        If errors.Count > 0 Then Return errors.Values.First()
        ' Only the details change here - stage and status go through their own actions
        existing.ProjectTitle = p.ProjectTitle.Trim()
        existing.ProjectType = p.ProjectType
        existing.EstimatedCost = p.EstimatedCost
        If Not ConstructionRepository.Update(existing) Then Return "The project could not be saved."
        AuditService.LogUpdate(TableName, p.ProjectId, "Edited construction project " & existing.ReferenceNo)
        Return Nothing
    End Function

    ' ==================== Clearances ====================

    ''' <summary>Why a clearance cannot be approved now (Nothing = it can).</summary>
    Public Shared Function GetApproveBlocker(p As ConstructionProject, clearanceType As String) As String
        If p Is Nothing Then Return "Select a project first."
        If p.Status <> Active Then Return "The project is " & p.Status & "."
        Dim clearances = ConstructionRepository.GetClearances(p.ProjectId)
        Dim c = clearances.FirstOrDefault(Function(x) x.ClearanceType = clearanceType)
        If c Is Nothing Then Return "Unknown clearance."
        If c.Status = Approved Then Return "Already approved (" & c.ClearanceNo & ")."
        Dim needStage = GetClearanceStage(clearanceType)
        If Array.IndexOf(Stages, p.CurrentStage) < Array.IndexOf(Stages, needStage) Then
            Return "Decided at the " & needStage & " stage."
        End If
        If clearanceType = ClearanceTypes.Building Then
            Dim fsec = clearances.First(Function(x) x.ClearanceType = ClearanceTypes.FSEC)
            If fsec.Status <> Approved Then Return "Approve the FSEC first."
            Dim docs = RequirementRepository.GetByModule(p.BusinessId, ModuleNames.ConstructionPermit, p.ProjectId)
            Dim notVerified = docs.AsEnumerable().Count(Function(r) r.Status <> "Verified")
            If notVerified > 0 Then Return notVerified & " of " & docs.Count & " technical documents not yet verified."
        End If
        Return Nothing
    End Function

    ''' <summary>Why a clearance cannot be rejected now (Nothing = it can).</summary>
    Public Shared Function GetRejectBlocker(p As ConstructionProject, clearanceType As String) As String
        If p Is Nothing Then Return "Select a project first."
        If p.Status <> Active Then Return "The project is " & p.Status & "."
        Dim c = ConstructionRepository.GetClearances(p.ProjectId).FirstOrDefault(Function(x) x.ClearanceType = clearanceType)
        If c Is Nothing Then Return "Unknown clearance."
        If c.Status = Approved Then Return "Already approved (" & c.ClearanceNo & ")."
        If c.Status = "Rejected" Then Return "Already rejected."
        Dim needStage = GetClearanceStage(clearanceType)
        If Array.IndexOf(Stages, p.CurrentStage) < Array.IndexOf(Stages, needStage) Then
            Return "Decided at the " & needStage & " stage."
        End If
        Return Nothing
    End Function

    ''' <summary>
    ''' Approves a clearance (Building / Admin): number LC / FSEC / BLDG / OCC-YYYY-xxxxx, approved by/at,
    ''' optional remarks. Approving the Locational clearance endorses Zoning on the business permit.
    ''' </summary>
    Public Shared Function ApproveClearance(p As ConstructionProject, clearanceType As String,
                                            Optional remarks As String = "") As ClearanceOutcome
        Dim outcome As New ClearanceOutcome
        If Not AccessService.CanManage(AppScreen.ConstructionPermits) Then
            outcome.ErrorMessage = "You are not allowed to approve clearances."
            Return outcome
        End If
        outcome.ErrorMessage = GetApproveBlocker(p, clearanceType)
        If outcome.ErrorMessage IsNot Nothing Then Return outcome

        Dim c = ConstructionRepository.GetClearances(p.ProjectId).First(Function(x) x.ClearanceType = clearanceType)
        c.Status = Approved
        c.ClearanceNo = ReferenceNoService.GetNextClearanceNo(clearanceType)
        c.ApprovedBy = Session.UserId
        c.ApprovedAt = Date.Now
        If Not String.IsNullOrWhiteSpace(remarks) Then c.Remarks = remarks.Trim()
        If Not ConstructionRepository.UpdateClearance(c) Then
            outcome.ErrorMessage = "The clearance could not be saved."
            Return outcome
        End If
        outcome.ClearanceNo = c.ClearanceNo
        AuditService.Log(AuditActions.StatusChange, ClearanceTable, c.ClearanceId,
                         clearanceType & " clearance Approved (" & c.ClearanceNo & ") - " & p.ReferenceNo)
        If clearanceType = ClearanceTypes.Locational Then
            outcome.EndorsedReference = BusinessPermitService.AutoEndorse(p.BusinessId, EndorsementOffices.Zoning,
                                                                          "Locational clearance " & c.ClearanceNo & " approved (" & p.ReferenceNo & ")")
        End If
        Return outcome
    End Function

    ''' <summary>Rejects a clearance with a reason (it can be approved later after corrections).</summary>
    Public Shared Function RejectClearance(p As ConstructionProject, clearanceType As String, reason As String) As String
        If Not AccessService.CanManage(AppScreen.ConstructionPermits) Then Return "You are not allowed to reject clearances."
        Dim blocker = GetRejectBlocker(p, clearanceType)
        If blocker IsNot Nothing Then Return blocker
        If String.IsNullOrWhiteSpace(reason) Then Return "Please enter the reason for rejecting."
        Dim c = ConstructionRepository.GetClearances(p.ProjectId).First(Function(x) x.ClearanceType = clearanceType)
        c.Status = "Rejected"
        c.Remarks = ("Rejected: " & reason.Trim())
        If c.Remarks.Length > 255 Then c.Remarks = c.Remarks.Substring(0, 255)
        c.ApprovedBy = Session.UserId
        c.ApprovedAt = Date.Now
        If Not ConstructionRepository.UpdateClearance(c) Then Return "The clearance could not be saved."
        AuditService.Log(AuditActions.StatusChange, ClearanceTable, c.ClearanceId,
                         clearanceType & " clearance Rejected - " & p.ReferenceNo & ": " & reason.Trim())
        Return Nothing
    End Function

    ' ==================== Stage workflow ====================

    ''' <summary>Why the project cannot move to the next stage (Nothing = it can). Stages are never skipped.</summary>
    Public Shared Function GetAdvanceBlocker(p As ConstructionProject) As String
        If p Is Nothing Then Return "Select a project first."
        If p.Status <> Active Then Return "The project is " & p.Status & "."
        Dim nextStage = GetNextStage(p.CurrentStage)
        If nextStage Is Nothing Then Return "The project is already completed."
        Dim required = GetRequiredClearance(p.CurrentStage)
        If required IsNot Nothing Then
            Dim c = ConstructionRepository.GetClearances(p.ProjectId).First(Function(x) x.ClearanceType = required)
            If c.Status <> Approved Then Return "Approve the " & GetClearanceTitle(required) & " first."
        End If
        Return Nothing
    End Function

    ''' <summary>Moves the project to the NEXT stage only (Completed also completes the project). Returns an error or Nothing.</summary>
    Public Shared Function Advance(p As ConstructionProject) As String
        If Not AccessService.CanManage(AppScreen.ConstructionPermits) Then Return "You are not allowed to change the stage."
        Dim blocker = GetAdvanceBlocker(p)
        If blocker IsNot Nothing Then Return blocker
        Dim oldStage = p.CurrentStage
        Dim oldStatus = p.Status
        p.CurrentStage = GetNextStage(oldStage)
        If p.CurrentStage = StageCompleted Then p.Status = Completed
        If Not ConstructionRepository.Update(p) Then
            p.CurrentStage = oldStage
            p.Status = oldStatus
            Return "The stage could not be saved."
        End If
        AuditService.LogStatusChange(TableName, p.ProjectId, oldStage, p.CurrentStage, p.ReferenceNo)
        Return Nothing
    End Function

    ''' <summary>Text for the green "ready" box of the stage dialog.</summary>
    Public Shared Function GetReadyMessage(p As ConstructionProject) As String
        Select Case GetNextStage(p.CurrentStage)
            Case StageBuilding : Return "Locational clearance approved. Proceed to the building permit evaluation (FSEC + Building)."
            Case StageConstruction : Return "Building permit approved. Construction may start."
            Case StageOccupancy : Return "Construction finished. Proceed to the occupancy inspection."
            Case StageCompleted : Return "Certificate of Occupancy approved. The project will be completed."
            Case Else : Return "Ready."
        End Select
    End Function

    ''' <summary>Puts an Active project On Hold (reason required, kept in the audit log).</summary>
    Public Shared Function PutOnHold(p As ConstructionProject, reason As String) As String
        If Not AccessService.CanManage(AppScreen.ConstructionPermits) Then Return "You are not allowed to change the project."
        If p.Status <> Active Then Return "Only an Active project can be put on hold."
        If String.IsNullOrWhiteSpace(reason) Then Return "Please enter the reason."
        Return SetStatus(p, OnHold, reason.Trim())
    End Function

    ''' <summary>Resumes a project that is On Hold.</summary>
    Public Shared Function ResumeProject(p As ConstructionProject) As String
        If Not AccessService.CanManage(AppScreen.ConstructionPermits) Then Return "You are not allowed to change the project."
        If p.Status <> OnHold Then Return "Only a project On Hold can be resumed."
        Return SetStatus(p, Active, "resumed")
    End Function

    ''' <summary>Rejects a project that is not final (reason required).</summary>
    Public Shared Function RejectProject(p As ConstructionProject, reason As String) As String
        If Not AccessService.CanManage(AppScreen.ConstructionPermits) Then Return "You are not allowed to change the project."
        If IsFinal(p.Status) Then Return "This project is already " & p.Status & "."
        If String.IsNullOrWhiteSpace(reason) Then Return "Please enter the reason for rejecting."
        Return SetStatus(p, Rejected, reason.Trim())
    End Function

    Private Shared Function SetStatus(p As ConstructionProject, newStatus As String, note As String) As String
        Dim old = p.Status
        p.Status = newStatus
        If Not ConstructionRepository.Update(p) Then
            p.Status = old
            Return "The project could not be saved."
        End If
        AuditService.LogStatusChange(TableName, p.ProjectId, old, newStatus, note)
        Return Nothing
    End Function

End Class
