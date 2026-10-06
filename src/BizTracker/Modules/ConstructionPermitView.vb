''' <summary>
''' Construction Permit module (same structure as BusinessPermitView / SanitaryPermitView).
'''   Toolbar  : business selector (staff) or the owner's business, and the actions
'''   Cards    : current stage, clearances approved, technical documents verified, estimated cost
'''   Left     : projects grid with search
'''   Right    : selected project - stage pipeline and tabs (Clearances | Documents | Details)
''' No SQL here: everything goes through ConstructionService / repositories.
''' </summary>
Public Class ConstructionPermitView
    Inherits ModuleView

    ''' <summary>Grid row (display only).</summary>
    Private Class ProjectRow
        Public Property ProjectId As Integer
        Public Property ReferenceNo As String
        Public Property Title As String
        Public Property Stage As String
        Public Property Status As String
        Public Property Filed As String
    End Class

    ' Toolbar
    Private ReadOnly toolbar As New ModuleToolbar("construction")
    Private btnNew As Button
    Private btnView As Button
    Private btnAdvance As Button
    Private btnHold As Button

    ' Summary cards
    Private ReadOnly cardStage As New StatCard(Icons.Build, "Current Stage")
    Private ReadOnly cardClearances As New StatCard(Icons.CheckShield, "Clearances")
    Private ReadOnly cardDocuments As New StatCard(Icons.Document, "Technical Documents")
    Private ReadOnly cardCost As New StatCard(Icons.Bank, "Estimated Cost")

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
    Private ReadOnly clearanceList As New WheelScrollPanel()
    Private ReadOnly requirements As New RequirementsPanel()
    Private ReadOnly detailsList As New WheelScrollPanel()
    Private ReadOnly detailBody As New Panel()
    Private ReadOnly detailContent As New Panel()
    Private ReadOnly lblDetailEmpty As New Label()
    Private ReadOnly tips As New ToolTip()

    ' State
    Private projects As New List(Of ConstructionProject)
    Private selected As ConstructionProject
    Private clearances As New List(Of ConstructionClearance)
    Private documents As New List(Of Requirement)
    Private loading As Boolean
    Private ReadOnly canManage As Boolean = AccessService.CanManage(AppScreen.ConstructionPermits)

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

        btnNew = toolbar.AddAction("New Project", "New", "primary", AddressOf NewProject_Click, canManage OrElse Session.IsOwner)
        btnView = toolbar.AddAction(If(canManage, "View / Edit", "View Details"), "View", "secondary", AddressOf ViewProject_Click)
        btnAdvance = toolbar.AddAction("Advance Stage", "Advance", "secondary", AddressOf Advance_Click, canManage)
        btnHold = toolbar.AddAction("Put On Hold", "Hold", "secondary", AddressOf Hold_Click, canManage)
        AddHandler toolbar.BusinessChanged, Sub()
                                                txtSearch.Text = ""
                                                LoadProjects(Nothing)
                                            End Sub

        root.Controls.Add(toolbar, 0, 0)
        root.Controls.Add(BuildCards(), 0, 1)
        root.Controls.Add(BuildMain(), 0, 2)
        Controls.Add(root)
        ResumeLayout(False)
    End Sub

    Public Overrides ReadOnly Property Subtitle As String
        Get
            Return "Locational, building and occupancy clearances with technical documents"
        End Get
    End Property

    ' =====================================================================
    '  Layout
    ' =====================================================================

    Private Function BuildCards() As Control
        Dim cards = {cardStage, cardClearances, cardDocuments, cardCost}
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
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 48))
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 52))
        row.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        row.Controls.Add(BuildListCard(), 0, 0)
        row.Controls.Add(BuildDetailCard(), 1, 0)
        Return row
    End Function

    Private Function BuildListCard() As Control
        Dim card As New RoundedPanel With {.Dock = DockStyle.Fill, .Margin = New Padding(0, 0, 8, 0), .Padding = New Padding(16, 14, 16, 16)}

        Dim header As New Panel With {.Dock = DockStyle.Top, .Height = Dpi(48), .BackColor = Color.White}
        Dim title As New Label With {.Text = "Projects", .Font = Theme.SubtitleFont, .ForeColor = Theme.TextDark,
                                     .AutoSize = True, .Location = New Point(0, Dpi(10)), .BackColor = Color.White}
        Dim searchBox = UiHelper.CreateInputBox(txtSearch, "Search…", Icons.Search)
        searchBox.Height = Dpi(38)
        header.Controls.AddRange({title, searchBox})
        AddHandler header.Resize,
            Sub()
                Dim w = Math.Max(Dpi(120), Math.Min(Dpi(240), header.ClientSize.Width - title.Width - Dpi(16)))
                searchBox.SetBounds(header.ClientSize.Width - w, Dpi(2), w, Dpi(38))
            End Sub
        AddHandler txtSearch.TextChanged, Sub() BindGrid(selected?.ProjectId)

        UiHelper.StyleGrid(grid, "Status")
        grid.Dock = DockStyle.Fill
        UiHelper.AddGridColumn(grid, "ReferenceNo", "REFERENCE", "ReferenceNo", 125, Dpi(125))
        UiHelper.AddGridColumn(grid, "Title", "PROJECT", "Title", 170, Dpi(120))
        UiHelper.AddGridColumn(grid, "Stage", "STAGE", "Stage", 115, Dpi(115))
        UiHelper.AddGridColumn(grid, "Status", "STATUS", "Status", 115, Dpi(115))
        UiHelper.AddGridColumn(grid, "Filed", "FILED", "Filed", 112, Dpi(112))
        AddHandler grid.SelectionChanged, Sub() If Not loading Then ShowSelected()
        AddHandler grid.CellDoubleClick, Sub(s, e) If e.RowIndex >= 0 Then ViewProject_Click(Nothing, EventArgs.Empty)
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

    ''' <summary>Width the longest project title needs (titles are never cut: the column hides instead).</summary>
    Private titleNeed As Integer = 120

    ''' <summary>Hides the less important columns when the grid is narrow (reference and status always stay).</summary>
    Private Sub FitGridColumns()
        grid.Columns("Stage").Visible = grid.Width >= Dpi(380)
        grid.Columns("Filed").Visible = grid.Width >= Dpi(500)
        Dim others = grid.Columns.Cast(Of DataGridViewColumn)().
                     Where(Function(c) c.Visible AndAlso c.Name <> "Title").Sum(Function(c) c.MinimumWidth)
        Dim available = grid.Width - others - 4
        Dim title = grid.Columns("Title")
        title.Visible = available >= titleNeed
        If title.Visible Then title.MinimumWidth = Math.Max(Dpi(120), titleNeed)
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
        tracker.Steps = {"Locational", "Building", "Construction", "Occupancy", "Completed"}
        Dim gap1 As New Panel With {.Dock = DockStyle.Top, .Height = 6, .BackColor = Color.White}

        tabs.Dock = DockStyle.Top
        tabs.Tabs = {"Clearances", "Documents", "Details"}
        AddHandler tabs.SelectedIndexChanged, Sub() ShowTab()
        Dim gap2 As New Panel With {.Dock = DockStyle.Top, .Height = 8, .BackColor = Color.White}

        detailBody.Dock = DockStyle.Fill
        detailBody.BackColor = Color.White
        requirements.Dock = DockStyle.Fill
        For Each list In {clearanceList, detailsList}
            list.Dock = DockStyle.Fill
            list.BackColor = Color.White
        Next
        detailBody.Controls.AddRange({clearanceList, requirements, detailsList})
        AddHandler requirements.RequirementsChanged, Sub() LoadProjects(selected?.ProjectId)

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
        LoadProjects(selected?.ProjectId)
    End Sub

    Private Sub LoadProjects(selectId As Integer?)
        projects = If(Business Is Nothing, New List(Of ConstructionProject)(), ConstructionService.GetProjects(Business.BusinessId))
        BindGrid(selectId)
    End Sub

    Private Sub BindGrid(selectId As Integer?)
        Dim term = txtSearch.Text.Trim().ToLowerInvariant()
        Dim rows = projects.
            Where(Function(p) term = "" OrElse
                              (p.ReferenceNo & " " & p.ProjectTitle & " " & p.ProjectType & " " & p.CurrentStage & " " & p.Status).ToLowerInvariant().Contains(term)).
            Select(Function(p) New ProjectRow With {
                .ProjectId = p.ProjectId, .ReferenceNo = p.ReferenceNo, .Title = p.ProjectTitle, .Stage = p.CurrentStage,
                .Status = p.Status, .Filed = UiHelper.FormatDate(p.DateFiled)
            }).ToList()

        titleNeed = Math.Max(Dpi(120), rows.Select(Function(r) UiHelper.TextWidth(r.Title, Theme.BodyFont) + 16).DefaultIfEmpty(0).Max())
        loading = True
        grid.DataSource = rows
        FitGridColumns()
        Dim index = If(selectId.HasValue, rows.FindIndex(Function(r) r.ProjectId = selectId.Value), -1)
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
                               If(term <> "", "No projects match your search.",
                                  "No construction projects yet." & vbCrLf & "Click ""New Project"" to file one."))
            lblEmpty.BringToFront()
        End If
        ShowSelected()
    End Sub

    Private Sub ShowSelected()
        Dim row = If(grid.SelectedRows.Count > 0, TryCast(grid.SelectedRows(0).DataBoundItem, ProjectRow), Nothing)
        selected = If(row Is Nothing, Nothing, projects.FirstOrDefault(Function(p) p.ProjectId = row.ProjectId))
        clearances = ConstructionService.GetClearances(selected)
        documents = ConstructionService.GetDocuments(selected)
        UpdateCards()
        ShowDetails()
        UpdateButtons()
    End Sub

    ' =====================================================================
    '  Cards, buttons and details
    ' =====================================================================

    ''' <summary>The cards describe the selected project (the newest one when the screen opens).</summary>
    Private Sub UpdateCards()
        If Business Is Nothing Then
            For Each c In {cardStage, cardClearances, cardDocuments, cardCost}
                c.SetValue("—", "", "No business selected")
            Next
            Return
        End If
        If selected Is Nothing Then
            cardStage.SetValue("None", "", "no construction project")
            cardClearances.SetValue("—", "", "no project yet")
            cardDocuments.SetValue("—", "", "no project yet")
            cardCost.SetValue("—", "", "no project yet")
            Return
        End If
        Dim stepNo = Array.IndexOf(ConstructionService.Stages, selected.CurrentStage) + 1
        cardStage.SetValue(selected.CurrentStage, selected.Status, If(selected.CurrentStage = ConstructionService.StageCompleted,
                                                                      "all stages done", "step " & stepNo & " of 5"))

        Dim approvedCount = clearances.AsEnumerable().Count(Function(c) c.Status = ConstructionService.Approved)
        Dim rejectedAny = clearances.Any(Function(c) c.Status = "Rejected")
        Dim required = ConstructionService.GetRequiredClearance(selected.CurrentStage)
        cardClearances.SetValue(ConstructionService.GetClearanceSummary(clearances),
                                If(approvedCount = 4, ConstructionService.Approved, If(rejectedAny, "Rejected", ConstructionService.Pending)),
                                If(required Is Nothing OrElse clearances.Any(Function(c) c.ClearanceType = required AndAlso c.Status = ConstructionService.Approved),
                                   If(approvedCount = 4, "all approved", "none needed now"), required & " next"))

        Dim verified = documents.AsEnumerable().Count(Function(r) r.Status = "Verified")
        Dim toUpload = documents.AsEnumerable().Count(Function(r) r.Status = "Pending Upload" OrElse r.Status = "Rejected")
        cardDocuments.SetValue(verified & " of " & documents.Count & " verified",
                               If(documents.Count > 0 AndAlso verified = documents.Count, "Verified", If(toUpload > 0, "Pending Upload", "Submitted")),
                               If(toUpload > 0, toUpload & " to upload", If(verified = documents.Count, "complete", (documents.Count - verified) & " to review")))

        cardCost.SetValue(If(selected.EstimatedCost.HasValue, UiHelper.FormatMoney(selected.EstimatedCost.Value), "Not given"), "",
                          selected.ProjectType)
    End Sub

    Private Sub UpdateButtons()
        Dim hasBusiness = Business IsNot Nothing
        btnNew.Enabled = hasBusiness
        toolbar.SetTip(btnNew, If(hasBusiness, "File a new construction permit application", "No business selected."))
        btnView.Enabled = selected IsNot Nothing
        toolbar.SetTip(btnView, If(selected Is Nothing, "Select a project first.", "Open the project details"))
        If Not canManage Then Return

        Dim advanceBlocker = ConstructionService.GetAdvanceBlocker(selected)
        btnAdvance.Enabled = selected IsNot Nothing AndAlso Not ConstructionService.IsFinal(selected.Status) AndAlso selected.Status = ConstructionService.Active
        toolbar.SetTip(btnAdvance, If(selected Is Nothing, "Select a project first.",
                                      If(Not btnAdvance.Enabled, "The project is " & selected.Status & ".",
                                         If(advanceBlocker, "Move to the " & ConstructionService.GetNextStage(selected.CurrentStage) & " stage"))))

        If selected IsNot Nothing AndAlso selected.Status = ConstructionService.OnHold Then
            toolbar.SetActionText(btnHold, "Resume Project", "Resume")
            btnHold.Enabled = True
            toolbar.SetTip(btnHold, "Continue processing this project")
        Else
            toolbar.SetActionText(btnHold, "Put On Hold", "Hold")
            btnHold.Enabled = selected IsNot Nothing AndAlso selected.Status = ConstructionService.Active
            toolbar.SetTip(btnHold, If(selected Is Nothing, "Select a project first.",
                                       If(btnHold.Enabled, "Pause this project (a reason is required)", "The project is " & selected.Status & ".")))
        End If
    End Sub

    Private Sub ShowDetails()
        Dim has = selected IsNot Nothing
        lblDetailEmpty.Visible = Not has
        detailContent.Visible = has
        If Not has Then
            lblDetailEmpty.Text = If(projects.Count = 0, "Nothing to show yet.", "Select a project to see its clearances.")
            requirements.ShowMessage("")
            Return
        End If

        lblDetailRef.Text = selected.ReferenceNo
        badgeDetail.Text = selected.Status
        PlaceBadge()
        Dim info = selected.ProjectTitle & " · " & selected.ProjectType & " · filed " & UiHelper.FormatDate(selected.DateFiled)
        lblDetailInfo.Text = info
        lblDetailInfo.ForeColor = If(selected.Status = ConstructionService.Rejected, Theme.StatusRed, Theme.TextMuted)
        tips.SetToolTip(lblDetailInfo, info)
        tracker.CurrentStep = ConstructionService.GetTrackerStep(selected)

        Dim isFinal = ConstructionService.IsFinal(selected.Status)
        clearanceList.SetRows(CreateClearanceRows())
        requirements.LoadFor(selected.BusinessId, ModuleNames.ConstructionPermit, selected.ProjectId, AppScreen.ConstructionPermits,
                             allowUpload:=Not isFinal AndAlso (Session.IsOwner OrElse canManage),
                             allowVerify:=Not isFinal)
        detailsList.SetRows(CreateDetailRows())
        ShowTab()
    End Sub

    Private Sub ShowTab()
        clearanceList.Visible = tabs.SelectedIndex = 0
        requirements.Visible = tabs.SelectedIndex = 1
        detailsList.Visible = tabs.SelectedIndex = 2
        detailBody.PerformLayout()
        requirements.PerformLayout()
    End Sub

    ' ---------- Clearances tab ----------

    Private Function CreateClearanceRows() As List(Of Control)
        Dim rows As New List(Of Control)
        For Each c In clearances
            Dim clearanceType = c.ClearanceType
            Dim detail As String
            Dim detailColor As Color = Nothing
            Dim links As New List(Of RowLink)
            Dim approveBlocker = ConstructionService.GetApproveBlocker(selected, clearanceType)
            Select Case c.Status
                Case ConstructionService.Approved
                    detail = c.ClearanceNo & If(c.ApprovedByName <> "", " · " & c.ApprovedByName, "") &
                             If(c.ApprovedAt.HasValue, " · " & c.ApprovedAt.Value.ToString("MMM d, yyyy"), "")
                Case "Rejected"
                    detail = If(c.Remarks <> "", c.Remarks, "Rejected")
                    detailColor = Theme.StatusRed
                Case Else
                    detail = If(approveBlocker, "Ready for approval")
                    If approveBlocker Is Nothing Then detailColor = Theme.StatusGreen
            End Select
            If canManage AndAlso c.Status <> ConstructionService.Approved Then
                If approveBlocker Is Nothing Then
                    links.Add(New RowLink("Approve", Theme.StatusGreen, Sub() ApproveClearance(clearanceType)))
                End If
                If ConstructionService.GetRejectBlocker(selected, clearanceType) Is Nothing Then
                    links.Add(New RowLink("Reject", Theme.StatusRed, Sub() RejectClearance(clearanceType)))
                End If
            End If
            rows.Add(ListRow(tips, ConstructionService.GetClearanceTitle(clearanceType), detail, c.Status, links, detailColor))
        Next
        Dim nextNote = ConstructionService.GetAdvanceBlocker(selected)
        If selected.Status = ConstructionService.Completed Then
            rows.Add(NoteRow(tips, "All stages are done. The Certificate of Occupancy is approved.", Theme.StatusGreen))
        ElseIf selected.Status <> ConstructionService.Active Then
            rows.Add(NoteRow(tips, "The project is " & selected.Status & ".", If(selected.Status = ConstructionService.Rejected, Theme.StatusRed, Theme.StatusAmber)))
        ElseIf nextNote Is Nothing Then
            rows.Add(NoteRow(tips, "Ready to move to the " & ConstructionService.GetNextStage(selected.CurrentStage) & " stage" &
                             If(canManage, " - click ""Advance Stage"".", "."), Theme.StatusGreen))
        Else
            rows.Add(NoteRow(tips, "Next step: " & nextNote, Theme.TextMuted))
        End If
        Return rows
    End Function

    ' ---------- Details tab ----------

    Private Function CreateDetailRows() As List(Of Control)
        Dim rows As New List(Of Control)
        rows.Add(InfoRow(tips, "Project title", selected.ProjectTitle, "Type", selected.ProjectType))
        rows.Add(InfoRow(tips, "Current stage", selected.CurrentStage, "Status", selected.Status))
        rows.Add(InfoRow(tips, "Estimated cost", If(selected.EstimatedCost.HasValue, UiHelper.FormatMoney(selected.EstimatedCost.Value), "Not given"),
                         "Date filed", UiHelper.FormatDate(selected.DateFiled)))
        rows.Add(InfoRow(tips, "Reference", selected.ReferenceNo, "Business", selected.BusinessName))
        rows.Add(NoteRow(tips, "Stages move one at a time; each needs its clearance approved. The Building permit needs the FSEC approved and all technical documents verified.", Theme.TextMuted))
        Return rows
    End Function

    ' =====================================================================
    '  Actions
    ' =====================================================================

    Private Sub NewProject_Click(sender As Object, e As EventArgs)
        If Business Is Nothing Then Return
        Using dlg As New ConstructionProjectDialog(ConstructionProjectDialog.DialogMode.NewProject, Business)
            If dlg.ShowDialog(FindForm()) <> DialogResult.OK Then Return
            LoadProjects(dlg.ProjectId)
        End Using
        If selected Is Nothing Then Return
        tabs.SelectedIndex = 1
        UiHelper.ShowSuccess("Project " & selected.ReferenceNo & " was filed." & vbCrLf &
                             "Next: upload the technical documents in the Documents tab.", "Project filed")
    End Sub

    Private Sub ViewProject_Click(sender As Object, e As EventArgs)
        If selected Is Nothing OrElse Business Is Nothing Then Return
        Dim canEdit = canManage AndAlso Not ConstructionService.IsFinal(selected.Status)
        Using dlg As New ConstructionProjectDialog(If(canEdit, ConstructionProjectDialog.DialogMode.Edit, ConstructionProjectDialog.DialogMode.View),
                                                   Business, ConstructionRepository.GetById(selected.ProjectId))
            If dlg.ShowDialog(FindForm()) <> DialogResult.OK Then Return
        End Using
        LoadProjects(selected.ProjectId)
        UiHelper.ShowSuccess("Project " & selected.ReferenceNo & " was updated.", "Changes saved")
    End Sub

    Private Sub ApproveClearance(clearanceType As String)
        If selected Is Nothing OrElse Not canManage Then Return
        Dim p = ConstructionRepository.GetById(selected.ProjectId)
        Dim blocker = ConstructionService.GetApproveBlocker(p, clearanceType)
        If blocker IsNot Nothing Then
            UiHelper.ShowWarning(blocker)
            Return
        End If
        Dim remarks = PromptDialog.Ask(FindForm(), "Approve " & ConstructionService.GetClearanceTitle(clearanceType),
                                       p.ReferenceNo & "  ·  " & p.BusinessName, "Remarks (optional)", False, "Approve")
        If remarks Is Nothing Then Return
        Dim outcome = ConstructionService.ApproveClearance(p, clearanceType, remarks)
        If outcome.ErrorMessage IsNot Nothing Then
            UiHelper.ShowWarning(outcome.ErrorMessage)
            Return
        End If
        LoadProjects(p.ProjectId)
        Dim nextStep = ConstructionService.GetAdvanceBlocker(selected)
        UiHelper.ShowSuccess(ConstructionService.GetClearanceTitle(clearanceType) & " approved: " & outcome.ClearanceNo & "." &
                             If(outcome.EndorsedReference IsNot Nothing,
                                vbCrLf & "The Zoning endorsement of " & outcome.EndorsedReference & " is now Endorsed.", "") & vbCrLf &
                             If(nextStep Is Nothing, "Next: click ""Advance Stage"" to move to " & ConstructionService.GetNextStage(selected.CurrentStage) & ".",
                                "Next: " & nextStep), "Clearance approved")
    End Sub

    Private Sub RejectClearance(clearanceType As String)
        If selected Is Nothing OrElse Not canManage Then Return
        Dim p = ConstructionRepository.GetById(selected.ProjectId)
        Dim reason = PromptDialog.Ask(FindForm(), "Reject " & ConstructionService.GetClearanceTitle(clearanceType),
                                      p.ReferenceNo & "  ·  " & p.BusinessName, "Reason for rejecting", True, "Reject", "danger")
        If reason Is Nothing Then Return
        Dim problem = ConstructionService.RejectClearance(p, clearanceType, reason)
        If problem IsNot Nothing Then
            UiHelper.ShowWarning(problem)
            Return
        End If
        LoadProjects(p.ProjectId)
        UiHelper.ShowSuccess(ConstructionService.GetClearanceTitle(clearanceType) & " was rejected." & vbCrLf &
                             "It can be approved later once the applicant corrects the requirements.", "Clearance rejected")
    End Sub

    Private Sub Advance_Click(sender As Object, e As EventArgs)
        If selected Is Nothing OrElse Not canManage Then Return
        Dim p = ConstructionRepository.GetById(selected.ProjectId)
        Dim nextStage = ConstructionService.GetNextStage(p.CurrentStage)
        Using dlg As New WorkflowDialog("Advance Stage", p.ReferenceNo & "  ·  " & p.BusinessName, p.CurrentStage, nextStage,
                                        ConstructionService.GetAdvanceBlocker(p), ConstructionService.GetReadyMessage(p),
                                        Function() ConstructionService.Advance(p),
                                        Function(reason) ConstructionService.RejectProject(p, reason), "Reject Project",
                                        If(nextStage = ConstructionService.StageCompleted, "Complete Project", ""))
            If dlg.ShowDialog(FindForm()) <> DialogResult.OK Then Return
        End Using
        LoadProjects(p.ProjectId)
        If p.Status = ConstructionService.Rejected Then
            UiHelper.ShowSuccess("Project " & p.ReferenceNo & " was rejected.", "Project rejected")
        ElseIf p.Status = ConstructionService.Completed Then
            UiHelper.ShowSuccess("Project " & p.ReferenceNo & " is completed.", "Project completed")
        Else
            Dim needed = ConstructionService.GetRequiredClearance(p.CurrentStage)
            UiHelper.ShowSuccess(p.ReferenceNo & " moved to the " & p.CurrentStage & " stage." & vbCrLf &
                                 If(needed Is Nothing, "Next: advance to Occupancy when construction is finished.",
                                    "Next: approve the " & ConstructionService.GetClearanceTitle(needed) &
                                    If(needed = ClearanceTypes.Building, " (FSEC first).", ".")), "Stage updated")
        End If
    End Sub

    Private Sub Hold_Click(sender As Object, e As EventArgs)
        If selected Is Nothing OrElse Not canManage Then Return
        Dim p = ConstructionRepository.GetById(selected.ProjectId)
        Dim problem As String
        If p.Status = ConstructionService.OnHold Then
            problem = ConstructionService.ResumeProject(p)
        Else
            Dim reason = PromptDialog.Ask(FindForm(), "Put Project On Hold", p.ReferenceNo & "  ·  " & p.BusinessName,
                                          "Reason (e.g. waiting for revised plans)", True, "Put On Hold")
            If reason Is Nothing Then Return
            problem = ConstructionService.PutOnHold(p, reason)
        End If
        If problem IsNot Nothing Then
            UiHelper.ShowWarning(problem)
            Return
        End If
        LoadProjects(p.ProjectId)
        UiHelper.ShowSuccess(If(p.Status = ConstructionService.OnHold,
                                p.ReferenceNo & " is on hold. Click ""Resume Project"" to continue processing it.",
                                p.ReferenceNo & " is active again."), If(p.Status = ConstructionService.OnHold, "Project on hold", "Project resumed"))
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then tips.Dispose()
        MyBase.Dispose(disposing)
    End Sub

End Class
