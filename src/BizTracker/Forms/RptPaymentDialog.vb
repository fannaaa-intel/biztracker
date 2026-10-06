''' <summary>
''' Records the payment of one RPT quarter: payment date with a live breakdown
''' (quarter amount, months late, penalty, total) and the Official Receipt number preview.
''' Saving is done by RptService.RecordPayment.
''' </summary>
Public Class RptPaymentDialog
    Inherits ModernDialog

    Private ReadOnly row As RptLedgerRow
    Private ReadOnly quarter As RptQuarter
    Private ReadOnly dtpDate As New DateTimePicker()
    Private ReadOnly errDate As Label
    Private ReadOnly lblAmount As New Label()
    Private ReadOnly lblLate As New Label()
    Private ReadOnly lblPenalty As New Label()
    Private ReadOnly lblTotal As New Label()
    Private ReadOnly noteBox As New RoundedPanel()
    Private ReadOnly lblNote As New Label()

    ''' <summary>The saved payment and endorsement info (after OK).</summary>
    Public Property Result As RptPaymentResult

    Public Sub New(ledgerRow As RptLedgerRow, payQuarter As RptQuarter)
        row = ledgerRow
        quarter = payQuarter
        ClientSize = New Size(560, 530)
        SetTitle("Record Payment", "PIN " & row.RealProperty.Pin & "  ·  " & row.RealProperty.BusinessName)

        ' --- Summary box ---
        Dim info As New RoundedPanel With {.BackColor = Theme.AccentSoft, .ShowShadow = False, .Radius = 10}
        info.SetBounds(24, 18, 512, 64)
        AddInfo(info, "Quarter", quarter.Label, 16, 10, 110)
        AddInfo(info, "Due date", UiHelper.FormatDate(quarter.DueDate), 136, 10, 130)
        AddInfo(info, "Property", row.RealProperty.PropertyType & " · TD " & row.RealProperty.TdNo, 276, 10, 224)
        Body.Controls.Add(info)

        ' --- Payment date + OR preview ---
        dtpDate.Format = DateTimePickerFormat.Custom
        dtpDate.CustomFormat = "MMM dd, yyyy"
        dtpDate.MinDate = New Date(quarter.TaxYear, 1, 1)
        dtpDate.MaxDate = If(Date.Today < dtpDate.MinDate, dtpDate.MinDate, Date.Today)
        dtpDate.Value = dtpDate.MaxDate
        dtpDate.Font = Theme.InputFont
        dtpDate.Height = 32
        errDate = AddField("Payment date", dtpDate, 24, 98, 220)
        AddInfo(Body, "Official Receipt No.", ReferenceNoService.GetNext(ReferenceNoService.OfficialReceipt), 290, 102, 246)

        ' --- Breakdown ---
        Dim breakdown As New RoundedPanel With {.BackColor = Theme.SoftBackground, .ShowShadow = False, .Radius = 10}
        breakdown.SetBounds(24, 178, 512, 150)
        AddLine(breakdown, "Quarter amount (basic + SEF)", lblAmount, 12, False)
        AddLine(breakdown, "Months late", lblLate, 44, False)
        AddLine(breakdown, "Penalty", lblPenalty, 76, False)
        Dim divider As New Panel With {.BackColor = Theme.Divider, .Height = 1}
        divider.SetBounds(16, 108, 480, 1)
        breakdown.Controls.Add(divider)
        AddLine(breakdown, "Total to collect", lblTotal, 114, True)
        Body.Controls.Add(breakdown)

        noteBox.ShowShadow = False
        noteBox.Radius = 10
        noteBox.Padding = New Padding(14, 0, 14, 0)
        noteBox.SetBounds(24, 340, 512, 44)
        lblNote.Dock = DockStyle.Fill
        lblNote.TextAlign = ContentAlignment.MiddleLeft
        lblNote.Font = Theme.SmallBoldFont
        lblNote.AutoEllipsis = True
        noteBox.Controls.Add(lblNote)
        Body.Controls.Add(noteBox)

        ' --- Buttons ---
        Dim save = AddFooterButton("Record Payment", "primary")
        Dim cancel = AddFooterButton("Cancel")
        AddHandler save.Click, AddressOf Save_Click
        AddHandler cancel.Click, Sub() DialogResult = DialogResult.Cancel
        AcceptButton = save
        CancelButton = cancel
        AddHandler dtpDate.ValueChanged, Sub()
                                             errDate.Text = ""
                                             UpdateBreakdown()
                                         End Sub
        UpdateBreakdown()
    End Sub

    ''' <summary>One caption (left) / value (right-aligned) line of the breakdown.</summary>
    Private Sub AddLine(parent As Control, caption As String, valueLabel As Label, top As Integer, bold As Boolean)
        Dim cap As New Label With {
            .Text = caption, .Font = If(bold, Theme.BodyBoldFont, Theme.BodyFont), .ForeColor = If(bold, Theme.TextDark, Theme.TextMuted),
            .AutoSize = False, .AutoEllipsis = True, .BackColor = parent.BackColor, .TextAlign = ContentAlignment.MiddleLeft
        }
        cap.SetBounds(16, top, 280, 28)
        valueLabel.Font = If(bold, Theme.SubtitleFont, Theme.BodyBoldFont)
        valueLabel.ForeColor = Theme.TextDark
        valueLabel.AutoSize = False
        valueLabel.AutoEllipsis = True
        valueLabel.BackColor = parent.BackColor
        valueLabel.TextAlign = ContentAlignment.MiddleRight
        valueLabel.SetBounds(300, top, 196, If(bold, 32, 28))
        parent.Controls.AddRange({cap, valueLabel})
    End Sub

    Private Sub UpdateBreakdown()
        Dim payDate = dtpDate.Value.Date
        Dim months = RptService.GetMonthsLate(quarter.DueDate, payDate)
        Dim rate = RptService.GetPenaltyRate(quarter.DueDate, payDate)
        Dim penalty = RptService.ComputePenalty(quarter.Amount, quarter.DueDate, payDate)
        lblAmount.Text = UiHelper.FormatMoney(quarter.Amount)
        lblLate.Text = If(months = 0, "On time", months & " month" & If(months = 1, "", "s"))
        lblPenalty.Text = UiHelper.FormatMoney(penalty) & If(months > 0, "  (" & (rate * 100D).ToString("0.##") & "%)", "")
        lblPenalty.ForeColor = If(penalty > 0, Theme.StatusRed, Theme.TextDark)
        lblTotal.Text = UiHelper.FormatMoney(quarter.Amount + penalty)

        If months = 0 Then
            noteBox.BackColor = Theme.StatusGreenSoft
            lblNote.ForeColor = Theme.StatusGreen
            lblNote.Text = "Paid on or before the due date - no penalty."
        Else
            noteBox.BackColor = Theme.StatusRedSoft
            lblNote.ForeColor = Theme.StatusRed
            lblNote.Text = "Late payment: " & (RptService.PenaltyRateMonthly * 100D).ToString("0.##") & "% per month, at most " &
                           (RptService.PenaltyMax * 100D).ToString("0.##") & "%. A part of a month counts as a month."
        End If
        lblNote.BackColor = noteBox.BackColor
        Tips.SetToolTip(lblNote, lblNote.Text)
        noteBox.Invalidate()
    End Sub

    Private Sub Save_Click(sender As Object, e As EventArgs)
        errDate.Text = ""
        Dim errors = RptService.ValidatePayment(row, dtpDate.Value.Date)
        If errors.ContainsKey("date") Then
            errDate.Text = errors("date")
            Return
        End If
        Cursor = Cursors.WaitCursor
        Dim saved = RptService.RecordPayment(quarter.AssessmentId, quarter.Quarter, dtpDate.Value.Date)
        Cursor = Cursors.Default
        If saved.ErrorMessage IsNot Nothing Then
            UiHelper.ShowWarning(saved.ErrorMessage)
            Return
        End If
        Result = saved
        DialogResult = DialogResult.OK
    End Sub

End Class
