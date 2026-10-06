''' <summary>
''' Add / Edit a real property (Real Property Tax module), with a live preview of the annual tax
''' (basic + SEF from the settings table). All rules and saving are done by RptService.
''' </summary>
Public Class PropertyDialog
    Inherits ModernDialog

    Private ReadOnly isNew As Boolean
    Private ReadOnly prop As RealProperty

    Private ReadOnly txtPin As New TextBox()
    Private ReadOnly txtTd As New TextBox()
    Private ReadOnly txtLocation As New TextBox()
    Private ReadOnly cboType As New ComboBox()
    Private ReadOnly cboClass As New ComboBox()
    Private ReadOnly txtValue As New TextBox()
    Private ReadOnly errPin As Label
    Private ReadOnly errTd As Label
    Private ReadOnly errLocation As Label
    Private ReadOnly errType As Label
    Private ReadOnly errClass As Label
    Private ReadOnly errValue As Label
    Private ReadOnly previewBox As New RoundedPanel()
    Private ReadOnly lblPreview As New Label()

    ''' <summary>The saved property id (after OK).</summary>
    Public ReadOnly Property PropertyId As Integer
        Get
            Return prop.PropertyId
        End Get
    End Property

    ''' <param name="existing">The property to edit (Nothing to add a new one).</param>
    Public Sub New(business As Business, Optional existing As RealProperty = Nothing)
        isNew = existing Is Nothing
        prop = If(existing, New RealProperty With {.BusinessId = business.BusinessId, .Location = business.Address})
        ClientSize = New Size(560, 586)
        If isNew Then
            SetTitle("Add Property", "Register a real property of " & business.BusinessName)
        Else
            SetTitle("Edit Property", "PIN " & prop.Pin & "  ·  " & business.BusinessName)
        End If

        ' --- Summary box ---
        Dim info As New RoundedPanel With {.BackColor = Theme.AccentSoft, .ShowShadow = False, .Radius = 10}
        info.SetBounds(24, 18, 512, 64)
        AddInfo(info, "Business", business.BusinessName, 16, 10, 300)
        AddInfo(info, "Tax rates", "Basic " & FormatRate(RptService.BasicRate) & " + SEF " & FormatRate(RptService.SefRate), 330, 10, 170)
        Body.Controls.Add(info)

        ' --- Fields ---
        StyleText(txtPin, prop.Pin, "e.g. 015-06-001-01-003", 30)
        errPin = AddField("Property Index No. (PIN)", txtPin, 24, 98, 246)
        StyleText(txtTd, prop.TdNo, "e.g. TD-2026-10023", 30)
        errTd = AddField("Tax Declaration No.", txtTd, 290, 98, 246)
        StyleText(txtLocation, prop.Location, "Street, barangay, municipality", 255)
        errLocation = AddField("Location", txtLocation, 24, 176, 512)

        StyleCombo(cboType, RptService.PropertyTypes, prop.PropertyType)
        errType = AddField("Type", UiHelper.CreateComboBox(cboType, 29), 24, 254, 158)
        StyleCombo(cboClass, RptService.Classifications, prop.Classification)
        errClass = AddField("Classification", UiHelper.CreateComboBox(cboClass, 29), 198, 254, 158)
        StyleText(txtValue, If(prop.AssessedValue > 0, prop.AssessedValue.ToString("0.00"), ""), "e.g. 40000", 16)
        errValue = AddField("Assessed value (₱)", txtValue, 372, 254, 164)

        ' Live annual tax preview
        previewBox.ShowShadow = False
        previewBox.Radius = 10
        previewBox.Padding = New Padding(14, 0, 14, 0)
        previewBox.SetBounds(24, 334, 512, 48)
        lblPreview.Dock = DockStyle.Fill
        lblPreview.TextAlign = ContentAlignment.MiddleLeft
        lblPreview.Font = Theme.SmallBoldFont
        lblPreview.AutoEllipsis = True
        previewBox.Controls.Add(lblPreview)
        Body.Controls.Add(previewBox)

        Dim note As New Label With {
            .Font = Theme.SmallFont, .ForeColor = Theme.TextMuted, .AutoSize = False, .AutoEllipsis = True,
            .Text = If(isNew, "After saving, use ""Generate Assessment"" to create this year's tax bill (payable quarterly).",
                       "Existing assessments keep their amounts. A new assessed value applies to assessments generated later.")
        }
        note.SetBounds(24, 392, 512, 40)
        Tips.SetToolTip(note, note.Text)
        Body.Controls.Add(note)

        ' --- Buttons ---
        Dim save = AddFooterButton(If(isNew, "Add Property", "Save Changes"), "primary")
        Dim cancel = AddFooterButton("Cancel")
        AddHandler save.Click, AddressOf Save_Click
        AddHandler cancel.Click, Sub() DialogResult = DialogResult.Cancel
        AcceptButton = save
        CancelButton = cancel

        AddHandler txtPin.TextChanged, Sub() errPin.Text = ""
        AddHandler txtTd.TextChanged, Sub() errTd.Text = ""
        AddHandler txtLocation.TextChanged, Sub() errLocation.Text = ""
        AddHandler cboType.SelectedIndexChanged, Sub() errType.Text = ""
        AddHandler cboClass.SelectedIndexChanged, Sub() errClass.Text = ""
        AddHandler txtValue.TextChanged, Sub()
                                             errValue.Text = ""
                                             UpdatePreview()
                                         End Sub
        UpdatePreview()
    End Sub

    Private Shared Function FormatRate(rate As Decimal) As String
        Return (rate * 100D).ToString("0.##") & "%"
    End Function

    Private Shared Sub StyleText(box As TextBox, value As String, placeholder As String, maxLength As Integer)
        box.Font = Theme.InputFont
        box.BorderStyle = BorderStyle.FixedSingle
        box.MaxLength = maxLength
        box.Text = value
        box.PlaceholderText = placeholder
    End Sub

    Private Shared Sub StyleCombo(combo As ComboBox, items As String(), selectedValue As String)
        combo.DropDownStyle = ComboBoxStyle.DropDownList
        combo.FlatStyle = FlatStyle.Flat
        combo.Font = Theme.InputFont
        combo.Items.AddRange(items)
        combo.SelectedItem = selectedValue
        If combo.SelectedIndex < 0 Then combo.SelectedIndex = 0
        UiHelper.StyleComboBox(combo)
    End Sub

    ''' <summary>Parses the assessed value (commas and ₱ allowed). Returns -1 when it is not a number.</summary>
    Private Function ParseValue() As Decimal
        Dim value As Decimal
        Dim cleaned = txtValue.Text.Replace("₱", "").Replace(",", "").Trim()
        If Decimal.TryParse(cleaned, Globalization.NumberStyles.Number, Globalization.CultureInfo.InvariantCulture, value) Then
            Return Math.Round(value, 2)
        End If
        Return -1D
    End Function

    Private Sub UpdatePreview()
        Dim value = ParseValue()
        If value <= 0 Then
            previewBox.BackColor = Theme.SoftBackground
            lblPreview.ForeColor = Theme.TextMuted
            lblPreview.Text = "Enter the assessed value to preview the annual tax."
        Else
            Dim tax = RptService.ComputeTax(value)
            previewBox.BackColor = Theme.StatusGreenSoft
            lblPreview.ForeColor = Theme.StatusGreen
            lblPreview.Text = "Annual tax " & UiHelper.FormatMoney(tax.TotalDue) & "  (basic " & UiHelper.FormatMoney(tax.BasicTax) &
                              " + SEF " & UiHelper.FormatMoney(tax.SefTax) & ")  ·  " &
                              UiHelper.FormatMoney(RptService.GetQuarterAmount(tax.TotalDue, 1)) & " per quarter"
        End If
        lblPreview.BackColor = previewBox.BackColor
        Tips.SetToolTip(lblPreview, lblPreview.Text)
        previewBox.Invalidate()
    End Sub

    Private Sub Save_Click(sender As Object, e As EventArgs)
        For Each errLabel In {errPin, errTd, errLocation, errType, errClass, errValue}
            errLabel.Text = ""
        Next
        Dim candidate As New RealProperty With {
            .PropertyId = prop.PropertyId, .BusinessId = prop.BusinessId, .Pin = txtPin.Text.Trim(), .TdNo = txtTd.Text.Trim(),
            .Location = txtLocation.Text.Trim(), .PropertyType = If(cboType.SelectedItem?.ToString(), ""),
            .Classification = If(cboClass.SelectedItem?.ToString(), ""), .AssessedValue = ParseValue()
        }
        Dim errors = RptService.ValidateProperty(candidate)
        If txtValue.Text.Trim() <> "" AndAlso candidate.AssessedValue < 0 Then errors("value") = "Enter a valid amount, e.g. 40000."
        If errors.Count > 0 Then
            If errors.ContainsKey("pin") Then errPin.Text = errors("pin")
            If errors.ContainsKey("td") Then errTd.Text = errors("td")
            If errors.ContainsKey("location") Then errLocation.Text = errors("location")
            If errors.ContainsKey("type") Then errType.Text = errors("type")
            If errors.ContainsKey("class") Then errClass.Text = errors("class")
            If errors.ContainsKey("value") Then errValue.Text = errors("value")
            Return
        End If

        Cursor = Cursors.WaitCursor
        Dim problem = If(isNew, RptService.AddProperty(candidate), RptService.UpdateProperty(candidate))
        Cursor = Cursors.Default
        If problem IsNot Nothing Then
            UiHelper.ShowWarning(problem)
            Return
        End If
        prop.PropertyId = candidate.PropertyId
        DialogResult = DialogResult.OK
    End Sub

End Class
