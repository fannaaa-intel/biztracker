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
End Class
