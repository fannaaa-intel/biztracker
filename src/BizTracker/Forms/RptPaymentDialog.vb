''' <summary>
''' Records a walk-in payment for the next unpaid RPT quarter (full or partial), with a live breakdown
''' (quarter amount, already paid, remaining principal, months late, penalty, total due now,
''' how this payment is split, and what remains after it). The shared fields (date, amount, method,
''' reference, OR, remarks) come from PaymentDialogBase. Saving is done by RptService.RecordPayment.
''' </summary>
Public Class RptPaymentDialog
    Inherits PaymentDialogBase

    Private ReadOnly row As RptLedgerRow
    Private ReadOnly quarter As RptQuarter

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

        Dim y = BuildPaymentFields(78, RptService.GetEarliestPaymentDate(row))

        ' --- Breakdown: two columns ---
        Dim breakdown = AddBreakdownPanel(y, 152)
        AddBreakdownLine(breakdown, "Quarter amount", lblQuarterAmount, 16, 8, False)
        AddBreakdownLine(breakdown, "Already paid", lblAlreadyPaid, 16, 35, False)
        AddBreakdownLine(breakdown, "Remaining principal", lblRemaining, 16, 62, False)
        AddBreakdownLine(breakdown, "Months late", lblLate, 16, 89, False)
        AddBreakdownLine(breakdown, "Penalty due", lblPenalty, 16, 116, False)
        AddBreakdownLine(breakdown, "Total due now", lblTotal, 286, 8, True)
        AddBreakdownLine(breakdown, "This payment: penalty", lblToPenalty, 286, 35, False)
        AddBreakdownLine(breakdown, "This payment: principal", lblToPrincipal, 286, 62, False)
        AddBreakdownLine(breakdown, "Still due after", lblAfter, 286, 116, True)
        Tips.SetToolTip(lblToPenalty, "A payment pays the penalty due first, then the tax (principal)")
        AddNoteBox(y + 160)

        FinishSetup()
    End Sub

    Protected Overrides Function TotalDueOn(payDate As Date) As Decimal
        Return RptService.PreviewPayment(quarter, payDate, 0D).TotalDue
    End Function

    Protected Overrides Sub UpdateBreakdown()
        Dim amount = AmountValue
        Dim p = RptService.PreviewPayment(quarter, PaymentDate, Math.Max(0D, amount))
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
        lblAfter.Text = UiHelper.FormatMoney(p.RemainingAfter)
        ShowAmountNote(p.TotalDue, p.CompletesQuarter, p.RemainingAfter, quarter.Label,
                       p.MonthsLate > 0 OrElse StatusService.IsOverdue(quarter.DueDate), p.PenaltyDue > 0)
    End Sub

    Protected Overrides Function ValidateInput(input As PaymentInput) As Dictionary(Of String, String)
        Return RptService.ValidatePayment(row, quarter, input)
    End Function

    Protected Overrides Function SavePayment(input As PaymentInput) As String
        Dim saved = RptService.RecordPayment(quarter.AssessmentId, quarter.Quarter, input)
        If saved.ErrorMessage Is Nothing Then Result = saved
        Return saved.ErrorMessage
    End Function

End Class
