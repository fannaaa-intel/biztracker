Imports System.Configuration
Imports MySql.Data.MySqlClient

''' <summary>
''' Shared database helpers. Every repository goes through this class.
''' Rules: parameterized queries only, Using blocks for every connection/command/reader,
''' and DB errors are shown as a friendly MessageBox instead of crashing the app.
''' </summary>
Public NotInheritable Class Db

    Private Sub New()
        ' Static helper class - no instances.
    End Sub

    ''' <summary>Connection string read from App.config ("BizTrackerDb").</summary>
    Public Shared ReadOnly Property ConnectionString As String
        Get
            Dim setting = ConfigurationManager.ConnectionStrings("BizTrackerDb")
            If setting Is Nothing Then
                Throw New ConfigurationErrorsException("Connection string 'BizTrackerDb' is missing from App.config.")
            End If
            Return setting.ConnectionString
        End Get
    End Property

    ''' <summary>
    ''' Returns a NEW, unopened connection to biztracker_db.
    ''' The caller must open it and wrap it in a Using block.
    ''' </summary>
    Public Shared Function GetConnection() As MySqlConnection
        Return New MySqlConnection(ConnectionString)
    End Function

    ' ------------------------------------------------------------------
    ' ExecuteNonQuery - INSERT / UPDATE / DELETE.
    ' Returns the number of affected rows, or -1 if a DB error occurred.
    ' ------------------------------------------------------------------
    Public Shared Function ExecuteNonQuery(sql As String, ParamArray parameters As MySqlParameter()) As Integer
        Try
            Using conn = GetConnection()
                conn.Open()
                Using cmd As New MySqlCommand(sql, conn)
                    AddParameters(cmd, parameters)
                    Return cmd.ExecuteNonQuery()
                End Using
            End Using
        Catch ex As Exception
            ShowError(ex)
            Return -1
        End Try
    End Function

    Public Shared Function ExecuteNonQuery(sql As String, parameters As Dictionary(Of String, Object)) As Integer
        Return ExecuteNonQuery(sql, ToParameters(parameters))
    End Function

    ' ------------------------------------------------------------------
    ' ExecuteScalar - returns the first column of the first row
    ' (e.g. COUNT(*), LAST_INSERT_ID()). Returns Nothing on error or no rows.
    ' ------------------------------------------------------------------
    Public Shared Function ExecuteScalar(sql As String, ParamArray parameters As MySqlParameter()) As Object
        Try
            Using conn = GetConnection()
                conn.Open()
                Using cmd As New MySqlCommand(sql, conn)
                    AddParameters(cmd, parameters)
                    Dim result = cmd.ExecuteScalar()
                    If result Is Nothing OrElse result Is DBNull.Value Then Return Nothing
                    Return result
                End Using
            End Using
        Catch ex As Exception
            ShowError(ex)
            Return Nothing
        End Try
    End Function

    Public Shared Function ExecuteScalar(sql As String, parameters As Dictionary(Of String, Object)) As Object
        Return ExecuteScalar(sql, ToParameters(parameters))
    End Function

    ' ------------------------------------------------------------------
    ' GetDataTable - SELECT queries for grids and repositories.
    ' Returns an empty DataTable on error so callers never get Nothing.
    ' ------------------------------------------------------------------
    Public Shared Function GetDataTable(sql As String, ParamArray parameters As MySqlParameter()) As DataTable
        Dim table As New DataTable()
        Try
            Using conn = GetConnection()
                conn.Open()
                Using cmd As New MySqlCommand(sql, conn)
                    AddParameters(cmd, parameters)
                    Using reader = cmd.ExecuteReader()
                        table.Load(reader)
                    End Using
                End Using
            End Using
        Catch ex As Exception
            ShowError(ex)
        End Try
        Return table
    End Function

    Public Shared Function GetDataTable(sql As String, parameters As Dictionary(Of String, Object)) As DataTable
        Return GetDataTable(sql, ToParameters(parameters))
    End Function

    ' ------------------------------------------------------------------
    ' ExecuteInsert - INSERT one row and return its new AUTO_INCREMENT id.
    ' Returns -1 if a DB error occurred (0 if INSERT IGNORE skipped the row).
    ' ------------------------------------------------------------------
    Public Shared Function ExecuteInsert(sql As String, ParamArray parameters As MySqlParameter()) As Integer
        Try
            Using conn = GetConnection()
                conn.Open()
                Using cmd As New MySqlCommand(sql, conn)
                    AddParameters(cmd, parameters)
                    cmd.ExecuteNonQuery()
                    Return CInt(cmd.LastInsertedId)
                End Using
            End Using
        Catch ex As Exception
            ShowError(ex)
            Return -1
        End Try
    End Function

    ' ------------------------------------------------------------------
    ' RunInTransaction - runs several statements as ONE unit of work.
    ' If anything fails, everything is rolled back and a friendly error is shown.
    ' Inside "work", run statements with TxExecute(conn, tx, sql, params...).
    ' Example: insert a permit, then insert its 5 endorsements.
    ' ------------------------------------------------------------------
    Public Shared Function RunInTransaction(work As Action(Of MySqlConnection, MySqlTransaction)) As Boolean
        Try
            Using conn = GetConnection()
                conn.Open()
                Using tx = conn.BeginTransaction()
                    Try
                        work(conn, tx)
                        tx.Commit()
                        Return True
                    Catch
                        tx.Rollback()
                        Throw
                    End Try
                End Using
            End Using
        Catch ex As Exception
            ShowError(ex)
            Return False
        End Try
    End Function

    ''' <summary>
    ''' Runs one statement inside RunInTransaction. Returns LAST_INSERT_ID for INSERTs.
    ''' Does NOT catch errors - the transaction catches them and rolls back.
    ''' </summary>
    Public Shared Function TxExecute(conn As MySqlConnection, tx As MySqlTransaction,
                                     sql As String, ParamArray parameters As MySqlParameter()) As Integer
        Using cmd As New MySqlCommand(sql, conn, tx)
            AddParameters(cmd, parameters)
            cmd.ExecuteNonQuery()
            Return CInt(cmd.LastInsertedId)
        End Using
    End Function

    ''' <summary>Runs a scalar query inside RunInTransaction (e.g. reading a status first).</summary>
    Public Shared Function TxScalar(conn As MySqlConnection, tx As MySqlTransaction,
                                    sql As String, ParamArray parameters As MySqlParameter()) As Object
        Using cmd As New MySqlCommand(sql, conn, tx)
            AddParameters(cmd, parameters)
            Return cmd.ExecuteScalar()
        End Using
    End Function

    ' ------------------------------------------------------------------
    ' Parameter helpers
    ' ------------------------------------------------------------------

    ''' <summary>
    ''' Short way to build a parameter: Db.P("@id", 5).
    ''' (Typed as Object on purpose: New MySqlParameter("@x", 0) would treat 0 as a DB type.)
    ''' Nothing and empty Nullable values are sent as SQL NULL.
    ''' </summary>
    Public Shared Function P(name As String, value As Object) As MySqlParameter
        Return New MySqlParameter With {.ParameterName = name, .Value = If(value, DBNull.Value)}
    End Function

    ''' <summary>Turns "" or spaces into Nothing so optional text columns are stored as NULL.</summary>
    Public Shared Function NullIfEmpty(text As String) As String
        If String.IsNullOrWhiteSpace(text) Then Return Nothing
        Return text.Trim()
    End Function


    ' ==================================================================
    ' Private helpers
    ' ==================================================================

    ''' <summary>Adds parameters to a command. Nothing values are sent as SQL NULL.</summary>
    Private Shared Sub AddParameters(cmd As MySqlCommand, parameters As MySqlParameter())
        If parameters Is Nothing Then Return
        For Each param In parameters
            If param.Value Is Nothing Then param.Value = DBNull.Value
            cmd.Parameters.Add(param)
        Next
    End Sub

    ''' <summary>Converts a Dictionary like {"@id", 5} into MySqlParameter objects.</summary>
    Private Shared Function ToParameters(values As Dictionary(Of String, Object)) As MySqlParameter()
        If values Is Nothing Then Return Array.Empty(Of MySqlParameter)()
        Return values.Select(Function(kv) New MySqlParameter(kv.Key, If(kv.Value, DBNull.Value))).ToArray()
    End Function

    ''' <summary>Shows a friendly message for a database error instead of crashing.</summary>
    Private Shared Sub ShowError(ex As Exception)
        MessageBox.Show(FriendlyMessage(ex), "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
    End Sub

    ''' <summary>Turns common MySQL error codes into plain-language messages.</summary>
    Private Shared Function FriendlyMessage(ex As Exception) As String
        ' Business rule broken inside a transaction - the message is already user-friendly.
        If TypeOf ex Is BusinessRuleException Then Return ex.Message

        Dim mysqlEx = TryCast(ex, MySqlException)
        If mysqlEx IsNot Nothing Then
            Select Case mysqlEx.Number
                Case 0, 1042
                    Return "Cannot connect to the MySQL server." & vbCrLf &
                           "Please make sure MySQL is started in the XAMPP Control Panel."
                Case 1045
                    Return "MySQL rejected the username or password in App.config."
                Case 1049
                    Return "The database 'biztracker_db' does not exist yet." & vbCrLf &
                           "Please import database/biztracker_schema.sql in phpMyAdmin."
                Case 1062
                    Return "This record already exists (duplicate value)."
                Case 1451
                    Return "This record cannot be deleted because other records depend on it."
                Case 1452
                    Return "The related record does not exist."
            End Select
            Return "A database error occurred:" & vbCrLf & mysqlEx.Message
        End If
        Return "An unexpected error occurred:" & vbCrLf & ex.Message
    End Function

End Class

''' <summary>
''' Throw this inside Db.RunInTransaction when a business rule is broken
''' (e.g. "Only Submitted applications can be deleted."). The transaction is
''' rolled back and the message is shown to the user as-is.
''' </summary>
Public Class BusinessRuleException
    Inherits Exception

    Public Sub New(message As String)
        MyBase.New(message)
    End Sub
End Class
