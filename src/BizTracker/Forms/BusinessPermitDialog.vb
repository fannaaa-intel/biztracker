Imports System.Globalization

''' <summary>
''' Add / Edit / View a business permit application.
''' New and Renewal file a new application; Edit saves changes (staff); View is read-only.
''' All rules and saving are done by BusinessPermitService.
''' </summary>
Public Class BusinessPermitDialog
    Inherits ModernDialog

    Public Enum DialogMode
        NewApplication
        Renewal
        Edit
        View
    End Enum

    Private ReadOnly mode As DialogMode
    Private ReadOnly permit As BusinessPermit

    Private ReadOnly numYear As New NumericUpDown()
    Private ReadOnly dtpFiled As New DateTimePicker()
    Private ReadOnly txtGross As New TextBox()
    Private ReadOnly txtRemarks As New TextBox()
    Private ReadOnly errYear As Label
    Private ReadOnly errFiled As Label
    Private ReadOnly errGross As Label

    ''' <summary>The saved permit id (after OK).</summary>
    Public ReadOnly Property PermitId As Integer
        Get
            Return permit.PermitId
        End Get
    End Property

    ''' <param name="business">The business the application is for.</param>
    ''' <param name="existing">The application to edit/view (Nothing for New / Renewal).</param>
    Public Sub New(dialogMode As DialogMode, business As Business, Optional existing As BusinessPermit = Nothing)
        mode = dialogMode
        ClientSize = New Size(580, 520)

        If existing IsNot Nothing Then
            permit = existing
        Else
            permit = New BusinessPermit With {
                .BusinessId = business.BusinessId,
                .BusinessName = business.BusinessName,
                .ApplicationType = If(mode = DialogMode.Renewal, "Renewal", "New"),
                .PermitYear = If(mode = DialogMode.Renewal,
                                 BusinessPermitService.SuggestRenewalYear(business.BusinessId), Date.Today.Year),
                .DateFiled = Date.Today
            }
        End If

        Select Case mode
            Case DialogMode.NewApplication
                SetTitle("New Business Permit Application", "File a new Mayor's / Business Permit for " & business.BusinessName)
            Case DialogMode.Renewal
                SetTitle("Renew Business Permit", "File a renewal application for " & business.BusinessName)
            Case DialogMode.Edit
                SetTitle("Edit Application", permit.ReferenceNo & "  ·  " & business.BusinessName)
            Case Else
                SetTitle("Application Details", permit.ReferenceNo & "  ·  " & business.BusinessName)
        End Select

        ' --- Summary box ---
        Dim info As New RoundedPanel With {.BackColor = Theme.AccentSoft, .ShowShadow = False, .Radius = 10}
        info.SetBounds(24, 18, 532, 64)
        AddInfo(info, "Business", business.BusinessName, 16, 10, 240)
        AddInfo(info, "Application type", permit.ApplicationType, 270, 10, 120)
        AddInfo(info, "Status", If(permit.PermitId = 0, "New", permit.Status), 400, 10, 120)
        Body.Controls.Add(info)

        ' --- Fields ---
        numYear.Minimum = Date.Today.Year - 5
        numYear.Maximum = Date.Today.Year + 1
        numYear.Value = Math.Max(numYear.Minimum, Math.Min(numYear.Maximum, permit.PermitYear))
        numYear.Font = Theme.InputFont
        numYear.Height = 32
        errYear = AddField("Permit year", numYear, 24, 98, 252)

        dtpFiled.Format = DateTimePickerFormat.Custom
        dtpFiled.CustomFormat = "MMM dd, yyyy"
        dtpFiled.MaxDate = Date.Today
        dtpFiled.Value = If(permit.DateFiled > Date.Today OrElse permit.DateFiled = Date.MinValue, Date.Today, permit.DateFiled)
        dtpFiled.Font = Theme.InputFont
        dtpFiled.Height = 32
        errFiled = AddField("Date filed", dtpFiled, 304, 98, 252)

        txtGross.Font = Theme.InputFont
        txtGross.BorderStyle = BorderStyle.FixedSingle
        txtGross.Height = 32
        txtGross.Text = If(permit.GrossReceipts > 0, permit.GrossReceipts.ToString("N2"), "")
        txtGross.PlaceholderText = "0.00"
        errGross = AddField(If(permit.ApplicationType = "New", "Capital investment (₱)",
                               "Gross receipts / sales of the previous year (₱)"), txtGross, 24, 176, 532)

        txtRemarks.Multiline = True
        txtRemarks.Font = Theme.InputFont
        txtRemarks.BorderStyle = BorderStyle.FixedSingle
        txtRemarks.Height = 70
        txtRemarks.MaxLength = 255
        txtRemarks.Text = permit.Remarks
        AddField("Remarks (optional)", txtRemarks, 24, 254, 532)

        ' --- Buttons ---
        If mode = DialogMode.View Then
            Dim close = AddFooterButton("Close", "primary")
            AddHandler close.Click, Sub() DialogResult = DialogResult.Cancel
            CancelButton = close
            For Each c In {numYear, dtpFiled, CType(txtGross, Control), txtRemarks}
                c.Enabled = False
            Next
        Else
            Dim save = AddFooterButton(If(mode = DialogMode.Edit, "Save Changes", "Submit Application"), "primary")
            Dim cancel = AddFooterButton("Cancel")
            AddHandler save.Click, AddressOf Save_Click
            AddHandler cancel.Click, Sub() DialogResult = DialogResult.Cancel
            AcceptButton = save
            CancelButton = cancel
        End If

        ' Clear a field's error as soon as the user changes it
        AddHandler numYear.ValueChanged, Sub() errYear.Text = ""
        AddHandler dtpFiled.ValueChanged, Sub() errFiled.Text = ""
        AddHandler txtGross.TextChanged, Sub() errGross.Text = ""
    End Sub

    Private Sub Save_Click(sender As Object, e As EventArgs)
        errYear.Text = "" : errFiled.Text = "" : errGross.Text = ""

        Dim gross As Decimal
        Dim grossText = txtGross.Text.Replace("₱", "").Replace(",", "").Trim()
        If Not Decimal.TryParse(grossText, NumberStyles.Number, CultureInfo.InvariantCulture, gross) Then
            errGross.Text = "Enter a valid amount, e.g. 150000.00"
            txtGross.Focus()
            Return
        End If

        ' Work on a copy so a failed save does not change the caller's object
        Dim candidate As New BusinessPermit With {
            .PermitId = permit.PermitId, .BusinessId = permit.BusinessId, .ReferenceNo = permit.ReferenceNo,
            .ApplicationType = permit.ApplicationType, .Status = permit.Status,
            .PermitYear = CInt(numYear.Value), .GrossReceipts = Math.Round(gross, 2),
            .AssessedAmount = permit.AssessedAmount, .Surcharge = permit.Surcharge, .Interest = permit.Interest,
            .MayorsPermitNo = permit.MayorsPermitNo, .DateFiled = dtpFiled.Value.Date,
            .DateIssued = permit.DateIssued, .ValidUntil = permit.ValidUntil, .Remarks = txtRemarks.Text.Trim()
        }

        Dim errors = BusinessPermitService.Validate(candidate)
        If errors.Count > 0 Then
            If errors.ContainsKey("year") Then errYear.Text = errors("year")
            If errors.ContainsKey("filed") Then errFiled.Text = errors("filed")
            If errors.ContainsKey("gross") Then errGross.Text = errors("gross")
            Return
        End If

        Cursor = Cursors.WaitCursor
        Dim ok As Boolean
        If mode = DialogMode.Edit Then
            ok = BusinessPermitService.UpdateApplication(candidate)
        Else
            Dim newId = BusinessPermitService.FileApplication(candidate)
            ok = newId > 0
            If ok Then candidate.PermitId = newId
        End If
        Cursor = Cursors.Default
        If Not ok Then
            UiHelper.ShowError("The application could not be saved. Please check the details and try again.")
            Return
        End If

        permit.PermitId = candidate.PermitId
        DialogResult = DialogResult.OK
    End Sub

End Class
