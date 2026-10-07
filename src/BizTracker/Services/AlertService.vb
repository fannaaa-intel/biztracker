''' <summary>What one GenerateAlerts run did.</summary>
Public Class AlertRunResult
    ''' <summary>New alerts added.</summary>
    Public Property Added As Integer
    ''' <summary>Existing alerts whose text/priority changed (e.g. "expires soon" became "expired").</summary>
    Public Property Updated As Integer
    ''' <summary>Unread alerts marked read because the problem is gone (e.g. the certificate was renewed).</summary>
    Public Property Resolved As Integer
End Class

''' <summary>
''' Notifications (the bell):
'''   - GenerateAlerts() runs after login: it scans every due / expiry date of the businesses the user may see
'''     and saves one alert per item and due date (the UNIQUE key prevents duplicates).
'''       Urgent  = already expired / overdue
'''       Warning = expires / falls due within expiry_warning_days, or a re-inspection is coming
'''       Info    = an annual inspection is scheduled within expiry_warning_days
'''   - Owners see only their business; staff see the alerts of the modules their role can open.
''' Every date-based status comes from StatusService / the module services (never stored).
''' </summary>
Public NotInheritable Class AlertService

    Private Sub New()
    End Sub

    Public Const Urgent As String = "Urgent"
    Public Const Warning As String = "Warning"
    Public Const Info As String = "Info"

    ''' <summary>Which screen handles each notifications.module value.</summary>
    Public Shared Function ScreenFor(moduleName As String) As AppScreen
        Select Case moduleName
            Case ModuleNames.BusinessPermit : Return AppScreen.BusinessPermits
            Case ModuleNames.SanitaryPermit : Return AppScreen.SanitaryPermits
            Case ModuleNames.HealthCertificate : Return AppScreen.HealthCertificates
            Case ModuleNames.RealPropertyTax : Return AppScreen.RealPropertyTax
            Case ModuleNames.AnnualInspection : Return AppScreen.AnnualInspections
            Case Else : Return AppScreen.ConstructionPermits
        End Select
    End Function

    ' ==================== Building the alerts ====================

    ''' <summary>The alerts one business should have today (not saved). Empty if the user may not see it.</summary>
    Public Shared Function BuildAlerts(b As Business) As List(Of Notification)
        Dim list As New List(Of Notification)
        If b Is Nothing OrElse Not Session.IsLoggedIn OrElse Not AccessService.CanSeeBusiness(b.BusinessId) Then Return list
        Dim id = b.BusinessId
        Dim warnDays = StatusService.WarningDays

        ' Health certificates: expiring within the warning days, or expired
        For Each r In HealthCertificateService.GetRoster(id)
            If r.Certificate Is Nothing Then Continue For      ' no date to alert on (shown on the dashboard instead)
            Dim status = r.Status
            If status = StatusService.Valid Then Continue For
            Dim expired = status = StatusService.Expired
            list.Add(NewAlert(id, ModuleNames.HealthCertificate, r.Certificate.CertId, If(expired, Urgent, Warning),
                              r.Employee.FullName & ": health certificate " & If(expired, "expired", "expires soon") &
                              " (" & r.Certificate.CertificateNo & ")", r.Certificate.ExpiryDate))
        Next

        ' Mayor's permit in force
        Dim permit = BusinessPermitService.GetCurrentPermit(id)
        If permit IsNot Nothing AndAlso permit.ValidUntil.HasValue Then
            Dim status = StatusService.GetExpiryStatus(permit.ValidUntil)
            If status <> StatusService.Valid Then
                Dim expired = status = StatusService.Expired
                list.Add(NewAlert(id, ModuleNames.BusinessPermit, permit.PermitId, If(expired, Urgent, Warning),
                                  "Mayor's permit " & If(permit.MayorsPermitNo <> "", permit.MayorsPermitNo, permit.ReferenceNo) &
                                  If(expired, " expired", " expires soon") & " - renew it", permit.ValidUntil.Value))
            End If
        End If

        ' Sanitary permit in force
        Dim sanitary = SanitaryPermitService.GetCurrentPermit(SanitaryPermitService.GetPermits(id))
        If sanitary IsNot Nothing AndAlso sanitary.ValidUntil.HasValue Then
            Dim status = SanitaryPermitService.GetDisplayStatus(sanitary)
            If status <> StatusService.Valid Then
                Dim expired = status = StatusService.Expired
                list.Add(NewAlert(id, ModuleNames.SanitaryPermit, sanitary.SanitaryId, If(expired, Urgent, Warning),
                                  "Sanitary permit " & sanitary.PermitNo & If(expired, " expired", " expires soon") & " - renew it",
                                  sanitary.ValidUntil.Value))
            End If
        End If

        ' Real property tax quarters: overdue, or due within the warning days
        For Each q In RptService.GetAllQuarters(id)
            If q.IsPaid Then Continue For
            If q.IsOverdue Then
                list.Add(NewAlert(id, ModuleNames.RealPropertyTax, q.AssessmentId, Urgent,
                                  "Real property tax " & q.Label & " is overdue (PIN " & q.Pin & ")", q.DueDate))
            ElseIf q.Status = StatusService.DueSoon Then
                list.Add(NewAlert(id, ModuleNames.RealPropertyTax, q.AssessmentId, Warning,
                                  "Real property tax " & q.Label & " is due soon (PIN " & q.Pin & ")", q.DueDate))
            End If
        Next

        ' Annual inspection: the next visit (schedule or re-inspection) within the warning days, or already passed
        Dim inspection = InspectionService.GetCurrentInspection(InspectionService.GetInspections(id))
        Dim visit = InspectionService.GetNextVisit(inspection)
        If visit.HasValue Then
            Dim days = StatusService.DaysUntil(visit.Value)
            Dim reinspection = inspection.Status = InspectionService.ForReinspection
            Dim what = If(reinspection, "Re-inspection ", "Annual inspection ") & inspection.ReferenceNo
            If days < 0 Then
                list.Add(NewAlert(id, ModuleNames.AnnualInspection, inspection.InspectionId, Warning,
                                  what & ": the visit date has passed - record the results or reschedule", visit.Value))
            ElseIf days <= warnDays Then
                list.Add(NewAlert(id, ModuleNames.AnnualInspection, inspection.InspectionId, If(reinspection, Warning, Info),
                                  what & " is scheduled", visit.Value))
            End If
        End If

        Return list
    End Function

    Private Shared Function NewAlert(businessId As Integer, moduleName As String, relatedId As Integer,
                                     priority As String, message As String, dueDate As Date) As Notification
        Return New Notification With {
            .BusinessId = businessId, .ModuleName = moduleName, .RelatedId = relatedId,
            .Priority = priority, .DueDate = dueDate.Date,
            .Message = If(message.Length > 255, message.Substring(0, 255), message)
        }
    End Function

    ''' <summary>Same alert = same business, module, record and due date (the table's UNIQUE key).</summary>
    Private Shared Function KeyOf(n As Notification) As String
        Return n.BusinessId & "|" & n.ModuleName & "|" & n.RelatedId & "|" & n.DueDate.ToString("yyyy-MM-dd")
    End Function

    ''' <summary>
    ''' Saves today's alerts for every business the user may see (run after login). Running it again adds
    ''' nothing new. Unread alerts whose problem is gone are marked read.
    ''' </summary>
    Public Shared Function GenerateAlerts() As AlertRunResult
        Dim result As New AlertRunResult
        If Not Session.IsLoggedIn Then Return result
        Dim businesses = If(Session.IsOwner,
                            BusinessRepository.GetAll().Where(Function(b) AccessService.CanSeeBusiness(b.BusinessId)).ToList(),
                            BusinessRepository.GetAll())
        For Each b In businesses
            Dim current = BuildAlerts(b)
            Dim existing = NotificationRepository.GetByBusinessId(b.BusinessId).
                           GroupBy(Function(n) KeyOf(n)).ToDictionary(Function(g) g.Key, Function(g) g.First())
            For Each n In current
                Dim old As Notification = Nothing
                If Not existing.TryGetValue(KeyOf(n), old) Then
                    If NotificationRepository.Upsert(n) >= 0 Then result.Added += 1
                ElseIf old.Priority <> n.Priority OrElse old.Message <> n.Message Then
                    If NotificationRepository.Upsert(n) >= 0 Then result.Updated += 1
                End If
            Next
            ' Resolved: still unread, but no longer in today's list
            Dim keys = New HashSet(Of String)(current.Select(Function(n) KeyOf(n)))
            For Each old In NotificationRepository.GetByBusinessId(b.BusinessId, unreadOnly:=True)
                If Not keys.Contains(KeyOf(old)) AndAlso NotificationRepository.MarkRead(old.NotificationId) Then result.Resolved += 1
            Next
        Next
        Return result
    End Function

    ' ==================== Reading (bell) ====================

    ''' <summary>True if the logged-in user may see this alert (own business for owners, own modules for staff).</summary>
    Public Shared Function CanSee(n As Notification) As Boolean
        If n Is Nothing OrElse Not Session.IsLoggedIn Then Return False
        If Not AccessService.CanSeeBusiness(n.BusinessId) Then Return False
        Return AccessService.CanAccess(ScreenFor(n.ModuleName))
    End Function

    ''' <summary>The user's alerts: unread first, then Urgent / Warning / Info, then by due date.</summary>
    Public Shared Function GetNotifications(Optional unreadOnly As Boolean = False) As List(Of Notification)
        If Not Session.IsLoggedIn Then Return New List(Of Notification)
        Dim all = If(Session.IsOwner,
                     If(Session.BusinessId.HasValue, NotificationRepository.GetByBusinessId(Session.BusinessId.Value, unreadOnly), New List(Of Notification)()),
                     NotificationRepository.GetAll(unreadOnly))
        Return all.Where(Function(n) CanSee(n)).ToList()
    End Function

    ''' <summary>Number on the bell.</summary>
    Public Shared Function GetUnreadCount() As Integer
        Return GetNotifications(unreadOnly:=True).Count
    End Function

    ''' <summary>Unread Urgent alerts (for the summary after login).</summary>
    Public Shared Function GetUrgentUnread() As List(Of Notification)
        Return GetNotifications(unreadOnly:=True).Where(Function(n) n.Priority = Urgent).ToList()
    End Function

    ''' <summary>The message of the pop-up after login (Nothing when there is nothing urgent).</summary>
    Public Shared Function GetUrgentSummary(Optional maxLines As Integer = 5) As String
        Dim urgentList = GetUrgentUnread()
        If urgentList.Count = 0 Then Return Nothing
        Dim lines = urgentList.Take(maxLines).Select(Function(n) ChrW(&H2022) & "  " & n.Message &
                                                                 If(Session.IsOwner, "", "  (" & n.BusinessName & ")")).ToList()
        If urgentList.Count > maxLines Then lines.Add("…and " & (urgentList.Count - maxLines) & " more.")
        Return String.Join(vbCrLf, lines) & vbCrLf & vbCrLf & "Open the notifications to see every alert and go to the module that handles it."
    End Function

    ''' <summary>
    ''' The urgent alerts as separate items for the pop-up after login (at most maxLines):
    '''   Headline : "Real property tax Q2 2026 is overdue"
    '''   Detail   : "PIN 015-06-014-02-007 · Kusina ni Aling Rosa · 99 days ago"
    ''' </summary>
    Public Shared Function GetUrgentItems(Optional maxLines As Integer = 5) As List(Of KeyValuePair(Of String, String))
        Return GetUrgentUnread().Take(maxLines).Select(
            Function(n) New KeyValuePair(Of String, String)(
                Headline(n), DetailText(n, withModule:=False) & " · " & DashboardService.ShortDaysText(StatusService.DaysUntil(n.DueDate)))).ToList()
    End Function

    ''' <summary>The message without its "(reference)" ending: "Noel Ramos: health certificate expired".</summary>
    Public Shared Function Headline(n As Notification) As String
        Dim msg = If(n.Message, "")
        Dim i = msg.LastIndexOf(" (", StringComparison.Ordinal)
        Return If(msg.EndsWith(")") AndAlso i > 0, msg.Substring(0, i), msg)
    End Function

    ''' <summary>The "(reference)" at the end of the message without brackets: "HC-2025-00040" (empty if none).</summary>
    Public Shared Function Reference(n As Notification) As String
        Dim msg = If(n.Message, "")
        Dim i = msg.LastIndexOf(" (", StringComparison.Ordinal)
        Return If(msg.EndsWith(")") AndAlso i > 0, msg.Substring(i + 2, msg.Length - i - 3), "")
    End Function

    ''' <summary>
    ''' Second line under the headline: reference, then business (staff) or module (owner).
    ''' "PIN 015-06-014-02-007 · Kusina ni Aling Rosa"
    ''' withModule adds the module for staff too: "HC-2025-00040 · Kusina ni Aling Rosa · Health Certificate".
    ''' </summary>
    Public Shared Function DetailText(n As Notification, Optional withModule As Boolean = True) As String
        Dim parts As New List(Of String)
        Dim ref = Reference(n)
        If ref <> "" Then parts.Add(ref)
        If Not Session.IsOwner AndAlso n.BusinessName <> "" Then parts.Add(n.BusinessName)
        If withModule OrElse Session.IsOwner Then parts.Add(n.ModuleName)
        Return String.Join(" · ", parts)
    End Function

    ''' <summary>Last line of the pop-up after login (also used when there are more alerts than shown).</summary>
    Public Shared Function GetUrgentFooter(Optional maxLines As Integer = 5) As String
        Dim more = GetUrgentUnread().Count - maxLines
        Return If(more > 0, "…and " & more & " more. ", "") & "Open the notifications to see every alert and go to the module that handles it."
    End Function

    ''' <summary>"N urgent alerts need attention"</summary>
    Public Shared Function GetUrgentTitle(count As Integer) As String
        Return count & If(count = 1, " urgent alert needs", " urgent alerts need") & " attention"
    End Function

    ''' <summary>"Oct 1, 2026 · 5 days ago" / "Oct 26, 2026 · in 20 days" / "today".</summary>
    Public Shared Function DueText(n As Notification) As String
        Return UiHelper.FormatDate(n.DueDate) & " · " & DashboardService.ShortDaysText(StatusService.DaysUntil(n.DueDate))
    End Function

    ' ==================== Marking read ====================

    Public Shared Function MarkRead(n As Notification) As Boolean
        If Not CanSee(n) Then Return False
        Return NotificationRepository.MarkRead(n.NotificationId)
    End Function

    ''' <summary>Marks every alert the user can see as read. Returns how many were changed.</summary>
    Public Shared Function MarkAllRead() As Integer
        Dim count = 0
        For Each n In GetNotifications(unreadOnly:=True)
            If NotificationRepository.MarkRead(n.NotificationId) Then count += 1
        Next
        Return count
    End Function

End Class
