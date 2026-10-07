''' <summary>
''' Annual Inspection module (same structure as BusinessPermitView / SanitaryPermitView).
'''   Toolbar  : business selector (staff) or the owner's business, and the actions (Inspector / Admin only)
'''   Cards    : next visit, departments passed ("3 of 4"), open deficiencies, certificate
'''   Left     : inspections grid with search
'''   Right    : selected inspection - progress tracker and tabs (Checklist | Deficiencies | Certificate)
''' No SQL here: everything goes through InspectionService / repositories.
''' </summary>
Public Class AnnualInspectionView
    Inherits ModuleView

    ''' <summary>Grid row (display only).</summary>
    Private Class InspectionRow
        Public Property InspectionId As Integer
        Public Property ReferenceNo As String
        Public Property Year As Integer
        Public Property Schedule As String
        Public Property Status As String
        Public Property Passed As String
        Public Property Result As String
    End Class

    ' Toolbar
    Private ReadOnly toolbar As New ModuleToolbar("inspection")
    Private btnSchedule As Button
    Private btnRecord As Button
    Private btnDate As Button
    Private btnCancel As Button

    ' Summary cards
    Private ReadOnly cardVisit As New StatCard(Icons.Calendar, "Next Visit")
    Private ReadOnly cardDepts As New StatCard(Icons.CheckShield, "Departments")
    Private ReadOnly cardDeficiency As New StatCard(Icons.Warning, "Deficiencies")
    Private ReadOnly cardCertificate As New StatCard(Icons.Document, "Certificate")

    ' List
    Private ReadOnly txtSearch As New TextBox()
    Private ReadOnly grid As New ModernGrid()
    Private ReadOnly lblEmpty As New Label()

    ' Details
    Private ReadOnly detailHeader As New Panel()
    Private ReadOnly lblDetailRef As New Label()
    Private ReadOnly badgeDetail As New StatusBadge()
    Private ReadOnly lblDetailInfo As New Label()
    Private ReadOnly tracker As New StepTracker()
    Private ReadOnly tabs As New SegmentedTabs()
    Private ReadOnly checklist As New WheelScrollPanel()
    Private ReadOnly deficiencyList As New WheelScrollPanel()
    Private ReadOnly certificateList As New WheelScrollPanel()
    Private ReadOnly detailBody As New Panel()
    Private ReadOnly detailContent As New Panel()
    Private ReadOnly lblDetailEmpty As New Label()
    Private ReadOnly tips As New ToolTip()

    ' State
    Private inspections As New List(Of Inspection)
    Private itemsById As New Dictionary(Of Integer, List(Of InspectionItem))
    Private selected As Inspection
    Private loading As Boolean
    Private ReadOnly canManage As Boolean = AccessService.CanManage(AppScreen.AnnualInspections)

    Private ReadOnly Property Business As Business
        Get
            Return toolbar.Business
        End Get
    End Property

    Private ReadOnly Property SelectedItems As List(Of InspectionItem)
        Get
            If selected Is Nothing OrElse Not itemsById.ContainsKey(selected.InspectionId) Then Return New List(Of InspectionItem)
            Return itemsById(selected.InspectionId)
        End Get
    End Property

    Public Sub New()
        SuspendLayout()
        Dim root As New TableLayoutPanel With {
            .Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 3, .BackColor = Theme.ContentBackground,
            .Margin = New Padding(0), .Padding = New Padding(0)
        }
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 56))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 110))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))

        btnSchedule = toolbar.AddAction("Schedule Inspection", "Schedule", "primary", AddressOf Schedule_Click, canManage)
        btnRecord = toolbar.AddAction("Record Result", "Record", "secondary", AddressOf Record_Click, canManage)
        btnDate = toolbar.AddAction("Reschedule", "Date", "secondary", AddressOf Date_Click, canManage)
        btnCancel = toolbar.AddAction("Cancel Inspection", "Cancel", "secondary", AddressOf CancelInspection_Click, canManage)
        AddHandler toolbar.BusinessChanged, Sub()
                                                txtSearch.Text = ""
                                                LoadInspections(Nothing)
                                            End Sub

        root.Controls.Add(toolbar, 0, 0)
        root.Controls.Add(BuildCards(), 0, 1)
        root.Controls.Add(BuildMain(), 0, 2)
        Controls.Add(root)
        ResumeLayout(False)
    End Sub

    Public Overrides ReadOnly Property Subtitle As String
        Get
            Return "Joint inspection schedule, department checklist, re-inspection and certificate"
        End Get
    End Property

    ' =====================================================================
    '  Layout
    ' =====================================================================

    Private Function BuildCards() As Control
        Dim cards = {cardVisit, cardDepts, cardDeficiency, cardCertificate}
        Dim row As New TableLayoutPanel With {
            .Dock = DockStyle.Fill, .ColumnCount = cards.Length, .RowCount = 1, .BackColor = Theme.ContentBackground,
            .Margin = New Padding(0), .Padding = New Padding(0)
        }
        row.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        For i = 0 To cards.Length - 1
            row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F / cards.Length))
            cards(i).Dock = DockStyle.Fill
            cards(i).Margin = New Padding(If(i = 0, 0, 8), 0, If(i = cards.Length - 1, 0, 8), 14)
            row.Controls.Add(cards(i), i, 0)
        Next
        Return row
    End Function

    Private Function BuildMain() As Control
        Dim row As New TableLayoutPanel With {
            .Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 1, .BackColor = Theme.ContentBackground,
            .Margin = New Padding(0), .Padding = New Padding(0)
        }
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        row.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        row.Controls.Add(BuildListCard(), 0, 0)
        row.Controls.Add(BuildDetailCard(), 1, 0)
        Return row
    End Function

    Private Function BuildListCard() As Control
        Dim card As New RoundedPanel With {.Dock = DockStyle.Fill, .Margin = New Padding(0, 0, 8, 0), .Padding = New Padding(16, 14, 16, 16)}

        Dim header As New Panel With {.Dock = DockStyle.Top, .Height = Dpi(48), .BackColor = Color.White}
        Dim title As New Label With {.Text = "Inspections", .Font = Theme.SubtitleFont, .ForeColor = Theme.TextDark,
                                     .AutoSize = True, .Location = New Point(0, Dpi(10)), .BackColor = Color.White}
        Dim searchBox = UiHelper.CreateInputBox(txtSearch, "Search…", Icons.Search)
        searchBox.Height = Dpi(38)
        header.Controls.AddRange({title, searchBox})
        AddHandler header.Resize,
            Sub()
                Dim w = Math.Max(Dpi(120), Math.Min(Dpi(240), header.ClientSize.Width - title.Width - Dpi(16)))
                searchBox.SetBounds(header.ClientSize.Width - w, Dpi(2), w, Dpi(38))
            End Sub
        AddHandler txtSearch.TextChanged, Sub() BindGrid(selected?.InspectionId)

        UiHelper.StyleGrid(grid, "Status")
        grid.Dock = DockStyle.Fill
        UiHelper.AddGridColumn(grid, "ReferenceNo", "REFERENCE", "ReferenceNo", 130, Dpi(125))
        UiHelper.AddGridColumn(grid, "Year", "YEAR", "Year", 60, Dpi(55))
        UiHelper.AddGridColumn(grid, "Schedule", "SCHEDULE", "Schedule", 130, Dpi(130))
        UiHelper.AddGridColumn(grid, "Status", "STATUS", "Status", 140, Dpi(140))
        UiHelper.AddGridColumn(grid, "Passed", "PASSED", "Passed", 75, Dpi(75))
        UiHelper.AddGridColumn(grid, "Result", "RESULT", "Result", 80, Dpi(80))
        AddHandler grid.SelectionChanged, Sub() If Not loading Then ShowSelected()
        AddHandler grid.CellDoubleClick, Sub(s, e) If e.RowIndex >= 0 AndAlso canManage AndAlso btnRecord.Enabled Then Record_Click(Nothing, EventArgs.Empty)
        AddHandler grid.Resize, Sub() FitGridColumns()

        lblEmpty.Dock = DockStyle.Fill
        lblEmpty.TextAlign = ContentAlignment.MiddleCenter
        lblEmpty.Font = Theme.BodyFont
        lblEmpty.ForeColor = Theme.TextMuted
        lblEmpty.BackColor = Color.White
        lblEmpty.Visible = False

        card.Controls.Add(lblEmpty)
        card.Controls.Add(grid)
        card.Controls.Add(header)
        Return card
    End Function

    ''' <summary>Hides the less important columns when the grid is narrow (reference and status always stay).</summary>
    Private Sub FitGridColumns()
        grid.Columns("Year").Visible = grid.Width >= Dpi(360)
        grid.Columns("Passed").Visible = grid.Width >= Dpi(450)
        grid.Columns("Result").Visible = grid.Width >= Dpi(550)
        grid.Columns("Schedule").Visible = grid.Width >= Dpi(700)
    End Sub

    Private Function BuildDetailCard() As Control
        Dim card As New RoundedPanel With {.Dock = DockStyle.Fill, .Margin = New Padding(8, 0, 0, 0), .Padding = New Padding(18, 14, 18, 16)}

        detailHeader.Dock = DockStyle.Top
        detailHeader.Height = Dpi(52)
        detailHeader.BackColor = Color.White
        lblDetailRef.Font = Theme.SubtitleFont
        lblDetailRef.ForeColor = Theme.TextDark
        lblDetailRef.AutoSize = True
        lblDetailRef.Location = New Point(0, Dpi(2))
        lblDetailRef.BackColor = Color.White
        badgeDetail.Height = Dpi(24)
        lblDetailInfo.Font = Theme.SmallFont
        lblDetailInfo.ForeColor = Theme.TextMuted
        lblDetailInfo.AutoEllipsis = True
        lblDetailInfo.BackColor = Color.White
        detailHeader.Controls.AddRange({lblDetailRef, badgeDetail, lblDetailInfo})
        AddHandler detailHeader.Resize, Sub()
                                            lblDetailInfo.SetBounds(0, Dpi(30), detailHeader.ClientSize.Width, Dpi(20))
                                            PlaceBadge()
                                        End Sub

        tracker.Dock = DockStyle.Top
        tracker.Height = 70
        tracker.Steps = InspectionService.TrackerSteps
        Dim gap1 As New Panel With {.Dock = DockStyle.Top, .Height = 6, .BackColor = Color.White}

        tabs.Dock = DockStyle.Top
        tabs.Tabs = {"Checklist", "Deficiencies", "Certificate"}
        AddHandler tabs.SelectedIndexChanged, Sub() ShowTab()
        Dim gap2 As New Panel With {.Dock = DockStyle.Top, .Height = 8, .BackColor = Color.White}

        detailBody.Dock = DockStyle.Fill
        detailBody.BackColor = Color.White
        For Each list In {checklist, deficiencyList, certificateList}
            list.Dock = DockStyle.Fill
            list.BackColor = Color.White
        Next
        detailBody.Controls.AddRange({checklist, deficiencyList, certificateList})

        lblDetailEmpty.Dock = DockStyle.Fill
        lblDetailEmpty.TextAlign = ContentAlignment.MiddleCenter
        lblDetailEmpty.Font = Theme.BodyFont
        lblDetailEmpty.ForeColor = Theme.TextMuted
        lblDetailEmpty.BackColor = Color.White

        detailContent.Dock = DockStyle.Fill
        detailContent.BackColor = Color.White
        detailContent.Controls.Add(detailBody)
        detailContent.Controls.Add(gap2)
        detailContent.Controls.Add(tabs)
        detailContent.Controls.Add(gap1)
        detailContent.Controls.Add(tracker)
        detailContent.Controls.Add(detailHeader)
        card.Controls.Add(lblDetailEmpty)
        card.Controls.Add(detailContent)
        Return card
    End Function

    ''' <summary>The status badge sits right of the reference, or is hidden if it would not fit.</summary>
    Private Sub PlaceBadge()
        badgeDetail.Location = New Point(lblDetailRef.Right + Dpi(10), Dpi(4))
        badgeDetail.Visible = selected IsNot Nothing AndAlso badgeDetail.Right <= detailHeader.ClientSize.Width
    End Sub

    ' =====================================================================
    '  Loading data
    ' =====================================================================

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        toolbar.LoadBusinesses()
    End Sub

    Public Overrides Sub RefreshData()
        LoadInspections(selected?.InspectionId)
    End Sub

    Private Sub LoadInspections(selectId As Integer?)
        inspections = If(Business Is Nothing, New List(Of Inspection)(), InspectionService.GetInspections(Business.BusinessId))
        itemsById = inspections.ToDictionary(Function(x) x.InspectionId, Function(x) InspectionService.GetItems(x))
        UpdateCards()
        BindGrid(selectId)
    End Sub

    Private Sub BindGrid(selectId As Integer?)
        Dim term = txtSearch.Text.Trim().ToLowerInvariant()
        Dim rows = inspections.
            Where(Function(x) term = "" OrElse
                              (x.ReferenceNo & " " & x.InspectionYear & " " & x.Status & " " & x.OverallResult & " " & x.CertificateNo).ToLowerInvariant().Contains(term)).
            Select(Function(x) New InspectionRow With {
                .InspectionId = x.InspectionId, .ReferenceNo = x.ReferenceNo, .Year = x.InspectionYear,
                .Schedule = x.ScheduleDate.ToString("MMM d · h:mm tt"), .Status = x.Status,
                .Passed = InspectionService.GetPassedCount(itemsById(x.InspectionId)) & " of 4",
                .Result = x.OverallResult
            }).ToList()

        loading = True
        grid.DataSource = rows
        FitGridColumns()
        Dim index = If(selectId.HasValue, rows.FindIndex(Function(r) r.InspectionId = selectId.Value), -1)
        If index < 0 AndAlso rows.Count > 0 Then index = 0
        grid.ClearSelection()
        If index >= 0 Then
            grid.CurrentCell = grid.Rows(index).Cells("ReferenceNo")
            grid.Rows(index).Selected = True
        End If
        loading = False

        lblEmpty.Visible = rows.Count = 0
        If rows.Count = 0 Then
            lblEmpty.Text = If(Business Is Nothing, "No business selected.",
                               If(term <> "", "No inspections match your search.",
                                  If(canManage, "No inspections yet." & vbCrLf & "Click ""Schedule Inspection"" to set one.",
                                     "No annual inspection on record yet." & vbCrLf & "The Joint Inspection Team schedules it.")))
            lblEmpty.BringToFront()
        End If
        ShowSelected()
    End Sub

    Private Sub ShowSelected()
        Dim row = If(grid.SelectedRows.Count > 0, TryCast(grid.SelectedRows(0).DataBoundItem, InspectionRow), Nothing)
        selected = If(row Is Nothing, Nothing, inspections.FirstOrDefault(Function(x) x.InspectionId = row.InspectionId))
        ShowDetails()
        UpdateButtons()
    End Sub

    ' =====================================================================
    '  Cards, buttons and details
    ' =====================================================================

    Private Sub UpdateCards()
        If Business Is Nothing Then
            For Each c In {cardVisit, cardDepts, cardDeficiency, cardCertificate}
                c.SetValue("—", "", "No business selected")
            Next
            Return
        End If
        Dim current = InspectionService.GetCurrentInspection(inspections)
        If current Is Nothing Then
            cardVisit.SetValue("None", "", "no inspection scheduled")
            cardDepts.SetValue("—", "", "no inspection yet")
            cardDeficiency.SetValue("None", "", "no inspection yet")
            cardCertificate.SetValue("None", "", "no inspection yet")
            Return
        End If
        Dim items = itemsById(current.InspectionId)

        ' Next visit
        Dim visit = InspectionService.GetNextVisit(current)
        If current.Status = InspectionService.Completed Then
            cardVisit.SetValue("Done", current.Status, "certified")
        ElseIf Not visit.HasValue Then
            cardVisit.SetValue("Not set", "Re-inspection", "set a date")
        Else
            Dim days = StatusService.DaysUntil(visit.Value)
            ' Short badge so the "in N days" note fits on a narrow card
            cardVisit.SetValue(visit.Value.ToString("MMM d, yyyy"),
                               If(current.Status = InspectionService.ForReinspection, "Re-inspection", current.Status),
                               If(days = 0, "today", If(days > 0, "in " & days & " day" & If(days = 1, "", "s"), Math.Abs(days) & " days ago")))
        End If

        ' Departments passed
        Dim passed = InspectionService.GetPassedCount(items)
        Dim failed = items.Where(Function(x) x.Result = InspectionService.Failed).ToList()
        cardDepts.SetValue(passed & " of 4 passed",
                           If(passed = 4, InspectionService.Passed, If(failed.Count > 0, InspectionService.Failed, InspectionService.Pending)),
                           If(failed.Count > 0, String.Join(", ", failed.Select(Function(x) x.Department)) & " failed",
                              If(passed = 4, "all clear", (4 - passed) & " pending")))

        ' Open deficiencies
        If failed.Count = 0 Then
            cardDeficiency.SetValue("None", If(passed = 4, InspectionService.Passed, ""), "nothing failed")
        Else
            cardDeficiency.SetValue(failed.Count & " open", InspectionService.Failed, "to re-inspect")
        End If

        ' Certificate
        If InspectionService.GetCertificateBlocker(current, items) Is Nothing Then
            cardCertificate.SetValue(current.CertificateNo, "Issued", "valid for " & current.InspectionYear)
        Else
            cardCertificate.SetValue("Locked", "", "all 4 must pass")
        End If
    End Sub

    Private Sub UpdateButtons()
        If Not canManage Then Return
        Dim hasBusiness = Business IsNot Nothing
        Dim suggested = SuggestedDate()
        Dim scheduleBlocker = If(Not hasBusiness, "No business selected.",
                                 If(suggested.HasValue, Nothing, InspectionService.GetScheduleBlocker(Business.BusinessId, Date.Today.Year)))
        btnSchedule.Enabled = scheduleBlocker Is Nothing
        toolbar.SetTip(btnSchedule, If(scheduleBlocker, "Schedule the annual inspection of this business"))

        Dim recordBlocker = InspectionService.GetRecordBlocker(selected)
        btnRecord.Enabled = recordBlocker Is Nothing
        toolbar.SetTip(btnRecord, If(recordBlocker, "Record a department's result (Passed / Failed)"))

        Dim dateTip As String
        If selected Is Nothing Then
            dateTip = "Select an inspection first."
        ElseIf selected.Status = InspectionService.Scheduled Then
            dateTip = Nothing
            toolbar.SetTip(btnDate, "Move the inspection to another date or time")
        ElseIf selected.Status = InspectionService.ForReinspection Then
            dateTip = Nothing
            toolbar.SetTip(btnDate, "Set the re-inspection date")
        Else
            dateTip = "Only a Scheduled inspection or one For Re-inspection can get a new date."
        End If
        btnDate.Enabled = dateTip Is Nothing
        If dateTip IsNot Nothing Then toolbar.SetTip(btnDate, dateTip)

        btnCancel.Enabled = selected IsNot Nothing AndAlso Not InspectionService.IsFinal(selected.Status)
        toolbar.SetTip(btnCancel, If(selected Is Nothing, "Select an inspection first.",
                                     If(btnCancel.Enabled, "Cancel this inspection (a reason is required)",
                                        "This inspection is already " & selected.Status & ".")))
    End Sub

    ''' <summary>
    ''' Default date for a new inspection: next week if this year has none, January next year if this year's
    ''' is completed, or Nothing when an inspection is still open.
    ''' </summary>
    Private Function SuggestedDate() As Date?
        If Business Is Nothing Then Return Nothing
        Dim thisYear = Date.Today.Year
        If InspectionService.GetScheduleBlocker(Business.BusinessId, thisYear) Is Nothing Then Return Date.Today.AddDays(7).AddHours(9)
        Dim open = inspections.Any(Function(x) Not InspectionService.IsFinal(x.Status))
        If open OrElse InspectionService.GetScheduleBlocker(Business.BusinessId, thisYear + 1) IsNot Nothing Then Return Nothing
        Return New Date(thisYear + 1, 1, 15, 9, 0, 0)
    End Function

    Private Sub ShowDetails()
        Dim has = selected IsNot Nothing
        lblDetailEmpty.Visible = Not has
        detailContent.Visible = has
        If Not has Then
            lblDetailEmpty.Text = If(inspections.Count = 0, "Nothing to show yet.", "Select an inspection to see its checklist.")
            Return
        End If

        lblDetailRef.Text = selected.ReferenceNo
        badgeDetail.Text = selected.Status
        PlaceBadge()
        Dim info = selected.InspectionYear & " · scheduled " & selected.ScheduleDate.ToString("MMM d, yyyy h:mm tt")
        If selected.ReinspectionDate.HasValue AndAlso selected.Status = InspectionService.ForReinspection Then
            info &= " · re-inspection " & selected.ReinspectionDate.Value.ToString("MMM d, h:mm tt")
        End If
        lblDetailInfo.Text = info
        lblDetailInfo.ForeColor = If(selected.Status = InspectionService.Cancelled, Theme.StatusRed, Theme.TextMuted)
        tips.SetToolTip(lblDetailInfo, info)
        tracker.CurrentStep = InspectionService.GetTrackerStep(selected.Status)

        checklist.SetRows(CreateChecklistRows())
        deficiencyList.SetRows(CreateDeficiencyRows())
        certificateList.SetRows(CreateCertificateRows())
        ShowTab()
    End Sub

    Private Sub ShowTab()
        checklist.Visible = tabs.SelectedIndex = 0
        deficiencyList.Visible = tabs.SelectedIndex = 1
        certificateList.Visible = tabs.SelectedIndex = 2
        detailBody.PerformLayout()
    End Sub

    ' ---------- Checklist tab ----------

    Private Function CreateChecklistRows() As List(Of Control)
        Dim rows As New List(Of Control)
        Dim items = SelectedItems
        Dim recordBlocker = InspectionService.GetRecordBlocker(selected)
        For Each item In items
            Dim dept = item.Department
            Dim detail As String
            If item.Result = InspectionService.Pending Then
                detail = "Not yet inspected"
            Else
                detail = If(item.InspectorName <> "", item.InspectorName, "Inspector not recorded") &
                         If(item.InspectedAt.HasValue, " · " & item.InspectedAt.Value.ToString("MMM d"), "")
            End If
            Dim links As New List(Of RowLink)
            If canManage AndAlso recordBlocker Is Nothing AndAlso item.Result <> InspectionService.Passed Then
                links.Add(New RowLink(If(item.Result = InspectionService.Failed, "Re-inspect", "Record"), Theme.SidebarBlue,
                                      Sub() OpenResultDialog(dept)))
            End If
            rows.Add(ListRow(tips, dept, detail, item.Result, links, Nothing, If(canManage, LinkColumnWidth("Re-inspect"), 0)))
        Next
        Dim passed = InspectionService.GetPassedCount(items)
        If selected.Status = InspectionService.Completed Then
            rows.Add(NoteRow(tips, "All 4 departments passed. Certificate " & selected.CertificateNo & " is issued.", Theme.StatusGreen))
        ElseIf selected.Status = InspectionService.Cancelled Then
            rows.Add(NoteRow(tips, "This inspection was cancelled.", Theme.StatusRed))
        ElseIf recordBlocker IsNot Nothing Then
            rows.Add(NoteRow(tips, recordBlocker, Theme.TextMuted))
        ElseIf items.Any(Function(x) x.Result = InspectionService.Failed) Then
            rows.Add(NoteRow(tips, passed & " of 4 passed. Failed departments must pass at re-inspection before the certificate is issued.", Theme.StatusRed))
        Else
            rows.Add(NoteRow(tips, passed & " of 4 passed. " & If(canManage, "Record each department's result as it is inspected.",
                                                                  "Results appear here as each department inspects."), Theme.TextMuted))
        End If
        Return rows
    End Function

    ' ---------- Deficiencies tab ----------

    Private Function CreateDeficiencyRows() As List(Of Control)
        Dim rows As New List(Of Control)
        Dim failed = SelectedItems.Where(Function(x) x.Result = InspectionService.Failed).ToList()
        If failed.Count = 0 Then
            Dim fixedAny = SelectedItems.Any(Function(x) x.Findings.Contains("Earlier ("))
            rows.Add(NoteRow(tips, If(selected.Status = InspectionService.Completed,
                                      "No open deficiencies." & If(fixedAny, " Earlier deficiencies were corrected and passed re-inspection.", ""),
                                      "No deficiencies recorded so far."), If(selected.Status = InspectionService.Completed, Theme.StatusGreen, Theme.TextMuted)))
        End If
        For Each item In failed
            rows.Add(HeadingRow(item.Department & " · " & If(item.InspectorName <> "", item.InspectorName, "inspector not recorded")))
            Dim findings As New Label With {
                .Text = item.Findings, .Height = Dpi(66), .Font = Theme.BodyFont, .ForeColor = Theme.TextDark,
                .BackColor = Color.White, .AutoEllipsis = True
            }
            tips.SetToolTip(findings, item.Findings)
            rows.Add(findings)
        Next
        If selected.Status = InspectionService.ForReinspection Then
            Dim links As New List(Of RowLink)
            If canManage Then links.Add(New RowLink(If(selected.ReinspectionDate.HasValue, "Change", "Set date"), Theme.SidebarBlue,
                                                    Sub() Date_Click(Nothing, EventArgs.Empty)))
            Dim visitAt = selected.ReinspectionDate
            rows.Add(ListRow(tips, "Re-inspection",
                             If(visitAt.HasValue, visitAt.Value.ToString("ddd, MMM d, yyyy h:mm tt"), "Not scheduled yet"),
                             If(visitAt.HasValue, InspectionService.Scheduled, InspectionService.Pending), links))
        End If
        Return rows
    End Function

    ' ---------- Certificate tab ----------

    Private Function CreateCertificateRows() As List(Of Control)
        Dim rows As New List(Of Control)
        Dim items = SelectedItems
        Dim blocker = InspectionService.GetCertificateBlocker(selected, items)
        If blocker Is Nothing Then
            Dim completedOn = items.Where(Function(x) x.InspectedAt.HasValue).Select(Function(x) x.InspectedAt.Value).DefaultIfEmpty(selected.ScheduleDate).Max()
            rows.Add(InfoRow(tips, "Certificate no.", selected.CertificateNo, "Overall result", selected.OverallResult))
            rows.Add(InfoRow(tips, "Inspection year", selected.InspectionYear.ToString(), "Completed on", UiHelper.FormatDate(completedOn)))
            rows.Add(NoteRow(tips, "All 4 departments passed. The Annual Inspection Certificate is valid for " & selected.InspectionYear & ".", Theme.StatusGreen))
        Else
            rows.Add(ListRow(tips, "Annual Inspection Certificate", blocker, "Locked"))
            For Each item In items.Where(Function(x) x.Result <> InspectionService.Passed)
                rows.Add(ListRow(tips, item.Department, If(item.Result = InspectionService.Failed, "Must pass at re-inspection", "Awaiting inspection"), item.Result))
            Next
        End If
        Return rows
    End Function

    ' =====================================================================
    '  Actions
    ' =====================================================================

    Private Sub Schedule_Click(sender As Object, e As EventArgs)
        If Business Is Nothing OrElse Not canManage Then Return
        Dim suggested = SuggestedDate()
        If Not suggested.HasValue Then
            UiHelper.ShowWarning(InspectionService.GetScheduleBlocker(Business.BusinessId, Date.Today.Year))
            Return
        End If
        Using dlg As New ScheduleInspectionDialog(ScheduleInspectionDialog.DialogMode.NewInspection, Business, suggestedDate:=suggested.Value)
            If dlg.ShowDialog(FindForm()) <> DialogResult.OK Then Return
            LoadInspections(dlg.InspectionId)
        End Using
        If selected Is Nothing Then Return
        UiHelper.ShowSuccess("Inspection " & selected.ReferenceNo & " is scheduled on " &
                             selected.ScheduleDate.ToString("ddd, MMM d, yyyy 'at' h:mm tt") & "." & vbCrLf &
                             "Next: on that day, record each department's result with ""Record Result"".", "Inspection scheduled")
    End Sub

    Private Sub Record_Click(sender As Object, e As EventArgs)
        If selected Is Nothing Then Return
        Dim first = InspectionService.GetRecordableItems(SelectedItems).FirstOrDefault()
        OpenResultDialog(If(first?.Department, ""))
    End Sub

    Private Sub OpenResultDialog(department As String)
        If selected Is Nothing OrElse Not canManage Then Return
        Dim current = InspectionRepository.GetById(selected.InspectionId)
        Dim blocker = InspectionService.GetRecordBlocker(current)
        If blocker IsNot Nothing Then
            UiHelper.ShowWarning(blocker)
            Return
        End If
        Dim outcome As InspectionResultOutcome
        Dim recorded As String
        Using dlg As New InspectionResultDialog(current, InspectionRepository.GetItems(current.InspectionId), department)
            If dlg.ShowDialog(FindForm()) <> DialogResult.OK Then Return
            outcome = dlg.Outcome
            recorded = dlg.Department
        End Using
        LoadInspections(current.InspectionId)
        If selected Is Nothing Then Return

        Dim items = SelectedItems
        Dim result = items.First(Function(x) x.Department = recorded).Result
        Dim message As String
        Select Case outcome.NewStatus
            Case InspectionService.Completed
                message = "All 4 departments passed. " & selected.ReferenceNo & " is completed and certificate " & outcome.CertificateNo & " was issued."
                tabs.SelectedIndex = 2
            Case InspectionService.ForReinspection
                message = recorded & " " & result.ToLowerInvariant() & ". " & selected.ReferenceNo & " is now For Re-inspection (" &
                          InspectionService.GetEndorsementText(items) & ")."
            Case Else
                message = recorded & " " & result.ToLowerInvariant() & " (" & InspectionService.GetEndorsementText(items) & ")." & vbCrLf &
                          "Next: record the remaining departments."
        End Select
        If outcome.EndorsedReference IsNot Nothing Then
            message &= vbCrLf & "The Fire endorsement of " & outcome.EndorsedReference & " is now Endorsed."
        End If
        UiHelper.ShowSuccess(message, If(result = InspectionService.Passed, "Result saved", "Deficiency recorded"))

        ' After a failure, offer to set the re-inspection date right away
        If outcome.NewStatus = InspectionService.ForReinspection AndAlso Not selected.ReinspectionDate.HasValue AndAlso
           UiHelper.Confirm("Set the re-inspection date now? The business should correct the deficiencies first.",
                            "Schedule re-inspection", "Set Date", "Later") Then
            Date_Click(Nothing, EventArgs.Empty)
        End If
    End Sub

    Private Sub Date_Click(sender As Object, e As EventArgs)
        If selected Is Nothing OrElse Business Is Nothing OrElse Not canManage Then Return
        Dim current = InspectionRepository.GetById(selected.InspectionId)
        Dim mode As ScheduleInspectionDialog.DialogMode
        If current.Status = InspectionService.Scheduled Then
            mode = ScheduleInspectionDialog.DialogMode.Reschedule
        ElseIf current.Status = InspectionService.ForReinspection Then
            mode = ScheduleInspectionDialog.DialogMode.Reinspection
        Else
            UiHelper.ShowWarning("Only a Scheduled inspection or one For Re-inspection can get a new date.")
            Return
        End If
        Dim failed = InspectionRepository.GetItems(current.InspectionId).
                     Where(Function(x) x.Result = InspectionService.Failed).Select(Function(x) x.Department).ToList()
        Using dlg As New ScheduleInspectionDialog(mode, Business, current, failed)
            If dlg.ShowDialog(FindForm()) <> DialogResult.OK Then Return
        End Using
        LoadInspections(current.InspectionId)
        If selected Is Nothing Then Return
        If mode = ScheduleInspectionDialog.DialogMode.Reinspection Then
            tabs.SelectedIndex = 1
            UiHelper.ShowSuccess("Re-inspection set for " & selected.ReinspectionDate.Value.ToString("ddd, MMM d, yyyy 'at' h:mm tt") & "." & vbCrLf &
                                 "Next: record the result of " & String.Join(", ", failed) & " at the re-inspection.", "Re-inspection scheduled")
        Else
            UiHelper.ShowSuccess("New date saved: " & selected.ScheduleDate.ToString("ddd, MMM d, yyyy 'at' h:mm tt") & ".", "Inspection rescheduled")
        End If
    End Sub

    Private Sub CancelInspection_Click(sender As Object, e As EventArgs)
        If selected Is Nothing OrElse Not canManage Then Return
        Dim current = InspectionRepository.GetById(selected.InspectionId)
        Dim reason = PromptDialog.Ask(FindForm(), "Cancel Inspection", current.ReferenceNo & "  ·  " & current.BusinessName,
                                      "Reason for cancelling", True, "Cancel Inspection", "danger")
        If reason Is Nothing Then Return
        Dim problem = InspectionService.Cancel(current, reason)
        If problem IsNot Nothing Then
            UiHelper.ShowWarning(problem)
            Return
        End If
        LoadInspections(current.InspectionId)
        UiHelper.ShowSuccess(current.ReferenceNo & " was cancelled." & vbCrLf &
                             "You can now schedule a new inspection for this business.", "Inspection cancelled")
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then tips.Dispose()
        MyBase.Dispose(disposing)
    End Sub

End Class
