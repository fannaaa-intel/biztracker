Imports System.Drawing.Drawing2D

''' <summary>
''' Real Property Tax module (same structure as BusinessPermitView / SanitaryPermitView).
'''   Toolbar  : business selector (staff) or the owner's business, and the actions (Assessor / Admin only)
'''   Cards    : total due this year, overdue quarters, tax clearance, total assessed value
'''   Left     : property ledger (year selector + search), Q1-Q4 payment status chips, overdue rows tinted red
'''   Right    : selected property - quarter tracker and tabs (Quarters | Assessment | Important Dates)
''' No SQL here: everything goes through RptService / repositories.
''' </summary>
Public Class RealPropertyTaxView
    Inherits ModuleView

    ''' <summary>Grid row (display only).</summary>
    Private Class LedgerGridRow
        Public Property PropertyId As Integer
        Public Property Pin As String
        Public Property TdNo As String
        Public Property Location As String
        Public Property PropertyType As String
        Public Property AssessedValue As String
        ''' <summary>Text behind the painted chips ("Q1 Q2 Q3 Q4" or "Not Assessed").</summary>
        Public Property Quarters As String
        ''' <summary>Q1-Q4 statuses, or Nothing when the property is not assessed for the year.</summary>
        Public Property QuarterStatuses As String()
        Public Property IsOverdue As Boolean
    End Class

    ' Toolbar
    Private ReadOnly toolbar As New ModuleToolbar("rpt")
    Private btnAdd As Button
    Private btnEdit As Button
    Private btnAssess As Button
    Private btnPay As Button

    ' Summary cards
    Private ReadOnly cardDue As New StatCard(Icons.Bank, "Total Due")
    Private ReadOnly cardOverdue As New StatCard(Icons.Warning, "Overdue")
    Private ReadOnly cardClearance As New StatCard(Icons.CheckShield, "Tax Clearance")
    Private ReadOnly cardValue As New StatCard(Icons.City, "Assessed Value")

    ' Ledger
    Private ReadOnly txtSearch As New TextBox()
    Private ReadOnly cboYear As New ComboBox()
    Private ReadOnly grid As New ModernGrid()
    Private ReadOnly lblEmpty As New Label()

    ' Details
    Private ReadOnly detailHeader As New Panel()
    Private ReadOnly lblDetailRef As New Label()
    Private ReadOnly badgeDetail As New StatusBadge()
    Private ReadOnly lblDetailInfo As New Label()
    Private ReadOnly tracker As New StepTracker()
    Private ReadOnly tabs As New SegmentedTabs()
    Private ReadOnly quarterList As New WheelScrollPanel()
    Private ReadOnly assessmentList As New WheelScrollPanel()
    Private ReadOnly datesList As New WheelScrollPanel()
    Private ReadOnly detailBody As New Panel()
    Private ReadOnly detailContent As New Panel()
    Private ReadOnly lblDetailEmpty As New Label()
    Private ReadOnly tips As New ToolTip()

    ' State
    Private ledger As New List(Of RptLedgerRow)
    Private selected As RptLedgerRow
    Private loading As Boolean
    Private ReadOnly canManage As Boolean = AccessService.CanManage(AppScreen.RealPropertyTax)

    Private ReadOnly Property Business As Business
        Get
            Return toolbar.Business
        End Get
    End Property

    ''' <summary>The tax year shown in the ledger (current year by default).</summary>
    Private ReadOnly Property TaxYear As Integer
        Get
            Return If(TypeOf cboYear.SelectedItem Is Integer, CInt(cboYear.SelectedItem), Date.Today.Year)
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

        btnAdd = toolbar.AddAction("Add Property", "Add", "primary", AddressOf AddProperty_Click, canManage)
        btnEdit = toolbar.AddAction("Edit Property", "Edit", "secondary", AddressOf EditProperty_Click, canManage)
        btnAssess = toolbar.AddAction("Generate Assessment", "Assess", "secondary", AddressOf Assess_Click, canManage)
        btnPay = toolbar.AddAction("Record Payment", "Pay", "secondary", AddressOf Pay_Click, canManage)
        AddHandler toolbar.BusinessChanged, Sub()
                                                txtSearch.Text = ""
                                                LoadYears()
                                                LoadLedger(Nothing)
                                            End Sub

        root.Controls.Add(toolbar, 0, 0)
        root.Controls.Add(BuildCards(), 0, 1)
        root.Controls.Add(BuildMain(), 0, 2)
        Controls.Add(root)
        ResumeLayout(False)
    End Sub

    Public Overrides ReadOnly Property Subtitle As String
        Get
            Return "Property ledger, yearly assessments, quarterly payments and tax clearance"
        End Get
    End Property

    ' =====================================================================
    '  Layout
    ' =====================================================================

    Private Function BuildCards() As Control
        Dim cards = {cardDue, cardOverdue, cardClearance, cardValue}
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
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 57))
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 43))
        row.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        row.Controls.Add(BuildListCard(), 0, 0)
        row.Controls.Add(BuildDetailCard(), 1, 0)
        Return row
    End Function

    Private Function BuildListCard() As Control
        Dim card As New RoundedPanel With {.Dock = DockStyle.Fill, .Margin = New Padding(0, 0, 8, 0), .Padding = New Padding(16, 14, 16, 16)}

        Dim header As New Panel With {.Dock = DockStyle.Top, .Height = Dpi(48), .BackColor = Color.White}
        Dim title As New Label With {.Text = "Property Ledger", .Font = Theme.SubtitleFont, .ForeColor = Theme.TextDark,
                                     .AutoSize = True, .Location = New Point(0, Dpi(10)), .BackColor = Color.White}
        Dim searchBox = UiHelper.CreateInputBox(txtSearch, "Search…", Icons.Search)
        searchBox.Height = Dpi(38)

        ' Year selector: a rounded box like the search box
        Dim yearBox As New RoundedPanel With {.Radius = 8, .ShowShadow = False, .BorderColor = Theme.InputBorder,
                                              .BackColor = Color.White, .Height = Dpi(38), .Padding = New Padding(Dpi(8), 0, Dpi(4), 0)}
        cboYear.DropDownStyle = ComboBoxStyle.DropDownList
        cboYear.FlatStyle = FlatStyle.Flat
        cboYear.Font = Theme.InputFont
        cboYear.BackColor = Color.White
        UiHelper.StyleComboBox(cboYear)
        yearBox.Controls.Add(cboYear)
        AddHandler yearBox.Resize,
            Sub()
                ' Keep the combo inside the rounded box at any screen scaling
                Dim itemH = Math.Max(16, yearBox.ClientSize.Height - Dpi(12))
                If cboYear.ItemHeight <> itemH Then cboYear.ItemHeight = itemH
                cboYear.Width = yearBox.ClientSize.Width - yearBox.Padding.Horizontal
                cboYear.Location = New Point(yearBox.Padding.Left, (yearBox.ClientSize.Height - cboYear.Height) \ 2)
            End Sub
        tips.SetToolTip(cboYear, "Tax year")
        AddHandler cboYear.SelectedIndexChanged, Sub() If Not loading Then LoadLedger(selected?.RealProperty.PropertyId)

        header.Controls.AddRange({title, searchBox, yearBox})
        AddHandler header.Resize,
            Sub()
                Dim yearWidth = Dpi(92)
                yearBox.SetBounds(header.ClientSize.Width - yearWidth, Dpi(2), yearWidth, Dpi(38))
                Dim w = Math.Max(Dpi(110), Math.Min(Dpi(220), yearBox.Left - title.Right - Dpi(20)))
                searchBox.SetBounds(yearBox.Left - Dpi(8) - w, Dpi(2), w, Dpi(38))
            End Sub
        AddHandler txtSearch.TextChanged, Sub() BindGrid(selected?.RealProperty.PropertyId)

        UiHelper.StyleGrid(grid)
        grid.Dock = DockStyle.Fill
        UiHelper.AddGridColumn(grid, "Pin", "PIN", "Pin", 150, 150)
        UiHelper.AddGridColumn(grid, "TdNo", "TD NO.", "TdNo", 120, 120)
        UiHelper.AddGridColumn(grid, "Location", "LOCATION", "Location", 170, 120)
        UiHelper.AddGridColumn(grid, "PropertyType", "TYPE", "PropertyType", 90, 90)
        UiHelper.AddGridColumn(grid, "AssessedValue", "ASSESSED VALUE", "AssessedValue", 120, 120)
        UiHelper.AddGridColumn(grid, "Quarters", "QUARTERS", "Quarters", 160, 160)
        AddHandler grid.CellPainting, AddressOf Grid_CellPainting       ' after StyleGrid's painter: draws the Q1-Q4 chips
        AddHandler grid.CellToolTipTextNeeded, AddressOf Grid_CellToolTipTextNeeded
        AddHandler grid.SelectionChanged, Sub() If Not loading Then ShowSelected()
        AddHandler grid.CellDoubleClick, Sub(s, e) If e.RowIndex >= 0 AndAlso canManage Then EditProperty_Click(Nothing, EventArgs.Empty)
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

    ''' <summary>Hides the less important columns when the grid is narrow (PIN and the quarter chips always stay).</summary>
    Private Sub FitGridColumns()
        Dim s = DeviceDpi / 96.0
        grid.Columns("PropertyType").Visible = grid.Width >= 410 * s
        grid.Columns("AssessedValue").Visible = grid.Width >= 530 * s
        grid.Columns("TdNo").Visible = grid.Width >= 660 * s
        grid.Columns("Location").Visible = grid.Width >= 800 * s
    End Sub

    Private Function RowData(rowIndex As Integer) As LedgerGridRow
        If rowIndex < 0 OrElse rowIndex >= grid.Rows.Count Then Return Nothing
        Return TryCast(grid.Rows(rowIndex).DataBoundItem, LedgerGridRow)
    End Function

    ''' <summary>Paints the QUARTERS column as four colored chips (Q1-Q4) or a gray "Not Assessed" pill.</summary>
    Private Sub Grid_CellPainting(sender As Object, e As DataGridViewCellPaintingEventArgs)
        If e.RowIndex < 0 OrElse e.ColumnIndex < 0 OrElse grid.Columns(e.ColumnIndex).Name <> "Quarters" Then Return
        Dim data = RowData(e.RowIndex)
        If data Is Nothing Then Return
        Dim g = e.Graphics
        Dim isSelected = (e.State And DataGridViewElementStates.Selected) <> 0
        g.SmoothingMode = SmoothingMode.None
        Using back As New SolidBrush(If(isSelected, e.CellStyle.SelectionBackColor, e.CellStyle.BackColor))
            g.FillRectangle(back, e.CellBounds)
        End Using

        Dim s = DeviceDpi / 96.0
        Dim font = Theme.SmallBoldFont
        Dim h = Math.Min(e.CellBounds.Height - 10, TextRenderer.MeasureText("Q1", font).Height + 8)
        Dim top = e.CellBounds.Y + (e.CellBounds.Height - h) \ 2
        g.SmoothingMode = SmoothingMode.AntiAlias
        If data.QuarterStatuses Is Nothing Then
            Dim w = Math.Min(e.CellBounds.Width - 12, TextRenderer.MeasureText(data.Quarters, font).Width + h)
            DrawChip(g, New Rectangle(e.CellBounds.X + 8, top, w, h), data.Quarters, RptService.NotAssessed, font)
        Else
            Dim chipW = CInt(32 * s)
            Dim gap = CInt(4 * s)
            For i = 0 To 3
                DrawChip(g, New Rectangle(e.CellBounds.X + 8 + i * (chipW + gap), top, chipW, h), "Q" & (i + 1), data.QuarterStatuses(i), font)
            Next
        End If
        g.SmoothingMode = SmoothingMode.None   ' reset after the rounded chips
        Using pen As New Pen(Theme.Divider)
            g.DrawLine(pen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1)
        End Using
        e.Handled = True
    End Sub

    Private Shared Sub DrawChip(g As Graphics, rect As Rectangle, text As String, status As String, font As Font)
        Using path = UiHelper.RoundedRect(rect, rect.Height \ 2), brush As New SolidBrush(Theme.StatusSoftColor(status))
            g.FillPath(brush, path)
        End Using
        TextRenderer.DrawText(g, text, font, rect, Theme.StatusColor(status),
                              TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or
                              TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPadding)
    End Sub

    Private Sub Grid_CellToolTipTextNeeded(sender As Object, e As DataGridViewCellToolTipTextNeededEventArgs)
        If e.ColumnIndex < 0 OrElse grid.Columns(e.ColumnIndex).Name <> "Quarters" Then Return
        Dim data = RowData(e.RowIndex)
        If data Is Nothing Then Return
        e.ToolTipText = If(data.QuarterStatuses Is Nothing, "No " & TaxYear & " assessment yet",
                           String.Join("  ·  ", data.QuarterStatuses.Select(Function(st, i) "Q" & (i + 1) & " " & st)))
    End Sub

    Private Function BuildDetailCard() As Control
        Dim card As New RoundedPanel With {.Dock = DockStyle.Fill, .Margin = New Padding(8, 0, 0, 0), .Padding = New Padding(18, 14, 18, 16)}

        detailHeader.Dock = DockStyle.Top
        detailHeader.Height = Dpi(52)
        detailHeader.BackColor = Color.White
        lblDetailRef.Font = Theme.SubtitleFont
        lblDetailRef.ForeColor = Theme.TextDark
        lblDetailRef.AutoSize = True
        lblDetailRef.Location = New Point(0, 2)
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
        tracker.Steps = {"Q1", "Q2", "Q3", "Q4"}
        Dim gap1 As New Panel With {.Dock = DockStyle.Top, .Height = 6, .BackColor = Color.White}

        tabs.Dock = DockStyle.Top
        tabs.Tabs = {"Quarters", "Assessment", "Important Dates"}
        AddHandler tabs.SelectedIndexChanged, Sub() ShowTab()
        AddHandler tabs.Resize, Sub() SetTabLabels()
        Dim gap2 As New Panel With {.Dock = DockStyle.Top, .Height = 8, .BackColor = Color.White}

        detailBody.Dock = DockStyle.Fill
        detailBody.BackColor = Color.White
        For Each list In {quarterList, assessmentList, datesList}
            list.Dock = DockStyle.Fill
            list.BackColor = Color.White
        Next
        detailBody.Controls.AddRange({quarterList, assessmentList, datesList})

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

    ''' <summary>"Important Dates" becomes "Dates" when the detail card is narrow.</summary>
    Private Sub SetTabLabels()
        Dim narrow = tabs.Width < CInt(330 * DeviceDpi / 96.0)
        Dim labels = {"Quarters", "Assessment", If(narrow, "Dates", "Important Dates")}
        If Not labels.SequenceEqual(tabs.Tabs) Then
            Dim index = tabs.SelectedIndex
            tabs.Tabs = labels
            tabs.SelectedIndex = index
        End If
    End Sub

    ''' <summary>The status badge sits right of the PIN, or is hidden if it would not fit.</summary>
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
        LoadLedger(selected?.RealProperty.PropertyId)
    End Sub

    ''' <summary>Fills the year selector for the business and selects the current year.</summary>
    Private Sub LoadYears()
        Dim keep = Date.Today.Year
        Dim years = If(Business Is Nothing, New List(Of Integer) From {Date.Today.Year}, RptService.GetLedgerYears(Business.BusinessId))
        loading = True
        cboYear.Items.Clear()
        For Each y In years
            cboYear.Items.Add(y)
        Next
        cboYear.SelectedItem = If(years.Contains(keep), keep, Date.Today.Year)
        If cboYear.SelectedIndex < 0 AndAlso cboYear.Items.Count > 0 Then cboYear.SelectedIndex = 0
        loading = False
    End Sub

    Private Sub LoadLedger(selectId As Integer?)
        ledger = If(Business Is Nothing, New List(Of RptLedgerRow)(), RptService.GetLedger(Business.BusinessId, TaxYear))
        UpdateCards()
        BindGrid(selectId)
    End Sub

    Private Sub BindGrid(selectId As Integer?)
        Dim term = txtSearch.Text.Trim().ToLowerInvariant()
        Dim rows = ledger.
            Where(Function(r) term = "" OrElse
                              (r.RealProperty.Pin & " " & r.RealProperty.TdNo & " " & r.RealProperty.Location & " " &
                               r.RealProperty.PropertyType & " " & r.RealProperty.Classification & " " & r.WorstStatus).ToLowerInvariant().Contains(term)).
            Select(Function(r) New LedgerGridRow With {
                .PropertyId = r.RealProperty.PropertyId, .Pin = r.RealProperty.Pin, .TdNo = r.RealProperty.TdNo,
                .Location = r.RealProperty.Location, .PropertyType = r.RealProperty.PropertyType,
                .AssessedValue = UiHelper.FormatMoney(r.RealProperty.AssessedValue),
                .Quarters = If(r.IsAssessed, "Q1 Q2 Q3 Q4", RptService.NotAssessed),
                .QuarterStatuses = If(r.IsAssessed, r.Quarters.Select(Function(q) q.Status).ToArray(), Nothing),
                .IsOverdue = r.WorstStatus = StatusService.Overdue
            }).ToList()

        loading = True
        grid.DataSource = rows
        ' Rows with an overdue quarter get a light red background
        For Each gridRow As DataGridViewRow In grid.Rows
            If TryCast(gridRow.DataBoundItem, LedgerGridRow)?.IsOverdue Then gridRow.DefaultCellStyle.BackColor = Theme.StatusRedSoft
        Next
        FitGridColumns()
        Dim index = If(selectId.HasValue, rows.FindIndex(Function(r) r.PropertyId = selectId.Value), -1)
        If index < 0 AndAlso rows.Count > 0 Then index = 0
        grid.ClearSelection()
        If index >= 0 Then
            grid.CurrentCell = grid.Rows(index).Cells("Pin")
            grid.Rows(index).Selected = True
        End If
        loading = False

        lblEmpty.Visible = rows.Count = 0
        If rows.Count = 0 Then
            lblEmpty.Text = If(Business Is Nothing, "No business selected.",
                               If(term <> "", "No properties match your search.",
                                  If(canManage, "No properties yet." & vbCrLf & "Click ""Add Property"" to register one.",
                                     "No real property is on record for this business." & vbCrLf &
                                     "The Assessor's Office registers properties.")))
            lblEmpty.BringToFront()
        End If
        ShowSelected()
    End Sub

    Private Sub ShowSelected()
        Dim row = If(grid.SelectedRows.Count > 0, TryCast(grid.SelectedRows(0).DataBoundItem, LedgerGridRow), Nothing)
        selected = If(row Is Nothing, Nothing, ledger.FirstOrDefault(Function(r) r.RealProperty.PropertyId = row.PropertyId))
        ShowDetails()
        UpdateButtons()
    End Sub

    ' =====================================================================
    '  Cards, buttons and details
    ' =====================================================================

    Private Sub UpdateCards()
        If Business Is Nothing Then
            For Each c In {cardDue, cardOverdue, cardClearance, cardValue}
                c.SetValue("—", "", "No business selected")
            Next
            Return
        End If
        Dim year = TaxYear
        Dim assessed = ledger.Where(Function(r) r.IsAssessed).ToList()

        ' Total due this year (unpaid quarters, penalties not included)
        Dim totalDue = ledger.Sum(Function(r) r.Balance)
        If assessed.Count = 0 Then
            cardDue.SetValue(UiHelper.FormatMoney(0D), "", If(ledger.Count = 0, "no properties on record", "no " & year & " assessment yet"))
        ElseIf totalDue = 0D Then
            cardDue.SetValue(UiHelper.FormatMoney(0D), StatusService.Paid, "all " & year & " quarters paid")
        Else
            Dim unpaid = assessed.SelectMany(Function(r) r.Quarters).Where(Function(q) Not q.IsPaid).ToList()
            Dim worst = If(unpaid.Any(Function(q) q.IsOverdue), StatusService.Overdue,
                           If(unpaid.Any(Function(q) q.Status = StatusService.DueSoon), StatusService.DueSoon, StatusService.NotYetDue))
            Dim nextDue = unpaid.Where(Function(q) Not q.IsOverdue).OrderBy(Function(q) q.DueDate).FirstOrDefault()
            cardDue.SetValue(UiHelper.FormatMoney(totalDue), worst,
                             If(worst = StatusService.Overdue OrElse nextDue Is Nothing, "for " & year, "due " & nextDue.DueDate.ToString("MMM d")))
        End If

        ' Overdue quarters (all years) - what they cost if paid today
        Dim overdue = RptService.GetOverdueQuarters(Business.BusinessId)
        If overdue.Count = 0 Then
            cardOverdue.SetValue("None", If(assessed.Count > 0 OrElse ledger.Count > 0, StatusService.Paid, ""), "nothing past due")
        Else
            ' Amount if paid today (tax + penalties)
            cardOverdue.SetValue(UiHelper.FormatMoney(overdue.Sum(Function(q) q.Amount + q.Penalty)), StatusService.Overdue,
                                 overdue.Count & " quarter" & If(overdue.Count = 1, "", "s"))
        End If

        ' Tax clearance (all years)
        Dim clearance = RptService.GetTaxClearanceStatus(Business.BusinessId)
        If ledger.Count = 0 Then
            cardClearance.SetValue("N/A", "", "no real property")
        ElseIf clearance = RptService.ClearanceIssued Then
            cardClearance.SetValue(clearance, StatusService.Valid, "all dues paid")
        Else
            cardClearance.SetValue(clearance, StatusService.Overdue, "pay " & ShortQuarterList(overdue))
        End If

        ' Assessed value
        cardValue.SetValue(UiHelper.FormatMoney(ledger.Sum(Function(r) r.RealProperty.AssessedValue)), "",
                           ledger.Count & " propert" & If(ledger.Count = 1, "y", "ies") & " · tax " &
                           UiHelper.FormatMoney(RptService.ComputeTax(ledger.Sum(Function(r) r.RealProperty.AssessedValue)).TotalDue) & "/yr")
    End Sub

    ''' <summary>"Q2, Q3" (same year) or "Q4 2025, Q1 2026" - for the narrow card note.</summary>
    Private Shared Function ShortQuarterList(quarters As List(Of RptQuarter)) As String
        Dim sameYear = quarters.Select(Function(q) q.TaxYear).Distinct().Count() = 1
        Return String.Join(", ", quarters.Select(Function(q) If(sameYear, "Q" & q.Quarter, q.Label)).Distinct())
    End Function

    Private Sub UpdateButtons()
        If Not canManage Then Return
        Dim hasBusiness = Business IsNot Nothing
        Dim year = TaxYear
        btnAdd.Enabled = hasBusiness
        toolbar.SetTip(btnAdd, If(hasBusiness, "Register a real property of this business", "No business selected."))

        btnEdit.Enabled = selected IsNot Nothing
        toolbar.SetTip(btnEdit, If(selected Is Nothing, "Select a property first.", "Edit the selected property"))

        Dim assessBlocker = If(selected Is Nothing, "Select a property first.",
                               RptService.GetAssessmentBlocker(selected.RealProperty.PropertyId, year))
        btnAssess.Enabled = assessBlocker Is Nothing
        toolbar.SetTip(btnAssess, If(assessBlocker, "Create the " & year & " assessment (basic + SEF) for the selected property"))

        Dim payBlocker As String
        If selected Is Nothing Then
            payBlocker = "Select a property first."
        ElseIf Not selected.IsAssessed Then
            payBlocker = "Generate the " & year & " assessment first."
        ElseIf selected.NextPayable Is Nothing Then
            payBlocker = "All quarters of " & year & " are paid."
        Else
            payBlocker = RptService.GetPaymentBlocker(selected, selected.NextPayable.Quarter)
        End If
        btnPay.Enabled = payBlocker Is Nothing
        toolbar.SetTip(btnPay, If(payBlocker, "Record the payment of " & If(selected?.NextPayable?.Label, "the next quarter")))
    End Sub

    Private Sub ShowDetails()
        Dim has = selected IsNot Nothing
        lblDetailEmpty.Visible = Not has
        detailContent.Visible = has
        If Not has Then
            lblDetailEmpty.Text = If(ledger.Count = 0, "Nothing to show yet.", "Select a property to see its quarterly payments.")
            Return
        End If

        Dim p = selected.RealProperty
        lblDetailRef.Text = "PIN " & p.Pin
        badgeDetail.Text = selected.WorstStatus
        PlaceBadge()
        Dim info = "TD " & p.TdNo & " · " & p.PropertyType & " · " & p.Classification & " · " & p.Location
        lblDetailInfo.Text = info
        tips.SetToolTip(lblDetailInfo, info)

        ' Tracker: Q1-Q4 with the due dates; done = paid quarters
        tracker.Steps = Enumerable.Range(1, 4).Select(Function(q) "Q" & q & " · " & StatusService.GetQuarterDueDate(TaxYear, q).ToString("MMM d")).ToArray()
        tracker.CurrentStep = If(selected.IsAssessed, selected.Quarters.TakeWhile(Function(q) q.IsPaid).Count(), -1)   ' -1 = all gray

        quarterList.SetRows(CreateQuarterRows())
        assessmentList.SetRows(CreateAssessmentRows())
        datesList.SetRows(CreateDateRows())
        ShowTab()
    End Sub

    Private Sub ShowTab()
        quarterList.Visible = tabs.SelectedIndex = 0
        assessmentList.Visible = tabs.SelectedIndex = 1
        datesList.Visible = tabs.SelectedIndex = 2
        detailBody.PerformLayout()
    End Sub

    ' ---------- Quarters tab ----------

    Private Function CreateQuarterRows() As List(Of Control)
        Dim rows As New List(Of Control)
        Dim year = TaxYear
        If Not selected.IsAssessed Then
            rows.Add(NoteRow(tips, If(canManage, "No " & year & " assessment yet. Click ""Generate Assessment"" to create the tax bill.",
                                      "The Assessor's Office has not assessed this property for " & year & " yet."), Theme.TextMuted))
            Return rows
        End If
        Dim nextQ = selected.NextPayable
        For Each q In selected.Quarters
            Dim title = "Q" & q.Quarter & " · due " & ShortDate(q.DueDate)
            Dim detail As String
            Dim detailColor As Color = Nothing
            Dim links As New List(Of RowLink)
            If q.IsPaid Then
                detail = UiHelper.FormatMoney(q.Payment.AmountPaid + q.Payment.Penalty) & " · " & q.Payment.OrNo & " · paid " &
                         ShortDate(q.Payment.PaymentDate) & If(q.Payment.Penalty > 0, " (incl. " & UiHelper.FormatMoney(q.Payment.Penalty) & " penalty)", "")
            Else
                ' Short enough to sit next to the badge and the link; the breakdown is in the payment dialog
                detail = If(q.Penalty > 0, UiHelper.FormatMoney(q.Amount + q.Penalty) & " with penalty", UiHelper.FormatMoney(q.Amount))
                If q.IsOverdue Then detailColor = Theme.StatusRed
                If canManage AndAlso nextQ IsNot Nothing AndAlso nextQ.Quarter = q.Quarter AndAlso
                   RptService.GetPaymentBlocker(selected, q.Quarter) Is Nothing Then
                    links.Add(New RowLink("Record Payment", Theme.SidebarBlue, Sub() Pay_Click(Nothing, EventArgs.Empty)))
                End If
            End If
            rows.Add(ListRow(tips, title, detail, q.Status, links, detailColor))
        Next
        Dim overdue = selected.Quarters.Where(Function(q) q.IsOverdue).ToList()
        If overdue.Count > 0 Then
            rows.Add(NoteRow(tips, overdue.Count & " overdue quarter" & If(overdue.Count = 1, "", "s") & ": " &
                             UiHelper.FormatMoney(overdue.Sum(Function(q) q.Amount + q.Penalty)) & " if paid today. " &
                             If(canManage, "Quarters are paid in order.", "Please settle at the Municipal Treasurer's Office."), Theme.StatusRed))
        ElseIf nextQ Is Nothing Then
            rows.Add(NoteRow(tips, "All " & year & " quarters are paid.", Theme.StatusGreen))
        End If
        Return rows
    End Function

    ''' <summary>"Jun 30" in the selected tax year, "Jun 30, 2025" otherwise.</summary>
    Private Function ShortDate(value As Date) As String
        Return If(value.Year = TaxYear, value.ToString("MMM d"), UiHelper.FormatDate(value))
    End Function

    ' ---------- Assessment tab ----------

    Private Function CreateAssessmentRows() As List(Of Control)
        Dim rows As New List(Of Control)
        Dim p = selected.RealProperty
        Dim year = TaxYear
        Dim basicLabel = "Basic tax (" & (RptService.BasicRate * 100D).ToString("0.##") & "%)"
        Dim sefLabel = "SEF (" & (RptService.SefRate * 100D).ToString("0.##") & "%)"
        rows.Add(InfoRow(tips, "Assessed value", UiHelper.FormatMoney(p.AssessedValue), "Classification", p.Classification & " " & p.PropertyType.ToLowerInvariant()))
        If selected.IsAssessed Then
            Dim a = selected.Assessment
            rows.Add(HeadingRow(year & " assessment"))
            rows.Add(InfoRow(tips, basicLabel, UiHelper.FormatMoney(a.BasicTax), sefLabel, UiHelper.FormatMoney(a.SefTax)))
            rows.Add(InfoRow(tips, "Annual tax", UiHelper.FormatMoney(a.TotalDue), "Per quarter", UiHelper.FormatMoney(RptService.GetQuarterAmount(a.TotalDue, 1))))
            rows.Add(InfoRow(tips, "Paid so far", UiHelper.FormatMoney(selected.PaidAmount), "Balance", UiHelper.FormatMoney(selected.Balance)))
            Dim penalties = selected.Quarters.Where(Function(q) Not q.IsPaid).Sum(Function(q) q.Penalty)
            If penalties > 0 Then
                rows.Add(NoteRow(tips, "Penalties if paid today: " & UiHelper.FormatMoney(penalties) & " (balance with penalties " &
                                 UiHelper.FormatMoney(selected.Balance + penalties) & ").", Theme.StatusRed))
            End If
            If a.TotalDue <> RptService.ComputeTax(p.AssessedValue).TotalDue Then
                rows.Add(NoteRow(tips, "The assessed value changed after this assessment was generated. The " & year &
                                 " bill keeps its original amount.", Theme.TextMuted))
            End If
        Else
            Dim tax = RptService.ComputeTax(p.AssessedValue)
            rows.Add(HeadingRow(year & " assessment"))
            Dim blocker = RptService.GetAssessmentBlocker(p.PropertyId, year)
            Dim links As New List(Of RowLink)
            If canManage AndAlso blocker Is Nothing Then links.Add(New RowLink("Generate", Theme.SidebarBlue, Sub() Assess_Click(Nothing, EventArgs.Empty)))
            rows.Add(ListRow(tips, "Not yet assessed", "Tax would be " & UiHelper.FormatMoney(tax.TotalDue) & " a year",
                             RptService.NotAssessed, links))
            If blocker IsNot Nothing AndAlso canManage Then rows.Add(NoteRow(tips, blocker, Theme.TextMuted))
        End If
        Return rows
    End Function

    ' ---------- Important dates tab ----------

    Private Function CreateDateRows() As List(Of Control)
        Dim rows As New List(Of Control)
        Dim year = TaxYear
        rows.Add(HeadingRow(year & " payment deadlines"))
        For q = 1 To 4
            Dim due = StatusService.GetQuarterDueDate(year, q)
            Dim days = StatusService.DaysUntil(due)
            Dim whenText = If(days = 0, "today", If(days > 0, "in " & days & " day" & If(days = 1, "", "s"),
                                                     Math.Abs(days) & " day" & If(days = -1, "", "s") & " ago"))
            Dim quarter = If(selected.IsAssessed, selected.Quarters(q - 1), Nothing)
            rows.Add(ListRow(tips, "Q" & q & " deadline · " & UiHelper.FormatDate(due), whenText,
                             If(quarter Is Nothing, "", quarter.Status), Nothing,
                             If(quarter IsNot Nothing AndAlso quarter.IsOverdue, Theme.StatusRed, Nothing)))
        Next
        rows.Add(NoteRow(tips, "Late payments: " & (RptService.PenaltyRateMonthly * 100D).ToString("0.##") & "% per month (a part of a month counts), up to " &
                         (RptService.PenaltyMax * 100D).ToString("0.##") & "%.", Theme.TextMuted))
        rows.Add(NoteRow(tips, "Tax Clearance is issued only when every quarter already due is paid; it endorses RPT on the business permit.", Theme.TextMuted))
        Return rows
    End Function

    ' =====================================================================
    '  Actions
    ' =====================================================================

    Private Sub AddProperty_Click(sender As Object, e As EventArgs)
        If Business Is Nothing OrElse Not canManage Then Return
        Using dlg As New PropertyDialog(Business)
            If dlg.ShowDialog(FindForm()) <> DialogResult.OK Then Return
            LoadLedger(dlg.PropertyId)
        End Using
        If selected Is Nothing Then Return
        UiHelper.ShowSuccess("Property PIN " & selected.RealProperty.Pin & " was added." & vbCrLf &
                             "Next: click ""Generate Assessment"" to create its " & TaxYear & " tax bill.", "Property added")
    End Sub

    Private Sub EditProperty_Click(sender As Object, e As EventArgs)
        If selected Is Nothing OrElse Business Is Nothing OrElse Not canManage Then Return
        Dim current = PropertyRepository.GetById(selected.RealProperty.PropertyId)
        If current Is Nothing Then Return
        Using dlg As New PropertyDialog(Business, current)
            If dlg.ShowDialog(FindForm()) <> DialogResult.OK Then Return
            LoadLedger(dlg.PropertyId)
        End Using
        UiHelper.ShowSuccess("Property PIN " & selected?.RealProperty.Pin & " was updated." & vbCrLf &
                             "Existing assessments keep their amounts; new ones use the new assessed value.", "Changes saved")
    End Sub

    Private Sub Assess_Click(sender As Object, e As EventArgs)
        If selected Is Nothing OrElse Not canManage Then Return
        Dim p = PropertyRepository.GetById(selected.RealProperty.PropertyId)
        If p Is Nothing Then Return
        Dim year = TaxYear
        Dim blocker = RptService.GetAssessmentBlocker(p.PropertyId, year)
        If blocker IsNot Nothing Then
            UiHelper.ShowWarning(blocker)
            Return
        End If
        Dim tax = RptService.ComputeTax(p.AssessedValue)
        If Not UiHelper.Confirm("Assessed value " & UiHelper.FormatMoney(p.AssessedValue) & vbCrLf &
                                "Basic " & UiHelper.FormatMoney(tax.BasicTax) & " + SEF " & UiHelper.FormatMoney(tax.SefTax) &
                                " = " & UiHelper.FormatMoney(tax.TotalDue) & " (" & UiHelper.FormatMoney(RptService.GetQuarterAmount(tax.TotalDue, 1)) &
                                " per quarter)", "Generate " & year & " assessment for PIN " & p.Pin & "?", "Generate", "Cancel") Then Return
        Dim problem = RptService.GenerateAssessment(p.PropertyId, year)
        If problem IsNot Nothing Then
            UiHelper.ShowWarning(problem)
            Return
        End If
        LoadLedger(p.PropertyId)
        tabs.SelectedIndex = 0
        Dim overdueNow = If(selected Is Nothing, 0, selected.Quarters.Where(Function(q) q.IsOverdue).Count())
        UiHelper.ShowSuccess("The " & year & " assessment for PIN " & p.Pin & " was created: " & UiHelper.FormatMoney(tax.TotalDue) & "." & vbCrLf &
                             If(overdueNow > 0, overdueNow & " quarter(s) are already past due and carry penalties. Record the payments in order.",
                                "Next: record each quarterly payment with ""Record Payment"" (Q1 is due Mar 31)."), "Assessment generated")
    End Sub

    Private Sub Pay_Click(sender As Object, e As EventArgs)
        If selected Is Nothing OrElse Not canManage Then Return
        Dim nextQ = selected.NextPayable
        Dim blocker = If(Not selected.IsAssessed, "Generate the " & TaxYear & " assessment first.",
                         If(nextQ Is Nothing, "All quarters of " & TaxYear & " are paid.", RptService.GetPaymentBlocker(selected, nextQ.Quarter)))
        If blocker IsNot Nothing Then
            UiHelper.ShowWarning(blocker)
            Return
        End If
        Dim businessId = selected.RealProperty.BusinessId
        Dim propertyId = selected.RealProperty.PropertyId
        Dim result As RptPaymentResult
        Using dlg As New RptPaymentDialog(selected, nextQ)
            If dlg.ShowDialog(FindForm()) <> DialogResult.OK Then Return
            result = dlg.Result
        End Using
        LoadLedger(propertyId)
        tabs.SelectedIndex = 0

        Dim pay = result.Payment
        Dim message = pay.OrNo & " recorded: " & UiHelper.FormatMoney(pay.AmountPaid + pay.Penalty) &
                      If(pay.Penalty > 0, " (" & UiHelper.FormatMoney(pay.AmountPaid) & " + " & UiHelper.FormatMoney(pay.Penalty) & " penalty)", "") & "."
        If result.EndorsedReference IsNot Nothing Then
            message &= vbCrLf & "All due quarters are paid. The RPT endorsement of " & result.EndorsedReference & " is now Endorsed."
        ElseIf RptService.GetTaxClearanceStatus(businessId) = RptService.ClearanceIssued Then
            message &= vbCrLf & "All due quarters are paid - the Tax Clearance is Issued."
        End If
        Dim upcoming = selected?.NextPayable
        message &= vbCrLf & If(upcoming Is Nothing, "All " & TaxYear & " quarters are now paid.",
                               "Next: " & upcoming.Label & " (" & UiHelper.FormatMoney(upcoming.Amount) & ") is " &
                               If(upcoming.IsOverdue, "overdue since ", "due ") & UiHelper.FormatDate(upcoming.DueDate) & ".")
        UiHelper.ShowSuccess(message, "Payment recorded")
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then tips.Dispose()
        MyBase.Dispose(disposing)
    End Sub

End Class
