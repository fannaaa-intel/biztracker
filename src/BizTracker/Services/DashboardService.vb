''' <summary>One thing that needs attention (shown in the dashboard's "Action Required" list).</summary>
Public Class ActionItem
    Public Property BusinessId As Integer
    Public Property BusinessName As String = ""
    ''' <summary>The module screen that handles it ("Open" goes there).</summary>
    Public Property Screen As AppScreen
    ''' <summary>notifications.module value (same ENUM as requirements.module).</summary>
    Public Property ModuleName As String = ""
    ''' <summary>The record it is about (employee / quarter's assessment / permit / inspection item ...).</summary>
    Public Property RelatedId As Integer
    Public Property Title As String = ""
    Public Property Detail As String = ""
    ''' <summary>A shorter detail for narrow screens ("5 days ago", "BP-2027-00001").</summary>
    Public Property ShortDetail As String = ""
    ''' <summary>Status word for the badge (Expired, Overdue, Pending, Failed, Expiring Soon...).</summary>
    Public Property Status As String = ""
    ''' <summary>True for expired / overdue / failed / rejected items, False for warnings (expiring soon, pending).</summary>
    Public Property IsUrgent As Boolean
    ''' <summary>The date the item is due or expired on (Nothing when there is none, e.g. a pending endorsement).</summary>
    Public Property DueDate As Date?
End Class

''' <summary>What one module card on the dashboard shows.</summary>
Public Class ModuleSummary
    Public Property Screen As AppScreen
    Public Property Title As String = ""
    ''' <summary>Big text: a reference number, an amount or a count.</summary>
    Public Property Value As String = ""
    ''' <summary>Status badge (empty = no badge).</summary>
    Public Property Status As String = ""
    ''' <summary>Short note next to the badge.</summary>
    Public Property Note As String = ""
    ''' <summary>One extra line under the badge.</summary>
    Public Property Detail As String = ""
    ''' <summary>Staff view: the number of open/pending records across all businesses.</summary>
    Public Property PendingCount As Integer
    ''' <summary>Progress bar at the bottom of the card: 0..1 (e.g. 3 of 4 departments passed = 0.75).</summary>
    Public Property Progress As Double
    ''' <summary>Caption of the progress bar ("Step 2 of 5 · Document Review"); empty = no bar.</summary>
    Public Property ProgressText As String = ""
    ''' <summary>Right side of the progress bar ("40%").</summary>
    Public Property ProgressNote As String = ""
End Class

''' <summary>
''' Everything the dashboard shows. Every number comes from the module services
''' (the same code the module screens use), so the dashboard always matches them.
'''   - Owner : their own business only.
'''   - Admin / BPLO : totals across all active businesses.
''' </summary>
Public NotInheritable Class DashboardService

    Private Sub New()
    End Sub

    ' ==================== Who sees what ====================

    ''' <summary>True if the logged-in user may open the dashboard (Admin, BPLO, Owner - CLAUDE.md).</summary>
    Public Shared Function CanView() As Boolean
        Return AccessService.CanAccess(AppScreen.Dashboard)
    End Function

    ''' <summary>The businesses the dashboard covers: the owner's own, or every active business for staff.</summary>
    Public Shared Function GetBusinesses() As List(Of Business)
        If Not CanView() Then Return New List(Of Business)
        If Session.IsOwner Then
            Dim own = If(Session.BusinessId.HasValue, BusinessRepository.GetById(Session.BusinessId.Value), Nothing)
            Return If(own Is Nothing OrElse Not own.IsActive, New List(Of Business)(), New List(Of Business) From {own})
        End If
        Return BusinessRepository.GetAll()
    End Function

    ' ==================== Action Required ====================

    ''' <summary>Action items of every business the user may see, urgent first.</summary>
    Public Shared Function GetActionItems() As List(Of ActionItem)
        Dim items As New List(Of ActionItem)
        For Each b In GetBusinesses()
            items.AddRange(GetActionItems(b))
        Next
        Return items
    End Function

    ''' <summary>
    ''' Action items of one business, urgent first:
    '''   health certificates expiring / expired / missing, overdue RPT quarters, pending endorsements of the
    '''   open business permit application, failed inspection departments, and business / sanitary permits
    '''   expiring within expiry_warning_days (or already expired).
    ''' </summary>
    Public Shared Function GetActionItems(b As Business) As List(Of ActionItem)
        Dim items As New List(Of ActionItem)
        If b Is Nothing OrElse Not CanView() OrElse Not AccessService.CanSeeBusiness(b.BusinessId) Then Return items
        Dim id = b.BusinessId

        ' 1. Health certificates (same list as the "Renew Now" tab)
        For Each r In HealthCertificateService.GetRenewalList(HealthCertificateService.GetRoster(id))
            Dim item = NewItem(b, AppScreen.HealthCertificates, ModuleNames.HealthCertificate, r.Employee.EmployeeId)
            item.Status = r.Status
            item.Title = r.Employee.FullName
            If r.Certificate Is Nothing Then
                item.Detail = "No health certificate on record"
                item.ShortDetail = "No certificate"
                item.IsUrgent = True
            Else
                item.DueDate = r.Certificate.ExpiryDate
                item.IsUrgent = r.Status = StatusService.Expired
                item.Detail = "Health certificate " & r.Certificate.CertificateNo & " · " & DaysText(r.DaysLeft.Value, r.Certificate.ExpiryDate)
                item.ShortDetail = ShortDaysText(r.DaysLeft.Value)
            End If
            items.Add(item)
        Next

        ' 2. Overdue real property tax quarters (all years)
        For Each q In RptService.GetOverdueQuarters(id)
            Dim item = NewItem(b, AppScreen.RealPropertyTax, ModuleNames.RealPropertyTax, q.AssessmentId)
            item.Status = StatusService.Overdue
            item.IsUrgent = True
            item.DueDate = q.DueDate
            item.Title = "Real property tax " & q.Label
            item.Detail = UiHelper.FormatMoney(q.Amount + q.Penalty) & " incl. penalty · was due " &
                          UiHelper.FormatDate(q.DueDate) & " · PIN " & q.Pin
            item.ShortDetail = UiHelper.FormatMoney(q.Amount + q.Penalty) & " to pay"
            items.Add(item)
        Next

        ' 3. Endorsements still missing on the open business permit application
        Dim latest = BusinessPermitService.GetLatestApplication(id)
        If latest IsNot Nothing AndAlso Not BusinessPermitService.IsFinal(latest.Status) Then
            For Each e In BusinessPermitRepository.GetEndorsements(latest.PermitId).
                          Where(Function(x) x.Status <> "Endorsed")
                Dim item = NewItem(b, AppScreen.BusinessPermits, ModuleNames.BusinessPermit, latest.PermitId)
                item.Status = e.Status
                item.IsUrgent = e.Status = "Rejected"
                item.Title = e.Office & " endorsement"
                item.Detail = latest.ReferenceNo & " · " & latest.ApplicationType & " " & latest.PermitYear &
                              If(e.Remarks <> "" AndAlso item.IsUrgent, " · " & e.Remarks, "")
                item.ShortDetail = latest.ReferenceNo
                items.Add(item)
            Next
        End If

        ' 4. Failed departments of the current annual inspection
        Dim inspection = InspectionService.GetCurrentInspection(InspectionService.GetInspections(id))
        If inspection IsNot Nothing Then
            Dim visit = InspectionService.GetNextVisit(inspection)
            For Each x In InspectionService.GetItems(inspection).Where(Function(i) i.Result = InspectionService.Failed)
                Dim item = NewItem(b, AppScreen.AnnualInspections, ModuleNames.AnnualInspection, x.ItemId)
                item.Status = InspectionService.Failed
                item.IsUrgent = True
                item.DueDate = If(visit.HasValue, visit.Value.Date, CType(Nothing, Date?))
                item.Title = x.Department & " inspection"
                item.Detail = inspection.ReferenceNo &
                              If(visit.HasValue, " · re-inspection " & visit.Value.ToString("MMM d, yyyy"), " · re-inspection not set")
                item.ShortDetail = If(visit.HasValue, "re-inspect " & visit.Value.ToString("MMM d"), "date not set")
                items.Add(item)
            Next
        End If

        ' 5. Business permit in force expiring soon / expired
        Dim permit = BusinessPermitService.GetCurrentPermit(id)
        If permit IsNot Nothing AndAlso permit.ValidUntil.HasValue Then
            Dim status = StatusService.GetExpiryStatus(permit.ValidUntil)
            ' A newer Issued renewal replaces it, so only the newest issued permit is checked
            If status <> StatusService.Valid Then
                Dim item = NewItem(b, AppScreen.BusinessPermits, ModuleNames.BusinessPermit, permit.PermitId)
                item.Status = status
                item.IsUrgent = status = StatusService.Expired
                item.DueDate = permit.ValidUntil
                item.Title = "Mayor's permit"
                item.Detail = If(permit.MayorsPermitNo <> "", permit.MayorsPermitNo, permit.ReferenceNo) & " · " &
                              DaysText(StatusService.DaysUntil(permit.ValidUntil.Value), permit.ValidUntil.Value)
                item.ShortDetail = ShortDaysText(StatusService.DaysUntil(permit.ValidUntil.Value))
                items.Add(item)
            End If
        End If

        ' 6. Sanitary permit in force expiring soon / expired
        Dim sanitary = SanitaryPermitService.GetCurrentPermit(SanitaryPermitService.GetPermits(id))
        If sanitary IsNot Nothing AndAlso sanitary.ValidUntil.HasValue Then
            Dim status = SanitaryPermitService.GetDisplayStatus(sanitary)
            If status <> StatusService.Valid Then
                Dim item = NewItem(b, AppScreen.SanitaryPermits, ModuleNames.SanitaryPermit, sanitary.SanitaryId)
                item.Status = status
                item.IsUrgent = status = StatusService.Expired
                item.DueDate = sanitary.ValidUntil
                item.Title = "Sanitary permit"
                item.Detail = sanitary.PermitNo & " · " & DaysText(StatusService.DaysUntil(sanitary.ValidUntil.Value), sanitary.ValidUntil.Value)
                item.ShortDetail = ShortDaysText(StatusService.DaysUntil(sanitary.ValidUntil.Value))
                items.Add(item)
            End If
        End If

        ' Urgent first; the original module order is kept inside each group
        Return items.Select(Function(x, i) New With {x, i}).
               OrderBy(Function(t) If(t.x.IsUrgent, 0, 1)).ThenBy(Function(t) t.i).
               Select(Function(t) t.x).ToList()
    End Function

    ''' <summary>Sets the card's progress bar (done of total). The note defaults to a percentage.</summary>
    Private Shared Sub SetProgress(m As ModuleSummary, done As Integer, total As Integer, caption As String, Optional note As String = Nothing)
        If total <= 0 Then Return
        m.Progress = Math.Max(0, Math.Min(1, done / CDbl(total)))
        m.ProgressText = caption
        m.ProgressNote = If(note, CInt(Math.Round(m.Progress * 100, MidpointRounding.AwayFromZero)) & "%")
    End Sub

    Private Shared Function NewItem(b As Business, screen As AppScreen, moduleName As String, relatedId As Integer) As ActionItem
        Return New ActionItem With {.BusinessId = b.BusinessId, .BusinessName = b.BusinessName, .Screen = screen,
                                    .ModuleName = moduleName, .RelatedId = relatedId}
    End Function

    ''' <summary>"expires in 20 days (Oct 26, 2026)", "expires today", "5 days ago (Oct 1, 2026)".</summary>
    Public Shared Function DaysText(days As Integer, target As Date) As String
        Dim d = UiHelper.FormatDate(target)
        If days = 0 Then Return "due today (" & d & ")"
        If days > 0 Then Return "in " & days & " day" & If(days = 1, "", "s") & " (" & d & ")"
        Return Math.Abs(days) & " day" & If(days = -1, "", "s") & " ago (" & d & ")"
    End Function

    ''' <summary>"in 20 days", "today", "5 days ago" (no date - for narrow screens).</summary>
    Public Shared Function ShortDaysText(days As Integer) As String
        If days = 0 Then Return "today"
        If days > 0 Then Return "in " & days & " day" & If(days = 1, "", "s")
        Return Math.Abs(days) & " day" & If(days = -1, "", "s") & " ago"
    End Function

    ''' <summary>"Action Required: 4 items need attention" / "1 item needs attention" / all clear.</summary>
    Public Shared Function GetActionHeadline(count As Integer) As String
        If count = 0 Then Return "All clear: nothing needs attention"
        Return "Action Required: " & count & If(count = 1, " item needs attention", " items need attention")
    End Function

    ' ==================== Module cards ====================

    ''' <summary>The six module cards: the owner's own business, or totals across all businesses for staff.</summary>
    Public Shared Function GetModuleSummaries() As List(Of ModuleSummary)
        If Not CanView() Then Return New List(Of ModuleSummary)
        Dim businesses = GetBusinesses()
        If Session.IsOwner Then
            Return If(businesses.Count = 0, New List(Of ModuleSummary)(), GetBusinessSummaries(businesses(0)))
        End If
        Return GetStaffSummaries(businesses)
    End Function

    ''' <summary>One business: the same values its module screens show on their cards.</summary>
    Public Shared Function GetBusinessSummaries(b As Business) As List(Of ModuleSummary)
        Dim id = b.BusinessId
        Dim list As New List(Of ModuleSummary)

        ' Business Permit: latest application + the permit in force
        Dim bp As New ModuleSummary With {.Screen = AppScreen.BusinessPermits, .Title = "Business Permit"}
        Dim latest = BusinessPermitService.GetLatestApplication(id)
        Dim current = BusinessPermitService.GetCurrentPermit(id)
        If latest Is Nothing Then
            bp.Value = "None" : bp.Note = "no applications yet"
        Else
            bp.Value = latest.ReferenceNo : bp.Status = latest.Status
            bp.Note = latest.ApplicationType & " · " & latest.PermitYear
            bp.PendingCount = If(BusinessPermitService.IsFinal(latest.Status), 0, 1)
        End If
        bp.Detail = If(current Is Nothing, "No Mayor's permit in force",
                       "Permit " & If(current.MayorsPermitNo <> "", current.MayorsPermitNo, current.ReferenceNo) & " · " &
                       StatusService.GetExpiryStatus(current.ValidUntil) & " until " & UiHelper.FormatDate(current.ValidUntil))
        If latest IsNot Nothing Then
            Dim stepNo = BusinessPermitService.GetTrackerStep(latest.Status)
            If stepNo >= 0 Then
                Dim n = BusinessPermitService.TrackerSteps.Length
                SetProgress(bp, Math.Min(stepNo, n), n, If(stepNo >= n, "All " & n & " filing steps done",
                            "Step " & (stepNo + 1) & " of " & n & " · " & BusinessPermitService.TrackerSteps(stepNo)))
            End If
        End If
        list.Add(bp)

        ' Sanitary Permit: permit in force + latest application
        Dim sp As New ModuleSummary With {.Screen = AppScreen.SanitaryPermits, .Title = "Sanitary Permit"}
        Dim permits = SanitaryPermitService.GetPermits(id)
        Dim spCurrent = SanitaryPermitService.GetCurrentPermit(permits)
        Dim spLatest = permits.FirstOrDefault()
        If spCurrent IsNot Nothing Then
            sp.Value = spCurrent.PermitNo : sp.Status = SanitaryPermitService.GetDisplayStatus(spCurrent)
            sp.Note = "until " & UiHelper.FormatDate(spCurrent.ValidUntil)
        ElseIf spLatest IsNot Nothing Then
            sp.Value = spLatest.PermitNo : sp.Status = spLatest.Status : sp.Note = spLatest.Category & " · " & spLatest.PermitYear
        Else
            sp.Value = "None" : sp.Note = "no sanitary permit yet"
        End If
        sp.PendingCount = permits.AsEnumerable().Count(Function(p) Not SanitaryPermitService.IsFinal(p.Status))
        Dim sync = SanitaryPermitService.GetHealthCardSync(id)
        sp.Detail = If(spLatest IsNot Nothing AndAlso spLatest IsNot spCurrent AndAlso Not SanitaryPermitService.IsFinal(spLatest.Status),
                       "Application " & spLatest.PermitNo & " · " & spLatest.Status,
                       If(sync.Total = 0, "No staff on record", "Staff health cards: " & sync.Valid & " of " & sync.Total & " valid"))
        If spLatest IsNot Nothing AndAlso Not SanitaryPermitService.IsFinal(spLatest.Status) Then
            Dim stepNo = SanitaryPermitService.GetTrackerStep(spLatest.Status)
            Dim n = SanitaryPermitService.TrackerSteps.Length
            SetProgress(sp, stepNo, n, "Step " & (stepNo + 1) & " of " & n & " · " & SanitaryPermitService.TrackerSteps(stepNo))
        ElseIf spCurrent IsNot Nothing Then
            Dim daysLeft = Math.Max(0, StatusService.DaysUntil(spCurrent.ValidUntil.Value))
            Dim yearDays = If(Date.IsLeapYear(spCurrent.ValidUntil.Value.Year), 366, 365)
            SetProgress(sp, daysLeft, yearDays, "Validity left", daysLeft & " day" & If(daysLeft = 1, "", "s"))
        End If
        list.Add(sp)

        ' Real Property Tax: this year's total due + tax clearance
        Dim rpt As New ModuleSummary With {.Screen = AppScreen.RealPropertyTax, .Title = "Real Property Tax"}
        Dim year = Date.Today.Year
        Dim ledger = RptService.GetLedger(id, year)
        Dim overdue = RptService.GetOverdueQuarters(id)
        Dim totalDue = ledger.Sum(Function(r) r.Balance)
        rpt.PendingCount = overdue.Count
        If ledger.Count = 0 Then
            rpt.Value = "No property" : rpt.Note = "nothing to pay"
        Else
            rpt.Value = UiHelper.FormatMoney(totalDue)
            If overdue.Count > 0 Then
                rpt.Status = StatusService.Overdue
                rpt.Note = overdue.Count & " quarter" & If(overdue.Count = 1, "", "s") & " overdue"
            ElseIf Not ledger.Any(Function(r) r.IsAssessed) Then
                rpt.Note = "no " & year & " assessment yet"
            ElseIf totalDue = 0D Then
                rpt.Status = StatusService.Paid : rpt.Note = "all " & year & " quarters paid"
            Else
                Dim nextDue = ledger.SelectMany(Function(r) r.Quarters).Where(Function(q) Not q.IsPaid).OrderBy(Function(q) q.DueDate).First()
                rpt.Status = nextDue.Status : rpt.Note = "due " & nextDue.DueDate.ToString("MMM d")
            End If
        End If
        rpt.Detail = If(ledger.Count = 0, "No real property on record",
                        "Tax clearance: " & RptService.GetTaxClearanceStatus(id) &
                        If(overdue.Count > 0, " · " & UiHelper.FormatMoney(RptService.GetOverdueAmount(id)) & " overdue", ""))
        Dim yearQuarters = ledger.SelectMany(Function(r) r.Quarters).ToList()
        If yearQuarters.Count > 0 Then
            SetProgress(rpt, yearQuarters.Where(Function(q) q.IsPaid).Count(), yearQuarters.Count, year & " quarters paid")
        End If
        list.Add(rpt)

        ' Health Certificates: same numbers as the module's summary cards
        Dim hc As New ModuleSummary With {.Screen = AppScreen.HealthCertificates, .Title = "Health Certificates"}
        Dim roster = HealthCertificateService.GetRoster(id)
        Dim sum = HealthCertificateService.GetSummary(roster)
        hc.PendingCount = HealthCertificateService.GetRenewalList(roster).Count
        If sum.Total = 0 Then
            hc.Value = "No staff" : hc.Note = "add employees first"
            hc.Detail = "No personnel on record"
        Else
            hc.Value = sum.Valid & " of " & sum.Total & " valid"
            hc.Status = If(sum.Expired + sum.NoRecord > 0, StatusService.Expired,
                           If(sum.ExpiringSoon > 0, StatusService.ExpiringSoon, StatusService.Valid))
            hc.Note = sum.CompliancePercent & "% compliant"
            hc.Detail = sum.ExpiringSoon & " expiring soon · " & (sum.Expired + sum.NoRecord) & " expired"
        End If
        If sum.Total > 0 Then SetProgress(hc, sum.Valid, sum.Total, "Valid health certificates")
        list.Add(hc)

        ' Annual Inspection: the current inspection
        Dim ins As New ModuleSummary With {.Screen = AppScreen.AnnualInspections, .Title = "Annual Inspection"}
        Dim inspection = InspectionService.GetCurrentInspection(InspectionService.GetInspections(id))
        If inspection Is Nothing Then
            ins.Value = "None" : ins.Note = "no inspection yet" : ins.Detail = "No inspection scheduled"
        Else
            Dim items = InspectionService.GetItems(inspection)
            ins.Value = inspection.ReferenceNo : ins.Status = inspection.Status
            ins.Note = InspectionService.GetEndorsementText(items)
            ins.PendingCount = If(InspectionService.IsFinal(inspection.Status), 0, 1)
            Dim visit = InspectionService.GetNextVisit(inspection)
            ins.Detail = If(inspection.Status = InspectionService.Completed AndAlso inspection.CertificateNo <> "",
                            "Certificate " & inspection.CertificateNo,
                            If(visit.HasValue, If(inspection.Status = InspectionService.ForReinspection, "Re-inspection ", "Visit ") &
                                               visit.Value.ToString("MMM d, yyyy"), "Next visit not set"))
        End If
        If inspection IsNot Nothing Then
            SetProgress(ins, InspectionService.GetPassedCount(InspectionService.GetItems(inspection)), InspectionDepartments.All.Length, "Departments passed")
        End If
        list.Add(ins)

        ' Construction Permit: the newest project (the one the module selects first)
        Dim cp As New ModuleSummary With {.Screen = AppScreen.ConstructionPermits, .Title = "Construction Permit"}
        Dim projects = ConstructionService.GetProjects(id)
        Dim project = projects.FirstOrDefault()
        cp.PendingCount = projects.AsEnumerable().Count(Function(p) Not ConstructionService.IsFinal(p.Status))
        If project Is Nothing Then
            cp.Value = "None" : cp.Note = "no construction project" : cp.Detail = "No projects filed"
        Else
            cp.Value = project.ReferenceNo : cp.Status = project.Status
            cp.Note = project.CurrentStage & If(project.CurrentStage = ConstructionService.StageCompleted, "", " stage")
            cp.Detail = "Clearances: " & ConstructionService.GetClearanceSummary(ConstructionService.GetClearances(project))
        End If
        If project IsNot Nothing AndAlso project.Status <> ConstructionService.Rejected Then
            Dim n = ConstructionService.Stages.Length
            Dim stageNo = Array.IndexOf(ConstructionService.Stages, project.CurrentStage)
            SetProgress(cp, If(project.CurrentStage = ConstructionService.StageCompleted, n, stageNo), n,
                        If(project.CurrentStage = ConstructionService.StageCompleted, "All " & n & " stages done",
                           "Stage " & (stageNo + 1) & " of " & n & " · " & project.CurrentStage))
        End If
        list.Add(cp)

        Return list
    End Function

    ''' <summary>Staff view: pending / open records per module across all the given businesses.</summary>
    Public Shared Function GetStaffSummaries(businesses As IEnumerable(Of Business)) As List(Of ModuleSummary)
        Dim bp As New ModuleSummary With {.Screen = AppScreen.BusinessPermits, .Title = "Business Permit"}
        Dim sp As New ModuleSummary With {.Screen = AppScreen.SanitaryPermits, .Title = "Sanitary Permit"}
        Dim rpt As New ModuleSummary With {.Screen = AppScreen.RealPropertyTax, .Title = "Real Property Tax"}
        Dim hc As New ModuleSummary With {.Screen = AppScreen.HealthCertificates, .Title = "Health Certificates"}
        Dim ins As New ModuleSummary With {.Screen = AppScreen.AnnualInspections, .Title = "Annual Inspection"}
        Dim cp As New ModuleSummary With {.Screen = AppScreen.ConstructionPermits, .Title = "Construction Permit"}

        Dim bpInForce = 0, spInForce = 0, rptOverdueBiz = 0, insCertified = 0, cpOnHold = 0
        Dim bizCount = 0, rptBiz = 0, cpApproved = 0, cpClearances = 0
        Dim rptOverdueAmount = 0D
        Dim hcTotal = 0, hcValid = 0, hcExpiring = 0, hcExpired = 0
        For Each b In businesses
            Dim id = b.BusinessId
            bizCount += 1
            ' Business permits: open applications (not Issued / Rejected) + permits in force
            Dim bpAll = BusinessPermitRepository.GetByBusinessId(id)
            bp.PendingCount += bpAll.AsEnumerable().Count(Function(p) Not BusinessPermitService.IsFinal(p.Status))
            Dim current = BusinessPermitService.GetCurrentPermit(id)
            If current IsNot Nothing AndAlso StatusService.GetExpiryStatus(current.ValidUntil) <> StatusService.Expired Then bpInForce += 1

            ' Sanitary permits
            Dim permits = SanitaryPermitService.GetPermits(id)
            sp.PendingCount += permits.AsEnumerable().Count(Function(p) Not SanitaryPermitService.IsFinal(p.Status))
            Dim spCurrent = SanitaryPermitService.GetCurrentPermit(permits)
            If spCurrent IsNot Nothing AndAlso SanitaryPermitService.GetDisplayStatus(spCurrent) <> StatusService.Expired Then spInForce += 1

            ' RPT: overdue quarters (all years)
            Dim overdue = RptService.GetOverdueQuarters(id)
            rpt.PendingCount += overdue.Count
            rptOverdueAmount += overdue.Sum(Function(q) q.Amount + q.Penalty)
            If overdue.Count > 0 Then rptOverdueBiz += 1
            If RptService.GetProperties(id).Count > 0 Then rptBiz += 1

            ' Health certificates
            Dim roster = HealthCertificateService.GetRoster(id)
            Dim sum = HealthCertificateService.GetSummary(roster)
            hc.PendingCount += HealthCertificateService.GetRenewalList(roster).Count
            hcTotal += sum.Total : hcValid += sum.Valid : hcExpiring += sum.ExpiringSoon : hcExpired += sum.Expired + sum.NoRecord

            ' Inspections: open (not Completed / Cancelled)
            Dim inspections = InspectionService.GetInspections(id)
            ins.PendingCount += inspections.AsEnumerable().Count(Function(x) Not InspectionService.IsFinal(x.Status))
            insCertified += inspections.AsEnumerable().Count(Function(x) x.Status = InspectionService.Completed AndAlso x.InspectionYear = Date.Today.Year)

            ' Construction: projects not Completed / Rejected
            Dim projects = ConstructionService.GetProjects(id)
            cp.PendingCount += projects.AsEnumerable().Count(Function(p) Not ConstructionService.IsFinal(p.Status))
            cpOnHold += projects.AsEnumerable().Count(Function(p) p.Status = ConstructionService.OnHold)
            For Each p In projects.Where(Function(x) Not ConstructionService.IsFinal(x.Status))
                Dim clearances = ConstructionService.GetClearances(p)
                cpClearances += clearances.Count
                cpApproved += clearances.AsEnumerable().Count(Function(c) c.Status = ConstructionService.Approved)
            Next
        Next

        bp.Value = bp.PendingCount & " pending" : bp.Status = If(bp.PendingCount > 0, "Pending", "")
        bp.Note = "applications" : bp.Detail = bpInForce & " Mayor's permit" & If(bpInForce = 1, "", "s") & " in force"
        sp.Value = sp.PendingCount & " pending" : sp.Status = If(sp.PendingCount > 0, "Pending", "")
        sp.Note = "applications" : sp.Detail = spInForce & " sanitary permit" & If(spInForce = 1, "", "s") & " in force"
        rpt.Value = UiHelper.FormatMoney(rptOverdueAmount) : rpt.Status = If(rpt.PendingCount > 0, StatusService.Overdue, StatusService.Paid)
        rpt.Note = If(rpt.PendingCount > 0, rpt.PendingCount & " quarter" & If(rpt.PendingCount = 1, "", "s") & " overdue", "nothing overdue")
        rpt.Detail = rptOverdueBiz & " business" & If(rptOverdueBiz = 1, "", "es") & " without tax clearance"
        hc.Value = hc.PendingCount & " to renew"
        hc.Status = If(hcExpired > 0, StatusService.Expired, If(hcExpiring > 0, StatusService.ExpiringSoon, StatusService.Valid))
        hc.Note = If(hcTotal = 0, "no personnel", CInt(Math.Round(hcValid * 100D / hcTotal, MidpointRounding.AwayFromZero)) & "% compliant")
        hc.Detail = hcValid & " of " & hcTotal & " valid · " & hcExpiring & " expiring · " & hcExpired & " expired"
        ins.Value = ins.PendingCount & " open" : ins.Status = If(ins.PendingCount > 0, "Pending", "")
        ins.Note = "inspections" : ins.Detail = insCertified & " certified this year"
        cp.Value = cp.PendingCount & " active" : cp.Status = If(cp.PendingCount > 0, ConstructionService.Active, "")
        cp.Note = "projects" : cp.Detail = cpOnHold & " on hold"
        SetProgress(bp, bpInForce, bizCount, "Businesses with a permit in force", bpInForce & " of " & bizCount)
        SetProgress(sp, spInForce, bizCount, "Businesses with a sanitary permit", spInForce & " of " & bizCount)
        If rptBiz > 0 Then SetProgress(rpt, rptBiz - rptOverdueBiz, rptBiz, "Businesses with tax clearance", (rptBiz - rptOverdueBiz) & " of " & rptBiz)
        If hcTotal > 0 Then SetProgress(hc, hcValid, hcTotal, "Valid health certificates")
        SetProgress(ins, insCertified, bizCount, "Businesses certified " & Date.Today.Year, insCertified & " of " & bizCount)
        If cpClearances > 0 Then SetProgress(cp, cpApproved, cpClearances, "Clearances approved", cpApproved & " of " & cpClearances)
        Return New List(Of ModuleSummary) From {bp, sp, rpt, hc, ins, cp}
    End Function

End Class
