''' <summary>
''' Business Permits module (the template for the other modules).
'''   Toolbar  : business selector (staff) or the owner's business, and the actions
'''   Cards    : current Mayor's Permit, latest application, amount due
'''   Left     : applications grid with search
'''   Right    : selected application - progress tracker and tabs
'''              (Endorsements | Requirements | Assessment)
''' No SQL here: everything goes through BusinessPermitService / repositories.
''' </summary>
Public Class BusinessPermitView
    Inherits ModuleView

    ''' <summary>Grid row (display only).</summary>
    Private Class PermitRow
        Public Property PermitId As Integer
        Public Property Reference As String
        Public Property Year As Integer
        Public Property AppType As String
        Public Property Status As String
        Public Property Filed As String
        Public Property Total As String
    End Class

    Private Shared lastBusinessId As Integer   ' remembers the staff's last selected business

    ' Toolbar
    Private ReadOnly businessBox As New RoundedPanel()
    Private ReadOnly cboBusiness As New ComboBox()
    Private ReadOnly lblBusiness As New Label()
    Private ReadOnly actions As New FlowLayoutPanel()
    Private ReadOnly btnNew As New Button()
    Private ReadOnly btnRenew As New Button()
    Private ReadOnly btnView As New Button()
    Private ReadOnly btnStatus As New Button()
    Private ReadOnly btnDelete As New Button()
    Private ReadOnly buttonTexts As New Dictionary(Of Button, String())   ' {full, short}

    ' Summary cards
    Private ReadOnly cardCurrent As New StatCard(Icons.Document, "Current Mayor's Permit")
    Private ReadOnly cardLatest As New StatCard(Icons.Calendar, "Latest Application")
    Private ReadOnly cardDue As New StatCard(Icons.Bank, "Amount Due")

    ' Applications list
    Private ReadOnly txtSearch As New TextBox()
    Private ReadOnly grid As New ModernGrid()
    Private ReadOnly lblEmpty As New Label()

    ' Details
    Private ReadOnly lblDetailRef As New Label()
    Private ReadOnly badgeDetail As New StatusBadge()
    Private ReadOnly lblDetailInfo As New Label()
    Private ReadOnly tracker As New StepTracker()
    Private ReadOnly tabs As New SegmentedTabs()
    Private ReadOnly endorsementList As New WheelScrollPanel()
    Private ReadOnly requirements As New RequirementsPanel()
    Private ReadOnly assessmentList As New WheelScrollPanel()
    Private ReadOnly detailBody As New Panel()
    Private ReadOnly detailContent As New Panel()
    Private ReadOnly lblDetailEmpty As New Label()
    Private ReadOnly tips As New ToolTip()

    ' State
    Private business As Business
    Private permits As New List(Of BusinessPermit)
    Private selected As BusinessPermit
    Private loading As Boolean
    Private ReadOnly canManage As Boolean = AccessService.CanManage(AppScreen.BusinessPermits)

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
        root.Controls.Add(BuildToolbar(), 0, 0)
        root.Controls.Add(BuildCards(), 0, 1)
        root.Controls.Add(BuildMain(), 0, 2)
        Controls.Add(root)
        ResumeLayout(False)
    End Sub

    Public Overrides ReadOnly Property Subtitle As String
        Get
            Return "Mayor's / Business Permit applications, endorsements and issuance"
        End Get
    End Property

    ' =====================================================================
    '  Layout
    ' =====================================================================

    Private Function BuildToolbar() As Control
        Dim bar As New Panel With {.Dock = DockStyle.Fill, .BackColor = Theme.ContentBackground, .Margin = New Padding(0)}

        ' Business selector (staff) or the owner's fixed business
        businessBox.ShowShadow = False
        businessBox.Radius = 10
        businessBox.Padding = New Padding(40, 0, 10, 0)
        businessBox.SetBounds(0, 2, 340, 44)
        Dim icon As New Label With {
            .Text = Icons.Contact, .Font = Theme.IconFont(12.0F), .ForeColor = Theme.SidebarBlue,
            .BackColor = Color.White, .TextAlign = ContentAlignment.MiddleCenter, .AutoSize = False
        }
        icon.SetBounds(10, 2, 26, 40)
        businessBox.Controls.Add(icon)

        If Session.IsOwner Then
            lblBusiness.Font = Theme.BodyBoldFont
            lblBusiness.ForeColor = Theme.TextDark
            lblBusiness.BackColor = Color.White
            lblBusiness.AutoEllipsis = True
            lblBusiness.TextAlign = ContentAlignment.MiddleLeft
            lblBusiness.Dock = DockStyle.Fill
            businessBox.Controls.Add(lblBusiness)
        Else
            cboBusiness.DropDownStyle = ComboBoxStyle.DropDownList
            cboBusiness.FlatStyle = FlatStyle.Flat
            cboBusiness.Font = Theme.InputFont
            cboBusiness.BackColor = Color.White
            cboBusiness.ForeColor = Theme.TextDark
            cboBusiness.Anchor = AnchorStyles.Left Or AnchorStyles.Right
            UiHelper.StyleComboBox(cboBusiness)
            businessBox.Controls.Add(cboBusiness)
            AddHandler businessBox.Resize,
                Sub()
                    cboBusiness.Width = businessBox.ClientSize.Width - businessBox.Padding.Horizontal
                    cboBusiness.Location = New Point(businessBox.Padding.Left, (businessBox.ClientSize.Height - cboBusiness.Height) \ 2)
                End Sub
            AddHandler cboBusiness.SelectedIndexChanged, AddressOf Business_Changed
            tips.SetToolTip(cboBusiness, "Choose a business")
        End If
        bar.Controls.Add(businessBox)

        ' Action buttons (right-aligned; labels shorten when the window is narrow)
        actions.Dock = DockStyle.Right
        actions.AutoSize = True
        actions.WrapContents = False
        actions.FlowDirection = FlowDirection.RightToLeft
        actions.Padding = New Padding(0, 3, 0, 0)
        actions.BackColor = Theme.ContentBackground
        SetupButton(btnDelete, "Delete", "Delete", "danger", AddressOf Delete_Click, Session.IsAdmin)
        SetupButton(btnStatus, "Update Status", "Status", "secondary", AddressOf Status_Click, canManage)
        SetupButton(btnView, If(canManage, "View / Edit", "View Details"), "View", "secondary", AddressOf View_Click, True)
        SetupButton(btnRenew, "Renew Permit", "Renew", "secondary", AddressOf Renew_Click, True)
        SetupButton(btnNew, "New Application", "New", "primary", AddressOf New_Click, True)
        bar.Controls.Add(actions)

        AddHandler bar.Resize, Sub() FitToolbar(bar)
        Return bar
    End Function

    Private Sub SetupButton(btn As Button, fullText As String, shortText As String, style As String,
                            handler As EventHandler, isVisible As Boolean)
        btn.Height = 40
        btn.Margin = New Padding(8, 0, 0, 0)
        Select Case style
            Case "primary" : UiHelper.StylePrimaryButton(btn)
            Case "danger" : UiHelper.StyleDangerButton(btn)
            Case Else : UiHelper.StyleSecondaryButton(btn)
        End Select
        btn.Visible = isVisible
        buttonTexts(btn) = {fullText, shortText}
        SetButtonText(btn, fullText)
        AddHandler btn.Click, handler
        actions.Controls.Add(btn)
    End Sub

    Private Sub SetButtonText(btn As Button, text As String)
        btn.Text = text
        btn.Width = TextRenderer.MeasureText(text, btn.Font).Width + CInt(32 * DeviceDpi / 96.0)
    End Sub

    ''' <summary>Uses short button labels and a narrower business box when space is tight.</summary>
    Private Sub FitToolbar(bar As Control)
        Dim gap = CInt(16 * DeviceDpi / 96.0)
        Dim minBox = CInt(220 * DeviceDpi / 96.0)
        Dim maxBox = CInt(380 * DeviceDpi / 96.0)
        For Each useShort In {False, True}
            For Each kv In buttonTexts
                SetButtonText(kv.Key, kv.Value(If(useShort, 1, 0)))
            Next
            actions.PerformLayout()
            If bar.ClientSize.Width - actions.PreferredSize.Width - gap >= minBox Then Exit For
        Next
        businessBox.Width = Math.Max(minBox, Math.Min(maxBox, bar.ClientSize.Width - actions.PreferredSize.Width - gap))
        For Each kv In buttonTexts
            tips.SetToolTip(kv.Key, kv.Value(0))
        Next
    End Sub

    Private Function BuildCards() As Control
        Dim row As New TableLayoutPanel With {
            .Dock = DockStyle.Fill, .ColumnCount = 3, .RowCount = 1, .BackColor = Theme.ContentBackground,
            .Margin = New Padding(0), .Padding = New Padding(0)
        }
        For i = 1 To 3
            row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 33.33F))
        Next
        row.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        cardCurrent.Dock = DockStyle.Fill : cardCurrent.Margin = New Padding(0, 0, 8, 14)
        cardLatest.Dock = DockStyle.Fill : cardLatest.Margin = New Padding(8, 0, 8, 14)
        cardDue.Dock = DockStyle.Fill : cardDue.Margin = New Padding(8, 0, 0, 14)
        row.Controls.Add(cardCurrent, 0, 0)
        row.Controls.Add(cardLatest, 1, 0)
        row.Controls.Add(cardDue, 2, 0)
        Return row
    End Function

    Private Function BuildMain() As Control
        Dim row As New TableLayoutPanel With {
            .Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 1, .BackColor = Theme.ContentBackground,
            .Margin = New Padding(0), .Padding = New Padding(0)
        }
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 44))
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 56))
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
        searchBox.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        header.Controls.AddRange({title, searchBox})
        AddHandler header.Resize,
            Sub()
                Dim w = Math.Max(120, Math.Min(240, header.ClientSize.Width - title.Width - 16))
                searchBox.SetBounds(header.ClientSize.Width - w, 2, w, 38)
            End Sub
        AddHandler txtSearch.TextChanged, Sub() BindGrid(selected?.PermitId)

        UiHelper.StyleGrid(grid, "Status")
        grid.Dock = DockStyle.Fill
        UiHelper.AddGridColumn(grid, "Reference", "REFERENCE", "Reference", 130, 110)
        UiHelper.AddGridColumn(grid, "Year", "YEAR", "Year", 55, 50)
        UiHelper.AddGridColumn(grid, "Type", "TYPE", "AppType", 75, 70)
        UiHelper.AddGridColumn(grid, "Status", "STATUS", "Status", 120, 110)
        UiHelper.AddGridColumn(grid, "Filed", "FILED", "Filed", 95, 90)
        UiHelper.AddGridColumn(grid, "Total", "TOTAL DUE", "Total", 100, 90)
        AddHandler grid.SelectionChanged, AddressOf Grid_SelectionChanged
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
        ' Reference + Status always show; the rest appear as the grid gets wider.
        Dim s = DeviceDpi / 96.0
        grid.Columns("Type").Visible = grid.Width >= 330 * s
        grid.Columns("Year").Visible = grid.Width >= 400 * s
        grid.Columns("Total").Visible = grid.Width >= 520 * s
        grid.Columns("Filed").Visible = grid.Width >= 620 * s
    End Sub

    Private Function BuildDetailCard() As Control
        Dim card As New RoundedPanel With {.Dock = DockStyle.Fill, .Margin = New Padding(8, 0, 0, 0), .Padding = New Padding(18, 14, 18, 16)}

        Dim header As New Panel With {.Dock = DockStyle.Top, .Height = 52, .BackColor = Color.White}
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
        lblDetailInfo.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        header.Controls.AddRange({lblDetailRef, badgeDetail, lblDetailInfo})
        AddHandler header.Resize, Sub() lblDetailInfo.SetBounds(0, 30, header.ClientSize.Width, 20)

        tracker.Dock = DockStyle.Top
        tracker.Height = 70
        tracker.Steps = BusinessPermitService.TrackerSteps
        Dim gap1 As New Panel With {.Dock = DockStyle.Top, .Height = 6, .BackColor = Color.White}

        tabs.Dock = DockStyle.Top
        tabs.Tabs = {"Endorsements", "Requirements", "Assessment"}
        AddHandler tabs.SelectedIndexChanged, Sub() ShowTab()
        Dim gap2 As New Panel With {.Dock = DockStyle.Top, .Height = 8, .BackColor = Color.White}

        detailBody.Dock = DockStyle.Fill
        detailBody.BackColor = Color.White
        endorsementList.Dock = DockStyle.Fill
        endorsementList.BackColor = Color.White
        requirements.Dock = DockStyle.Fill
        assessmentList.Dock = DockStyle.Fill
        assessmentList.BackColor = Color.White
        detailBody.Controls.AddRange({endorsementList, requirements, assessmentList})
        AddHandler requirements.RequirementsChanged, Sub() ReloadKeepingSelection()

        lblDetailEmpty.Dock = DockStyle.Fill
        lblDetailEmpty.TextAlign = ContentAlignment.MiddleCenter
        lblDetailEmpty.Font = Theme.BodyFont
        lblDetailEmpty.ForeColor = Theme.TextMuted
        lblDetailEmpty.BackColor = Color.White
        lblDetailEmpty.Text = "Select an application to see its progress."

        ' Everything for the selected application lives in detailContent, so the
        ' empty message can replace it completely when nothing is selected.
        detailContent.Dock = DockStyle.Fill
        detailContent.BackColor = Color.White
        detailContent.Controls.Add(detailBody)
        detailContent.Controls.Add(gap2)
        detailContent.Controls.Add(tabs)
        detailContent.Controls.Add(gap1)
        detailContent.Controls.Add(tracker)
        detailContent.Controls.Add(header)
        card.Controls.Add(lblDetailEmpty)
        card.Controls.Add(detailContent)
        Return card
    End Function

    ' =====================================================================
    '  Loading data
    ' =====================================================================

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        LoadBusinesses()
    End Sub

    Public Overrides Sub RefreshData()
        ReloadKeepingSelection()
    End Sub

    Private Sub LoadBusinesses()
        If Session.IsOwner Then
            business = If(Session.BusinessId.HasValue, BusinessRepository.GetById(Session.BusinessId.Value), Nothing)
            lblBusiness.Text = If(business?.BusinessName, "No business linked")
            tips.SetToolTip(lblBusiness, lblBusiness.Text)
            LoadPermits(Nothing)
            Return
        End If

        loading = True
        Dim list = BusinessRepository.GetAll()
        cboBusiness.Items.Clear()
        cboBusiness.Items.AddRange(list.Cast(Of Object)().ToArray())
        Dim requested = ModuleView.TakePendingBusiness()     ' e.g. opened from the dashboard
        If requested.HasValue Then lastBusinessId = requested.Value
        Dim index = list.FindIndex(Function(b) b.BusinessId = lastBusinessId)
        loading = False
        If list.Count > 0 Then
            cboBusiness.SelectedIndex = Math.Max(0, index)   ' triggers Business_Changed
        Else
            business = Nothing
            LoadPermits(Nothing)
        End If
    End Sub

    Private Sub Business_Changed(sender As Object, e As EventArgs)
        If loading Then Return
        business = TryCast(cboBusiness.SelectedItem, Business)
        If business IsNot Nothing Then lastBusinessId = business.BusinessId
        txtSearch.Text = ""
        LoadPermits(Nothing)
    End Sub

    ''' <summary>Reloads the selected business's applications and re-selects a permit.</summary>
    Private Sub LoadPermits(selectPermitId As Integer?)
        permits = If(business Is Nothing, New List(Of BusinessPermit)(),
                     BusinessPermitRepository.GetByBusinessId(business.BusinessId))
        UpdateCards()
        BindGrid(selectPermitId)
    End Sub

    Private Sub ReloadKeepingSelection()
        LoadPermits(selected?.PermitId)
    End Sub

    Private Sub BindGrid(selectPermitId As Integer?)
        Dim term = txtSearch.Text.Trim().ToLowerInvariant()
        Dim rows = permits.
            Where(Function(p) term = "" OrElse
                              (p.ReferenceNo & " " & p.PermitYear & " " & p.ApplicationType & " " & p.Status).ToLowerInvariant().Contains(term)).
            Select(Function(p) New PermitRow With {
                .PermitId = p.PermitId, .Reference = p.ReferenceNo, .Year = p.PermitYear, .AppType = p.ApplicationType,
                .Status = p.Status, .Filed = UiHelper.FormatDate(p.DateFiled),
                .Total = If(p.AssessedAmount > 0, UiHelper.FormatMoney(BusinessPermitService.GetTotalDue(p)), "—")
            }).ToList()

        loading = True
        grid.DataSource = rows
        FitGridColumns()
        Dim index = If(selectPermitId.HasValue, rows.FindIndex(Function(r) r.PermitId = selectPermitId.Value), -1)
        If index < 0 AndAlso rows.Count > 0 Then index = 0
        grid.ClearSelection()
        If index >= 0 Then
            grid.CurrentCell = grid.Rows(index).Cells(0)
            grid.Rows(index).Selected = True
        End If
        loading = False

        lblEmpty.Visible = rows.Count = 0
        If rows.Count = 0 Then
            lblEmpty.Text = If(business Is Nothing, "No business selected.",
                               If(term <> "", "No applications match your search.",
                                  "No applications yet." & vbCrLf & "Click ""New Application"" to file one."))
            lblEmpty.BringToFront()
        End If
        ShowSelected()
    End Sub

    Private Sub Grid_SelectionChanged(sender As Object, e As EventArgs)
        If loading Then Return
        ShowSelected()
    End Sub

    Private Sub ShowSelected()
        ' Use the SELECTED row (CurrentRow can lag behind during SelectionChanged)
        Dim row = If(grid.SelectedRows.Count > 0, TryCast(grid.SelectedRows(0).DataBoundItem, PermitRow), Nothing)
        selected = If(row Is Nothing, Nothing, permits.FirstOrDefault(Function(p) p.PermitId = row.PermitId))
        ShowDetails()
        UpdateButtons()
    End Sub

    ' =====================================================================
    '  Cards, buttons and details
    ' =====================================================================

    Private Sub UpdateCards()
        If business Is Nothing Then
            cardCurrent.SetValue("—", "", "No business selected")
            cardLatest.SetValue("—")
            cardDue.SetValue("—")
            Return
        End If

        Dim current = permits.Where(Function(p) p.Status = BusinessPermitService.Issued).
                      OrderByDescending(Function(p) p.PermitYear).FirstOrDefault()
        If current Is Nothing Then
            cardCurrent.SetValue("None", "", "No permit issued yet")
        Else
            Dim validity = StatusService.GetExpiryStatus(current.ValidUntil)
            Dim days = If(current.ValidUntil.HasValue, StatusService.DaysUntil(current.ValidUntil.Value), 0)
            cardCurrent.SetValue(If(current.MayorsPermitNo <> "", current.MayorsPermitNo, current.ReferenceNo), validity,
                                 If(days >= 0, "until " & UiHelper.FormatDate(current.ValidUntil) & " (" & days & " days)",
                                    "expired " & UiHelper.FormatDate(current.ValidUntil)))
        End If

        Dim latest = permits.Where(Function(p) p.Status <> BusinessPermitService.Rejected).
                     OrderByDescending(Function(p) p.PermitYear).ThenByDescending(Function(p) p.PermitId).FirstOrDefault()
        If latest Is Nothing Then
            cardLatest.SetValue("None", "", "No applications yet")
            cardDue.SetValue("—", "", "Nothing to pay")
            Return
        End If
        cardLatest.SetValue(latest.ReferenceNo, latest.Status, latest.ApplicationType & " · " & latest.PermitYear)

        Dim total = BusinessPermitService.GetTotalDue(latest)
        Select Case latest.Status
            Case BusinessPermitService.Assessed
                cardDue.SetValue(UiHelper.FormatMoney(total), "Unpaid", latest.ReferenceNo)
            Case BusinessPermitService.Paid, BusinessPermitService.Issued
                cardDue.SetValue(UiHelper.FormatMoney(total), "Paid", latest.ReferenceNo)
            Case Else
                Dim deadline = BusinessPermitService.GetRenewalDeadline(latest.PermitYear)
                Dim late = latest.ApplicationType = "Renewal" AndAlso Date.Today > deadline
                cardDue.SetValue("Not yet assessed", If(late, "Late", ""),
                                 If(late, "surcharge + interest will apply", latest.ReferenceNo))
        End Select
    End Sub

    Private Sub UpdateButtons()
        Dim hasBusiness = business IsNot Nothing
        Dim active = permits.Where(Function(p) p.Status <> BusinessPermitService.Rejected).ToList()
        Dim open = active.FirstOrDefault(Function(p) Not BusinessPermitService.IsFinal(p.Status))

        btnNew.Enabled = hasBusiness AndAlso active.Count = 0
        tips.SetToolTip(btnNew, If(active.Count > 0, "This business already has permits - use Renew Permit.", "File a new business permit"))

        btnRenew.Enabled = hasBusiness AndAlso active.Count > 0 AndAlso open Is Nothing
        tips.SetToolTip(btnRenew, If(open IsNot Nothing, "An application is already in progress (" & open.ReferenceNo & ").",
                                     If(active.Count = 0, "No previous permit to renew - use New Application.", "File a renewal application")))

        btnView.Enabled = selected IsNot Nothing
        btnStatus.Enabled = selected IsNot Nothing AndAlso Not BusinessPermitService.IsFinal(selected.Status)
        btnDelete.Enabled = selected IsNot Nothing AndAlso selected.Status = BusinessPermitService.Submitted
        tips.SetToolTip(btnDelete, "Delete (only applications that are still Submitted)")
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

        lblDetailRef.Text = selected.ReferenceNo
        badgeDetail.Text = selected.Status
        badgeDetail.Visible = True
        badgeDetail.Location = New Point(lblDetailRef.Right + 10, 4)
        lblDetailInfo.Text = selected.ApplicationType & " · " & selected.PermitYear & " · filed " & UiHelper.FormatDate(selected.DateFiled) &
                             If(selected.Status = BusinessPermitService.Rejected AndAlso selected.Remarks <> "",
                                " · Reason: " & selected.Remarks, "")
        lblDetailInfo.ForeColor = If(selected.Status = BusinessPermitService.Rejected, Theme.StatusRed, Theme.TextMuted)
        tips.SetToolTip(lblDetailInfo, lblDetailInfo.Text)
        tracker.CurrentStep = BusinessPermitService.GetTrackerStep(selected.Status)

        Dim isFinal = BusinessPermitService.IsFinal(selected.Status)
        endorsementList.SetRows(BusinessPermitRepository.GetEndorsements(selected.PermitId).
                                Select(Function(en) CreateEndorsementRow(en, isFinal)).ToList())
        requirements.LoadFor(selected.BusinessId, ModuleNames.BusinessPermit, selected.PermitId, AppScreen.BusinessPermits,
                             allowUpload:=Not isFinal AndAlso (Session.IsOwner OrElse canManage),
                             allowVerify:=Not isFinal)
        assessmentList.SetRows(CreateAssessmentRows(selected))
        ShowTab()
    End Sub

    Private Sub ShowTab()
        endorsementList.Visible = tabs.SelectedIndex = 0
        requirements.Visible = tabs.SelectedIndex = 1
        assessmentList.Visible = tabs.SelectedIndex = 2
        detailBody.PerformLayout()        ' size the newly shown tab right away
        requirements.PerformLayout()
    End Sub

    ' ---------- Endorsement rows ----------

    Private Shared Function OfficeTitle(office As String) As String
        Select Case office
            Case EndorsementOffices.Barangay : Return "Barangay Clearance"
            Case EndorsementOffices.Sanitary : Return "Sanitary / Health Office"
            Case EndorsementOffices.RPT : Return "Real Property Tax (Treasurer)"
            Case EndorsementOffices.Fire : Return "Fire Safety (BFP)"
            Case EndorsementOffices.Zoning : Return "Zoning (MPDO)"
            Case Else : Return office
        End Select
    End Function

    Private Function CreateEndorsementRow(en As ClearanceEndorsement, isFinal As Boolean) As Control
        Dim links As New List(Of RowLink)
        Dim manage = canManage AndAlso Not isFinal
        If manage Then
            If en.Status = "Pending" Then
                links.Add(New RowLink("Endorse", Theme.SidebarBlue, Sub() SetEndorsement(en, "Endorsed", "")))
                links.Add(New RowLink("Reject", Theme.StatusRed, Sub() RejectEndorsement(en)))
            Else
                links.Add(New RowLink("Reset", Theme.TextMuted, Sub() SetEndorsement(en, "Pending", "")))
            End If
        End If

        Dim detail As String
        Select Case en.Status
            Case "Endorsed"
                detail = "Endorsed" & If(en.EndorsedByName <> "", " by " & en.EndorsedByName, "") & " · " & UiHelper.FormatDate(en.EndorsedAt)
            Case "Rejected"
                detail = "Rejected" & If(en.Remarks <> "", ": " & en.Remarks, "")
            Case Else
                detail = If(en.Remarks <> "", en.Remarks, "Waiting for the " & en.Office & " office")
        End Select
        ' Same link column on every row so the status badges line up in one column
        Return ListRow(tips, OfficeTitle(en.Office), detail, en.Status, links,
                       If(en.Status = "Rejected", Theme.StatusRed, Nothing),
                       If(manage, LinkColumnWidth("Endorse", "Reject"), 0))
    End Function

    Private Sub SetEndorsement(en As ClearanceEndorsement, status As String, remarks As String)
        If Not BusinessPermitService.SetEndorsement(selected, en.Office, status, remarks) Then
            UiHelper.ShowWarning("The endorsement could not be updated.")
        End If
        ReloadKeepingSelection()
    End Sub

    Private Sub RejectEndorsement(en As ClearanceEndorsement)
        Dim reason = PromptDialog.Ask(FindForm(), "Reject Endorsement", OfficeTitle(en.Office) & " for " & selected.ReferenceNo,
                                      "Reason", True, "Reject", "danger")
        If reason IsNot Nothing Then SetEndorsement(en, "Rejected", reason)
    End Sub

    ' ---------- Assessment rows ----------

    Private Function CreateAssessmentRows(p As BusinessPermit) As List(Of Control)
        Dim rows As New List(Of Control)
        Dim assessed = p.AssessedAmount > 0
        Dim deadline = BusinessPermitService.GetRenewalDeadline(p.PermitYear)
        Dim lateNow = p.ApplicationType = "Renewal" AndAlso Not assessed AndAlso Date.Today > deadline

        rows.Add(InfoRow(If(p.ApplicationType = "New", "Capital investment", "Gross receipts (previous year)"),
                         UiHelper.FormatMoney(p.GrossReceipts),
                         "Assessed business tax", If(assessed, UiHelper.FormatMoney(p.AssessedAmount), "Not yet assessed")))
        rows.Add(InfoRow("Surcharge (late renewal)", If(assessed, UiHelper.FormatMoney(p.Surcharge), "—"),
                         "Interest (late renewal)", If(assessed, UiHelper.FormatMoney(p.Interest), "—")))
        rows.Add(InfoRow("Total due", If(assessed, UiHelper.FormatMoney(BusinessPermitService.GetTotalDue(p)), "—"),
                         "Renewal deadline", If(p.ApplicationType = "Renewal", UiHelper.FormatDate(deadline), "Not applicable (new)")))
        rows.Add(InfoRow("Mayor's Permit No.", If(p.MayorsPermitNo <> "", p.MayorsPermitNo, "—"),
                         "Valid until", UiHelper.FormatDate(p.ValidUntil)))
        rows.Add(InfoRow("Date filed", UiHelper.FormatDate(p.DateFiled), "Date issued", UiHelper.FormatDate(p.DateIssued)))
        If lateNow Then
            Dim note As New Label With {
                .Height = 44, .Font = Theme.SmallBoldFont, .ForeColor = Theme.StatusAmber, .BackColor = Color.White,
                .TextAlign = ContentAlignment.MiddleLeft, .AutoEllipsis = True,
                .Text = "Late renewal: a " & (SettingsRepository.GetDecimal("bp_surcharge_rate", 0.25D) * 100).ToString("0") &
                        "% surcharge and " & (SettingsRepository.GetDecimal("bp_interest_rate_monthly", 0.02D) * 100).ToString("0") &
                        "% monthly interest will be added at assessment."
            }
            tips.SetToolTip(note, note.Text)
            rows.Insert(0, note)
        End If
        Return rows
    End Function

    ''' <summary>A row with two caption/value pairs side by side.</summary>
    Private Function InfoRow(caption1 As String, value1 As String, caption2 As String, value2 As String) As Control
        Dim row As New TableLayoutPanel With {.Height = 48, .ColumnCount = 2, .RowCount = 1, .BackColor = Color.White,
                                              .Margin = New Padding(0), .Padding = New Padding(0)}
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        row.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        row.Controls.Add(InfoPair(caption1, value1), 0, 0)
        row.Controls.Add(InfoPair(caption2, value2), 1, 0)
        Return row
    End Function

    Private Function InfoPair(caption As String, value As String) As Control
        Dim pair As New Panel With {.Dock = DockStyle.Fill, .BackColor = Color.White, .Margin = New Padding(0)}
        Dim cap As New Label With {.Text = caption, .Font = Theme.SmallFont, .ForeColor = Theme.TextMuted,
                                   .Dock = DockStyle.Top, .Height = 20, .AutoEllipsis = True}
        Dim val As New Label With {.Text = value, .Font = Theme.BodyBoldFont, .ForeColor = Theme.TextDark,
                                   .Dock = DockStyle.Top, .Height = 22, .AutoEllipsis = True}
        tips.SetToolTip(val, value)
        pair.Controls.Add(val)
        pair.Controls.Add(cap)
        Return pair
    End Function

    ' =====================================================================
    '  Actions
    ' =====================================================================

    Private Sub New_Click(sender As Object, e As EventArgs)
        If business Is Nothing Then Return
        OpenDialog(New BusinessPermitDialog(BusinessPermitDialog.DialogMode.NewApplication, business))
    End Sub

    Private Sub Renew_Click(sender As Object, e As EventArgs)
        If business Is Nothing Then Return
        OpenDialog(New BusinessPermitDialog(BusinessPermitDialog.DialogMode.Renewal, business))
    End Sub

    Private Sub View_Click(sender As Object, e As EventArgs)
        If selected Is Nothing OrElse business Is Nothing Then Return
        Dim canEdit = canManage AndAlso Not BusinessPermitService.IsFinal(selected.Status)
        OpenDialog(New BusinessPermitDialog(If(canEdit, BusinessPermitDialog.DialogMode.Edit,
                                               BusinessPermitDialog.DialogMode.View), business, CopyOf(selected)))
    End Sub

    Private Sub Status_Click(sender As Object, e As EventArgs)
        If selected Is Nothing OrElse Not canManage Then Return
        Using dlg As New UpdateStatusDialog(CopyOf(selected))
            If dlg.ShowDialog(FindForm()) = DialogResult.OK Then ReloadKeepingSelection()
        End Using
    End Sub

    Private Sub Delete_Click(sender As Object, e As EventArgs)
        If selected Is Nothing OrElse Not Session.IsAdmin Then Return
        Dim reference = selected.ReferenceNo
        If Not UiHelper.Confirm("Delete application " & reference & "?" & vbCrLf &
                                "Its endorsements and requirement records will also be removed. This cannot be undone.",
                                "Delete application", "Delete", "Cancel", danger:=True) Then Return
        If BusinessPermitService.DeleteApplication(selected) Then
            LoadPermits(Nothing)
            UiHelper.ShowSuccess("Application " & reference & " was deleted.", "Deleted")
        Else
            UiHelper.ShowWarning("Only applications that are still Submitted can be deleted.")
        End If
    End Sub

    Private Sub OpenDialog(dlg As BusinessPermitDialog)
        Using dlg
            Dim isNew = dlg.Text <> "Edit Application" AndAlso dlg.Text <> "Application Details"
            If dlg.ShowDialog(FindForm()) <> DialogResult.OK Then Return
            LoadPermits(dlg.PermitId)
            If isNew AndAlso selected IsNot Nothing Then
                UiHelper.ShowSuccess("Application " & selected.ReferenceNo & " was submitted." & vbCrLf &
                                     "Next: upload the requirements in the Requirements tab.", "Application filed")
            End If
        End Using
    End Sub

    ''' <summary>Dialogs work on a copy so a cancelled/failed change does not alter the list.</summary>
    Private Shared Function CopyOf(p As BusinessPermit) As BusinessPermit
        Return BusinessPermitRepository.GetById(p.PermitId)
    End Function

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then tips.Dispose()
        MyBase.Dispose(disposing)
    End Sub

End Class
