''' <summary>Result of an audit log search: the rows shown (newest first) and how many matched in total.</summary>
Public Class AuditLogResult
    Public Property Entries As New List(Of AuditLogEntry)
    Public Property TotalCount As Integer
End Class

''' <summary>
''' Audit Log viewer (Admin only): filtered, read-only list of everything written by AuditService.
''' </summary>
Public NotInheritable Class AuditLogService

    Private Sub New()
    End Sub

    ''' <summary>The viewer shows at most this many rows (the newest); narrow the filters to see older ones.</summary>
    Public Const MaxRows As Integer = 500

    Public Shared Function CanView() As Boolean
        Return Session.IsAdmin
    End Function

    ''' <summary>Entries matching the filter (empty for non-admins).</summary>
    Public Shared Function Search(filter As AuditLogFilter) As AuditLogResult
        If Not CanView() Then Return New AuditLogResult()
        Return New AuditLogResult With {
            .Entries = AuditLogRepository.GetFiltered(filter, MaxRows),
            .TotalCount = AuditLogRepository.CountFiltered(filter)
        }
    End Function

    ''' <summary>Why the filter cannot be used (Nothing = fine).</summary>
    Public Shared Function ValidateFilter(filter As AuditLogFilter) As String
        If filter.FromDate.HasValue AndAlso filter.ToDate.HasValue AndAlso filter.FromDate.Value.Date > filter.ToDate.Value.Date Then
            Return "The ""From"" date is after the ""To"" date."
        End If
        Return Nothing
    End Function

    ''' <summary>Table names that appear in the log (for the Table filter).</summary>
    Public Shared Function GetTableNames() As List(Of String)
        If Not CanView() Then Return New List(Of String)
        Return AuditLogRepository.GetTableNames()
    End Function

    ''' <summary>Number of entries written today (dashboard card of the admin screen).</summary>
    Public Shared Function CountToday() As Integer
        If Not CanView() Then Return 0
        Return AuditLogRepository.CountFiltered(New AuditLogFilter With {.FromDate = Date.Today, .ToDate = Date.Today})
    End Function

    ''' <summary>"STATUS_CHANGE" -> "Status Change", "LOGIN" -> "Login".</summary>
    Public Shared Function ActionLabel(action As String) As String
        If String.IsNullOrEmpty(action) Then Return ""
        Return String.Join(" ", action.Split("_"c).Select(Function(w) w.Substring(0, 1) & w.Substring(1).ToLowerInvariant()))
    End Function

    ''' <summary>"admin (System Administrator)" or "System" for entries without a user (failed logins).</summary>
    Public Shared Function UserLabel(entry As AuditLogEntry) As String
        If entry.UserId Is Nothing OrElse entry.Username = "" Then Return "System"
        Return entry.Username
    End Function

End Class
