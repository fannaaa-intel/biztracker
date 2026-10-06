''' <summary>
''' One row of the health_certificates table.
''' Status (Valid / Expiring Soon / Expired) is NOT stored - use StatusService.GetExpiryStatus(ExpiryDate).
''' </summary>
Public Class HealthCertificate
    Public Property CertId As Integer
    Public Property EmployeeId As Integer
    Public Property CertificateNo As String = ""    ' HC-YYYY-00001
    Public Property IssueDate As Date
    Public Property ExpiryDate As Date              ' issue date + 1 year
    Public Property IssuedBy As String = ""         ' doctor's name (text, not a user id)
    Public Property CreatedAt As Date
End Class
