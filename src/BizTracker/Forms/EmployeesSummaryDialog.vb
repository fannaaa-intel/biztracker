''' <summary>
''' Employees Summary (Revision 4B): is each employee still working at the business?
'''   Top    : one row per business - total, active, separated, and the health certificates of the ACTIVE employees
'''   Bottom : every employee of the selected business (active first) with hire / separation dates
''' Health and Admin may reactivate a separated employee (rehired). Owners see only their own business, read-only.
''' No SQL here: everything comes from HealthCertificateService.
''' </summary>
Public Class EmployeesSummaryDialog
    Inherits ModernDialog

    ''' <summary>Business grid row (display only).</summary>
    Private Class BusinessRow
        Public Property BusinessId As Integer
        Public Property Business As String
        Public Property Total As Integer
        Public Property Active As Integer
        Public Property Separated As Integer
        Public Property Valid As Integer
        Public Property Expiring As Integer
        Public Property Expired As Integer
        Public Property NoCert As Integer
    End Class

    ''' <summary>Employee grid row (display only).</summary>
    Private Class EmployeeRow
        Public Property EmployeeId As Integer
        Public Property Name As String
        Public Property Position As String
        Public Property Status As String
        Public Property Hired As String
        Public Property Separated As String
        Public Property Service As String
        Public Property Certificate As String
    End Class

    Private ReadOnly txtSearch As New TextBox()
    Private ReadOnly businessGrid As New ModernGrid()
    Private ReadOnly employeeGrid As New ModernGrid()
    Private ReadOnly lblEmployees As New Label()
    Private ReadOnly lblTotals As New Label()
    Private ReadOnly btnReactivate As Button
    Private ReadOnly canManage As Boolean = AccessService.CanManage(AppScreen.HealthCertificates)

    Private summaries As New List(Of BusinessEmployeeSummary)
    Private employees As New List(Of EmployeeHealth)
    Private loading As Boolean

    ''' <summary>True when an employee was reactivated (the caller reloads its list).</summary>
    Public Property Changed As Boolean

    Public Sub New(Optional selectBusinessId As Integer? = Nothing)
        ClientSize = New Size(980, 660)
        SetTitle("Employees Summary", If(Session.IsOwner, "Who is still working at your business",
                                         "Who is still working at each business, and their health certificates"))

        ' --- Search + totals ---
        Dim searchBox = UiHelper.CreateInputBox(txtSearch, "Search business…", Icons.Search)
        searchBox.SetBounds(24, 14, 300, 38)
        lblTotals.Font = Theme.SmallBoldFont
        lblTotals.ForeColor = Theme.TextMuted
        lblTotals.TextAlign = ContentAlignment.MiddleRight
        lblTotals.AutoEllipsis = True
        lblTotals.SetBounds(340, 14, 616, 38)
        Body.Controls.AddRange({searchBox, lblTotals})
        AddHandler txtSearch.TextChanged, Sub() BindBusinesses(SelectedBusinessId())

        ' --- Businesses ---
        UiHelper.StyleGrid(businessGrid)
        businessGrid.SetBounds(24, 62, 932, 200)
        UiHelper.AddGridColumn(businessGrid, "Business", "BUSINESS", "Business", 220, 160)
        UiHelper.AddGridColumn(businessGrid, "Total", "TOTAL", "Total", 70, 60)
        UiHelper.AddGridColumn(businessGrid, "Active", "ACTIVE", "Active", 70, 60)
        UiHelper.AddGridColumn(businessGrid, "Separated", "SEPARATED", "Separated", 90, 80)
        UiHelper.AddGridColumn(businessGrid, "Valid", "HC VALID", "Valid", 80, 70)
        UiHelper.AddGridColumn(businessGrid, "Expiring", "EXPIRING", "Expiring", 80, 70)
        UiHelper.AddGridColumn(businessGrid, "Expired", "EXPIRED", "Expired", 80, 70)
        UiHelper.AddGridColumn(businessGrid, "NoCert", "NO HC", "NoCert", 70, 60)
        Tips.SetToolTip(businessGrid, "HC = health certificate. Certificate counts are for active employees only.")
        AddHandler businessGrid.SelectionChanged, Sub() If Not loading Then LoadEmployees()
        Body.Controls.Add(businessGrid)

        ' --- Employees of the selected business ---
        lblEmployees.Font = Theme.SubtitleFont
        lblEmployees.ForeColor = Theme.TextDark
        lblEmployees.AutoEllipsis = True
        lblEmployees.SetBounds(24, 274, 932, 28)
        Body.Controls.Add(lblEmployees)

        UiHelper.StyleGrid(employeeGrid, "Status,Certificate")
        employeeGrid.SetBounds(24, 306, 932, 214)
        UiHelper.AddGridColumn(employeeGrid, "Name", "NAME", "Name", 170, 140)
        UiHelper.AddGridColumn(employeeGrid, "Position", "POSITION", "Position", 120, 100)
        UiHelper.AddGridColumn(employeeGrid, "Status", "EMPLOYMENT", "Status", 115, 110)
        UiHelper.AddGridColumn(employeeGrid, "Hired", "DATE HIRED", "Hired", 105, 100)
        UiHelper.AddGridColumn(employeeGrid, "Separated", "SEPARATED", "Separated", 105, 100)
        UiHelper.AddGridColumn(employeeGrid, "Service", "SERVICE", "Service", 105, 95)
        UiHelper.AddGridColumn(employeeGrid, "Certificate", "HEALTH CERT.", "Certificate", 125, 120)
        AddHandler employeeGrid.SelectionChanged, Sub() If Not loading Then UpdateButtons()
        Body.Controls.Add(employeeGrid)

        ' --- Buttons ---
        Dim close = AddFooterButton("Close", "primary")
        AddHandler close.Click, Sub() DialogResult = If(Changed, DialogResult.OK, DialogResult.Cancel)
        CancelButton = close
        btnReactivate = AddFooterButton("Reactivate (Rehired)")
        btnReactivate.Visible = canManage
        AddHandler btnReactivate.Click, AddressOf Reactivate_Click

        LoadSummaries(selectBusinessId)
    End Sub

    ' ==================== Loading ====================

    Private Sub LoadSummaries(selectBusinessId As Integer?)
        summaries = HealthCertificateService.GetEmployeeSummaries()
        Dim active = summaries.Sum(Function(s) s.Active)
        Dim separated = summaries.Sum(Function(s) s.Separated)
        lblTotals.Text = active & " active  ·  " & separated & " separated  ·  " &
                         summaries.Sum(Function(s) s.Expired + s.NoCertificate) & " active without a valid certificate"
        BindBusinesses(selectBusinessId)
    End Sub

    Private Sub BindBusinesses(selectBusinessId As Integer?)
        Dim term = txtSearch.Text.Trim().ToLowerInvariant()
        Dim rows = summaries.
            Where(Function(s) term = "" OrElse s.Business.BusinessName.ToLowerInvariant().Contains(term)).
            Select(Function(s) New BusinessRow With {
                .BusinessId = s.Business.BusinessId, .Business = s.Business.BusinessName, .Total = s.Total,
                .Active = s.Active, .Separated = s.Separated, .Valid = s.Valid, .Expiring = s.ExpiringSoon,
                .Expired = s.Expired, .NoCert = s.NoCertificate}).ToList()
        loading = True
        businessGrid.DataSource = rows
        Dim index = If(selectBusinessId.HasValue, rows.FindIndex(Function(r) r.BusinessId = selectBusinessId.Value), -1)
        If index < 0 AndAlso rows.Count > 0 Then index = 0
        businessGrid.ClearSelection()
        If index >= 0 Then
            businessGrid.CurrentCell = businessGrid.Rows(index).Cells("Business")
            businessGrid.Rows(index).Selected = True
        End If
        loading = False
        LoadEmployees()
    End Sub

    Private Function SelectedBusinessId() As Integer?
        If businessGrid.SelectedRows.Count = 0 Then Return Nothing
        Return TryCast(businessGrid.SelectedRows(0).DataBoundItem, BusinessRow)?.BusinessId
    End Function

    Private Sub LoadEmployees(Optional selectEmployeeId As Integer? = Nothing)
        Dim bid = SelectedBusinessId()
        Dim summary = summaries.FirstOrDefault(Function(s) bid.HasValue AndAlso s.Business.BusinessId = bid.Value)
        employees = If(bid.HasValue, HealthCertificateService.GetAllEmployees(bid.Value), New List(Of EmployeeHealth)())
        lblEmployees.Text = If(summary Is Nothing, "No business selected",
                               "Employees of " & summary.Business.BusinessName & "  (" & summary.Active & " active, " &
                               summary.Separated & " separated)")
        Dim rows = employees.Select(Function(r) New EmployeeRow With {
            .EmployeeId = r.Employee.EmployeeId, .Name = r.Employee.FullName,
            .Position = r.Employee.Position & " · " & r.Employee.Category,
            .Status = HealthCertificateService.GetEmploymentStatus(r.Employee),
            .Hired = If(r.Employee.DateHired.HasValue, UiHelper.FormatDate(r.Employee.DateHired.Value), "Not recorded"),
            .Separated = If(r.Employee.IsActive, "—",
                            If(r.Employee.DateSeparated.HasValue, UiHelper.FormatDate(r.Employee.DateSeparated.Value), "Not recorded")),
            .Service = If(r.Employee.DateHired.HasValue, HealthCertificateService.GetLengthOfService(r.Employee), "—"),
            .Certificate = If(r.Employee.IsActive, r.Status, "")      ' separated: history only, no badge
        }).ToList()
        loading = True
        employeeGrid.DataSource = rows
        employeeGrid.ClearSelection()
        Dim index = If(selectEmployeeId.HasValue, rows.FindIndex(Function(r) r.EmployeeId = selectEmployeeId.Value), -1)
        If index >= 0 Then
            employeeGrid.CurrentCell = employeeGrid.Rows(index).Cells("Name")
            employeeGrid.Rows(index).Selected = True
        End If
        loading = False
        UpdateButtons()
    End Sub

    Private Function SelectedEmployee() As Employee
        If employeeGrid.SelectedRows.Count = 0 Then Return Nothing
        Dim id = TryCast(employeeGrid.SelectedRows(0).DataBoundItem, EmployeeRow)?.EmployeeId
        Return employees.FirstOrDefault(Function(r) id.HasValue AndAlso r.Employee.EmployeeId = id.Value)?.Employee
    End Function

    Private Sub UpdateButtons()
        If Not canManage Then Return
        Dim emp = SelectedEmployee()
        UiHelper.SetAvailable(btnReactivate,
            If(emp Is Nothing, "Select a separated employee in the lower list first.",
               If(emp.IsActive, emp.FullName & " is still working here.", Nothing)))
        Tips.SetToolTip(btnReactivate, "Put a separated employee back on the list (rehired)")
    End Sub

    ' ==================== Actions ====================

    Private Sub Reactivate_Click(sender As Object, e As EventArgs)
        Dim emp = SelectedEmployee()
        If emp Is Nothing OrElse emp.IsActive OrElse Not canManage Then Return
        If Not UiHelper.Confirm(emp.FullName & " will be back on the employee list and counted again." & vbCrLf &
                                "Their health certificate may need renewing.", "Reactivate employee", "Reactivate", "Cancel") Then Return
        Dim problem = HealthCertificateService.ReactivateEmployee(emp)
        If problem IsNot Nothing Then
            UiHelper.ShowWarning(problem)
            Return
        End If
        Changed = True
        Dim bid = SelectedBusinessId()
        LoadSummaries(bid)
        LoadEmployees(emp.EmployeeId)
        UiHelper.ShowSuccess(emp.FullName & " is active again.", "Employee reactivated")
    End Sub

End Class
