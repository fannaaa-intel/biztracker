''' <summary>
''' Database access for the audit_log table.
''' Code should write through AuditService, not call Insert directly.
''' </summary>
Public NotInheritable Class AuditLogRepository

    Private Sub New()
    End Sub

    Private Const SelectSql As String =
        "SELECT a.*, u.username, u.full_name FROM audit_log a " &
        "LEFT JOIN users u ON u.user_id = a.user_id "

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

    ''' <summary>Most recent entries first.</summary>
    Public Shared Function GetRecent(Optional limit As Integer = 200) As List(Of AuditLogEntry)
        Return GetFiltered(New AuditLogFilter(), limit)
    End Function

    ''' <summary>
    ''' Entries matching the filter, newest first, at most "limit" rows.
    ''' Every filter value is a parameter; empty filter values are ignored.
    ''' </summary>
    Public Shared Function GetFiltered(filter As AuditLogFilter, Optional limit As Integer = 500) As List(Of AuditLogEntry)
        Dim parameters As New List(Of MySql.Data.MySqlClient.MySqlParameter)
        Dim sql = SelectSql & BuildWhere(filter, parameters) &
                  "ORDER BY a.created_at DESC, a.log_id DESC LIMIT @limit"
        parameters.Add(Db.P("@limit", limit))
        Dim table = Db.GetDataTable(sql, parameters.ToArray())
        Return table.Rows.Cast(Of DataRow)().Select(AddressOf Map).ToList()
    End Function

    ''' <summary>Number of entries matching the filter (to say "showing 500 of 1,234").</summary>
    Public Shared Function CountFiltered(filter As AuditLogFilter) As Integer
        Dim parameters As New List(Of MySql.Data.MySqlClient.MySqlParameter)
        Dim sql = "SELECT COUNT(*) FROM audit_log a " & BuildWhere(filter, parameters)
        Return Convert.ToInt32(Db.ExecuteScalar(sql, parameters.ToArray()))
    End Function

    ''' <summary>Distinct table names found in the log (for the Table filter).</summary>
    Public Shared Function GetTableNames() As List(Of String)
        Dim table = Db.GetDataTable("SELECT DISTINCT table_name FROM audit_log WHERE table_name IS NOT NULL ORDER BY table_name")
        Return table.Rows.Cast(Of DataRow)().Select(Function(r) r.GetString("table_name")).ToList()
    End Function

    ''' <summary>Builds "WHERE ... " from the filter (fixed column names; values go into parameters).</summary>
    Private Shared Function BuildWhere(filter As AuditLogFilter, parameters As List(Of MySql.Data.MySqlClient.MySqlParameter)) As String
        Dim conditions As New List(Of String)
        If filter.FromDate.HasValue Then
            conditions.Add("a.created_at >= @from")
            parameters.Add(Db.P("@from", filter.FromDate.Value.Date))
        End If
        If filter.ToDate.HasValue Then
            conditions.Add("a.created_at < @to")                      ' whole "to" day included
            parameters.Add(Db.P("@to", filter.ToDate.Value.Date.AddDays(1)))
        End If
        If filter.UserId.HasValue Then
            conditions.Add("a.user_id = @uid")
            parameters.Add(Db.P("@uid", filter.UserId.Value))
        End If
        If Not String.IsNullOrEmpty(filter.TableName) Then
            conditions.Add("a.table_name = @table")
            parameters.Add(Db.P("@table", filter.TableName))
        End If
        If Not String.IsNullOrEmpty(filter.Action) Then
            conditions.Add("a.action = @action")
            parameters.Add(Db.P("@action", filter.Action))
        End If
        Return If(conditions.Count = 0, "", "WHERE " & String.Join(" AND ", conditions) & " ")
    End Function

    Private Shared Function Map(row As DataRow) As AuditLogEntry
        Return New AuditLogEntry With {
            .LogId = row.GetInt("log_id"),
            .UserId = row.GetNullableInt("user_id"),
            .Action = row.GetString("action"),
            .TableName = row.GetString("table_name"),
            .RecordId = row.GetNullableInt("record_id"),
            .Details = row.GetString("details"),
            .CreatedAt = row.GetDate("created_at"),
            .Username = row.GetString("username"),
            .FullName = row.GetString("full_name")
        }
    End Function

End Class
