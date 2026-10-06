''' <summary>One row of the audit_log table.</summary>
Public Class AuditLogEntry
    Public Property LogId As Integer
    Public Property UserId As Integer?
    Public Property Action As String = ""           ' see AuditActions constants
    Public Property TableName As String = ""
    Public Property RecordId As Integer?
    Public Property Details As String = ""
    Public Property CreatedAt As Date

    ' Display only (filled from a JOIN, not saved)
    Public Property Username As String = ""
    Public Property FullName As String = ""
End Class

''' <summary>Filters for the Audit Log viewer. Nothing / "" = no filter on that field.</summary>
Public Class AuditLogFilter
    Public Property FromDate As Date?
    Public Property ToDate As Date?
    Public Property UserId As Integer?
    Public Property TableName As String = ""
    Public Property Action As String = ""
End Class
