''' <summary>One row of the inspections table (annual joint inspection of a business).</summary>
Public Class Inspection
    Public Property InspectionId As Integer
    Public Property BusinessId As Integer
    Public Property ReferenceNo As String = ""      ' INS-YYYY-00001
    Public Property InspectionYear As Integer
    Public Property ScheduleDate As Date
    Public Property ReinspectionDate As Date?
    ' Scheduled / In Progress / For Re-inspection / Completed / Cancelled
    Public Property Status As String = "Scheduled"
    Public Property OverallResult As String = "Pending"   ' Pending / Passed / Failed
    Public Property CertificateNo As String = ""    ' AIC-YYYY-00001, set when all 4 departments pass
    Public Property CreatedAt As Date

    ' Display only (filled from a JOIN, not saved)
    Public Property BusinessName As String = ""
End Class
