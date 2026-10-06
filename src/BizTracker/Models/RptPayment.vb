''' <summary>One row of the rpt_payments table (one quarter of one assessment).</summary>
Public Class RptPayment
    Public Property PaymentId As Integer
    Public Property AssessmentId As Integer
    Public Property Quarter As Integer              ' 1-4
    Public Property AmountPaid As Decimal
    Public Property Penalty As Decimal
    Public Property OrNo As String = ""             ' Official Receipt No. OR-YYYY-00001
    Public Property PaymentDate As Date
    Public Property ReceivedBy As Integer?
    Public Property CreatedAt As Date
End Class
