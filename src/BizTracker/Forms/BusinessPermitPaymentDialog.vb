''' <summary>
''' Records a walk-in business tax payment for an Assessed application (full or partial), with a live breakdown
''' (assessed tax, surcharge, interest, already paid, total due now, how this payment is split into
''' interest / surcharge / tax, and what remains). The shared fields come from PaymentDialogBase.
''' Saving is done by BusinessTaxService.RecordPayment (the application becomes Paid when fully paid).
''' </summary>
Public Class BusinessPermitPaymentDialog
    Inherits PaymentDialogBase

    Private ReadOnly permit As BusinessPermit
    Private ReadOnly payments As List(Of BusinessPermitPayment)

    ' Breakdown values
    Private ReadOnly lblTax As New Label()
    Private ReadOnly lblSurcharge As New Label()
    Private ReadOnly lblLate As New Label()
    Private ReadOnly lblInterest As New Label()
    Private ReadOnly lblAlreadyPaid As New Label()
    Private ReadOnly lblTotal As New Label()
    Private ReadOnly lblToInterest As New Label()
    Private ReadOnly lblToSurcharge As New Label()
    Private ReadOnly lblToTax As New Label()
    Private ReadOnly lblAfter As New Label()

    ''' <summary>The saved payment (after OK).</summary>
    Public Property Result As BusinessTaxPaymentResult

    Public Sub New(p As BusinessPermit)
        permit = p
        payments = BusinessTaxService.GetActivePayments(p.PermitId)
        ClientSize = New Size(600, 76 + 508 + 64)
        SetTitle("Record Payment", "Business tax  ·  " & p.ReferenceNo & "  ·  " & p.BusinessName)

        ' --- Summary box ---
        Dim info As New RoundedPanel With {.BackColor = Theme.AccentSoft, .ShowShadow = False, .Radius = 10}
        info.SetBounds(24, 14, 552, 52)
        AddInfo(info, "Application", p.ApplicationType & " " & p.PermitYear, 16, 6, 130)
        AddInfo(info, "Renewal deadline", If(p.ApplicationType = "Renewal",
                                             UiHelper.FormatDate(BusinessPermitService.GetRenewalDeadline(p.PermitYear)), "None (new)"), 156, 6, 150)
        AddInfo(info, "Gross receipts", UiHelper.FormatMoney(p.GrossReceipts), 316, 6, 222)
        Body.Controls.Add(info)

        Dim y = BuildPaymentFields(78, BusinessTaxService.GetEarliestPaymentDate(p, payments))

        ' --- Breakdown: two columns ---
        Dim breakdown = AddBreakdownPanel(y, 152)
        AddBreakdownLine(breakdown, "Assessed tax", lblTax, 16, 8, False)
        AddBreakdownLine(breakdown, "Surcharge (25%)", lblSurcharge, 16, 35, False)
        AddBreakdownLine(breakdown, "Months late", lblLate, 16, 62, False)
        AddBreakdownLine(breakdown, "Interest due", lblInterest, 16, 89, False)
        AddBreakdownLine(breakdown, "Already paid", lblAlreadyPaid, 16, 116, False)
        AddBreakdownLine(breakdown, "Total due now", lblTotal, 286, 8, True)
        AddBreakdownLine(breakdown, "Paying interest", lblToInterest, 286, 35, False)
        AddBreakdownLine(breakdown, "Paying surcharge", lblToSurcharge, 286, 62, False)
        AddBreakdownLine(breakdown, "Paying tax", lblToTax, 286, 89, False)
        AddBreakdownLine(breakdown, "Still due after", lblAfter, 286, 116, True)
        Tips.SetToolTip(lblToInterest, "A payment pays the interest first, then the surcharge, then the tax")
        AddNoteBox(y + 160)

        FinishSetup()
    End Sub

    Protected Overrides Function TotalDueOn(payDate As Date) As Decimal
        Return BusinessTaxService.GetBalance(permit, payDate, payments).Balance
    End Function

    Protected Overrides Sub UpdateBreakdown()
        Dim amount = AmountValue
        Dim p = BusinessTaxService.PreviewPayment(permit, payments, PaymentDate, Math.Max(0D, amount))
        Dim b = p.Balance
        lblTax.Text = UiHelper.FormatMoney(b.Tax)
        lblSurcharge.Text = UiHelper.FormatMoney(b.SurchargeCharged)
        lblLate.Text = If(b.MonthsLate = 0, "On time", b.MonthsLate & " mo." &
                                                       " (" & (b.InterestRate * 100D).ToString("0.##") & "%)")
        lblInterest.Text = UiHelper.FormatMoney(b.InterestDue)
        lblInterest.ForeColor = If(b.InterestDue > 0, Theme.StatusRed, Theme.TextDark)
        lblAlreadyPaid.Text = UiHelper.FormatMoney(b.TotalPaid)
        lblTotal.Text = UiHelper.FormatMoney(b.Balance)
        lblToInterest.Text = UiHelper.FormatMoney(p.Split.ToInterest)
        lblToSurcharge.Text = UiHelper.FormatMoney(p.Split.ToSurcharge)
        lblToTax.Text = UiHelper.FormatMoney(p.Split.ToTax)
        lblAfter.Text = UiHelper.FormatMoney(p.RemainingAfter)
        ShowAmountNote(b.Balance, p.Completes, p.RemainingAfter, permit.ReferenceNo,
                       b.MonthsLate > 0, b.SurchargeDue + b.InterestDue > 0)
    End Sub

    Protected Overrides Function ValidateInput(input As PaymentInput) As Dictionary(Of String, String)
        Return BusinessTaxService.ValidatePayment(permit, payments, input)
    End Function

    Protected Overrides Function SavePayment(input As PaymentInput) As String
        Dim saved = BusinessTaxService.RecordPayment(permit.PermitId, input)
        If saved.ErrorMessage Is Nothing Then Result = saved
        Return saved.ErrorMessage
    End Function

End Class
