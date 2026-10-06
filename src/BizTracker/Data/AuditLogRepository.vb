''' <summary>
''' Database access for the audit_log table.
''' Code should write through AuditService, not call Insert directly.
''' </summary>
Public NotInheritable Class AuditLogRepository

    Private Sub New()
    End Sub

    Public Shared Function Insert(userId As Integer?, action As String, tableName As String,
                                  recordId As Integer?, details As String) As Integer
        Const sql As String =
            "INSERT INTO audit_log (user_id, action, table_name, record_id, details) " &
            "VALUES (@uid, @action, @table, @rid, @details)"
        Return Db.ExecuteInsert(sql,
            Db.P("@uid", userId),
            Db.P("@action", action),
            Db.P("@table", Db.NullIfEmpty(tableName)),
            Db.P("@rid", recordId),
            Db.P("@details", Db.NullIfEmpty(details)))
    End Function

    ''' <summary>Most recent entries first (filters are added in Phase 14).</summary>
    Public Shared Function GetRecent(Optional limit As Integer = 200) As List(Of AuditLogEntry)
        Const sql As String =
            "SELECT a.*, u.username FROM audit_log a " &
            "LEFT JOIN users u ON u.user_id = a.user_id " &
            "ORDER BY a.created_at DESC, a.log_id DESC LIMIT @limit"
        Dim table = Db.GetDataTable(sql, Db.P("@limit", limit))
        Return table.Rows.Cast(Of DataRow)().Select(
            Function(row) New AuditLogEntry With {
                .LogId = row.GetInt("log_id"),
                .UserId = row.GetNullableInt("user_id"),
                .Action = row.GetString("action"),
                .TableName = row.GetString("table_name"),
                .RecordId = row.GetNullableInt("record_id"),
                .Details = row.GetString("details"),
                .CreatedAt = row.GetDate("created_at"),
                .Username = row.GetString("username")
            }).ToList()
    End Function

End Class
