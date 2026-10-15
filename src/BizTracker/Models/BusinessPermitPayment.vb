''' <summary>
''' One row of the business_permit_payments table: one walk-in payment of business tax for a permit application.
''' An application can have several payments (partial payments). The receipt total is split into
''' AmountPaid (the tax), Surcharge and Interest (late renewal charges).
''' </summary>
Public Class BusinessPermitPayment
    Public Property PaymentId As Integer
    Public Property PermitId As Integer
    ''' <summary>Part of this payment applied to the business tax (principal).</summary>
    Public Property AmountPaid As Decimal
    ''' <summary>Part applied to the 25% late-renewal surcharge.</summary>
    Public Property Surcharge As Decimal
    ''' <summary>Part applied to the monthly interest.</summary>
    Public Property Interest As Decimal
    Public Property PaymentMethod As String = PaymentMethods.Cash
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
    Public Property BusinessId As Integer
    Public Property PermitReferenceNo As String = ""
    Public Property PermitYear As Integer

    ''' <summary>The receipt total (tax + surcharge + interest).</summary>
    Public ReadOnly Property Total As Decimal
        Get
            Return AmountPaid + Surcharge + Interest
        End Get
    End Property

    Public ReadOnly Property IsVoided As Boolean
        Get
            Return VoidedAt.HasValue
        End Get
    End Property
End Class
