''' <summary>One row of the sanitary_permits table.</summary>
Public Class SanitaryPermit
    Public Property SanitaryId As Integer
    Public Property BusinessId As Integer
    Public Property PermitNo As String = ""         ' SP-YYYY-00001
    Public Property PermitYear As Integer
    Public Property Category As String = "Food"     ' Food / Non-Food
    Public Property Status As String = "Submitted"  ' Submitted / Lab Analysis / For Inspection / Issued / Rejected
    Public Property InspectionDate As Date?
    Public Property InspectionScore As Integer?     ' 0-100
    Public Property InspectorName As String = ""
    Public Property Findings As String = ""
    Public Property DateFiled As Date
    Public Property DateIssued As Date?
    Public Property ValidUntil As Date?             ' Dec 31 of the year issued
    Public Property CreatedAt As Date

    ' Display only (filled from a JOIN, not saved)
    Public Property BusinessName As String = ""
End Class
