''' <summary>
''' Sanitary Permits module (same structure as BusinessPermitView).
'''   Toolbar  : business selector (staff) or the owner's business, and the actions
'''   Cards    : current sanitary permit, latest application, staff health cards (synced from Health Certificates)
'''   Left     : applications grid with search
'''   Right    : selected application - workflow tracker and tabs (Prerequisites | Inspection | Health Cards)
''' No SQL here: everything goes through SanitaryPermitService / repositories.
''' </summary>
Public Class SanitaryPermitView
    Inherits ModuleView

    ''' <summary>Grid row (display only).</summary>
    Private Class PermitRow
        Public Property SanitaryId As Integer
        Public Property PermitNo As String
        Public Property Year As Integer
        Public Property Category As String
        Public Property Status As String
        Public Property Filed As String
        Public Property Score As String
    End Class

    ' Toolbar
    Private ReadOnly toolbar As New ModuleToolbar("sanitary")
    Private btnNew As Button
    Private btnRenew As Button
    Private btnView As Button
    Private btnInspect As Button
    Private btnStatus As Button

    ' Summary cards
    Private ReadOnly cardCurrent As New StatCard(Icons.CheckShield, "Current Sanitary Permit")
    Private ReadOnly cardLatest As New StatCard(Icons.Calendar, "Latest Application")
    Private ReadOnly cardHealth As New StatCard(Icons.Health, "Staff Health Cards")

    ' Applications list
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
    Private ReadOnly requirements As New RequirementsPanel()
    Private ReadOnly inspectionList As New WheelScrollPanel()
    Private ReadOnly healthList As New WheelScrollPanel()
    Private ReadOnly detailBody As New Panel()
    Private ReadOnly detailContent As New Panel()
    Private ReadOnly lblDetailEmpty As New Label()
    Private ReadOnly tips As New ToolTip()

    ' State
    Private permits As New List(Of SanitaryPermit)
    Private selected As SanitaryPermit
    Private sync As New HealthCardSync
    Private loading As Boolean
    Private ReadOnly canManage As Boolean = AccessService.CanManage(AppScreen.SanitaryPermits)

    Private ReadOnly Property Business As Business
        Get
            Return toolbar.Business
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

        btnNew = toolbar.AddAction("New Application", "New", "primary", AddressOf New_Click)
        btnRenew = toolbar.AddAction("Renew Permit", "Renew", "secondary", AddressOf Renew_Click)
        btnView = toolbar.AddAction(If(canManage, "View / Edit", "View Details"), "View", "secondary", AddressOf View_Click)
        btnInspect = toolbar.AddAction("Record Inspection", "Inspect", "secondary", AddressOf Inspect_Click, canManage)
        btnStatus = toolbar.AddAction("Update Status", "Status", "secondary", AddressOf Status_Click, canManage)
        AddHandler toolbar.BusinessChanged, Sub()
                                                txtSearch.Text = ""
                                                LoadPermits(Nothing)
                                            End Sub

        root.Controls.Add(toolbar, 0, 0)
        root.Controls.Add(BuildCards(), 0, 1)
        root.Controls.Add(BuildMain(), 0, 2)
        Controls.Add(root)
        ResumeLayout(False)
    End Sub

    Public Overrides ReadOnly Property Subtitle As String
        Get
            Return "Sanitary permit applications, laboratory prerequisites and inspection"
        End Get
    End Property

    ' =====================================================================
    '  Layout
    ' =====================================================================

    Private Function BuildCards() As Control
        Dim cards = {cardCurrent, cardLatest, cardHealth}
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
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 46))
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 54))
        row.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        row.Controls.Add(BuildListCard(), 0, 0)
        row.Controls.Add(BuildDetailCard(), 1, 0)
        Return row
    End Function

    Private Function BuildListCard() As Control
        Dim card As New RoundedPanel With {.Dock = DockStyle.Fill, .Margin = New Padding(0, 0, 8, 0), .Padding = New Padding(16, 14, 16, 16)}

        Dim header As New Panel With {.Dock = DockStyle.Top, .Height = 48, .BackColor = Color.White}
        Dim title As New Label With {.Text = "Applications", .Font = Theme.SubtitleFont, .ForeColor = Theme.TextDark,
                                     .AutoSize = True, .Location = New Point(0, 10), .BackColor = Color.White}
        Dim searchBox = UiHelper.CreateInputBox(txtSearch, "Search…", Icons.Search)
        searchBox.Height = 38
        header.Controls.AddRange({title, searchBox})
        AddHandler header.Resize,
            Sub()
                Dim w = Math.Max(120, Math.Min(240, header.ClientSize.Width - title.Width - 16))
                searchBox.SetBounds(header.ClientSize.Width - w, 2, w, 38)
            End Sub
        AddHandler txtSearch.TextChanged, Sub() BindGrid(selected?.SanitaryId)

        UiHelper.StyleGrid(grid, "Status")
        grid.Dock = DockStyle.Fill
        UiHelper.AddGridColumn(grid, "PermitNo", "PERMIT NO.", "PermitNo", 130, 125)
        UiHelper.AddGridColumn(grid, "Year", "YEAR", "Year", 60, 55)
        UiHelper.AddGridColumn(grid, "Category", "CATEGORY", "Category", 90, 90)
        UiHelper.AddGridColumn(grid, "Status", "STATUS", "Status", 130, 130)
        UiHelper.AddGridColumn(grid, "Score", "SCORE", "Score", 65, 65)
        UiHelper.AddGridColumn(grid, "Filed", "FILED", "Filed", 110, 110)
        AddHandler grid.SelectionChanged, Sub() If Not loading Then ShowSelected()
        AddHandler grid.CellDoubleClick, Sub(s, e) If e.RowIndex >= 0 Then View_Click(Nothing, EventArgs.Empty)
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

    ''' <summary>Hides the less important columns when the grid is narrow.</summary>
    Private Sub FitGridColumns()
        Dim s = DeviceDpi / 96.0
        grid.Columns("Year").Visible = grid.Width >= 340 * s
        grid.Columns("Category").Visible = grid.Width >= 440 * s
        grid.Columns("Score").Visible = grid.Width >= 520 * s
        grid.Columns("Filed").Visible = grid.Width >= 640 * s
    End Sub

    Private Function BuildDetailCard() As Control
        Dim card As New RoundedPanel With {.Dock = DockStyle.Fill, .Margin = New Padding(8, 0, 0, 0), .Padding = New Padding(18, 14, 18, 16)}

        detailHeader.Dock = DockStyle.Top
        detailHeader.Height = 52
        detailHeader.BackColor = Color.White
        lblDetailRef.Font = Theme.SubtitleFont
        lblDetailRef.ForeColor = Theme.TextDark
        lblDetailRef.AutoSize = True
        lblDetailRef.Location = New Point(0, 2)
        lblDetailRef.BackColor = Color.White
        badgeDetail.Height = 24
        lblDetailInfo.Font = Theme.SmallFont
        lblDetailInfo.ForeColor = Theme.TextMuted
        lblDetailInfo.AutoEllipsis = True
        lblDetailInfo.BackColor = Color.White
        detailHeader.Controls.AddRange({lblDetailRef, badgeDetail, lblDetailInfo})
        AddHandler detailHeader.Resize, Sub() lblDetailInfo.SetBounds(0, 30, detailHeader.ClientSize.Width, 20)

        tracker.Dock = DockStyle.Top
        tracker.Height = 70
        tracker.Steps = SanitaryPermitService.TrackerSteps
        Dim gap1 As New Panel With {.Dock = DockStyle.Top, .Height = 6, .BackColor = Color.White}

        tabs.Dock = DockStyle.Top
        tabs.Tabs = {"Prerequisites", "Inspection", "Health Cards"}
        AddHandler tabs.SelectedIndexChanged, Sub() ShowTab()
        Dim gap2 As New Panel With {.Dock = DockStyle.Top, .Height = 8, .BackColor = Color.White}

        detailBody.Dock = DockStyle.Fill
        detailBody.BackColor = Color.White
        requirements.Dock = DockStyle.Fill
        For Each list In {inspectionList, healthList}
            list.Dock = DockStyle.Fill
            list.BackColor = Color.White
        Next
        detailBody.Controls.AddRange({requirements, inspectionList, healthList})
        AddHandler requirements.RequirementsChanged, Sub() ReloadKeepingSelection()

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

    ' =====================================================================
    '  Loading data
    ' =====================================================================

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        toolbar.LoadBusinesses()
    End Sub

    Public Overrides Sub RefreshData()
        ReloadKeepingSelection()
    End Sub

    Private Sub LoadPermits(selectId As Integer?)
        permits = If(Business Is Nothing, New List(Of SanitaryPermit)(), SanitaryPermitService.GetPermits(Business.BusinessId))
        sync = If(Business Is Nothing, New HealthCardSync(), SanitaryPermitService.GetHealthCardSync(Business.BusinessId))
        UpdateCards()
        BindGrid(selectId)
    End Sub

    Private Sub ReloadKeepingSelection()
        LoadPermits(selected?.SanitaryId)
    End Sub

    Private Sub BindGrid(selectId As Integer?)
        Dim term = txtSearch.Text.Trim().ToLowerInvariant()
        Dim rows = permits.
            Where(Function(p) term = "" OrElse
                              (p.PermitNo & " " & p.PermitYear & " " & p.Category & " " & p.Status).ToLowerInvariant().Contains(term)).
            Select(Function(p) New PermitRow With {
                .SanitaryId = p.SanitaryId, .PermitNo = p.PermitNo, .Year = p.PermitYear, .Category = p.Category,
                .Status = p.Status, .Filed = UiHelper.FormatDate(p.DateFiled),
                .Score = If(p.InspectionScore.HasValue, p.InspectionScore.Value.ToString(), "—")
            }).ToList()

        loading = True
        grid.DataSource = rows
        FitGridColumns()
        Dim index = If(selectId.HasValue, rows.FindIndex(Function(r) r.SanitaryId = selectId.Value), -1)
        If index < 0 AndAlso rows.Count > 0 Then index = 0
        grid.ClearSelection()
        If index >= 0 Then
            grid.CurrentCell = grid.Rows(index).Cells("PermitNo")
            grid.Rows(index).Selected = True
        End If
        loading = False

        lblEmpty.Visible = rows.Count = 0
        If rows.Count = 0 Then
            lblEmpty.Text = If(Business Is Nothing, "No business selected.",
                               If(term <> "", "No applications match your search.",
                                  "No sanitary permit applications yet." & vbCrLf & "Click ""New Application"" to apply."))
            lblEmpty.BringToFront()
        End If
        ShowSelected()
    End Sub

    Private Sub ShowSelected()
        Dim row = If(grid.SelectedRows.Count > 0, TryCast(grid.SelectedRows(0).DataBoundItem, PermitRow), Nothing)
        selected = If(row Is Nothing, Nothing, permits.FirstOrDefault(Function(p) p.SanitaryId = row.SanitaryId))
        ShowDetails()
        UpdateButtons()
    End Sub

    ' =====================================================================
    '  Cards, buttons and details
    ' =====================================================================

    Private Sub UpdateCards()
        If Business Is Nothing Then
            For Each c In {cardCurrent, cardLatest, cardHealth}
                c.SetValue("—", "", "No business selected")
            Next
            Return
        End If

        Dim current = SanitaryPermitService.GetCurrentPermit(permits)
        If current Is Nothing Then
            cardCurrent.SetValue("None", "", "No sanitary permit issued yet")
        Else
            Dim days = StatusService.DaysUntil(current.ValidUntil.Value)
            cardCurrent.SetValue(current.PermitNo, SanitaryPermitService.GetDisplayStatus(current),
                                 If(days >= 0, "until " & UiHelper.FormatDate(current.ValidUntil) & " (" & days & " days)",
                                    "expired " & UiHelper.FormatDate(current.ValidUntil)))
        End If

        Dim latest = permits.FirstOrDefault()
        If latest Is Nothing Then
            cardLatest.SetValue("None", "", "No applications yet")
        Else
            cardLatest.SetValue(latest.PermitNo, latest.Status, latest.Category & " · " & latest.PermitYear)
        End If

        If sync.Total = 0 Then
            cardHealth.SetValue("No staff", "", "add employees in Health Certificates")
        Else
            Dim notCovered = sync.FoodHandlersNotCovered.Count + sync.OthersNotCovered.Count
            cardHealth.SetValue(sync.Valid & " of " & sync.Total & " valid",
                                If(sync.FoodHandlersNotCovered.Count > 0, "Expired", If(notCovered > 0 OrElse sync.ExpiringSoon > 0, "Expiring Soon", "Valid")),
                                If(notCovered > 0, notCovered & " to renew", If(sync.ExpiringSoon > 0, sync.ExpiringSoon & " expiring soon", "all staff covered")))
        End If
    End Sub

    Private Sub UpdateButtons()
        Dim hasBusiness = Business IsNot Nothing
        Dim canFile = hasBusiness AndAlso (Session.IsOwner OrElse canManage)
        Dim blocker = If(hasBusiness, SanitaryPermitService.GetFilingBlocker(Business.BusinessId), "No business selected.")
        Dim hasPrevious = permits.Any(Function(p) p.Status <> SanitaryPermitService.Rejected)

        btnNew.Enabled = canFile AndAlso Not hasPrevious AndAlso blocker Is Nothing
        toolbar.SetTip(btnNew, If(hasPrevious, "This business already has a sanitary permit - use Renew Permit.",
                                  If(blocker, "Apply for a sanitary permit for " & Date.Today.Year)))
        btnRenew.Enabled = canFile AndAlso hasPrevious AndAlso blocker Is Nothing
        toolbar.SetTip(btnRenew, If(Not hasPrevious, "No previous permit to renew - use New Application.",
                                    If(blocker, "File the " & Date.Today.Year & " renewal")))

        btnView.Enabled = selected IsNot Nothing
        btnInspect.Enabled = selected IsNot Nothing AndAlso selected.Status = SanitaryPermitService.ForInspection
        toolbar.SetTip(btnInspect, If(btnInspect.Enabled, "Record the on-site inspection score and findings",
                                      "Inspections are recorded when the application is For Inspection"))
        btnStatus.Enabled = selected IsNot Nothing AndAlso Not SanitaryPermitService.IsFinal(selected.Status)
    End Sub

    Private Sub ShowDetails()
        Dim has = selected IsNot Nothing
        lblDetailEmpty.Visible = Not has
        detailContent.Visible = has
        If Not has Then
            lblDetailEmpty.Text = If(permits.Count = 0, "Nothing to show yet.", "Select an application to see its progress.")
            requirements.ShowMessage("")
            Return
        End If

        lblDetailRef.Text = selected.PermitNo
        badgeDetail.Text = selected.Status
        badgeDetail.Location = New Point(lblDetailRef.Right + 10, 4)
        Dim info = selected.Category & " · " & selected.PermitYear & " · filed " & UiHelper.FormatDate(selected.DateFiled)
        If selected.Status = SanitaryPermitService.Issued Then
            info &= " · valid until " & UiHelper.FormatDate(selected.ValidUntil) & " (" & SanitaryPermitService.GetDisplayStatus(selected) & ")"
        End If
        lblDetailInfo.Text = info
        lblDetailInfo.ForeColor = If(selected.Status = SanitaryPermitService.Rejected, Theme.StatusRed, Theme.TextMuted)
        tips.SetToolTip(lblDetailInfo, info)
        tracker.CurrentStep = SanitaryPermitService.GetTrackerStep(selected.Status)

        Dim isFinal = SanitaryPermitService.IsFinal(selected.Status)
        requirements.LoadFor(selected.BusinessId, ModuleNames.SanitaryPermit, selected.SanitaryId, AppScreen.SanitaryPermits,
                             allowUpload:=Not isFinal AndAlso (Session.IsOwner OrElse canManage),
                             allowVerify:=Not isFinal)
        inspectionList.SetRows(CreateInspectionRows(selected))
        healthList.SetRows(CreateHealthRows())
        ShowTab()
    End Sub

    Private Sub ShowTab()
        requirements.Visible = tabs.SelectedIndex = 0
        inspectionList.Visible = tabs.SelectedIndex = 1
        healthList.Visible = tabs.SelectedIndex = 2
        detailBody.PerformLayout()
        requirements.PerformLayout()
    End Sub

    ' ---------- Inspection tab ----------

    Private Function CreateInspectionRows(p As SanitaryPermit) As List(Of Control)
        Dim rows As New List(Of Control)
        Dim passing = SanitaryPermitService.PassingScore
        If Not p.InspectionScore.HasValue Then
            Dim hint = If(p.Status = SanitaryPermitService.ForInspection,
                          If(canManage, "Ready for the on-site inspection. Click ""Record Inspection"" to enter the score.",
                             "The on-site inspection will be scheduled by the Municipal Health Office."),
                          If(p.Status = SanitaryPermitService.Rejected, "This application was rejected.",
                             "The on-site inspection comes after the laboratory analysis."))
            rows.Add(NoteRow(tips, "Not yet inspected. " & hint, Theme.TextMuted))
        Else
            Dim passed = p.InspectionScore.Value >= passing
            rows.Add(InfoRow(tips, "Inspection score", p.InspectionScore.Value & " / 100", "Result",
                             If(passed, "Passed", "Failed") & " (passing score " & passing & ")"))
            rows.Add(InfoRow(tips, "Inspection date", UiHelper.FormatDate(p.InspectionDate), "Sanitary inspector",
                             If(p.InspectorName <> "", p.InspectorName, "—")))
            If Not passed AndAlso Not SanitaryPermitService.IsFinal(p.Status) Then
                rows.Add(NoteRow(tips, "Below the passing score. Correct the deficiencies and record a re-inspection.", Theme.StatusRed))
            End If
        End If
        If p.Findings <> "" Then
            rows.Add(HeadingRow("Findings"))
            Dim findings As New Label With {
                .Text = p.Findings, .Height = 66, .Font = Theme.BodyFont, .ForeColor = Theme.TextDark,
                .BackColor = Color.White, .AutoEllipsis = True
            }
            tips.SetToolTip(findings, p.Findings)
            rows.Add(findings)
        End If
        If p.Status = SanitaryPermitService.Issued Then
            rows.Add(InfoRow(tips, "Date issued", UiHelper.FormatDate(p.DateIssued), "Valid until", UiHelper.FormatDate(p.ValidUntil)))
        End If
        Return rows
    End Function

    ' ---------- Health cards tab ----------

    Private Function CreateHealthRows() As List(Of Control)
        Dim rows As New List(Of Control)
        If sync.Total = 0 Then
            rows.Add(NoteRow(tips, "No employees on record. Staff are managed in Health Certificates.", Theme.TextMuted))
            Return rows
        End If
        Dim notCovered = sync.FoodHandlersNotCovered.Count + sync.OthersNotCovered.Count
        rows.Add(ListRow(tips, "Staff health certificates", sync.Valid & " of " & sync.Total & " employees have a valid certificate" &
                         If(sync.ExpiringSoon > 0, " (" & sync.ExpiringSoon & " expiring soon)", ""),
                         If(notCovered = 0, "Valid", If(sync.FoodHandlersNotCovered.Count > 0, "Expired", "Expiring Soon"))))
        If sync.FoodHandlersNotCovered.Count > 0 Then
            rows.Add(NoteRow(tips, "The permit cannot be issued until every food handler has a valid health certificate.", Theme.StatusRed))
            For Each staffName In sync.FoodHandlersNotCovered
                rows.Add(ListRow(tips, staffName, "Food handler · no valid health certificate", "Expired"))
            Next
        End If
        For Each staffName In sync.OthersNotCovered
            rows.Add(ListRow(tips, staffName, "Non-food staff · no valid health certificate", "Expired"))
        Next
        If notCovered = 0 Then rows.Add(NoteRow(tips, "All employees are covered by a valid health certificate.", Theme.StatusGreen))
        Return rows
    End Function

    ' =====================================================================
    '  Actions
    ' =====================================================================

    Private Sub New_Click(sender As Object, e As EventArgs)
        If Business Is Nothing Then Return
        OpenDialog(New SanitaryPermitDialog(SanitaryPermitDialog.DialogMode.NewApplication, Business), True)
    End Sub

    Private Sub Renew_Click(sender As Object, e As EventArgs)
        If Business Is Nothing Then Return
        OpenDialog(New SanitaryPermitDialog(SanitaryPermitDialog.DialogMode.Renewal, Business), True)
    End Sub

    Private Sub View_Click(sender As Object, e As EventArgs)
        If selected Is Nothing OrElse Business Is Nothing Then Return
        Dim canEdit = canManage AndAlso Not SanitaryPermitService.IsFinal(selected.Status)
        OpenDialog(New SanitaryPermitDialog(If(canEdit, SanitaryPermitDialog.DialogMode.Edit, SanitaryPermitDialog.DialogMode.View),
                                            Business, SanitaryRepository.GetById(selected.SanitaryId)), False)
    End Sub

    Private Sub OpenDialog(dlg As SanitaryPermitDialog, isNew As Boolean)
        Using dlg
            If dlg.ShowDialog(FindForm()) <> DialogResult.OK Then Return
            LoadPermits(dlg.SanitaryId)
            If isNew AndAlso selected IsNot Nothing Then
                UiHelper.ShowSuccess("Application " & selected.PermitNo & " was submitted." & vbCrLf &
                                     "Next: upload the laboratory prerequisites in the Prerequisites tab.", "Application filed")
            End If
        End Using
    End Sub

    Private Sub Inspect_Click(sender As Object, e As EventArgs)
        If selected Is Nothing OrElse Not canManage Then Return
        Using dlg As New SanitaryInspectionDialog(SanitaryRepository.GetById(selected.SanitaryId))
            If dlg.ShowDialog(FindForm()) <> DialogResult.OK Then Return
        End Using
        ReloadKeepingSelection()
        tabs.SelectedIndex = 1
        UiHelper.ShowSuccess("The inspection result was saved." & vbCrLf &
                             If(selected.InspectionScore.GetValueOrDefault() >= SanitaryPermitService.PassingScore,
                                "Next: use Update Status to issue the permit.",
                                "The score is below passing. Record a re-inspection after corrections."), "Inspection recorded")
    End Sub

    Private Sub Status_Click(sender As Object, e As EventArgs)
        If selected Is Nothing OrElse Not canManage Then Return
        Dim p = SanitaryRepository.GetById(selected.SanitaryId)
        Dim nextStatus = SanitaryPermitService.GetNextStatus(p.Status)
        Using dlg As New WorkflowDialog("Update Status", p.PermitNo & "  ·  " & p.BusinessName, p.Status, nextStatus,
                                        SanitaryPermitService.GetAdvanceBlocker(p), SanitaryPermitService.GetReadyMessage(p),
                                        Function() SanitaryPermitService.Advance(p),
                                        Function(reason) SanitaryPermitService.Reject(p, reason), "Reject Application",
                                        If(nextStatus = SanitaryPermitService.Issued, "Issue Permit", ""))
            If dlg.ShowDialog(FindForm()) <> DialogResult.OK Then Return
        End Using
        ReloadKeepingSelection()
        If p.Status = SanitaryPermitService.Issued Then
            Dim endorsed = BusinessPermitService.GetLatestApplication(p.BusinessId)
            UiHelper.ShowSuccess("Sanitary permit " & p.PermitNo & " was issued." & vbCrLf &
                                 "Valid until " & UiHelper.FormatDate(p.ValidUntil) & "." &
                                 If(endorsed IsNot Nothing AndAlso Not BusinessPermitService.IsFinal(endorsed.Status),
                                    vbCrLf & "The Sanitary endorsement of " & endorsed.ReferenceNo & " is now Endorsed.", ""),
                                 "Permit issued")
        End If
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then tips.Dispose()
        MyBase.Dispose(disposing)
    End Sub

End Class
