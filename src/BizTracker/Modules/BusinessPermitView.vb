''' <summary>
''' Business Permits module (the template for the other modules).
'''   Toolbar  : business selector (staff) or the owner's business, and the actions
'''   Cards    : current Mayor's Permit, latest application, amount due
'''   Left     : applications grid with search
'''   Right    : selected application - progress tracker and tabs
'''              (Endorsements | Requirements | Assessment | Payments)
'''   Payments : business tax walk-in payments (partial ones too), Print Receipt and Void
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
        Public Property Balance As String
    End Class

    ''' <summary>Payment history grid row (display only).</summary>
    Private Class PaymentGridRow
        Public Property PaymentId As Integer
        Public Property PaymentDate As String
        Public Property OrNo As String
        Public Property Method As String
        Public Property Reference As String
        Public Property Tax As String
        Public Property Charges As String
        Public Property Total As String
        Public Property ReceivedBy As String
        Public Property Status As String
        Public Property IsVoided As Boolean
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
    Private ReadOnly btnPay As New Button()
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
    Private ReadOnly paymentsPage As New Panel()
    Private ReadOnly paymentGrid As New ModernGrid()
    Private ReadOnly lblPaymentsInfo As New Label()
    Private ReadOnly lblPaymentsEmpty As New Label()
    Private ReadOnly btnPrintReceipt As New Button()
    Private ReadOnly btnVoid As New Button()
    Private history As New List(Of BusinessPermitPayment)
    Private loadingPayments As Boolean
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
    ''' <summary>Record Payment / Void follow AccessService's payment rule (shared with RPT).</summary>
    Private ReadOnly canCollect As Boolean = AccessService.CanCollectPayments(AppScreen.BusinessPermits)
    Private ReadOnly canVoid As Boolean = AccessService.CanVoidPayments(AppScreen.BusinessPermits)

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
        SetupButton(btnPay, "Record Payment", "Pay", "secondary", AddressOf Pay_Click, canCollect)
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
        UiHelper.AddGridColumn(grid, "Balance", "BALANCE", "Balance", 100, 90)
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
        grid.Columns("Balance").Visible = grid.Width >= 520 * s
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
        tabs.Tabs = {"Endorsements", "Requirements", "Assessment", "Payments"}
        AddHandler tabs.SelectedIndexChanged, Sub() ShowTab()
        Dim gap2 As New Panel With {.Dock = DockStyle.Top, .Height = 8, .BackColor = Color.White}

        detailBody.Dock = DockStyle.Fill
        detailBody.BackColor = Color.White
        endorsementList.Dock = DockStyle.Fill
        endorsementList.BackColor = Color.White
        requirements.Dock = DockStyle.Fill
        assessmentList.Dock = DockStyle.Fill
        assessmentList.BackColor = Color.White
        BuildPaymentsPage()
        detailBody.Controls.AddRange({endorsementList, requirements, assessmentList, paymentsPage})
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
                .Status = BusinessTaxService.GetDisplayStatus(p), .Filed = UiHelper.FormatDate(p.DateFiled),
                .Balance = BalanceText(p)
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
        cardLatest.SetValue(latest.ReferenceNo, BusinessTaxService.GetDisplayStatus(latest), latest.ApplicationType & " · " & latest.PermitYear)

        Dim bal = BusinessTaxService.GetBalance(latest, Date.Today)
        Select Case latest.Status
            Case BusinessPermitService.Assessed
                If bal.IsPartiallyPaid Then
                    cardDue.SetValue(UiHelper.FormatMoney(bal.Balance), StatusService.PartiallyPaid,
                                     UiHelper.FormatMoney(bal.TotalPaid) & " paid · " & latest.ReferenceNo)
                Else
                    cardDue.SetValue(UiHelper.FormatMoney(bal.Balance), "Unpaid", latest.ReferenceNo)
                End If
            Case BusinessPermitService.Paid, BusinessPermitService.Issued
                cardDue.SetValue(UiHelper.FormatMoney(If(bal.IsLegacy, BusinessPermitService.GetTotalDue(latest), bal.TotalPaid)), "Paid",
                                 latest.ReferenceNo)
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

        Dim noBusiness = If(Session.IsOwner, "No business is linked to your account.", "Choose a business first.")
        UiHelper.SetAvailable(btnNew, If(Not hasBusiness, noBusiness,
            If(active.Count > 0, "This business already has a business permit. New Application is only for a business's first permit - use Renew Permit for the next year.", Nothing)))

        UiHelper.SetAvailable(btnRenew, If(Not hasBusiness, noBusiness,
            If(active.Count = 0, "There is no previous permit to renew yet - use New Application.",
            If(open IsNot Nothing, "Application " & open.ReferenceNo & " is still in progress (" & open.Status & "). Renew again after it is issued or rejected.", Nothing))))

        Dim noSelection = If(permits.Count = 0, "There are no applications yet.", "Select an application in the list first.")
        UiHelper.SetAvailable(btnView, If(selected Is Nothing, noSelection, Nothing))
        UiHelper.SetAvailable(btnStatus, If(selected Is Nothing, noSelection,
            If(BusinessPermitService.IsFinal(selected.Status), selected.ReferenceNo & " is already " & selected.Status & ". Its status can no longer change.", Nothing)))
        UiHelper.SetAvailable(btnDelete, If(selected Is Nothing, noSelection,
            If(selected.Status <> BusinessPermitService.Submitted, "Only applications that are still Submitted can be deleted. " & selected.ReferenceNo & " is " & selected.Status & ".", Nothing)))
        If canCollect Then
            UiHelper.SetAvailable(btnPay, If(selected Is Nothing, noSelection, BusinessTaxService.GetPaymentBlocker(selected)))
            tips.SetToolTip(btnPay, "Record a business tax payment (full or partial) for the selected application")
        End If
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
        badgeDetail.Text = BusinessTaxService.GetDisplayStatus(selected)
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
        LoadPaymentHistory()
        ShowTab()
    End Sub

    Private Sub ShowTab()
        endorsementList.Visible = tabs.SelectedIndex = 0
        requirements.Visible = tabs.SelectedIndex = 1
        assessmentList.Visible = tabs.SelectedIndex = 2
        paymentsPage.Visible = tabs.SelectedIndex = 3
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
        Dim bal = BusinessTaxService.GetBalance(p, Date.Today)
        Dim settled = bal.IsFullyPaid AndAlso Not bal.IsLegacy
        If bal.IsLegacy Then
            rows.Add(InfoRow("Surcharge (late renewal)", UiHelper.FormatMoney(p.Surcharge), "Interest (late renewal)", UiHelper.FormatMoney(p.Interest)))
            rows.Add(InfoRow("Total paid", UiHelper.FormatMoney(BusinessPermitService.GetTotalDue(p)), "Balance", UiHelper.FormatMoney(0D)))
        ElseIf assessed AndAlso bal.IsAssessed Then
            rows.Add(InfoRow("Surcharge (late renewal)", UiHelper.FormatMoney(If(settled, bal.SurchargePaid, bal.SurchargeCharged)),
                             If(settled, "Interest charged", "Interest (as of today)"),
                             UiHelper.FormatMoney(bal.InterestPaid + If(settled, 0D, bal.InterestDue))))
            rows.Add(InfoRow("Paid so far", UiHelper.FormatMoney(bal.TotalPaid) & If(bal.Payments.Count > 0, " (" & bal.Payments.Count & ")", ""),
                             "Balance (if paid today)", UiHelper.FormatMoney(bal.Balance)))
        Else
            rows.Add(InfoRow("Surcharge (late renewal)", "—", "Interest (late renewal)", "—"))
        End If
        rows.Add(InfoRow("Renewal deadline", If(p.ApplicationType = "Renewal", UiHelper.FormatDate(deadline), "Not applicable (new)"),
                         "Status", BusinessTaxService.GetDisplayStatus(p)))
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
        If bal.IsLegacy Then
            rows.Add(NoteRow(tips, "Paid before payment recording started - no payment rows or receipts in BizTracker.", Theme.TextMuted))
        ElseIf p.Status = BusinessPermitService.Assessed AndAlso bal.Balance > 0 Then
            Dim links As New List(Of RowLink)
            If canCollect Then links.Add(New RowLink("Record Payment", Theme.SidebarBlue, Sub() Pay_Click(Nothing, EventArgs.Empty), "Pay"))
            rows.Add(ListRow(tips, If(bal.IsPartiallyPaid, "Partially paid", "Not yet paid"),
                             UiHelper.FormatMoney(bal.Balance) & " due today" & If(bal.InterestDue > 0, " (interest keeps running)", ""),
                             If(bal.IsPartiallyPaid, StatusService.PartiallyPaid, "Unpaid"), links,
                             If(bal.MonthsLate > 0, Theme.StatusRed, Nothing), If(canCollect, LinkColumnWidth("Record Payment"), 0)))
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

    ''' <summary>Grid "BALANCE": what is still due today, "Paid" when settled, "—" before assessment.</summary>
    Private Shared Function BalanceText(p As BusinessPermit) As String
        If p.AssessedAmount <= 0 OrElse p.Status = BusinessPermitService.Rejected Then Return "—"
        Dim bal = BusinessTaxService.GetBalance(p, Date.Today)
        If Not bal.IsAssessed Then Return "—"
        Return If(bal.IsFullyPaid, "Paid", UiHelper.FormatMoney(bal.Balance))
    End Function

    ' =====================================================================
    '  Payments tab: business tax payments of the selected application
    ' =====================================================================

    Private Sub BuildPaymentsPage()
        paymentsPage.Dock = DockStyle.Fill
        paymentsPage.BackColor = Color.White

        Dim bar As New Panel With {.Dock = DockStyle.Top, .Height = Dpi(44), .BackColor = Color.White}
        lblPaymentsInfo.Font = Theme.SmallBoldFont
        lblPaymentsInfo.ForeColor = Theme.TextMuted
        lblPaymentsInfo.AutoEllipsis = True
        lblPaymentsInfo.TextAlign = ContentAlignment.MiddleLeft
        lblPaymentsInfo.BackColor = Color.White
        btnPrintReceipt.Text = "Print Receipt"
        UiHelper.StyleSecondaryButton(btnPrintReceipt)
        btnVoid.Text = "Void"
        UiHelper.StyleDangerButton(btnVoid)
        btnVoid.Visible = canVoid
        AddHandler btnPrintReceipt.Click, AddressOf PrintReceipt_Click
        AddHandler btnVoid.Click, AddressOf VoidPayment_Click
        bar.Controls.AddRange({lblPaymentsInfo, btnPrintReceipt, btnVoid})
        AddHandler bar.Layout,
            Sub()
                Dim h = Dpi(34)
                Dim top = (bar.ClientSize.Height - h) \ 2
                Dim right = bar.ClientSize.Width
                If btnVoid.Visible Then
                    Dim w = UiHelper.TextWidth(btnVoid.Text, Theme.BodyBoldFont) + Dpi(30)
                    btnVoid.SetBounds(right - w, top, w, h)
                    right = btnVoid.Left - Dpi(8)
                End If
                Dim pw = UiHelper.TextWidth(btnPrintReceipt.Text, Theme.BodyBoldFont) + Dpi(30)
                btnPrintReceipt.SetBounds(right - pw, top, pw, h)
                lblPaymentsInfo.SetBounds(0, top, Math.Max(0, btnPrintReceipt.Left - Dpi(10)), h)
            End Sub

        UiHelper.StyleGrid(paymentGrid, "Status")
        paymentGrid.Dock = DockStyle.Fill
        UiHelper.AddGridColumn(paymentGrid, "PaymentDate", "DATE", "PaymentDate", 95, 70)
        UiHelper.AddGridColumn(paymentGrid, "OrNo", "OR NO.", "OrNo", 85, 85)
        UiHelper.AddGridColumn(paymentGrid, "Method", "METHOD", "Method", 95, 95)
        UiHelper.AddGridColumn(paymentGrid, "Reference", "REFERENCE", "Reference", 120, 110)
        UiHelper.AddGridColumn(paymentGrid, "Tax", "TAX", "Tax", 95, 95)
        UiHelper.AddGridColumn(paymentGrid, "Charges", "SURCH. + INT.", "Charges", 100, 100)
        UiHelper.AddGridColumn(paymentGrid, "Total", "TOTAL", "Total", 100, 100)
        UiHelper.AddGridColumn(paymentGrid, "ReceivedBy", "RECEIVED BY", "ReceivedBy", 130, 120)
        UiHelper.AddGridColumn(paymentGrid, "Status", "STATUS", "Status", 90, UiHelper.TextWidth(PaymentRules.Voided, Theme.SmallBoldFont) + 36)
        For Each colName In {"Tax", "Charges", "Total"}
            paymentGrid.Columns(colName).DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight
            paymentGrid.Columns(colName).HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight
        Next
        AddHandler paymentGrid.SelectionChanged, Sub() If Not loadingPayments Then UpdatePaymentButtons()
        AddHandler paymentGrid.CellToolTipTextNeeded, AddressOf PaymentGrid_CellToolTipTextNeeded
        AddHandler paymentGrid.CellDoubleClick, Sub(s, e) If e.RowIndex >= 0 Then PrintReceipt_Click(Nothing, EventArgs.Empty)
        AddHandler paymentGrid.Resize, Sub() FitPaymentColumns()

        lblPaymentsEmpty.Dock = DockStyle.Fill
        lblPaymentsEmpty.TextAlign = ContentAlignment.MiddleCenter
        lblPaymentsEmpty.Font = Theme.BodyFont
        lblPaymentsEmpty.ForeColor = Theme.TextMuted
        lblPaymentsEmpty.BackColor = Color.White
        lblPaymentsEmpty.Visible = False

        paymentsPage.Controls.Add(lblPaymentsEmpty)
        paymentsPage.Controls.Add(paymentGrid)
        paymentsPage.Controls.Add(bar)
    End Sub

    ''' <summary>Hides the less important columns when the card is narrow (date, OR, total and status always stay).</summary>
    Private Sub FitPaymentColumns()
        Dim s = DeviceDpi / 96.0
        Dim w = paymentGrid.Width
        paymentGrid.Columns("Tax").Visible = w >= 480 * s
        paymentGrid.Columns("Charges").Visible = w >= 580 * s
        paymentGrid.Columns("Method").Visible = w >= 675 * s
        paymentGrid.Columns("ReceivedBy").Visible = w >= 795 * s
        paymentGrid.Columns("Reference").Visible = w >= 905 * s
    End Sub

    ''' <summary>"Jun 25" for this year's payments, "Jan 5, 2025" for older ones.</summary>
    Private Shared Function PaymentDateText(value As Date) As String
        Return If(value.Year = Date.Today.Year, value.ToString("MMM d"), UiHelper.FormatDate(value))
    End Function

    Private Sub LoadPaymentHistory()
        history = If(selected Is Nothing, New List(Of BusinessPermitPayment)(), BusinessTaxService.GetPaymentHistory(selected.PermitId))
        Dim rows = history.Select(Function(x) New PaymentGridRow With {
            .PaymentId = x.PaymentId, .PaymentDate = PaymentDateText(x.PaymentDate), .OrNo = x.OrNo, .Method = x.PaymentMethod,
            .Reference = If(x.ReferenceNo = "", "—", x.ReferenceNo), .Tax = UiHelper.FormatMoney(x.AmountPaid),
            .Charges = UiHelper.FormatMoney(x.Surcharge + x.Interest), .Total = UiHelper.FormatMoney(x.Total),
            .ReceivedBy = If(x.ReceivedByName = "", "—", x.ReceivedByName),
            .Status = If(x.IsVoided, PaymentRules.Voided, StatusService.Paid), .IsVoided = x.IsVoided
        }).ToList()
        paymentGrid.Columns("PaymentDate").MinimumWidth =
            Math.Max(Dpi(70), rows.Select(Function(r) UiHelper.TextWidth(r.PaymentDate, Theme.BodyFont) + Dpi(22)).DefaultIfEmpty(0).Max())
        loadingPayments = True
        paymentGrid.DataSource = rows
        For Each gridRow As DataGridViewRow In paymentGrid.Rows
            If TryCast(gridRow.DataBoundItem, PaymentGridRow)?.IsVoided Then gridRow.DefaultCellStyle.ForeColor = Theme.TextMuted
        Next
        FitPaymentColumns()
        paymentGrid.ClearSelection()
        If paymentGrid.Rows.Count > 0 Then
            paymentGrid.CurrentCell = paymentGrid.Rows(0).Cells("PaymentDate")
            paymentGrid.Rows(0).Selected = True
        End If
        loadingPayments = False

        Dim active = history.Where(Function(x) Not x.IsVoided).ToList()
        Dim voidedCount = history.Count - active.Count
        lblPaymentsInfo.Text = If(history.Count = 0, "",
            active.Count & " payment" & If(active.Count = 1, "", "s") & " · " & UiHelper.FormatMoney(active.Sum(Function(x) x.Total)) & " collected" &
            If(voidedCount > 0, " · " & voidedCount & " voided", ""))
        tips.SetToolTip(lblPaymentsInfo, lblPaymentsInfo.Text)
        lblPaymentsEmpty.Visible = history.Count = 0
        lblPaymentsEmpty.Text = If(selected IsNot Nothing AndAlso selected.LegacyPaid,
                                   "Paid before payment recording started." & vbCrLf & "There are no payment rows for this application.",
                                   "No business tax payments recorded yet." &
                                   If(canCollect, vbCrLf & "Use ""Record Payment"" once the application is Assessed.", ""))
        If lblPaymentsEmpty.Visible Then lblPaymentsEmpty.BringToFront()
        UpdatePaymentButtons()
    End Sub

    Private ReadOnly Property SelectedPayment As BusinessPermitPayment
        Get
            If paymentGrid.SelectedRows.Count = 0 Then Return Nothing
            Dim gridRow = TryCast(paymentGrid.SelectedRows(0).DataBoundItem, PaymentGridRow)
            Return If(gridRow Is Nothing, Nothing, history.FirstOrDefault(Function(x) x.PaymentId = gridRow.PaymentId))
        End Get
    End Property

    Private Sub SelectPayment(paymentId As Integer)
        For Each gridRow As DataGridViewRow In paymentGrid.Rows
            If TryCast(gridRow.DataBoundItem, PaymentGridRow)?.PaymentId = paymentId Then
                paymentGrid.ClearSelection()
                paymentGrid.CurrentCell = gridRow.Cells("PaymentDate")
                gridRow.Selected = True
                Exit For
            End If
        Next
        UpdatePaymentButtons()
    End Sub

    Private Sub UpdatePaymentButtons()
        Dim pay = SelectedPayment
        UiHelper.SetAvailable(btnPrintReceipt,
            If(pay Is Nothing, "Select a payment in the list first.",
               If(pay.IsVoided, "OR " & pay.OrNo & " was voided - a voided receipt cannot be printed.", Nothing)))
        tips.SetToolTip(btnPrintReceipt, "Print the official receipt of the selected payment")
        If Not canVoid Then Return
        Dim blocker As String = Nothing
        If pay Is Nothing Then
            blocker = "Select a payment in the list first."
        ElseIf pay.IsVoided Then
            blocker = "OR " & pay.OrNo & " is already voided."
        ElseIf selected IsNot Nothing AndAlso selected.Status = BusinessPermitService.Issued AndAlso
               Not AccessService.CanVoidPayments(AppScreen.BusinessPermits, afterPermitIssued:=True) Then
            blocker = "The Mayor's Permit is already issued - only an Admin can void its payments."
        Else
            Dim newest = history.Where(Function(x) Not x.IsVoided).OrderByDescending(Function(x) x.PaymentId).First()
            If newest.PaymentId <> pay.PaymentId Then blocker = "Only the newest payment can be voided - void OR " & newest.OrNo & " first."
        End If
        UiHelper.SetAvailable(btnVoid, blocker)
        tips.SetToolTip(btnVoid, "Void the selected payment (mistake correction) - it stays in the history")
    End Sub

    Private Sub PaymentGrid_CellToolTipTextNeeded(sender As Object, e As DataGridViewCellToolTipTextNeededEventArgs)
        If e.RowIndex < 0 OrElse e.RowIndex >= paymentGrid.Rows.Count Then Return
        Dim gridRow = TryCast(paymentGrid.Rows(e.RowIndex).DataBoundItem, PaymentGridRow)
        Dim pay = If(gridRow Is Nothing, Nothing, history.FirstOrDefault(Function(x) x.PaymentId = gridRow.PaymentId))
        If pay Is Nothing Then Return
        Dim lines As New List(Of String) From {
            "OR " & pay.OrNo & " · " & UiHelper.FormatDate(pay.PaymentDate),
            UiHelper.FormatMoney(pay.AmountPaid) & " tax + " & UiHelper.FormatMoney(pay.Surcharge) & " surcharge + " &
                UiHelper.FormatMoney(pay.Interest) & " interest = " & UiHelper.FormatMoney(pay.Total),
            pay.PaymentMethod & If(pay.ReferenceNo <> "", " · Ref. " & pay.ReferenceNo, "") &
                If(pay.ReceivedByName <> "", " · received by " & pay.ReceivedByName, "")
        }
        If pay.Remarks <> "" Then lines.Add("Remarks: " & pay.Remarks)
        If pay.IsVoided Then
            lines.Add("VOIDED " & UiHelper.FormatDate(pay.VoidedAt) & If(pay.VoidedByName <> "", " by " & pay.VoidedByName, "") & ": " & pay.VoidReason)
        End If
        e.ToolTipText = String.Join(vbCrLf, lines)
    End Sub

    Private Sub Pay_Click(sender As Object, e As EventArgs)
        If selected Is Nothing OrElse Not canCollect Then Return
        Dim current = CopyOf(selected)
        Dim blocker = If(current Is Nothing, "This application no longer exists.", BusinessTaxService.GetPaymentBlocker(current))
        If blocker IsNot Nothing Then
            UiHelper.ShowWarning(blocker)
            Return
        End If
        Dim result As BusinessTaxPaymentResult
        Using dlg As New BusinessPermitPaymentDialog(current)
            If dlg.ShowDialog(FindForm()) <> DialogResult.OK Then Return
            result = dlg.Result
        End Using
        LoadPermits(current.PermitId)
        tabs.SelectedIndex = 3          ' Payments: the new payment is at the top, ready to print
        SelectPayment(result.Payment.PaymentId)

        Dim pay = result.Payment
        Dim message = "OR " & pay.OrNo & " recorded: " & UiHelper.FormatMoney(pay.Total) &
                      If(pay.Surcharge + pay.Interest > 0, " (" & UiHelper.FormatMoney(pay.Interest) & " interest + " &
                         UiHelper.FormatMoney(pay.Surcharge) & " surcharge + " & UiHelper.FormatMoney(pay.AmountPaid) & " tax)", "") & "."
        If result.BecamePaid Then
            message &= vbCrLf & current.ReferenceNo & " is now fully paid and marked Paid." & vbCrLf &
                       "Next: issue the Mayor's Permit with ""Update Status""."
        Else
            Dim left = BusinessTaxService.GetBalance(selected, Date.Today).Balance
            message &= vbCrLf & UiHelper.FormatMoney(left) & " is still due on " & current.ReferenceNo & " (partial payment)."
        End If
        message &= vbCrLf & "Click ""Print Receipt"" in the Payments tab for a printed copy."
        UiHelper.ShowSuccess(message, "Payment recorded")
    End Sub

    Private Sub PrintReceipt_Click(sender As Object, e As EventArgs)
        Dim pay = SelectedPayment
        If pay Is Nothing Then Return
        Dim doc = DocumentService.GetBusinessTaxReceipt(pay.PaymentId)
        Dim blocker = If(doc Is Nothing, "This receipt is not available.", DocumentService.GetPrintBlocker(doc))
        If blocker IsNot Nothing Then
            UiHelper.ShowWarning(blocker, "Cannot print")
            Return
        End If
        Dim printed = ReportService.Print(doc)
        If printed.ErrorMessage IsNot Nothing Then UiHelper.ShowWarning(printed.ErrorMessage, "Cannot print")
    End Sub

    Private Sub VoidPayment_Click(sender As Object, e As EventArgs)
        Dim pay = SelectedPayment
        If pay Is Nothing OrElse Not canVoid Then Return
        Dim impact = BusinessTaxService.GetVoidImpact(pay.PaymentId)
        If impact.Blocker IsNot Nothing Then
            UiHelper.ShowWarning(impact.Blocker, "Cannot void")
            Return
        End If

        Dim message = "OR " & pay.OrNo & " · " & UiHelper.FormatMoney(pay.Total) & " paid " & UiHelper.FormatDate(pay.PaymentDate) & vbCrLf &
                      "It stays in the history as Voided, no longer counts toward the balance, and its OR number stays used."
        If impact.RevertsToAssessed Then message &= vbCrLf & impact.Permit.ReferenceNo & " goes back from Paid to Assessed."
        If impact.IssuedWarning IsNot Nothing Then message &= vbCrLf & vbCrLf & "WARNING: " & impact.IssuedWarning
        If Not UiHelper.Confirm(message, "Void OR " & pay.OrNo & "?", "Continue", "Cancel", danger:=True) Then Return

        Dim reason = PromptDialog.Ask(FindForm(), "Void Payment", "OR " & pay.OrNo & " · " & impact.Permit.ReferenceNo & " · " & UiHelper.FormatMoney(pay.Total),
                                      "Reason for voiding (required, e.g. wrong amount entered)", True, "Void Payment", "danger")
        If reason Is Nothing Then Return
        Dim problem = BusinessTaxService.VoidPayment(pay.PaymentId, reason)
        If problem IsNot Nothing Then
            UiHelper.ShowWarning(problem, "Cannot void")
            Return
        End If

        LoadPermits(pay.PermitId)
        tabs.SelectedIndex = 3
        SelectPayment(pay.PaymentId)
        UiHelper.ShowSuccess("OR " & pay.OrNo & " was voided. It stays in the payment history; its OR number stays used." &
                             If(impact.RevertsToAssessed, vbCrLf & impact.Permit.ReferenceNo & " is Assessed again.", "") &
                             If(impact.IssuedWarning IsNot Nothing, vbCrLf & impact.IssuedWarning & " This was noted in the audit log.", "") &
                             vbCrLf & "Next: record the correct payment with ""Record Payment"".", "Payment voided")
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
