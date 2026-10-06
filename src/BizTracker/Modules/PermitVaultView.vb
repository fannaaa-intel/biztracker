''' <summary>
''' Permit Vault (Admin, BPLO, Owner): every issued document of a business, ready to print.
'''   Toolbar : business selector (staff) or the owner's business, Print Document, Verify Document
'''   Cards   : issued documents, valid, expiring soon, expired
'''   Left    : documents grid with search
'''   Right   : the selected document's details, verification code and print rule
''' Only valid (unexpired) issued documents can be printed. No SQL here: DocumentService / ReportService.
''' </summary>
Public Class PermitVaultView
    Inherits ModuleView

    ''' <summary>Grid row (display only).</summary>
    Private Class DocumentRow
        Public Property Index As Integer
        Public Property Document As String
        Public Property ReferenceNo As String
        Public Property Holder As String
        Public Property Issued As String
        Public Property Status As String
    End Class

    ' Toolbar
    Private ReadOnly toolbar As New ModuleToolbar("vault")
    Private btnPrint As Button
    Private btnVerify As Button

    ' Summary cards
    Private ReadOnly cardTotal As New StatCard(Icons.Document, "Issued Documents")
    Private ReadOnly cardValid As New StatCard(Icons.CheckShield, "Valid")
    Private ReadOnly cardExpiring As New StatCard(Icons.Calendar, "Expiring Soon")
    Private ReadOnly cardExpired As New StatCard(Icons.Warning, "Expired")

    ' List
    Private ReadOnly txtSearch As New TextBox()
    Private ReadOnly grid As New ModernGrid()
    Private ReadOnly lblEmpty As New Label()

    ' Details
    Private ReadOnly detailHeader As New Panel()
    Private ReadOnly lblDetailRef As New Label()
    Private ReadOnly badgeDetail As New StatusBadge()
    Private ReadOnly lblDetailInfo As New Label()
    Private ReadOnly detailsList As New WheelScrollPanel()
    Private ReadOnly detailContent As New Panel()
    Private ReadOnly lblDetailEmpty As New Label()
    Private ReadOnly tips As New ToolTip()

    ' State
    Private documents As New List(Of IssuedDocument)
    Private selected As IssuedDocument
    Private loading As Boolean

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

        btnPrint = toolbar.AddAction("Print Document", "Print", "primary", AddressOf Print_Click)
        btnVerify = toolbar.AddAction("Verify Document", "Verify", "secondary", AddressOf Verify_Click,
                                      tip:="Check a printed document's reference number and verification code")
        AddHandler toolbar.BusinessChanged, Sub()
                                                txtSearch.Text = ""
                                                LoadDocuments(Nothing)
                                            End Sub

        root.Controls.Add(toolbar, 0, 0)
        root.Controls.Add(BuildCards(), 0, 1)
        root.Controls.Add(BuildMain(), 0, 2)
        Controls.Add(root)
        ResumeLayout(False)
    End Sub

    Public Overrides ReadOnly Property Subtitle As String
        Get
            Return "Issued permits, certificates and receipts, ready to print"
        End Get
    End Property

    ' =====================================================================
    '  Layout
    ' =====================================================================

    Private Function BuildCards() As Control
        Dim cards = {cardTotal, cardValid, cardExpiring, cardExpired}
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
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 56))
        row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 44))
        row.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        row.Controls.Add(BuildListCard(), 0, 0)
        row.Controls.Add(BuildDetailCard(), 1, 0)
        Return row
    End Function

    Private Function BuildListCard() As Control
        Dim card As New RoundedPanel With {.Dock = DockStyle.Fill, .Margin = New Padding(0, 0, 8, 0), .Padding = New Padding(16, 14, 16, 16)}

        Dim header As New Panel With {.Dock = DockStyle.Top, .Height = Dpi(48), .BackColor = Color.White}
        Dim title As New Label With {.Text = "Issued Documents", .Font = Theme.SubtitleFont, .ForeColor = Theme.TextDark,
                                     .AutoSize = True, .Location = New Point(0, Dpi(10)), .BackColor = Color.White}
        Dim searchBox = UiHelper.CreateInputBox(txtSearch, "Search…", Icons.Search)
        searchBox.Height = Dpi(38)
        header.Controls.AddRange({title, searchBox})
        AddHandler header.Resize,
            Sub()
                Dim w = Math.Max(Dpi(120), Math.Min(Dpi(240), header.ClientSize.Width - title.Width - Dpi(16)))
                searchBox.SetBounds(header.ClientSize.Width - w, Dpi(2), w, Dpi(38))
            End Sub
        AddHandler txtSearch.TextChanged, Sub() BindGrid(selected?.ReferenceNo)

        UiHelper.StyleGrid(grid, "Status")
        grid.Dock = DockStyle.Fill
        UiHelper.AddGridColumn(grid, "Document", "DOCUMENT", "Document", 150, Dpi(130))
        UiHelper.AddGridColumn(grid, "ReferenceNo", "REFERENCE", "ReferenceNo", 135, Dpi(135))
        UiHelper.AddGridColumn(grid, "Holder", "ISSUED TO", "Holder", 160, Dpi(120))
        UiHelper.AddGridColumn(grid, "Issued", "ISSUED", "Issued", 112, Dpi(112))
        UiHelper.AddGridColumn(grid, "Status", "STATUS", "Status", 120, Dpi(120))
        AddHandler grid.SelectionChanged, Sub() If Not loading Then ShowSelected()
        AddHandler grid.CellDoubleClick, Sub(s, e) If e.RowIndex >= 0 Then Print_Click(Nothing, EventArgs.Empty)
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

    ''' <summary>Widths the longest document name / holder need (text is never cut: the column hides instead).</summary>
    Private documentNeed As Integer = 130
    Private holderNeed As Integer = 120

    ''' <summary>Hides the less important columns when the grid is narrow (document, reference and status always stay).</summary>
    Private Sub FitGridColumns()
        grid.Columns("Issued").Visible = grid.Width >= Dpi(560)
        Dim docCol = grid.Columns("Document")
        docCol.MinimumWidth = documentNeed
        Dim others = grid.Columns.Cast(Of DataGridViewColumn)().
                     Where(Function(c) c.Visible AndAlso c.Name <> "Holder").Sum(Function(c) c.MinimumWidth)
        Dim holder = grid.Columns("Holder")
        holder.Visible = grid.Width - others - 4 >= holderNeed
        If holder.Visible Then holder.MinimumWidth = holderNeed
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

        Dim gap As New Panel With {.Dock = DockStyle.Top, .Height = 8, .BackColor = Color.White}
        detailsList.Dock = DockStyle.Fill
        detailsList.BackColor = Color.White

        lblDetailEmpty.Dock = DockStyle.Fill
        lblDetailEmpty.TextAlign = ContentAlignment.MiddleCenter
        lblDetailEmpty.Font = Theme.BodyFont
        lblDetailEmpty.ForeColor = Theme.TextMuted
        lblDetailEmpty.BackColor = Color.White

        detailContent.Dock = DockStyle.Fill
        detailContent.BackColor = Color.White
        detailContent.Controls.Add(detailsList)
        detailContent.Controls.Add(gap)
        detailContent.Controls.Add(detailHeader)
        card.Controls.Add(lblDetailEmpty)
        card.Controls.Add(detailContent)
        Return card
    End Function

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
        LoadDocuments(selected?.ReferenceNo)
    End Sub

    Private Sub LoadDocuments(selectRef As String)
        documents = If(Business Is Nothing, New List(Of IssuedDocument)(), DocumentService.GetDocuments(Business.BusinessId))
        UpdateCards()
        BindGrid(selectRef)
    End Sub

    ''' <summary>Short name for the grid ("Inspection Cert.", "Building Permit"...).</summary>
    Private Shared Function GridName(d As IssuedDocument) As String
        Select Case d.Kind
            Case DocumentKinds.OrderOfPayment : Return "Order of Payment"
            Case DocumentKinds.OfficialReceipt : Return "Official Receipt"
            Case DocumentKinds.TaxClearance : Return "Tax Clearance"
            Case DocumentKinds.InspectionCertificate : Return "Inspection Cert."
            Case DocumentKinds.ConstructionClearance
                Return If(d.ReferenceNo.StartsWith("LC-"), "Locational", If(d.ReferenceNo.StartsWith("FSEC-"), "FSEC",
                          If(d.ReferenceNo.StartsWith("OCC-"), "Occupancy", "Building Permit")))
            Case Else : Return d.Kind
        End Select
    End Function

    Private Sub BindGrid(selectRef As String)
        Dim term = txtSearch.Text.Trim().ToLowerInvariant()
        Dim rows = documents.Select(Function(d, i) New With {d, i}).
            Where(Function(x) term = "" OrElse
                              (x.d.Kind & " " & x.d.Title & " " & x.d.ReferenceNo & " " & x.d.Holder & " " & x.d.Status).ToLowerInvariant().Contains(term)).
            Select(Function(x) New DocumentRow With {
                .Index = x.i, .Document = GridName(x.d), .ReferenceNo = x.d.ReferenceNo, .Holder = x.d.Holder,
                .Issued = UiHelper.FormatDate(x.d.IssueDate), .Status = x.d.Status
            }).ToList()

        documentNeed = Math.Max(Dpi(130), rows.Select(Function(r) TextRenderer.MeasureText(r.Document, Theme.BodyFont).Width + 16).DefaultIfEmpty(0).Max())
        holderNeed = Math.Max(Dpi(120), rows.Select(Function(r) TextRenderer.MeasureText(r.Holder, Theme.BodyFont).Width + 16).DefaultIfEmpty(0).Max())
        loading = True
        grid.DataSource = rows
        FitGridColumns()
        Dim index = If(selectRef IsNot Nothing, rows.FindIndex(Function(r) r.ReferenceNo = selectRef), -1)
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
                               If(term <> "", "No documents match your search.",
                                  "No issued documents yet." & vbCrLf & "Permits, certificates and receipts appear here once they are issued."))
            lblEmpty.BringToFront()
        End If
        ShowSelected()
    End Sub

    Private Sub ShowSelected()
        Dim row = If(grid.SelectedRows.Count > 0, TryCast(grid.SelectedRows(0).DataBoundItem, DocumentRow), Nothing)
        selected = If(row Is Nothing, Nothing, documents(row.Index))
        ShowDetails()
        UpdateButtons()
    End Sub

    ' =====================================================================
    '  Cards, buttons and details
    ' =====================================================================

    Private Sub UpdateCards()
        If Business Is Nothing Then
            For Each c In {cardTotal, cardValid, cardExpiring, cardExpired}
                c.SetValue("—", "", "No business selected")
            Next
            Return
        End If
        Dim counts = DocumentService.CountByStatus(documents)
        Dim kinds = documents.Select(Function(d) d.Kind).Distinct().Count()
        cardTotal.SetValue(documents.Count.ToString(), "", If(documents.Count = 0, "nothing issued yet", kinds & " document type" & If(kinds = 1, "", "s")))
        cardValid.SetValue(counts(StatusService.Valid).ToString(), "", "can be printed")
        cardExpiring.SetValue(counts(StatusService.ExpiringSoon).ToString(), "", "within " & StatusService.WarningDays & " days")
        cardExpired.SetValue(counts(StatusService.Expired).ToString(), "", If(counts(StatusService.Expired) > 0, "renew to print", "none"))
    End Sub

    Private Sub UpdateButtons()
        Dim blocker = DocumentService.GetPrintBlocker(selected)
        btnPrint.Enabled = blocker Is Nothing
        toolbar.SetTip(btnPrint, If(blocker, "Open " & selected.ReferenceNo & " in your browser to print or save as PDF"))
    End Sub

    Private Sub ShowDetails()
        Dim has = selected IsNot Nothing
        lblDetailEmpty.Visible = Not has
        detailContent.Visible = has
        If Not has Then
            lblDetailEmpty.Text = If(documents.Count = 0, "Nothing to show yet.", "Select a document to see its details.")
            Return
        End If

        lblDetailRef.Text = selected.ReferenceNo
        badgeDetail.Text = selected.Status
        PlaceBadge()
        Dim info = selected.Title & " · " & selected.BusinessName
        lblDetailInfo.Text = info
        tips.SetToolTip(lblDetailInfo, info)

        Dim rows As New List(Of Control)
        rows.Add(InfoRow(tips, "Document", selected.Title, "Reference", selected.ReferenceNo))
        rows.Add(InfoRow(tips, "Issued to", selected.Holder, "Business", selected.BusinessName))
        rows.Add(InfoRow(tips, "Date issued", UiHelper.FormatDate(selected.IssueDate),
                         "Valid until", If(selected.ValidUntil.HasValue, UiHelper.FormatDate(selected.ValidUntil), "No expiry")))
        rows.Add(InfoRow(tips, "Verification code", DocumentService.GetVerificationCode(selected), "Status", selected.Status))
        Dim blocker = DocumentService.GetPrintBlocker(selected)
        If blocker IsNot Nothing Then
            rows.Add(NoteRow(tips, blocker, Theme.StatusRed))
        ElseIf selected.Status = StatusService.ExpiringSoon Then
            rows.Add(NoteRow(tips, "Expires in " & StatusService.DaysUntil(selected.ValidUntil.Value) & " days - it can still be printed, but plan the renewal.", Theme.StatusAmber))
        Else
            rows.Add(NoteRow(tips, "Click ""Print Document"" to open it in your browser, then print it or save it as PDF.", Theme.StatusGreen))
        End If
        If AccessService.CanAccess(selected.Screen) Then
            Dim target = selected
            rows.Add(ListRow(tips, "Source record", "Managed in " & ModuleTitle(selected.Screen), "",
                             {New RowLink("Open module", Theme.SidebarBlue, Sub() RequestNavigate(target.Screen, target.BusinessId))}))
        End If
        detailsList.SetRows(rows)
    End Sub

    Private Shared Function ModuleTitle(screen As AppScreen) As String
        Select Case screen
            Case AppScreen.BusinessPermits : Return "Business Permits"
            Case AppScreen.SanitaryPermits : Return "Sanitary Permits"
            Case AppScreen.HealthCertificates : Return "Health Certificates"
            Case AppScreen.RealPropertyTax : Return "Real Property Tax"
            Case AppScreen.AnnualInspections : Return "Annual Inspections"
            Case Else : Return "Construction Permit"
        End Select
    End Function

    ' =====================================================================
    '  Actions
    ' =====================================================================

    Private Sub Print_Click(sender As Object, e As EventArgs)
        If selected Is Nothing Then Return
        Dim blocker = DocumentService.GetPrintBlocker(selected)
        If blocker IsNot Nothing Then
            UiHelper.ShowWarning(blocker, "Cannot print")
            Return
        End If
        Dim result = ReportService.Print(selected)
        If result.ErrorMessage IsNot Nothing Then
            UiHelper.ShowWarning(result.ErrorMessage, "Cannot print")
            Return
        End If
        UiHelper.ShowSuccess(selected.Title & " " & selected.ReferenceNo & " opened in your browser." & vbCrLf &
                             "Next: click ""Print / Save as PDF"" on the page to print it or keep a PDF copy.", "Document ready")
    End Sub

    Private Sub Verify_Click(sender As Object, e As EventArgs)
        Using dlg As New VerifyDocumentDialog(selected?.ReferenceNo)
            dlg.ShowDialog(FindForm())
        End Using
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then tips.Dispose()
        MyBase.Dispose(disposing)
    End Sub

End Class
