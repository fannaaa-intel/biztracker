Imports System.Globalization

''' <summary>
''' Records a walk-in payment for the next unpaid RPT quarter (full or partial):
''' payment date, amount (defaults to the full balance), method, reference no. (not for cash),
''' the OR number typed from the AF 51 booklet, and remarks - with a live breakdown
''' (quarter amount, already paid, remaining principal, months late, penalty, total due now,
''' how this payment is split, and what remains after it).
''' Saving is done by RptService.RecordPayment.
''' </summary>
Public Class RptPaymentDialog
    Inherits ModernDialog

    Private ReadOnly row As RptLedgerRow
    Private ReadOnly quarter As RptQuarter

    Private ReadOnly dtpDate As New DateTimePicker()
    Private ReadOnly txtAmount As New TextBox()
    Private ReadOnly cboMethod As New ComboBox()
    Private ReadOnly txtOr As New TextBox()
    Private ReadOnly txtReference As New TextBox()
    Private ReadOnly txtRemarks As New TextBox()
    Private ReadOnly errDate As Label
    Private ReadOnly errAmount As Label
    Private ReadOnly errMethod As Label
    Private ReadOnly errOrNo As Label
    Private ReadOnly errReference As Label
    Private ReadOnly errRemarks As Label
    Private ReadOnly referenceControls As New List(Of Control)
    Private ReadOnly lblReferenceCaption As Label

    ' Breakdown values
    Private ReadOnly lblQuarterAmount As New Label()
    Private ReadOnly lblAlreadyPaid As New Label()
    Private ReadOnly lblRemaining As New Label()
    Private ReadOnly lblLate As New Label()
    Private ReadOnly lblPenalty As New Label()
    Private ReadOnly lblTotal As New Label()
    Private ReadOnly lblToPenalty As New Label()
    Private ReadOnly lblToPrincipal As New Label()
    Private ReadOnly lblAfter As New Label()
    Private ReadOnly noteBox As New RoundedPanel()
    Private ReadOnly lblNote As New Label()

    ''' <summary>True once the cashier typed an amount (then a date change no longer replaces it).</summary>
    Private amountTouched As Boolean
    Private settingAmount As Boolean

    ''' <summary>The saved payment and endorsement info (after OK).</summary>
    Public Property Result As RptPaymentResult

    Public Sub New(ledgerRow As RptLedgerRow, payQuarter As RptQuarter)
        row = ledgerRow
        quarter = payQuarter
        ClientSize = New Size(600, 76 + 508 + 64)
        SetTitle("Record Payment", "PIN " & row.RealProperty.Pin & "  ·  " & row.RealProperty.BusinessName)

        ' --- Summary box ---
        Dim info As New RoundedPanel With {.BackColor = Theme.AccentSoft, .ShowShadow = False, .Radius = 10}
        info.SetBounds(24, 14, 552, 52)
        AddInfo(info, "Quarter", quarter.Label, 16, 6, 110)
        AddInfo(info, "Due date", UiHelper.FormatDate(quarter.DueDate), 136, 6, 130)
        AddInfo(info, "Property", row.RealProperty.PropertyType & " · TD " & row.RealProperty.TdNo, 276, 6, 262)
        Body.Controls.Add(info)

        ' --- Row 1: date, amount, method ---
        dtpDate.Format = DateTimePickerFormat.Custom
        dtpDate.CustomFormat = "MMM dd, yyyy"
        dtpDate.MinDate = RptService.GetEarliestPaymentDate(row)
        dtpDate.MaxDate = If(Date.Today < dtpDate.MinDate, dtpDate.MinDate, Date.Today)
        dtpDate.Value = dtpDate.MaxDate
        dtpDate.Font = Theme.InputFont
        dtpDate.Height = 32
        errDate = AddField("Payment date", dtpDate, 24, 78, 190)

        StyleText(txtAmount, "", "0.00", 15)
        txtAmount.TextAlign = HorizontalAlignment.Right
        errAmount = AddField("Amount received (₱)", txtAmount, 234, 78, 150)

        cboMethod.DropDownStyle = ComboBoxStyle.DropDownList
        cboMethod.FlatStyle = FlatStyle.Flat
        cboMethod.Font = Theme.InputFont
        UiHelper.StyleComboBox(cboMethod)
        cboMethod.Items.AddRange(PaymentMethods.All)
        cboMethod.SelectedItem = PaymentMethods.Cash
        errMethod = AddField("Payment method", UiHelper.CreateComboBox(cboMethod, 29), 404, 78, 172)

        ' --- Row 2: OR number + reference ---
        Dim lastOr = RptRepository.GetLastOrNo()
        StyleText(txtOr, "", If(lastOr = "", "e.g. 7001033", "Last used: " & lastOr), RptService.MaxOrNoLength)
        errOrNo = AddField("OR No. (AF 51)", txtOr, 24, 152, 190)
        Tips.SetToolTip(txtOr, "Type the number printed on the Official Receipt (AF 51) you hand to the payer")

        Dim refStart = Body.Controls.Count
        StyleText(txtReference, "", "", RptService.MaxReferenceLength)
        errReference = AddField("Reference no.", txtReference, 234, 152, 342)
        For i = refStart To Body.Controls.Count - 1
            referenceControls.Add(Body.Controls(i))
        Next
        lblReferenceCaption = DirectCast(referenceControls(0), Label)

        ' --- Row 3: remarks ---
        StyleText(txtRemarks, "", "Optional, e.g. partial payment - will pay the rest next week", RptService.MaxRemarksLength)
        errRemarks = AddField("Remarks", txtRemarks, 24, 226, 552)

        ' --- Breakdown: two columns ---
        Dim breakdown As New RoundedPanel With {.BackColor = Theme.SoftBackground, .ShowShadow = False, .Radius = 10}
        breakdown.SetBounds(24, 302, 552, 152)
        AddLine(breakdown, "Quarter amount", lblQuarterAmount, 16, 8, False)
        AddLine(breakdown, "Already paid", lblAlreadyPaid, 16, 35, False)
        AddLine(breakdown, "Remaining principal", lblRemaining, 16, 62, False)
        AddLine(breakdown, "Months late", lblLate, 16, 89, False)
        AddLine(breakdown, "Penalty due", lblPenalty, 16, 116, False)
        AddLine(breakdown, "Total due now", lblTotal, 286, 8, True)
        AddLine(breakdown, "This payment: penalty", lblToPenalty, 286, 35, False)
        AddLine(breakdown, "This payment: principal", lblToPrincipal, 286, 62, False)
        AddLine(breakdown, "Still due after", lblAfter, 286, 116, True)
        Dim divider As New Panel With {.BackColor = Theme.Divider}
        divider.SetBounds(276, 12, 1, 128)
        breakdown.Controls.Add(divider)
        Body.Controls.Add(breakdown)

        noteBox.ShowShadow = False
        noteBox.Radius = 10
        noteBox.Padding = New Padding(14, 0, 14, 0)
        noteBox.SetBounds(24, 462, 552, 38)
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
                                             If Not amountTouched Then SetAmount(Preview(0D).TotalDue)
                                             UpdateBreakdown()
                                         End Sub
        AddHandler txtAmount.TextChanged, Sub()
                                              errAmount.Text = ""
                                              If Not settingAmount Then amountTouched = True
                                              UpdateBreakdown()
                                          End Sub
        AddHandler txtAmount.KeyPress, Sub(s, e) e.Handled = Not (Char.IsDigit(e.KeyChar) OrElse Char.IsControl(e.KeyChar) OrElse
                                                                  e.KeyChar = "."c OrElse e.KeyChar = ","c)
        AddHandler cboMethod.SelectedIndexChanged, Sub()
                                                       errMethod.Text = ""
                                                       ShowReferenceField()
                                                   End Sub
        AddHandler txtOr.TextChanged, Sub() errOrNo.Text = ""
        AddHandler txtReference.TextChanged, Sub() errReference.Text = ""
        AddHandler txtRemarks.TextChanged, Sub() errRemarks.Text = ""

        SetAmount(Preview(0D).TotalDue)       ' default: the full remaining balance
        ShowReferenceField()
        UpdateBreakdown()
    End Sub

    ' ==================== Helpers ====================

    Private Shared Sub StyleText(box As TextBox, value As String, placeholder As String, maxLength As Integer)
        box.Font = Theme.InputFont
        box.BorderStyle = BorderStyle.FixedSingle
        box.MaxLength = maxLength
        box.Text = value
        box.PlaceholderText = placeholder
    End Sub

    ''' <summary>One caption (left) / value (right-aligned) line of the breakdown, 252 wide at "left".</summary>
    Private Sub AddLine(parent As Control, caption As String, valueLabel As Label, left As Integer, top As Integer, bold As Boolean)
        Dim cap As New Label With {
            .Text = caption, .Font = If(bold, Theme.BodyBoldFont, Theme.BodyFont), .ForeColor = If(bold, Theme.TextDark, Theme.TextMuted),
            .AutoSize = False, .AutoEllipsis = True, .BackColor = parent.BackColor, .TextAlign = ContentAlignment.MiddleLeft
        }
        cap.SetBounds(left, top, 152, 26)
        valueLabel.Font = If(bold, Theme.BodyBoldFont, Theme.BodyFont)
        valueLabel.ForeColor = Theme.TextDark
        valueLabel.AutoSize = False
        valueLabel.AutoEllipsis = True
        valueLabel.BackColor = parent.BackColor
        valueLabel.TextAlign = ContentAlignment.MiddleRight
        valueLabel.SetBounds(left + 152, top, 100, 26)
        parent.Controls.AddRange({cap, valueLabel})
    End Sub

    Private Sub SetAmount(value As Decimal)
        settingAmount = True
        txtAmount.Text = value.ToString("0.00", CultureInfo.InvariantCulture)
        settingAmount = False
    End Sub

    ''' <summary>The typed amount (commas allowed as thousands separators); -1 when it is not a number.</summary>
    Private ReadOnly Property AmountValue As Decimal
        Get
            Dim value As Decimal
            If Decimal.TryParse(txtAmount.Text.Replace(",", "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, value) Then Return value
            Return -1D
        End Get
    End Property

    Private ReadOnly Property SelectedMethod As String
        Get
            Return If(cboMethod.SelectedItem?.ToString(), "")
        End Get
    End Property

    Private Function Preview(amount As Decimal) As PaymentPreview
        Return RptService.PreviewPayment(quarter, dtpDate.Value.Date, Math.Max(0D, amount))
    End Function

    ''' <summary>Reference no. only for Check / GCash / Bank Transfer, with a caption that says what to type.</summary>
    Private Sub ShowReferenceField()
        Dim needs = PaymentMethods.NeedsReference(SelectedMethod)
        For Each c In referenceControls
            c.Visible = needs
        Next
        Select Case SelectedMethod
            Case PaymentMethods.Check
                lblReferenceCaption.Text = "Check no. (and bank)"
                txtReference.PlaceholderText = "e.g. 0004512 - Landbank"
            Case PaymentMethods.GCash
                lblReferenceCaption.Text = "GCash reference no."
                txtReference.PlaceholderText = "13-digit reference from the GCash receipt"
            Case PaymentMethods.BankTransfer
                lblReferenceCaption.Text = "Bank transaction reference"
                txtReference.PlaceholderText = "Reference from the deposit slip or transfer"
        End Select
    End Sub

    Private Sub UpdateBreakdown()
        Dim amount = AmountValue
        Dim p = Preview(If(amount < 0D, 0D, amount))
        lblQuarterAmount.Text = UiHelper.FormatMoney(p.QuarterAmount)
        lblAlreadyPaid.Text = UiHelper.FormatMoney(p.AlreadyPaid)
        lblRemaining.Text = UiHelper.FormatMoney(p.RemainingPrincipal)
        lblLate.Text = If(p.MonthsLate = 0, "On time", p.MonthsLate & " month" & If(p.MonthsLate = 1, "", "s") &
                                                       " (" & (p.PenaltyRate * 100D).ToString("0.##") & "%)")
        lblPenalty.Text = UiHelper.FormatMoney(p.PenaltyDue)
        lblPenalty.ForeColor = If(p.PenaltyDue > 0, Theme.StatusRed, Theme.TextDark)
        lblTotal.Text = UiHelper.FormatMoney(p.TotalDue)
        lblToPenalty.Text = UiHelper.FormatMoney(p.Split.ToPenalty)
        lblToPrincipal.Text = UiHelper.FormatMoney(p.Split.ToPrincipal)
        Tips.SetToolTip(lblToPenalty, "A payment pays the penalty due first, then the tax (principal)")
        lblAfter.Text = UiHelper.FormatMoney(p.RemainingAfter)

        Dim color As Color, soft As Color, text As String
        If amount <= 0D Then
            color = Theme.StatusAmber : soft = Theme.StatusAmberSoft
            text = "Enter the amount received."
        ElseIf amount > p.TotalDue Then
            color = Theme.StatusRed : soft = Theme.StatusRedSoft
            text = "More than the balance due (" & UiHelper.FormatMoney(p.TotalDue) & ") - give change instead."
        ElseIf p.CompletesQuarter Then
            color = Theme.StatusGreen : soft = Theme.StatusGreenSoft
            text = "This pays " & quarter.Label & " in full" & If(p.PenaltyDue > 0, " (penalty included).", ".")
        Else
            color = Theme.StatusAmber : soft = Theme.StatusAmberSoft
            text = "Partial payment: " & UiHelper.FormatMoney(p.RemainingAfter) & " of " & quarter.Label & " will still be due" &
                   If(p.MonthsLate > 0 OrElse StatusService.IsOverdue(quarter.DueDate), " (penalty keeps running).", ".")
        End If
        noteBox.BackColor = soft
        lblNote.BackColor = soft
        lblNote.ForeColor = color
        lblNote.Text = text
        Tips.SetToolTip(lblNote, text)
        noteBox.Invalidate()
    End Sub

    Private Sub Save_Click(sender As Object, e As EventArgs)
        For Each lbl In {errDate, errAmount, errMethod, errOrNo, errReference, errRemarks}
            lbl.Text = ""
        Next
        Dim input As New RptPaymentInput With {
            .PaymentDate = dtpDate.Value.Date, .Amount = AmountValue, .Method = SelectedMethod,
            .ReferenceNo = txtReference.Text, .OrNo = txtOr.Text, .Remarks = txtRemarks.Text
        }
        Dim errors = RptService.ValidatePayment(row, quarter, input)
        If AmountValue < 0D Then errors("amount") = "Numbers only, e.g. 125.00"
        If errors.Count > 0 Then
            Dim map As New Dictionary(Of String, Label) From {
                {"date", errDate}, {"amount", errAmount}, {"method", errMethod}, {"or", errOrNo},
                {"reference", errReference}, {"remarks", errRemarks}}
            For Each kv In errors
                Dim lbl As Label = Nothing
                If map.TryGetValue(kv.Key, lbl) Then
                    lbl.Text = kv.Value
                    Tips.SetToolTip(lbl, kv.Value)
                End If
            Next
            Return
        End If

        Cursor = Cursors.WaitCursor
        Dim saved = RptService.RecordPayment(quarter.AssessmentId, quarter.Quarter, input)
        Cursor = Cursors.Default
        If saved.ErrorMessage IsNot Nothing Then
            UiHelper.ShowWarning(saved.ErrorMessage)
            Return
        End If
        Result = saved
        DialogResult = DialogResult.OK
    End Sub

End Class
