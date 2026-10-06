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
    ' TEMPORARY (Phase 1): connects to the MySQL SERVER only, without
    ' selecting biztracker_db, and returns the server version.
    ' Returns Nothing if the server cannot be reached.
    ' ------------------------------------------------------------------
    Public Shared Function GetServerVersion() As String
        Try
            Dim builder As New MySqlConnectionStringBuilder(ConnectionString)
            builder.Database = ""   ' server only - the database may not exist yet
            Using conn As New MySqlConnection(builder.ConnectionString)
                conn.Open()
                Using cmd As New MySqlCommand("SELECT VERSION()", conn)
                    Return Convert.ToString(cmd.ExecuteScalar())
                End Using
            End Using
        Catch ex As Exception
            ShowError(ex)
            Return Nothing
        End Try
    End Function

    ' ==================================================================
    ' Private helpers
    ' ==================================================================

    ''' <summary>Adds parameters to a command. Nothing values are sent as SQL NULL.</summary>
    Private Shared Sub AddParameters(cmd As MySqlCommand, parameters As MySqlParameter())
        If parameters Is Nothing Then Return
        For Each p In parameters
            If p.Value Is Nothing Then p.Value = DBNull.Value
            cmd.Parameters.Add(p)
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
