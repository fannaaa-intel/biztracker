''' <summary>
''' Database access for the notifications table.
''' Duplicates are prevented by the UNIQUE key (business_id, module, related_id, due_date) + INSERT IGNORE.
''' </summary>
Public NotInheritable Class NotificationRepository

    Private Sub New()
    End Sub

    Private Const SelectSql As String = "SELECT * FROM notifications "
    Private Const OrderSql As String = " ORDER BY is_read, FIELD(priority, 'Urgent', 'Warning', 'Info'), due_date"

    ''' <summary>All notifications (staff view).</summary>
    Public Shared Function GetAll(Optional unreadOnly As Boolean = False) As List(Of Notification)
        Return ToList(Db.GetDataTable(SelectSql & If(unreadOnly, "WHERE is_read = 0", "") & OrderSql))
    End Function

    Public Shared Function GetByBusinessId(businessId As Integer, Optional unreadOnly As Boolean = False) As List(Of Notification)
        Dim sql = SelectSql & "WHERE business_id = @bid" & If(unreadOnly, " AND is_read = 0", "") & OrderSql
        Return ToList(Db.GetDataTable(sql, Db.P("@bid", businessId)))
    End Function

    ''' <summary>Unread count for the bell icon. businessId = Nothing counts all businesses.</summary>
    Public Shared Function GetUnreadCount(businessId As Integer?) As Integer
        Dim sql = "SELECT COUNT(*) FROM notifications WHERE is_read = 0" &
                  If(businessId.HasValue, " AND business_id = @bid", "")
        Dim result = Db.ExecuteScalar(sql, Db.P("@bid", businessId))
        Return If(result Is Nothing, 0, Convert.ToInt32(result))
    End Function

    ''' <summary>
    ''' Adds an alert unless the same one (business + module + record + due date) already exists.
    ''' Returns the new id, 0 if it was a duplicate, or -1 on error.
    ''' </summary>
    Public Shared Function InsertIfNew(n As Notification) As Integer
        Const sql As String =
            "INSERT IGNORE INTO notifications (business_id, module, related_id, priority, message, due_date) " &
            "VALUES (@bid, @module, @rid, @priority, @message, @due)"
        Return Db.ExecuteInsert(sql,
            Db.P("@bid", n.BusinessId),
            Db.P("@module", n.ModuleName),
            Db.P("@rid", n.RelatedId),
            Db.P("@priority", n.Priority),
            Db.P("@message", n.Message),
            Db.P("@due", n.DueDate.Date))
    End Function

    Public Shared Function MarkRead(notificationId As Integer) As Boolean
        Return Db.ExecuteNonQuery("UPDATE notifications SET is_read = 1 WHERE notification_id = @id",
                                  Db.P("@id", notificationId)) > 0
    End Function

    ''' <summary>Marks all as read for one business (or all businesses when Nothing).</summary>
    Public Shared Function MarkAllRead(businessId As Integer?) As Boolean
        Dim sql = "UPDATE notifications SET is_read = 1 WHERE is_read = 0" &
                  If(businessId.HasValue, " AND business_id = @bid", "")
        Return Db.ExecuteNonQuery(sql, Db.P("@bid", businessId)) >= 0
    End Function

    ' ---------------- helpers ----------------

    Private Shared Function ToList(table As DataTable) As List(Of Notification)
        Return table.Rows.Cast(Of DataRow)().Select(
            Function(row) New Notification With {
                .NotificationId = row.GetInt("notification_id"),
                .BusinessId = row.GetInt("business_id"),
                .ModuleName = row.GetString("module"),
                .RelatedId = row.GetInt("related_id"),
                .Priority = row.GetString("priority"),
                .Message = row.GetString("message"),
                .DueDate = row.GetDate("due_date"),
                .IsRead = row.GetBool("is_read"),
                .CreatedAt = row.GetDate("created_at")
            }).ToList()
    End Function

End Class
