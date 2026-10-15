''' <summary>
''' One row of the rpt_payments table: one walk-in payment for one quarter of one assessment.
''' A quarter can have several payments (partial payments). AmountPaid + Penalty = the receipt total:
''' AmountPaid is the part applied to the quarter's tax (principal), Penalty the part applied to the penalty.
''' </summary>
Public Class RptPayment
    Public Property PaymentId As Integer
    Public Property AssessmentId As Integer
    Public Property Quarter As Integer              ' 1-4
    ''' <summary>Part of this payment applied to principal (the quarter's tax).</summary>
    Public Property AmountPaid As Decimal
    ''' <summary>Part of this payment applied to the late-payment penalty.</summary>
    Public Property Penalty As Decimal
    Public Property PaymentMethod As String = PaymentMethods.Cash
    ''' <summary>Check no. / GCash or bank transaction reference ("" for cash).</summary>
    Public Property ReferenceNo As String = ""
    Public Property OrNo As String = ""             ' typed from the pre-printed AF 51 booklet
    Public Property PaymentDate As Date
    Public Property ReceivedBy As Integer?
    Public Property Remarks As String = ""
    Public Property VoidedAt As Date?
    Public Property VoidedBy As Integer?
    Public Property VoidReason As String = ""
    Public Property CreatedAt As Date

    ' Display only (filled from JOINs, not saved)
    Public Property ReceivedByName As String = ""
    Public Property VoidedByName As String = ""
    Public Property TaxYear As Integer
    Public Property PropertyId As Integer
    Public Property Pin As String = ""

    ''' <summary>The receipt total (principal + penalty).</summary>
    Public ReadOnly Property Total As Decimal
        Get
            Return AmountPaid + Penalty
        End Get
    End Property

    Public ReadOnly Property IsVoided As Boolean
        Get
            Return VoidedAt.HasValue
        End Get
    End Property

    ''' <summary>"Q2 2026"</summary>
    Public ReadOnly Property QuarterLabel As String
        Get
            Return "Q" & Quarter & " " & TaxYear
        End Get
    End Property
End Class
