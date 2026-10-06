''' <summary>
''' Issue / Renew a health certificate for one employee, or for several at once (Renew Selected).
''' Shows a live preview of the certificate number(s) and the expiry date.
''' All rules and saving are done by HealthCertificateService.
''' </summary>
Public Class IssueCertificateDialog
    Inherits ModernDialog

    Private ReadOnly targets As List(Of EmployeeHealth)
    Private ReadOnly dtpIssue As New DateTimePicker()
    Private ReadOnly txtIssuedBy As New TextBox()
    Private ReadOnly errIssue As Label
    Private ReadOnly errIssuedBy As Label
    Private ReadOnly preview As New RoundedPanel()
    Private ReadOnly lblPreviewNo As New Label()
    Private ReadOnly lblPreviewExpiry As New Label()

    ''' <summary>The certificates that were issued (after OK).</summary>
    Public ReadOnly Property Result As RenewResult

    ''' <param name="employees">Employees to issue a certificate to (all should need renewal).</param>
    Public Sub New(business As Business, employees As List(Of EmployeeHealth))
        targets = employees
        ClientSize = New Size(560, 470)
        Dim isSingle = targets.Count = 1
        If isSingle Then
            Dim isFirst = targets(0).Certificate Is Nothing
            SetTitle(If(isFirst, "Issue Health Certificate", "Renew Health Certificate"),
                     targets(0).Employee.FullName & "  ·  " & business.BusinessName)
        Else
            SetTitle("Renew Selected Certificates", targets.Count & " employees  ·  " & business.BusinessName)
        End If

        ' --- Summary box ---
        Dim info As New RoundedPanel With {.BackColor = Theme.AccentSoft, .ShowShadow = False, .Radius = 10}
        info.SetBounds(24, 18, 512, 64)
        If isSingle Then
            Dim row = targets(0)
            AddInfo(info, "Employee", row.Employee.FullName & " (" & row.Employee.Position & ")", 16, 10, 230)
            AddInfo(info, "Current certificate", If(row.Certificate Is Nothing, "None", row.Certificate.CertificateNo), 256, 10, 130)
            AddInfo(info, "Status", row.Status, 396, 10, 104)
        Else
            Dim names = String.Join(", ", targets.Select(Function(t) t.Employee.FullName))
            AddInfo(info, "Employees (" & targets.Count & ")", names, 16, 10, 484)
        End If
        Body.Controls.Add(info)

        ' --- Fields ---
        dtpIssue.Format = DateTimePickerFormat.Custom
        dtpIssue.CustomFormat = "MMM dd, yyyy"
        dtpIssue.MinDate = HealthCertificateService.GetMinIssueDate()
        dtpIssue.MaxDate = Date.Today
        dtpIssue.Value = Date.Today
        dtpIssue.Font = Theme.InputFont
        dtpIssue.Height = 32
        errIssue = AddField("Issue date", dtpIssue, 24, 98, 200)

        txtIssuedBy.Font = Theme.InputFont
        txtIssuedBy.BorderStyle = BorderStyle.FixedSingle
        txtIssuedBy.MaxLength = 150
        txtIssuedBy.PlaceholderText = "e.g. Dr. Paolo Agustin"
        txtIssuedBy.Text = HealthCertificateService.GetDefaultIssuer(business.BusinessId)
        errIssuedBy = AddField("Issued by (physician / health officer)", txtIssuedBy, 244, 98, 292)

        ' --- Live preview of what will be issued ---
        preview.BackColor = Theme.StatusGreenSoft
        preview.ShowShadow = False
        preview.Radius = 10
        preview.SetBounds(24, 182, 512, 74)
        Dim icon As New Label With {
            .Text = Icons.CheckMark, .Font = Theme.IconFont(16.0F), .ForeColor = Theme.StatusGreen,
            .BackColor = Theme.StatusGreenSoft, .TextAlign = ContentAlignment.MiddleCenter, .AutoSize = False
        }
        icon.SetBounds(12, 17, 40, 40)
        lblPreviewNo.Font = Theme.BodyBoldFont
        lblPreviewNo.ForeColor = Theme.TextDark
        lblPreviewNo.BackColor = Theme.StatusGreenSoft
        lblPreviewNo.AutoEllipsis = True
        lblPreviewNo.SetBounds(60, 14, 440, 22)
        lblPreviewExpiry.Font = Theme.SmallFont
        lblPreviewExpiry.ForeColor = Theme.TextMuted
        lblPreviewExpiry.BackColor = Theme.StatusGreenSoft
        lblPreviewExpiry.AutoEllipsis = True
        lblPreviewExpiry.SetBounds(60, 38, 440, 20)
        preview.Controls.AddRange({icon, lblPreviewNo, lblPreviewExpiry})
        Body.Controls.Add(preview)

        Dim note As New Label With {
            .Text = "The certificate number and expiry date are generated automatically.",
            .Font = Theme.SmallFont, .ForeColor = Theme.TextMuted, .AutoSize = False, .AutoEllipsis = True
        }
        note.SetBounds(24, 266, 512, 20)
        Body.Controls.Add(note)

        ' --- Buttons ---
        Dim save = AddFooterButton(If(isSingle, If(targets(0).Certificate Is Nothing, "Issue Certificate", "Renew Certificate"),
                                      "Renew " & targets.Count & " Certificates"), "primary")
        Dim cancel = AddFooterButton("Cancel")
        AddHandler save.Click, AddressOf Save_Click
        AddHandler cancel.Click, Sub() DialogResult = DialogResult.Cancel
        AcceptButton = save
        CancelButton = cancel

        AddHandler dtpIssue.ValueChanged, Sub()
                                              errIssue.Text = ""
                                              UpdatePreview()
                                          End Sub
        AddHandler txtIssuedBy.TextChanged, Sub() errIssuedBy.Text = ""
        UpdatePreview()
    End Sub

    Private Sub UpdatePreview()
        Dim issueDate = dtpIssue.Value.Date
        Dim numbers = HealthCertificateService.PreviewNumbers(issueDate, targets.Count)
        lblPreviewNo.Text = If(numbers.Count = 1, "Certificate No. " & numbers(0),
                               "Certificate Nos. " & numbers.First() & " to " & numbers.Last())
        Dim expiry = StatusService.GetHealthCertificateExpiry(issueDate)
        lblPreviewExpiry.Text = "Valid until " & UiHelper.FormatDate(expiry) & "  (" & StatusService.DaysUntil(expiry) & " days from today)"
        Tips.SetToolTip(lblPreviewNo, lblPreviewNo.Text)
    End Sub

    Private Sub Save_Click(sender As Object, e As EventArgs)
        errIssue.Text = "" : errIssuedBy.Text = ""
        Dim errors = HealthCertificateService.ValidateIssue(dtpIssue.Value.Date, txtIssuedBy.Text)
        If errors.Count > 0 Then
            If errors.ContainsKey("issue") Then errIssue.Text = errors("issue")
            If errors.ContainsKey("by") Then
                errIssuedBy.Text = errors("by")
                txtIssuedBy.Focus()
            End If
            Return
        End If

        Cursor = Cursors.WaitCursor
        Dim outcome = HealthCertificateService.RenewMany(targets.Select(Function(t) t.Employee.EmployeeId),
                                                         dtpIssue.Value.Date, txtIssuedBy.Text)
        Cursor = Cursors.Default
        If outcome.ErrorMessage IsNot Nothing Then
            UiHelper.ShowError(outcome.ErrorMessage)
            Return
        End If
        If outcome.Issued.Count = 0 Then
            UiHelper.ShowWarning("No certificate was issued." & vbCrLf & vbCrLf & String.Join(vbCrLf, outcome.Skipped))
            Return
        End If
        _Result = outcome
        DialogResult = DialogResult.OK
    End Sub

End Class
