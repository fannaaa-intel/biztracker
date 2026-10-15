Imports Xunit

''' <summary>Business tax rules (Revision 2B): surcharge, interest, payment order, legacy permits.</summary>
Public Class BusinessTaxServiceTests

    Public Sub New()
        TestSettings.UseSeededRates()
    End Sub

    Private Shared Function Permit(type As String, tax As Decimal, Optional status As String = "Assessed",
                                   Optional legacy As Boolean = False) As BusinessPermit
        Return New BusinessPermit With {.PermitId = 1, .ApplicationType = type, .PermitYear = 2026,
                                        .AssessedAmount = tax, .Status = status, .LegacyPaid = legacy}
    End Function

    Private Shared Function Pay(id As Integer, tax As Decimal, surcharge As Decimal, interest As Decimal,
                                onDate As Date) As BusinessPermitPayment
        Return New BusinessPermitPayment With {.PaymentId = id, .PermitId = 1, .AmountPaid = tax,
                                               .Surcharge = surcharge, .Interest = interest, .PaymentDate = onDate}
    End Function

    Private Shared ReadOnly NoPayments As New List(Of BusinessPermitPayment)

    <Fact>
    Public Sub Renewal_PaidByTheDeadline_HasNoLateCharges()
        Dim b = BusinessTaxService.GetBalance(Permit("Renewal", 10000D), #1/20/2026#, NoPayments)
        Assert.Equal(0D, b.SurchargeDue)
        Assert.Equal(0D, b.InterestDue)
        Assert.Equal(10000D, b.Balance)
    End Sub

    <Fact>
    Public Sub NewApplication_IsNeverLate()
        Dim b = BusinessTaxService.GetBalance(Permit("New", 10000D), #10/15/2026#, NoPayments)
        Assert.Equal(0, b.MonthsLate)
        Assert.Equal(10000D, b.Balance)
    End Sub

    <Fact>
    Public Sub WorkedExample_FromGetBalance()
        ' Tax 10,000, nothing paid by Jan 20 -> surcharge 2,500. Mar 15 (2 months, 4%): interest 500.
        Dim p = Permit("Renewal", 10000D)
        Dim march = BusinessTaxService.GetBalance(p, #3/15/2026#, NoPayments)
        Assert.Equal(2500D, march.SurchargeDue)
        Assert.Equal(500D, march.InterestDue)
        Assert.Equal(13000D, march.Balance)

        ' 5,000 paid = 500 interest + 2,500 surcharge + 2,000 tax (interest, then surcharge, then tax).
        Dim split = BusinessTaxService.AllocatePayment(5000D, march)
        Assert.Equal(500D, split.ToInterest)
        Assert.Equal(2500D, split.ToSurcharge)
        Assert.Equal(2000D, split.ToTax)

        ' Jun 10 (5 months, 10%): 4,500 x 4% + 8,000 x 10% - 500 = 480 interest due.
        Dim paid = New List(Of BusinessPermitPayment) From {Pay(1, 2000D, 2500D, 500D, #3/15/2026#)}
        Dim june = BusinessTaxService.GetBalance(p, #6/10/2026#, paid)
        Assert.Equal(0D, june.SurchargeDue)
        Assert.Equal(480D, june.InterestDue)
        Assert.Equal(8000D, june.TaxRemaining)
        Assert.Equal(8480D, june.Balance)
        Assert.True(june.IsPartiallyPaid)
    End Sub

    <Fact>
    Public Sub Surcharge_IsOnlyOnTaxStillUnpaidOnTheDeadline()
        ' 6,000 paid on Jan 10 (on time) -> surcharge only on the 4,000 left: 1,000.
        Dim paid = New List(Of BusinessPermitPayment) From {Pay(1, 6000D, 0D, 0D, #1/10/2026#)}
        Dim b = BusinessTaxService.GetBalance(Permit("Renewal", 10000D), #2/1/2026#, paid)
        Assert.Equal(1000D, b.SurchargeDue)
    End Sub

    <Fact>
    Public Sub FullPayment_MakesItFullyPaid()
        Dim p = Permit("Renewal", 10000D)
        Dim preview = BusinessTaxService.PreviewPayment(p, NoPayments, #3/15/2026#, 99999D)
        Assert.Equal(13000D, preview.Split.Total)        ' never more than the balance
        Assert.True(preview.Completes)
        Assert.Equal(0D, preview.RemainingAfter)

        Dim paid = New List(Of BusinessPermitPayment) From {Pay(1, 10000D, 2500D, 500D, #3/15/2026#)}
        Dim after = BusinessTaxService.GetBalance(p, #12/31/2026#, paid)
        Assert.True(after.IsFullyPaid)
        Assert.Equal(0D, after.Balance)                  ' no more interest once everything is paid
    End Sub

    <Fact>
    Public Sub LegacyPaidPermit_CountsAsFullyPaid_WithNothingToCollect()
        Dim b = BusinessTaxService.GetBalance(Permit("Renewal", 10000D, "Issued", legacy:=True), #10/15/2026#, NoPayments)
        Assert.True(b.IsFullyPaid)
        Assert.Equal(0D, b.Balance)
        Assert.Equal(0D, b.TaxRemaining)
    End Sub

    <Fact>
    Public Sub NotYetAssessed_OwesNothing()
        Dim b = BusinessTaxService.GetBalance(Permit("Renewal", 0D, "Under Review"), #10/15/2026#, NoPayments)
        Assert.False(b.IsAssessed)
        Assert.Equal(0D, b.Balance)
    End Sub

    <Fact>
    Public Sub Interest_StopsGrowingAfter36Months()
        Dim p As New BusinessPermit With {.ApplicationType = "Renewal", .PermitYear = 2020}
        Assert.Equal(36, BusinessTaxService.GetMonthsLate(p, #10/15/2026#))
        Assert.Equal(0.72D, BusinessTaxService.GetInterestRate(p, #10/15/2026#))
    End Sub

End Class
