Imports Xunit

''' <summary>Real Property Tax rules (Revision 2A): tax, quarters, months late, penalty, payment split.</summary>
Public Class RptServiceTests

    Public Sub New()
        TestSettings.UseSeededRates()
    End Sub

    Private Shared Function Pay(amount As Decimal, penalty As Decimal, onDate As Date) As RptPayment
        Return New RptPayment With {.AmountPaid = amount, .Penalty = penalty, .PaymentDate = onDate}
    End Function

    <Fact>
    Public Sub ComputeTax_IsOnePercentBasicPlusOnePercentSef()
        Dim a = RptService.ComputeTax(40000D)
        Assert.Equal(400D, a.BasicTax)
        Assert.Equal(400D, a.SefTax)
        Assert.Equal(800D, a.TotalDue)
    End Sub

    <Fact>
    Public Sub QuarterAmounts_AddUpExactly_Q4TakesTheCentavos()
        Dim total = 1000.02D
        Assert.Equal(250.01D, RptService.GetQuarterAmount(total, 1))
        Assert.Equal(249.99D, RptService.GetQuarterAmount(total, 4))
        Assert.Equal(total, Enumerable.Range(1, 4).Sum(Function(q) RptService.GetQuarterAmount(total, q)))
    End Sub

    <Theory>
    <InlineData("2026-03-31", "2026-03-31", 0)>   ' on the due date = on time
    <InlineData("2026-03-31", "2026-04-01", 1)>   ' one day late = one month
    <InlineData("2026-03-31", "2026-06-15", 3)>
    <InlineData("2026-06-30", "2026-10-06", 4)>
    <InlineData("2026-03-31", "2026-08-10", 5)>
    Public Sub MonthsLate_PartOfAMonthCounts(due As String, paid As String, expected As Integer)
        Assert.Equal(expected, RptService.GetMonthsLate(Date.Parse(due), Date.Parse(paid)))
    End Sub

    <Fact>
    Public Sub Penalty_IsTwoPercentPerMonth()
        ' ₱125 due Jun 30 paid Oct 6 -> 4 months x 2% = ₱10.00
        Assert.Equal(10D, RptService.ComputePenalty(125D, #6/30/2026#, #10/6/2026#))
    End Sub

    <Fact>
    Public Sub Penalty_IsCappedAt72Percent()
        Assert.Equal(0.72D, RptService.GetPenaltyRate(#3/31/2020#, #10/15/2026#))
        Assert.Equal(72D, RptService.ComputePenalty(100D, #3/31/2020#, #10/15/2026#))
    End Sub

    <Fact>
    Public Sub PenaltyDue_ChargesEachPesoForTheMonthsItStayedUnpaid()
        ' The example in RptService.ComputePenaltyDue: ₱100 due Mar 31.
        ' Jun 15 (6%): ₱50 paid -> ₱6 penalty + ₱44 principal.
        Dim due = #3/31/2026#
        Dim first = RptService.AllocatePayment(50D, RptService.ComputePenaltyDue(100D, due, {}, #6/15/2026#), 100D)
        Assert.Equal(6D, first.ToPenalty)
        Assert.Equal(44D, first.ToPrincipal)

        ' Aug 10 (10%): 44 x 6% + 56 x 10% - 6 = ₱2.24 still due.
        Dim payments = {Pay(44D, 6D, #6/15/2026#)}
        Assert.Equal(2.24D, RptService.ComputePenaltyDue(100D, due, payments, #8/10/2026#))
    End Sub

    <Fact>
    Public Sub PenaltyDue_IsZeroOnceThePrincipalIsFullyPaid()
        Dim payments = {Pay(100D, 0D, #4/15/2026#)}
        Assert.Equal(0D, RptService.ComputePenaltyDue(100D, #3/31/2026#, payments, #10/15/2026#))
    End Sub

    <Fact>
    Public Sub PenaltyDue_IsZeroWhenPaidOnTime()
        Assert.Equal(0D, RptService.ComputePenaltyDue(100D, #3/31/2026#, {}, #3/31/2026#))
    End Sub

    <Fact>
    Public Sub AllocatePayment_PaysPenaltyFirst_AndNeverMoreThanOwed()
        Dim split = RptService.AllocatePayment(1000D, 10D, 125D)
        Assert.Equal(10D, split.ToPenalty)
        Assert.Equal(125D, split.ToPrincipal)

        Dim small = RptService.AllocatePayment(4D, 10D, 125D)
        Assert.Equal(4D, small.ToPenalty)
        Assert.Equal(0D, small.ToPrincipal)

        Dim negative = RptService.AllocatePayment(-5D, 10D, 125D)
        Assert.Equal(0D, negative.ToPenalty + negative.ToPrincipal)
    End Sub

    <Fact>
    Public Sub Preview_PartialThenFullPayment_CompletesTheQuarter()
        Dim q As New RptQuarter With {.Amount = 125D, .DueDate = #6/30/2026#, .TaxYear = 2026, .Quarter = 2}
        Dim first = RptService.PreviewPayment(q, #10/6/2026#, 50D)        ' 4 months late -> ₱10 penalty
        Assert.Equal(10D, first.PenaltyDue)
        Assert.Equal(135D, first.TotalDue)
        Assert.Equal(40D, first.Split.ToPrincipal)
        Assert.False(first.CompletesQuarter)
        Assert.Equal(85D, first.RemainingAfter)

        q.Payments.Add(Pay(40D, 10D, #10/6/2026#))
        Assert.True(q.IsPartiallyPaid)
        Dim rest = RptService.PreviewPayment(q, #10/6/2026#, 500D)        ' more than owed: only ₱85 is applied
        Assert.Equal(85D, rest.Split.ToPrincipal)
        Assert.Equal(0D, rest.Split.ToPenalty)
        Assert.True(rest.CompletesQuarter)
        Assert.Equal(0D, rest.RemainingAfter)
    End Sub

End Class
