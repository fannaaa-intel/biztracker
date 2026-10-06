''' <summary>
''' Health Certificates module (same structure as BusinessPermitView).
'''   Toolbar  : business selector (staff) or the owner's business, and the actions
'''   Cards    : Total Personnel, Valid, Expiring Soon, Expired, Compliance %
'''   Left     : employees grid with search (Ctrl/Shift-click selects several for "Renew Selected")
'''   Right    : selected employee - validity bar and tabs (Certificate | History | Renew Now)
''' Every status is computed by StatusService from today's date. No SQL here.
''' </summary>
Public Class HealthCertificateView
    Inherits ModuleView

    ''' <summary>Grid row (display only).</summary>
    Private Class EmployeeRow
        Public Property EmployeeId As Integer
        Public Property Name As String
        Public Property Position As String
        Public Property Category As String
        Public Property CertificateNo As String
        Public Property Issued As String
        Public Property Expiry As String
        Public Property Status As String
    End Class

    Private Shared lastBusinessId As Integer   ' remembers the staff's last selected business

    ' Toolbar
    Private ReadOnly businessBox As New RoundedPanel()
    Private ReadOnly cboBusiness As New ComboBox()
    Private ReadOnly lblBusiness As New Label()
    Private ReadOnly actions As New FlowLayoutPanel()
    Private ReadOnly btnAdd As New Button()
    Private ReadOnly btnEdit As New Button()
    Private ReadOnly btnDeactivate As New Button()
    Private ReadOnly btnIssue As New Button()
    Private ReadOnly btnRenewSelected As New Button()
    Private ReadOnly buttonTexts As New Dictionary(Of Button, String())   ' {full, short}

    ' Summary cards
    Private ReadOnly cardTotal As New StatCard(Icons.Contact, "Total Personnel")
    Private ReadOnly cardValid As New StatCard(Icons.CheckMark, "Valid")
    Private ReadOnly cardExpiring As New StatCard(Icons.Calendar, "Expiring Soon")
    Private ReadOnly cardExpired As New StatCard(Icons.Warning, "Expired")
    Private ReadOnly cardCompliance As New StatCard(Icons.Health, "Compliance")

    ' Employee list
    Private ReadOnly txtSearch As New TextBox()
    Private ReadOnly grid As New ModernGrid()
    Private ReadOnly lblEmpty As New Label()

    ' Details
    Private ReadOnly detailHeader As New Panel()
    Private ReadOnly lblDetailName As New Label()
    Private ReadOnly badgeDetail As New StatusBadge()
    Private ReadOnly lblDetailInfo As New Label()
    Private ReadOnly validity As New ValidityBar()
    Private ReadOnly tabs As New SegmentedTabs()
    Private ReadOnly certificateList As New WheelScrollPanel()
    Private ReadOnly historyList As New WheelScrollPanel()
    Private ReadOnly renewList As New WheelScrollPanel()
    Private ReadOnly detailBody As New Panel()
    Private ReadOnly detailContent As New Panel()
    Private ReadOnly lblDetailEmpty As New Label()
    Private ReadOnly tips As New ToolTip()

    ' State
    Private business As Business
    Private roster As New List(Of EmployeeHealth)
    Private selected As EmployeeHealth
    Private loading As Boolean
    Private renewCount As Integer          ' employees in the "Renew Now" list
    Private ReadOnly canManage As Boolean = AccessService.CanManage(AppScreen.HealthCertificates)

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
            Return "Employee health certificates, validity and renewals"
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

        ' Action buttons (staff only; right-aligned; labels shorten when the window is narrow)
        actions.Dock = DockStyle.Right
        actions.AutoSize = True
        actions.WrapContents = False
        actions.FlowDirection = FlowDirection.RightToLeft
        actions.Padding = New Padding(0, 3, 0, 0)
        actions.BackColor = Theme.ContentBackground
        SetupButton(btnDeactivate, "Deactivate", "Remove", "danger", AddressOf Deactivate_Click)
        SetupButton(btnRenewSelected, "Renew Selected", "Bulk Renew", "secondary", AddressOf RenewSelected_Click)
        SetupButton(btnIssue, "Issue / Renew", "Renew", "secondary", AddressOf Issue_Click)
        SetupButton(btnEdit, "Edit Employee", "Edit", "secondary", AddressOf Edit_Click)
        SetupButton(btnAdd, "Add Employee", "Add", "primary", AddressOf AddEmployee_Click)
        bar.Controls.Add(actions)

        AddHandler bar.Resize, Sub() FitToolbar(bar)
        Return bar
    End Function

    Private Sub SetupButton(btn As Button, fullText As String, shortText As String, style As String, handler As EventHandler)
        btn.Height = 40
        btn.Margin = New Padding(8, 0, 0, 0)
        Select Case style
            Case "primary" : UiHelper.StylePrimaryButton(btn)
            Case "danger" : UiHelper.StyleDangerButton(btn)
            Case Else : UiHelper.StyleSecondaryButton(btn)
        End Select
        btn.Visible = canManage            ' Owners only view their certificates
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
        tips.SetToolTip(btnAdd, "Add a staff member")
        tips.SetToolTip(btnEdit, "Edit the selected employee")
        tips.SetToolTip(btnDeactivate, "Remove the selected employee from the list (their certificate history is kept)")
    End Sub

    Private Function BuildCards() As Control
        Dim cards = {cardTotal, cardValid, cardExpiring, cardExpired, cardCompliance}
        Dim row As New TableLayoutPanel With {
            .Dock = DockStyle.Fill, .ColumnCount = cards.Length, .RowCount = 1, .BackColor = Theme.ContentBackground,
            .Margin = New Padding(0), .Padding = New Padding(0)
        }
        row.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        For i = 0 To cards.Length - 1
            row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F / cards.Length))
            cards(i).Dock = DockStyle.Fill
            cards(i).Margin = New Padding(If(i = 0, 0, 7), 0, If(i = cards.Length - 1, 0, 7), 14)
            row.Controls.Add(cards(i), i, 0)
        Next
        Return row
    End Function

    Private Function BuildMain() As Control
        Dim row As New TableLayoutPanel With {
            .Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 1, .BackColor = Theme.ContentBackground,
            .Margin = New Padding(0), .Padding = New Padding(0)
        }
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 58))
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 42))
        row.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        row.Controls.Add(BuildListCard(), 0, 0)
        row.Controls.Add(BuildDetailCard(), 1, 0)
        Return row
    End Function

    Private Function BuildListCard() As Control
        Dim card As New RoundedPanel With {.Dock = DockStyle.Fill, .Margin = New Padding(0, 0, 8, 0), .Padding = New Padding(16, 14, 16, 16)}

        Dim header As New Panel With {.Dock = DockStyle.Top, .Height = 48, .BackColor = Color.White}
        Dim title As New Label With {.Text = "Employees", .Font = Theme.SubtitleFont, .ForeColor = Theme.TextDark,
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
        AddHandler txtSearch.TextChanged, Sub() BindGrid(selected?.Employee.EmployeeId)

        UiHelper.StyleGrid(grid, "Status")
        grid.MultiSelect = True            ' Ctrl/Shift-click for "Renew Selected"
        grid.Dock = DockStyle.Fill
        UiHelper.AddGridColumn(grid, "Name", "NAME", "Name", 170, 120)
        UiHelper.AddGridColumn(grid, "Position", "POSITION", "Position", 120, 90)
        UiHelper.AddGridColumn(grid, "Category", "CATEGORY", "Category", 105, 100)
        UiHelper.AddGridColumn(grid, "CertNo", "CERTIFICATE NO.", "CertificateNo", 125, 125)
        UiHelper.AddGridColumn(grid, "Issued", "ISSUED", "Issued", 112, 112)
        UiHelper.AddGridColumn(grid, "Expiry", "EXPIRY", "Expiry", 112, 112)
        UiHelper.AddGridColumn(grid, "Status", "STATUS", "Status", 125, 125)
        AddHandler grid.SelectionChanged, AddressOf Grid_SelectionChanged
        AddHandler grid.CellDoubleClick, Sub(s, e) If e.RowIndex >= 0 AndAlso canManage Then Edit_Click(Nothing, EventArgs.Empty)
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
        ' Name + Status always show; the rest appear as the grid gets wider.
        Dim s = DeviceDpi / 96.0
        grid.Columns("Expiry").Visible = grid.Width >= 390 * s
        grid.Columns("CertNo").Visible = grid.Width >= 520 * s
        grid.Columns("Position").Visible = grid.Width >= 640 * s
        grid.Columns("Category").Visible = grid.Width >= 760 * s
        grid.Columns("Issued").Visible = grid.Width >= 860 * s
    End Sub

    Private Function BuildDetailCard() As Control
        Dim card As New RoundedPanel With {.Dock = DockStyle.Fill, .Margin = New Padding(8, 0, 0, 0), .Padding = New Padding(18, 14, 18, 16)}

        detailHeader.Dock = DockStyle.Top
        detailHeader.Height = Dpi(52)
        detailHeader.BackColor = Color.White
        lblDetailName.Font = Theme.SubtitleFont
        lblDetailName.ForeColor = Theme.TextDark
        lblDetailName.AutoSize = False
        lblDetailName.AutoEllipsis = True
        lblDetailName.BackColor = Color.White
        badgeDetail.Height = 24
        lblDetailInfo.Font = Theme.SmallFont
        lblDetailInfo.ForeColor = Theme.TextMuted
        lblDetailInfo.AutoEllipsis = True
        lblDetailInfo.BackColor = Color.White
        detailHeader.Controls.AddRange({lblDetailName, badgeDetail, lblDetailInfo})
        AddHandler detailHeader.Resize, Sub() LayoutDetailHeader()

        validity.Dock = DockStyle.Top
        validity.Height = 42
        Dim gap1 As New Panel With {.Dock = DockStyle.Top, .Height = 10, .BackColor = Color.White}

        tabs.Dock = DockStyle.Top
        tabs.Tabs = {"Certificate", "History", "Renew Now"}
        AddHandler tabs.SelectedIndexChanged, Sub() ShowTab()
        AddHandler tabs.Resize, Sub() SetTabLabels()
        Dim gap2 As New Panel With {.Dock = DockStyle.Top, .Height = 8, .BackColor = Color.White}

        detailBody.Dock = DockStyle.Fill
        detailBody.BackColor = Color.White
        For Each list In {certificateList, historyList, renewList}
            list.Dock = DockStyle.Fill
            list.BackColor = Color.White
        Next
        detailBody.Controls.AddRange({certificateList, historyList, renewList})

        lblDetailEmpty.Dock = DockStyle.Fill
        lblDetailEmpty.TextAlign = ContentAlignment.MiddleCenter
        lblDetailEmpty.Font = Theme.BodyFont
        lblDetailEmpty.ForeColor = Theme.TextMuted
        lblDetailEmpty.BackColor = Color.White
        lblDetailEmpty.Text = "Select an employee to see their certificate."

        ' Everything for the selected employee lives in detailContent, so the
        ' empty message can replace it completely when nothing is selected.
        detailContent.Dock = DockStyle.Fill
        detailContent.BackColor = Color.White
        detailContent.Controls.Add(detailBody)
        detailContent.Controls.Add(gap2)
        detailContent.Controls.Add(tabs)
        detailContent.Controls.Add(gap1)
        detailContent.Controls.Add(validity)
        detailContent.Controls.Add(detailHeader)
        card.Controls.Add(lblDetailEmpty)
        card.Controls.Add(detailContent)
        Return card
    End Function

    ''' <summary>Name (ellipsized if long) + status badge on one line, info line underneath.</summary>
    Private Sub LayoutDetailHeader()
        Dim w = detailHeader.ClientSize.Width
        Dim gap = CInt(10 * DeviceDpi / 96.0)
        Dim badgeW = If(badgeDetail.Visible, badgeDetail.Width + gap, 0)
        Dim nameNeed = UiHelper.TextWidth(lblDetailName.Text, lblDetailName.Font) + 4
        lblDetailName.SetBounds(0, 0, Math.Max(40, Math.Min(nameNeed, w - badgeW)), CInt(28 * DeviceDpi / 96.0))
        badgeDetail.Location = New Point(lblDetailName.Right + gap, CInt(3 * DeviceDpi / 96.0))
        lblDetailInfo.SetBounds(0, CInt(30 * DeviceDpi / 96.0), w, CInt(20 * DeviceDpi / 96.0))
    End Sub

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
            LoadRoster(Nothing)
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
            LoadRoster(Nothing)
        End If
    End Sub

    Private Sub Business_Changed(sender As Object, e As EventArgs)
        If loading Then Return
        business = TryCast(cboBusiness.SelectedItem, Business)
        If business IsNot Nothing Then lastBusinessId = business.BusinessId
        txtSearch.Text = ""
        LoadRoster(Nothing)
    End Sub

    ''' <summary>Reloads the business's employees + certificates and re-selects an employee.</summary>
    Private Sub LoadRoster(selectEmployeeId As Integer?)
        roster = If(business Is Nothing, New List(Of EmployeeHealth)(), HealthCertificateService.GetRoster(business.BusinessId))
        UpdateCards()
        BindGrid(selectEmployeeId)
    End Sub

    Private Sub ReloadKeepingSelection()
        LoadRoster(selected?.Employee.EmployeeId)
    End Sub

    Private Sub BindGrid(selectEmployeeId As Integer?)
        Dim term = txtSearch.Text.Trim().ToLowerInvariant()
        Dim rows = roster.
            Where(Function(r) term = "" OrElse
                              (r.Employee.FullName & " " & r.Employee.Position & " " & r.Employee.Category & " " &
                               r.Certificate?.CertificateNo & " " & r.Status).ToLowerInvariant().Contains(term)).
            OrderBy(Function(r) r.Employee.FullName).
            Select(Function(r) New EmployeeRow With {
                .EmployeeId = r.Employee.EmployeeId, .Name = r.Employee.FullName, .Position = r.Employee.Position,
                .Category = r.Employee.Category, .Status = r.Status,
                .CertificateNo = If(r.Certificate?.CertificateNo, "—"),
                .Issued = UiHelper.FormatDate(r.Certificate?.IssueDate),
                .Expiry = UiHelper.FormatDate(r.Certificate?.ExpiryDate)
            }).ToList()

        loading = True
        grid.DataSource = rows
        FitGridColumns()
        Dim index = If(selectEmployeeId.HasValue, rows.FindIndex(Function(r) r.EmployeeId = selectEmployeeId.Value), -1)
        If index < 0 AndAlso rows.Count > 0 Then index = 0
        grid.ClearSelection()
        If index >= 0 Then
            grid.CurrentCell = grid.Rows(index).Cells("Name")
            grid.Rows(index).Selected = True
        End If
        loading = False

        lblEmpty.Visible = rows.Count = 0
        If rows.Count = 0 Then
            lblEmpty.Text = If(business Is Nothing, "No business selected.",
                               If(term <> "", "No employees match your search.",
                                  "No employees yet." & If(canManage, vbCrLf & "Click ""Add Employee"" to add one.", "")))
            lblEmpty.BringToFront()
        End If
        ShowSelected()
    End Sub

    Private Sub Grid_SelectionChanged(sender As Object, e As EventArgs)
        If loading Then Return
        ShowSelected()
    End Sub

    ''' <summary>The employees of all selected grid rows (several when Ctrl/Shift-clicking).</summary>
    Private Function SelectedRoster() As List(Of EmployeeHealth)
        Dim ids = grid.SelectedRows.Cast(Of DataGridViewRow)().
                  Select(Function(r) TryCast(r.DataBoundItem, EmployeeRow)).
                  Where(Function(r) r IsNot Nothing).Select(Function(r) r.EmployeeId).ToList()
        Return roster.Where(Function(r) ids.Contains(r.Employee.EmployeeId)).OrderBy(Function(r) r.Employee.FullName).ToList()
    End Function

    Private Sub ShowSelected()
        ' The details show the CURRENT row when it is selected (the last one clicked), otherwise the first selected row
        Dim row As EmployeeRow = Nothing
        If grid.CurrentRow IsNot Nothing AndAlso grid.CurrentRow.Selected Then
            row = TryCast(grid.CurrentRow.DataBoundItem, EmployeeRow)
        ElseIf grid.SelectedRows.Count > 0 Then
            row = TryCast(grid.SelectedRows(0).DataBoundItem, EmployeeRow)
        End If
        selected = If(row Is Nothing, Nothing, roster.FirstOrDefault(Function(r) r.Employee.EmployeeId = row.EmployeeId))
        ShowDetails()
        UpdateButtons()
    End Sub

    ' =====================================================================
    '  Cards, buttons and details
    ' =====================================================================

    Private Sub UpdateCards()
        If business Is Nothing Then
            For Each c In {cardTotal, cardValid, cardExpiring, cardExpired, cardCompliance}
                c.SetValue("—", "", "No business selected")
            Next
            Return
        End If
        Dim sum = HealthCertificateService.GetSummary(roster)
        cardTotal.SetValue(sum.Total.ToString(), "",
                           If(sum.NoRecord > 0, sum.NoRecord & " without certificate", "active employees"))
        cardValid.SetValue(sum.Valid.ToString(), "", "certificates in force")
        cardExpiring.SetValue(sum.ExpiringSoon.ToString(), "", "within " & StatusService.WarningDays & " days")
        cardExpired.SetValue(sum.Expired.ToString(), "", If(sum.Expired > 0, "renew now", "none"))
        cardCompliance.SetValue(If(sum.Total = 0, "—", sum.CompliancePercent & "%"), "",
                                sum.Valid & " of " & sum.Total & " valid")
    End Sub

    Private Sub UpdateButtons()
        Dim hasBusiness = business IsNot Nothing
        btnAdd.Enabled = hasBusiness
        btnEdit.Enabled = selected IsNot Nothing
        btnDeactivate.Enabled = selected IsNot Nothing

        Dim blocker = HealthCertificateService.GetIssueBlocker(selected)
        btnIssue.Enabled = blocker Is Nothing
        tips.SetToolTip(btnIssue, If(blocker, If(selected?.Certificate Is Nothing, "Issue the first health certificate",
                                                 "Renew the health certificate")))

        Dim picked = SelectedRoster()
        Dim due = picked.Where(Function(r) r.NeedsRenewal).Count()
        btnRenewSelected.Enabled = picked.Count >= 2 AndAlso due > 0
        tips.SetToolTip(btnRenewSelected,
            If(picked.Count < 2, "Ctrl+click or Shift+click several employees, then renew them together",
               If(due = 0, "None of the selected certificates is due for renewal",
                  "Renew " & due & " of the " & picked.Count & " selected employees")))
    End Sub

    Private Sub ShowDetails()
        Dim renewDue = HealthCertificateService.GetRenewalList(roster)
        renewCount = renewDue.Count
        SetTabLabels()

        Dim has = selected IsNot Nothing
        lblDetailEmpty.Visible = Not has
        detailContent.Visible = has
        If Not has Then
            lblDetailEmpty.Text = If(roster.Count = 0, "Nothing to show yet.", "Select an employee to see their certificate.")
            Return
        End If

        Dim emp = selected.Employee
        Dim cert = selected.Certificate
        lblDetailName.Text = emp.FullName
        tips.SetToolTip(lblDetailName, emp.FullName)
        badgeDetail.Text = selected.Status
        badgeDetail.Visible = True
        lblDetailInfo.Text = emp.Position & " · " & emp.Category & If(cert Is Nothing, "", " · " & cert.CertificateNo)
        tips.SetToolTip(lblDetailInfo, lblDetailInfo.Text)
        LayoutDetailHeader()

        ' Validity bar: how much of the certificate's validity period is left
        If cert Is Nothing Then
            validity.SetValue("No health certificate on record", 0, StatusService.Expired, "Not issued")
        Else
            Dim totalDays = Math.Max(1, (cert.ExpiryDate - cert.IssueDate).Days)
            Dim daysLeft = selected.DaysLeft.Value
            validity.SetValue(UiHelper.FormatDate(cert.IssueDate) & "  –  " & UiHelper.FormatDate(cert.ExpiryDate),
                              daysLeft / CDbl(totalDays), selected.Status,
                              If(daysLeft >= 0, daysLeft & If(daysLeft = 1, " day", " days") & " left",
                                 "Expired " & -daysLeft & If(daysLeft = -1, " day", " days") & " ago"))
        End If

        certificateList.SetRows(CreateCertificateRows(selected))
        historyList.SetRows(CreateHistoryRows(emp))
        renewList.SetRows(CreateRenewRows(renewDue))
        ShowTab()
    End Sub

    ''' <summary>"Renew Now (2)", or the shorter "Renew (2)" when the tab is too narrow.</summary>
    Private Sub SetTabLabels()
        Dim count = If(renewCount > 0, " (" & renewCount & ")", "")
        Dim full = "Renew Now" & count
        Dim fits = TextRenderer.MeasureText(full, tabs.Font).Width + CInt(20 * DeviceDpi / 96.0) <= tabs.Width \ 3
        Dim label = If(fits, full, "Renew" & count)
        If tabs.Tabs.Length <> 3 OrElse tabs.Tabs(2) <> label Then tabs.Tabs = {"Certificate", "History", label}
    End Sub

    Private Sub ShowTab()
        certificateList.Visible = tabs.SelectedIndex = 0
        historyList.Visible = tabs.SelectedIndex = 1
        renewList.Visible = tabs.SelectedIndex = 2
        detailBody.PerformLayout()        ' size the newly shown tab right away
    End Sub

    ' ---------- Certificate tab ----------

    Private Function CreateCertificateRows(r As EmployeeHealth) As List(Of Control)
        Dim rows As New List(Of Control)
        Dim cert = r.Certificate
        If cert Is Nothing Then
            rows.Add(NoteRow("This employee has no health certificate yet." &
                             If(canManage, " Click ""Issue / Renew"" to issue one.", " Please ask the Municipal Health Office."),
                             Theme.StatusAmber))
        Else
            rows.Add(InfoRow("Certificate No.", cert.CertificateNo, "Status", r.Status))
            rows.Add(InfoRow("Date issued", UiHelper.FormatDate(cert.IssueDate), "Valid until", UiHelper.FormatDate(cert.ExpiryDate)))
            rows.Add(InfoRow("Issued by", If(cert.IssuedBy <> "", cert.IssuedBy, "—"),
                             "Days left", If(r.DaysLeft.Value >= 0, r.DaysLeft.Value.ToString(), "Expired")))
        End If
        rows.Add(InfoRow("Position", r.Employee.Position, "Category", r.Employee.Category))
        If r.NeedsRenewal AndAlso cert IsNot Nothing Then
            rows.Insert(0, NoteRow(If(r.Status = StatusService.Expired,
                                      "This certificate has expired. The employee should not work until it is renewed.",
                                      "This certificate expires soon. Renew it before " & UiHelper.FormatDate(cert.ExpiryDate) & "."),
                                   Theme.StatusColor(r.Status)))
        End If
        Return rows
    End Function

    ' ---------- History tab ----------

    Private Function CreateHistoryRows(emp As Employee) As List(Of Control)
        Dim history = HealthCertificateService.GetHistory(emp)
        If history.Count = 0 Then Return New List(Of Control) From {NoteRow("No certificates on record yet.", Theme.TextMuted)}
        Return history.Select(Function(c, i) ListRow(
            c.CertificateNo & If(i = 0, "  (current)", ""),
            UiHelper.FormatDate(c.IssueDate) & " to " & UiHelper.FormatDate(c.ExpiryDate) &
                If(c.IssuedBy <> "", " · " & c.IssuedBy, ""),
            StatusService.GetExpiryStatus(c.ExpiryDate), Nothing, Nothing)).ToList()
    End Function

    ' ---------- Renew Now tab ----------

    Private Function CreateRenewRows(due As List(Of EmployeeHealth)) As List(Of Control)
        Dim rows As New List(Of Control)
        If due.Count = 0 Then
            rows.Add(NoteRow("All health certificates are valid. Nothing to renew.", Theme.StatusGreen))
            Return rows
        End If
        If canManage AndAlso due.Count > 1 Then
            rows.Add(ListRow(due.Count & " employees need a new certificate", "Renew them all at once with the same issue date",
                             "", "Renew all", Sub() OpenIssueDialog(due)))
        End If
        For Each r In due
            Dim item = r
            Dim detail As String
            If r.Certificate Is Nothing Then
                detail = r.Employee.Position & " · no certificate yet"
            ElseIf r.DaysLeft.Value < 0 Then
                detail = r.Employee.Position & " · expired " & -r.DaysLeft.Value & If(r.DaysLeft.Value = -1, " day", " days") & " ago"
            Else
                detail = r.Employee.Position & " · expires in " & r.DaysLeft.Value & If(r.DaysLeft.Value = 1, " day", " days")
            End If
            rows.Add(ListRow(r.Employee.FullName, detail, r.Status,
                             If(canManage, "Renew", "View"),
                             Sub()
                                 SelectEmployee(item.Employee.EmployeeId)
                                 If canManage Then OpenIssueDialog({item}.ToList())
                             End Sub))
        Next
        If Not canManage Then
            rows.Add(NoteRow("Please have these employees renew at the Municipal Health Office.", Theme.TextMuted))
        End If
        Return rows
    End Function

    ' ---------- Row builders ----------

    ''' <summary>A list row: bold title, muted detail, optional status badge and optional link.</summary>
    Private Function ListRow(title As String, detail As String, status As String, linkText As String, action As Action) As Control
        Dim row As New Panel With {.Height = 52, .BackColor = Color.White}
        AddHandler row.Paint,
            Sub(s, e)
                Using pen As New Pen(Theme.Divider)
                    e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1)
                End Using
            End Sub

        Dim right As New FlowLayoutPanel With {
            .Dock = DockStyle.Right, .AutoSize = True, .WrapContents = False,
            .FlowDirection = FlowDirection.RightToLeft, .BackColor = Color.White, .Padding = New Padding(0, 13, 0, 0)
        }
        If linkText IsNot Nothing AndAlso action IsNot Nothing Then
            Dim link As New LinkLabel With {
                .Text = linkText, .AutoSize = True, .Font = Theme.SmallBoldFont, .LinkColor = Theme.SidebarBlue,
                .ActiveLinkColor = Theme.SidebarActive, .LinkBehavior = LinkBehavior.HoverUnderline,
                .Margin = New Padding(10, 4, 0, 0), .BackColor = Color.White
            }
            ' Deferred: the action rebuilds this list, which disposes the link that was clicked
            AddHandler link.LinkClicked, Sub() BeginInvoke(action)
            right.Controls.Add(link)
        End If
        If status <> "" Then right.Controls.Add(New StatusBadge With {.Text = status, .Height = 24, .Margin = New Padding(8, 1, 4, 0)})

        Dim left As New Panel With {.Dock = DockStyle.Fill, .BackColor = Color.White}
        Dim lblTitle As New Label With {
            .Text = title, .Font = Theme.BodyBoldFont, .ForeColor = Theme.TextDark,
            .Dock = DockStyle.Top, .Height = 26, .AutoEllipsis = True, .TextAlign = ContentAlignment.BottomLeft
        }
        Dim lblDetail As New Label With {
            .Text = detail, .Font = Theme.SmallFont, .ForeColor = Theme.TextMuted,
            .Dock = DockStyle.Top, .Height = 20, .AutoEllipsis = True
        }
        tips.SetToolTip(lblTitle, title)
        tips.SetToolTip(lblDetail, detail)
        left.Controls.Add(lblDetail)
        left.Controls.Add(lblTitle)

        row.Controls.Add(left)
        row.Controls.Add(right)
        Return row
    End Function

    ''' <summary>A colored one-line note (ellipsized, full text in the tooltip).</summary>
    Private Function NoteRow(text As String, color As Color) As Control
        Dim note As New Label With {
            .Height = 44, .Font = Theme.SmallBoldFont, .ForeColor = color, .BackColor = Color.White,
            .TextAlign = ContentAlignment.MiddleLeft, .AutoEllipsis = True, .Text = text
        }
        tips.SetToolTip(note, text)
        Return note
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

    ''' <summary>Selects one employee in the grid (clears the search if they are filtered out).</summary>
    Private Sub SelectEmployee(employeeId As Integer)
        If Not grid.Rows.Cast(Of DataGridViewRow)().Any(Function(r) TryCast(r.DataBoundItem, EmployeeRow)?.EmployeeId = employeeId) Then
            txtSearch.Text = ""
        End If
        BindGrid(employeeId)
    End Sub

    Private Sub AddEmployee_Click(sender As Object, e As EventArgs)
        If business Is Nothing OrElse Not canManage Then Return
        Using dlg As New EmployeeDialog(business)
            If dlg.ShowDialog(FindForm()) <> DialogResult.OK Then Return
            LoadRoster(dlg.EmployeeId)
            UiHelper.ShowSuccess(selected?.Employee.FullName & " was added." & vbCrLf &
                                 "Next: click ""Issue / Renew"" to record their health certificate.", "Employee added")
        End Using
    End Sub

    Private Sub Edit_Click(sender As Object, e As EventArgs)
        If selected Is Nothing OrElse business Is Nothing OrElse Not canManage Then Return
        Dim fresh = EmployeeRepository.GetById(selected.Employee.EmployeeId)   ' edit a fresh copy
        If fresh Is Nothing Then Return
        Using dlg As New EmployeeDialog(business, fresh, selected.Status)
            If dlg.ShowDialog(FindForm()) = DialogResult.OK Then LoadRoster(dlg.EmployeeId)
        End Using
    End Sub

    Private Sub Deactivate_Click(sender As Object, e As EventArgs)
        If selected Is Nothing OrElse Not canManage Then Return
        Dim name = selected.Employee.FullName
        If Not UiHelper.Confirm("Remove " & name & " from the employee list?" & vbCrLf &
                                "Use this when the employee has resigned. Their certificate history is kept on record.",
                                "Deactivate employee", "Deactivate", "Cancel", danger:=True) Then Return
        If HealthCertificateService.DeactivateEmployee(EmployeeRepository.GetById(selected.Employee.EmployeeId)) Then
            LoadRoster(Nothing)
            UiHelper.ShowSuccess(name & " was removed from the list.", "Employee deactivated")
        Else
            UiHelper.ShowWarning("The employee could not be deactivated.")
        End If
    End Sub

    Private Sub Issue_Click(sender As Object, e As EventArgs)
        If selected Is Nothing OrElse Not canManage Then Return
        Dim blocker = HealthCertificateService.GetIssueBlocker(selected)
        If blocker IsNot Nothing Then
            UiHelper.ShowInfo(blocker, "Certificate still valid")
            Return
        End If
        OpenIssueDialog({selected}.ToList())
    End Sub

    Private Sub RenewSelected_Click(sender As Object, e As EventArgs)
        If Not canManage Then Return
        Dim picked = SelectedRoster()
        Dim due = picked.Where(Function(r) r.NeedsRenewal).ToList()
        If due.Count = 0 Then
            UiHelper.ShowInfo("None of the selected certificates is due for renewal.", "Nothing to renew")
            Return
        End If
        OpenIssueDialog(due)
    End Sub

    Private Sub OpenIssueDialog(targets As List(Of EmployeeHealth))
        If business Is Nothing OrElse targets.Count = 0 Then Return
        Dim keepId = If(targets.Count = 1, targets(0).Employee.EmployeeId, selected?.Employee.EmployeeId)
        Using dlg As New IssueCertificateDialog(business, targets)
            If dlg.ShowDialog(FindForm()) <> DialogResult.OK Then Return
            LoadRoster(keepId)
            Dim issued = dlg.Result.Issued
            Dim expiry = UiHelper.FormatDate(issued(0).ExpiryDate)
            Dim message = If(issued.Count = 1,
                             "Certificate " & issued(0).CertificateNo & " was issued." & vbCrLf & "Valid until " & expiry & ".",
                             issued.Count & " certificates were issued (" & issued.First().CertificateNo & " to " &
                             issued.Last().CertificateNo & ")." & vbCrLf & "Valid until " & expiry & ".")
            If dlg.Result.Skipped.Count > 0 Then
                message &= vbCrLf & vbCrLf & "Skipped:" & vbCrLf & String.Join(vbCrLf, dlg.Result.Skipped)
            End If
            UiHelper.ShowSuccess(message, If(issued.Count = 1, "Certificate issued", "Certificates issued"))
        End Using
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then tips.Dispose()
        MyBase.Dispose(disposing)
    End Sub

End Class
