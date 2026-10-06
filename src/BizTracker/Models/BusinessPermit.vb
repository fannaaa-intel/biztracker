''' <summary>One row of the business_permits table.</summary>
Public Class BusinessPermit
    Public Property PermitId As Integer
    Public Property BusinessId As Integer
    Public Property ReferenceNo As String = ""       ' BP-YYYY-00001
    Public Property ApplicationType As String = "New" ' New / Renewal
    Public Property PermitYear As Integer
    ' Submitted -> Under Review -> For Assessment -> Assessed -> Paid -> Issued (or Rejected)
    Public Property Status As String = "Submitted"
    Public Property GrossReceipts As Decimal
    Public Property AssessedAmount As Decimal
    Public Property Surcharge As Decimal
    Public Property Interest As Decimal
    Public Property MayorsPermitNo As String = ""    ' MP-YYYY-00001, set when Issued
    Public Property DateFiled As Date
    Public Property DateIssued As Date?
    Public Property ValidUntil As Date?
    Public Property Remarks As String = ""
    Public Property CreatedAt As Date

    ' Display only (filled from a JOIN, not saved)
    Public Property BusinessName As String = ""
End Class
