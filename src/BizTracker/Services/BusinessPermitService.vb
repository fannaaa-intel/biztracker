''' <summary>Late renewal charges for a business permit (RA 7160 Sec. 168).</summary>
Public Class LateCharges
    Public Property IsLate As Boolean
    Public Property Deadline As Date
    Public Property MonthsLate As Integer
    Public Property Surcharge As Decimal
    Public Property Interest As Decimal
End Class

''' <summary>
''' Business permit workflow and rules. Screens call this class; it calls the repositories.
''' Status flow: Submitted -> Under Review -> For Assessment -> Assessed -> Paid -> Issued (or Rejected).
''' Every change is written to audit_log.
''' </summary>
Public NotInheritable Class BusinessPermitService

    Private Sub New()
    End Sub

    Public Const Submitted As String = "Submitted"
    Public Const UnderReview As String = "Under Review"
    Public Const ForAssessment As String = "For Assessment"
    Public Const Assessed As String = "Assessed"
    Public Const Paid As String = "Paid"
    Public Const Issued As String = "Issued"
    Public Const Rejected As String = "Rejected"

    Public Shared ReadOnly StatusFlow As String() = {Submitted, UnderReview, ForAssessment, Assessed, Paid, Issued}

    ''' <summary>The 5 steps shown in the filing progress tracker.</summary>
    Public Shared ReadOnly TrackerSteps As String() =
        {"Submission", "Document Review", "Multi-Agency Clearance", "Final Assessment", "Issuance"}

    ''' <summary>Default document checklist created with each new application.</summary>
    Public Shared ReadOnly NewRequirements As String() = {
        "DTI/SEC Registration", "Barangay Business Clearance", "Lease Contract or Land Title",
        "Occupancy Permit", "Fire Safety Inspection Certificate (FSIC)"}
    Public Shared ReadOnly RenewalRequirements As String() = {
        "DTI/SEC Registration", "Gross Receipts Declaration", "Lease Contract",
        "Occupancy Permit", "Fire Safety Inspection Certificate (FSIC)"}

    Private Const TableName As String = "business_permits"

    ' ==================== Reading ====================

    ''' <summary>The newest Issued permit of a business (the one currently in force), or Nothing.</summary>
    Public Shared Function GetCurrentPermit(businessId As Integer) As BusinessPermit
        Return BusinessPermitRepository.GetByBusinessId(businessId).
               Where(Function(p) p.Status = Issued).
               OrderByDescending(Function(p) p.PermitYear).FirstOrDefault()
    End Function

    ''' <summary>The newest application that is not Rejected, or Nothing.</summary>
    Public Shared Function GetLatestApplication(businessId As Integer) As BusinessPermit
        Return BusinessPermitRepository.GetByBusinessId(businessId).
               Where(Function(p) p.Status <> Rejected).
               OrderByDescending(Function(p) p.PermitYear).ThenByDescending(Function(p) p.PermitId).FirstOrDefault()
    End Function

    Public Shared Function IsFinal(status As String) As Boolean
        Return status = Issued OrElse status = Rejected
    End Function

    ''' <summary>The next status in the flow, or Nothing if the application is finished.</summary>
    Public Shared Function GetNextStatus(status As String) As String
        Dim i = Array.IndexOf(StatusFlow, status)
        If i < 0 OrElse i >= StatusFlow.Length - 1 Then Return Nothing
        Return StatusFlow(i + 1)
    End Function

    ''' <summary>
    ''' Tracker position: 0-4 = the step in progress, 5 = all steps done (Issued), -1 = Rejected.
    ''' </summary>
    Public Shared Function GetTrackerStep(status As String) As Integer
        Select Case status
            Case Submitted : Return 0
            Case UnderReview : Return 1
            Case ForAssessment : Return 2
            Case Assessed, Paid : Return 3
            Case Issued : Return 5
            Case Else : Return -1
        End Select
    End Function

    ''' <summary>Assessed tax + surcharge + interest.</summary>
    Public Shared Function GetTotalDue(p As BusinessPermit) As Decimal
        Return p.AssessedAmount + p.Surcharge + p.Interest
    End Function

    ' ==================== Late renewal ====================

    ''' <summary>Renewal deadline for a permit year (settings: bp_renewal_deadline, "MM-DD").</summary>
    Public Shared Function GetRenewalDeadline(permitYear As Integer) As Date
        Dim parts = SettingsRepository.GetValue("bp_renewal_deadline", "01-20").Split("-"c)
        Dim month = 1, day = 20
        If parts.Length = 2 Then
            Integer.TryParse(parts(0), month)
            Integer.TryParse(parts(1), day)
        End If
        month = Math.Max(1, Math.Min(12, month))
        day = Math.Max(1, Math.Min(Date.DaysInMonth(permitYear, month), day))
        Return New Date(permitYear, month, day)
    End Function

    ''' <summary>
    ''' Late renewal charges as of a date: a one-time surcharge (25%) on the tax, plus interest
    ''' (2% per month or fraction of a month, not compounded, max 36 months) on the tax + surcharge.
    ''' New applications are never late. Rates come from the settings table.
    ''' </summary>
    Public Shared Function ComputeLateCharges(applicationType As String, permitYear As Integer,
                                              assessedAmount As Decimal, asOf As Date) As LateCharges
        Dim result As New LateCharges With {.Deadline = GetRenewalDeadline(permitYear)}
        If applicationType <> "Renewal" OrElse asOf.Date <= result.Deadline Then Return result

        Dim months = (asOf.Year - result.Deadline.Year) * 12 + asOf.Month - result.Deadline.Month
        If asOf.Day > result.Deadline.Day Then months += 1          ' a fraction of a month counts as a month
        months = Math.Max(1, Math.Min(months, SettingsRepository.GetInt("bp_interest_max_months", 36)))

        Dim surchargeRate = SettingsRepository.GetDecimal("bp_surcharge_rate", 0.25D)
        Dim interestRate = SettingsRepository.GetDecimal("bp_interest_rate_monthly", 0.02D)
        result.IsLate = True
        result.MonthsLate = months
        result.Surcharge = Math.Round(assessedAmount * surchargeRate, 2)
        result.Interest = Math.Round((assessedAmount + result.Surcharge) * interestRate * months, 2)
        Return result
    End Function

    ' ==================== Filing / editing ====================

    ''' <summary>Suggested permit year for a renewal: the year after the newest application, at least this year.</summary>
    Public Shared Function SuggestRenewalYear(businessId As Integer) As Integer
        Dim latest = GetLatestApplication(businessId)
        If latest Is Nothing Then Return Date.Today.Year
        Return Math.Max(latest.PermitYear + 1, Date.Today.Year)
    End Function

    ''' <summary>True if the business already has an application for that year that is not Rejected.</summary>
    Public Shared Function HasActiveApplication(businessId As Integer, permitYear As Integer,
                                                Optional ignorePermitId As Integer = 0) As Boolean
        Return BusinessPermitRepository.GetByBusinessId(businessId).Any(
            Function(p) p.PermitYear = permitYear AndAlso p.Status <> Rejected AndAlso p.PermitId <> ignorePermitId)
    End Function

    ''' <summary>
    ''' Checks an application before saving. Returns a list of field -> message (empty = valid).
    ''' Keys: "year", "gross", "filed", "assessed".
    ''' </summary>
    Public Shared Function Validate(p As BusinessPermit) As Dictionary(Of String, String)
        Dim errors As New Dictionary(Of String, String)
        Dim thisYear = Date.Today.Year
        If p.PermitYear < thisYear - 5 OrElse p.PermitYear > thisYear + 1 Then
            errors("year") = "Permit year must be between " & (thisYear - 5) & " and " & (thisYear + 1) & "."
        ElseIf HasActiveApplication(p.BusinessId, p.PermitYear, p.PermitId) Then
            errors("year") = "This business already has an application for " & p.PermitYear & "."
        End If
        If p.GrossReceipts <= 0 Then
            errors("gross") = If(p.ApplicationType = "New", "Enter the capital investment (more than 0).",
                                 "Enter the gross receipts for the previous year (more than 0).")
        End If
        If p.DateFiled.Date > Date.Today Then
            errors("filed") = "Date filed cannot be in the future."
        ElseIf p.DateFiled.Year < p.PermitYear - 1 Then
            errors("filed") = "Date filed is too early for a " & p.PermitYear & " permit."
        End If
        If p.AssessedAmount < 0 Then errors("assessed") = "Assessed amount cannot be negative."
        Return errors
    End Function

    ''' <summary>
    ''' Files a New or Renewal application: reference number, 5 Pending endorsements and the
    ''' default requirements checklist. Returns the new permit_id, or -1 if it failed.
    ''' </summary>
    Public Shared Function FileApplication(p As BusinessPermit) As Integer
        If Not AccessService.CanSeeBusiness(p.BusinessId) Then Return -1
        If Validate(p).Count > 0 Then Return -1

        p.Status = Submitted
        p.ReferenceNo = ReferenceNoService.GetNext(ReferenceNoService.BusinessPermit, p.PermitYear)
        p.AssessedAmount = 0 : p.Surcharge = 0 : p.Interest = 0
        Dim newId = BusinessPermitRepository.Insert(p)
        If newId <= 0 Then Return -1

        Dim docs = If(p.ApplicationType = "New", NewRequirements, RenewalRequirements)
        For Each doc In docs
            RequirementRepository.Insert(New Requirement With {
                .BusinessId = p.BusinessId, .ModuleName = ModuleNames.BusinessPermit,
                .RelatedId = newId, .DocumentName = doc, .Status = "Pending Upload"})
        Next
        AuditService.LogInsert(TableName, newId, "Filed " & p.ApplicationType & " application " & p.ReferenceNo &
                               " for " & p.PermitYear)
        Return newId
    End Function

    ''' <summary>Saves edits (year, gross receipts, date filed, remarks, assessed amount). Staff only, not when final.</summary>
    Public Shared Function UpdateApplication(p As BusinessPermit) As Boolean
        If Not AccessService.CanManage(AppScreen.BusinessPermits) OrElse IsFinal(p.Status) Then Return False
        If Validate(p).Count > 0 Then Return False
        If Not BusinessPermitRepository.Update(p) Then Return False
        AuditService.LogUpdate(TableName, p.PermitId, "Edited application " & p.ReferenceNo)
        Return True
    End Function

    ''' <summary>Admin only, and only while the application is still Submitted.</summary>
    Public Shared Function DeleteApplication(p As BusinessPermit) As Boolean
        If Not Session.IsAdmin OrElse p.Status <> Submitted Then Return False
        If Not BusinessPermitRepository.Delete(p.PermitId) Then Return False
        AuditService.LogDelete(TableName, p.PermitId, "Deleted application " & p.ReferenceNo)
        Return True
    End Function

    ' ==================== Status workflow ====================

    ''' <summary>
    ''' Why the application cannot move to the next status yet (Nothing = it can).
    ''' Rules: documents verified before assessment; all 5 endorsements before final assessment.
    ''' </summary>
    Public Shared Function GetAdvanceBlocker(p As BusinessPermit) As String
        Dim nextStatus = GetNextStatus(p.Status)
        If nextStatus Is Nothing Then Return "This application is already " & p.Status & "."

        Select Case nextStatus
            Case ForAssessment
                Dim docs = RequirementRepository.GetByModule(p.BusinessId, ModuleNames.BusinessPermit, p.PermitId)
                Dim notVerified = docs.Where(Function(r) r.Status <> "Verified").ToList()
                If docs.Count = 0 Then Return "There are no requirements on file yet."
                If notVerified.Count > 0 Then
                    Return notVerified.Count & " of " & docs.Count & " documents are not yet verified."
                End If
            Case Assessed
                Dim pending = BusinessPermitRepository.GetEndorsements(p.PermitId).
                              Where(Function(e) e.Status <> "Endorsed").Select(Function(e) e.Office).ToList()
                If pending.Count > 0 Then Return "Waiting for endorsement from: " & String.Join(", ", pending) & "."
        End Select
        Return Nothing
    End Function

    ''' <summary>
    ''' Moves the application to the next status. For "Assessed" pass the assessed tax
    ''' (late renewal charges are added automatically). Returns an error message, or Nothing on success.
    ''' </summary>
    Public Shared Function Advance(p As BusinessPermit, Optional assessedAmount As Decimal = 0D) As String
        If Not AccessService.CanManage(AppScreen.BusinessPermits) Then Return "You are not allowed to change the status."
        Dim blocker = GetAdvanceBlocker(p)
        If blocker IsNot Nothing Then Return blocker

        Dim oldStatus = p.Status
        Dim nextStatus = GetNextStatus(p.Status)
        Select Case nextStatus
            Case Assessed
                If assessedAmount <= 0 Then Return "Enter the assessed business tax (more than 0)."
                p.AssessedAmount = Math.Round(assessedAmount, 2)
                ApplyLateCharges(p, Date.Today)
            Case Paid
                ApplyLateCharges(p, Date.Today)     ' interest runs until the day of payment
            Case Issued
                p.MayorsPermitNo = ReferenceNoService.GetNext(ReferenceNoService.MayorsPermit, p.PermitYear)
                p.DateIssued = Date.Today
                p.ValidUntil = New Date(p.PermitYear, 12, 31)
        End Select

        p.Status = nextStatus
        If Not BusinessPermitRepository.Update(p) Then
            p.Status = oldStatus
            Return "The status could not be saved."
        End If
        AuditService.LogStatusChange(TableName, p.PermitId, oldStatus, nextStatus,
            If(nextStatus = Issued, "Mayor's Permit " & p.MayorsPermitNo,
               If(nextStatus = Assessed, "Total due " & GetTotalDue(p).ToString("N2"), "")))
        Return Nothing
    End Function

    ''' <summary>Rejects an application that is not yet final. A reason is required.</summary>
    Public Shared Function Reject(p As BusinessPermit, reason As String) As String
        If Not AccessService.CanManage(AppScreen.BusinessPermits) Then Return "You are not allowed to change the status."
        If IsFinal(p.Status) Then Return "This application is already " & p.Status & "."
        If String.IsNullOrWhiteSpace(reason) Then Return "Please enter the reason for rejecting."

        Dim oldStatus = p.Status
        p.Status = Rejected
        p.Remarks = reason.Trim()
        If Not BusinessPermitRepository.Update(p) Then
            p.Status = oldStatus
            Return "The status could not be saved."
        End If
        AuditService.LogStatusChange(TableName, p.PermitId, oldStatus, Rejected, reason.Trim())
        Return Nothing
    End Function

    Private Shared Sub ApplyLateCharges(p As BusinessPermit, asOf As Date)
        Dim charges = ComputeLateCharges(p.ApplicationType, p.PermitYear, p.AssessedAmount, asOf)
        p.Surcharge = charges.Surcharge
        p.Interest = charges.Interest
    End Sub

    ' ==================== Endorsements ====================

    ''' <summary>Staff marks an office's endorsement Endorsed / Rejected / Pending.</summary>
    Public Shared Function SetEndorsement(p As BusinessPermit, office As String, status As String,
                                          Optional remarks As String = "") As Boolean
        If Not AccessService.CanManage(AppScreen.BusinessPermits) OrElse IsFinal(p.Status) Then Return False
        If Not BusinessPermitRepository.SetEndorsement(p.PermitId, office, status, Session.UserId, remarks) Then Return False
        AuditService.Log(AuditActions.StatusChange, "clearance_endorsements", p.PermitId,
                         office & " endorsement -> " & status & " (" & p.ReferenceNo & ")")
        Return True
    End Function

End Class
