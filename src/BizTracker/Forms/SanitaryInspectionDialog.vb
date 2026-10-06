''' <summary>
''' Records the on-site sanitary inspection: date, score (0-100), inspector and findings,
''' with a live Passed / Failed preview against the passing score. Saving is done by SanitaryPermitService.
''' </summary>
Public Class SanitaryInspectionDialog
    Inherits ModernDialog

    Private ReadOnly permit As SanitaryPermit
    Private ReadOnly dtpDate As New DateTimePicker()
    Private ReadOnly numScore As New NumericUpDown()
    Private ReadOnly txtInspector As New TextBox()
    Private ReadOnly txtFindings As New TextBox()
    Private ReadOnly errDate As Label
    Private ReadOnly errScore As Label
    Private ReadOnly errInspector As Label
    Private ReadOnly errFindings As Label
    Private ReadOnly resultBox As New RoundedPanel()
    Private ReadOnly lblResult As New Label()

    Public Sub New(p As SanitaryPermit)
        permit = p
        ClientSize = New Size(560, 520)
        SetTitle(If(p.InspectionScore.HasValue, "Re-inspection", "Record Inspection"), p.PermitNo & "  ·  " & p.BusinessName)

        dtpDate.Format = DateTimePickerFormat.Custom
        dtpDate.CustomFormat = "MMM dd, yyyy"
        dtpDate.MinDate = p.DateFiled.Date
        dtpDate.MaxDate = Date.Today
        dtpDate.Value = Date.Today
        dtpDate.Font = Theme.InputFont
        dtpDate.Height = 32
        errDate = AddField("Inspection date", dtpDate, 24, 18, 160)

        numScore.Minimum = 0
        numScore.Maximum = 100
        numScore.Value = If(p.InspectionScore, 0)
        numScore.Font = Theme.InputFont
        numScore.Height = 32
        errScore = AddField("Score (0-100)", numScore, 204, 18, 110)

        txtInspector.Font = Theme.InputFont
        txtInspector.BorderStyle = BorderStyle.FixedSingle
        txtInspector.MaxLength = 150
        txtInspector.PlaceholderText = "e.g. Sanitary Insp. R. Ramos"
        txtInspector.Text = If(p.InspectorName <> "", p.InspectorName, "")
        errInspector = AddField("Sanitary inspector", txtInspector, 334, 18, 202)

        ' Live pass / fail preview
        resultBox.ShowShadow = False
        resultBox.Radius = 10
        resultBox.Padding = New Padding(14, 0, 14, 0)
        resultBox.SetBounds(24, 96, 512, 48)
        lblResult.Dock = DockStyle.Fill
        lblResult.TextAlign = ContentAlignment.MiddleLeft
        lblResult.Font = Theme.SmallBoldFont
        lblResult.AutoEllipsis = True
        resultBox.Controls.Add(lblResult)
        Body.Controls.Add(resultBox)

        txtFindings.Multiline = True
        txtFindings.Font = Theme.InputFont
        txtFindings.BorderStyle = BorderStyle.FixedSingle
        txtFindings.Height = 110
        txtFindings.MaxLength = 1000
        txtFindings.PlaceholderText = "Water supply, waste disposal, pest control, food storage, premises cleanliness..."
        txtFindings.Text = If(p.Findings, "")
        errFindings = AddField("Findings / deficiencies", txtFindings, 24, 158, 512)

        Dim save = AddFooterButton("Save Inspection", "primary")
        Dim cancel = AddFooterButton("Cancel")
        AddHandler save.Click, AddressOf Save_Click
        AddHandler cancel.Click, Sub() DialogResult = DialogResult.Cancel
        CancelButton = cancel

        AddHandler numScore.ValueChanged, Sub()
                                              errScore.Text = ""
                                              UpdateResult()
                                          End Sub
        AddHandler dtpDate.ValueChanged, Sub() errDate.Text = ""
        AddHandler txtInspector.TextChanged, Sub() errInspector.Text = ""
        AddHandler txtFindings.TextChanged, Sub() errFindings.Text = ""
        UpdateResult()
    End Sub

    Private Sub UpdateResult()
        Dim passing = SanitaryPermitService.PassingScore
        Dim passed = numScore.Value >= passing
        resultBox.BackColor = If(passed, Theme.StatusGreenSoft, Theme.StatusRedSoft)
        lblResult.BackColor = resultBox.BackColor
        lblResult.ForeColor = If(passed, Theme.StatusGreen, Theme.StatusRed)
        lblResult.Text = If(passed, "PASSED - score " & numScore.Value & " meets the passing score of " & passing & ".",
                            "FAILED - score " & numScore.Value & " is below the passing score of " & passing & ". Findings are required.")
        resultBox.Invalidate()
    End Sub

    Private Sub Save_Click(sender As Object, e As EventArgs)
        errDate.Text = "" : errScore.Text = "" : errInspector.Text = "" : errFindings.Text = ""
        Dim score = CInt(numScore.Value)
        Dim errors = SanitaryPermitService.ValidateInspection(permit, dtpDate.Value.Date, score, txtInspector.Text, txtFindings.Text)
        If errors.Count > 0 Then
            If errors.ContainsKey("date") Then errDate.Text = errors("date")
            If errors.ContainsKey("score") Then errScore.Text = errors("score")
            If errors.ContainsKey("inspector") Then errInspector.Text = errors("inspector")
            If errors.ContainsKey("findings") Then errFindings.Text = errors("findings")
            Return
        End If
        Dim problem = SanitaryPermitService.RecordInspection(permit, dtpDate.Value.Date, score, txtInspector.Text, txtFindings.Text)
        If problem IsNot Nothing Then
            UiHelper.ShowWarning(problem)
            Return
        End If
        DialogResult = DialogResult.OK
    End Sub

End Class
