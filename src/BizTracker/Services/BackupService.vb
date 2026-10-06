Imports System.IO
Imports MySql.Data.MySqlClient

''' <summary>Outcome of BackupService.Backup.</summary>
Public Class BackupResult
    ''' <summary>Nothing when the backup file was written.</summary>
    Public Property ErrorMessage As String
    Public Property FilePath As String = ""
    Public Property SizeBytes As Long
End Class

''' <summary>
''' Database backup (Admin only): runs XAMPP's mysqldump.exe and writes a .sql file that can be
''' imported again in phpMyAdmin. The server, user and database come from the App.config connection
''' string (so the tests back up biztracker_test). Each backup is written to audit_log
''' (action INSERT, table "backups") so the screen can show the backup history.
''' </summary>
Public NotInheritable Class BackupService

    Private Sub New()
    End Sub

    Public Const AuditTable As String = "backups"
    Private Const TimeoutMilliseconds As Integer = 120000
    Private Const NotAllowed As String = "Only an Admin can back up the database."

    ''' <summary>Where XAMPP installs mysqldump (checked first), then every folder on PATH.</summary>
    Private Shared ReadOnly KnownPaths As String() = {
        "C:\xampp\mysql\bin\mysqldump.exe", "D:\xampp\mysql\bin\mysqldump.exe"
    }

    Public Shared Function CanBackup() As Boolean
        Return Session.IsAdmin
    End Function

    ''' <summary>Full path of mysqldump.exe, or Nothing if it cannot be found.</summary>
    Public Shared Function FindMysqldump() As String
        For Each p In KnownPaths
            If File.Exists(p) Then Return p
        Next
        For Each folder In If(Environment.GetEnvironmentVariable("PATH"), "").Split(";"c)
            Try
                Dim candidate = Path.Combine(folder.Trim(), "mysqldump.exe")
                If folder.Trim() <> "" AndAlso File.Exists(candidate) Then Return candidate
            Catch ex As ArgumentException
                ' A malformed PATH entry - skip it.
            End Try
        Next
        Return Nothing
    End Function

    ''' <summary>The database being backed up (from the connection string).</summary>
    Public Shared Function DatabaseName() As String
        Return New MySqlConnectionStringBuilder(Db.ConnectionString).Database
    End Function

    ''' <summary>"localhost:3306"</summary>
    Public Shared Function ServerName() As String
        Dim cs As New MySqlConnectionStringBuilder(Db.ConnectionString)
        Return cs.Server & ":" & cs.Port
    End Function

    ''' <summary>e.g. "biztracker_db_2026-10-06_2130.sql"</summary>
    Public Shared Function DefaultFileName() As String
        Return DatabaseName() & "_" & Date.Now.ToString("yyyy-MM-dd_HHmm") & ".sql"
    End Function

    ''' <summary>Why a backup cannot run right now (Nothing = ready).</summary>
    Public Shared Function GetBackupBlocker() As String
        If Not CanBackup() Then Return NotAllowed
        If FindMysqldump() Is Nothing Then
            Return "mysqldump.exe was not found. Install XAMPP in C:\xampp (it includes mysqldump)."
        End If
        Return Nothing
    End Function

    ''' <summary>Writes a full backup of the database to filePath (.sql).</summary>
    Public Shared Function Backup(filePath As String) As BackupResult
        Dim blocker = GetBackupBlocker()
        If blocker IsNot Nothing Then Return New BackupResult With {.ErrorMessage = blocker}
        If String.IsNullOrWhiteSpace(filePath) Then Return New BackupResult With {.ErrorMessage = "Choose where to save the backup."}
        Dim folder = Path.GetDirectoryName(Path.GetFullPath(filePath))
        If Not Directory.Exists(folder) Then Return New BackupResult With {.ErrorMessage = "The folder " & folder & " does not exist."}

        ' Dump into a temporary file first: a failed backup never overwrites an existing file
        Dim partialPath = filePath & ".partial"
        Dim cs As New MySqlConnectionStringBuilder(Db.ConnectionString)
        Dim info As New ProcessStartInfo(FindMysqldump()) With {
            .UseShellExecute = False, .CreateNoWindow = True, .RedirectStandardError = True
        }
        ' ArgumentList quotes each value safely (paths with spaces, etc.)
        info.ArgumentList.Add("--host=" & cs.Server)
        info.ArgumentList.Add("--port=" & cs.Port.ToString())
        info.ArgumentList.Add("--user=" & cs.UserID)
        info.ArgumentList.Add("--default-character-set=utf8mb4")
        info.ArgumentList.Add("--single-transaction")
        info.ArgumentList.Add("--routines")
        info.ArgumentList.Add("--result-file=" & partialPath)
        info.ArgumentList.Add(cs.Database)
        ' The password (if any) goes through the environment, never on the command line
        If cs.Password <> "" Then info.Environment("MYSQL_PWD") = cs.Password

        Dim errorText As String
        Dim exitCode As Integer
        Try
            Using proc = Process.Start(info)
                Dim stderrTask = proc.StandardError.ReadToEndAsync()
                If Not proc.WaitForExit(TimeoutMilliseconds) Then
                    proc.Kill(True)
                    DeletePartial(partialPath)
                    Return New BackupResult With {.ErrorMessage = "The backup took too long and was stopped. Please make sure MySQL is running in XAMPP."}
                End If
                exitCode = proc.ExitCode
                errorText = stderrTask.Result.Trim()
            End Using
        Catch ex As Exception When TypeOf ex Is ComponentModel.Win32Exception OrElse TypeOf ex Is IOException OrElse
                                    TypeOf ex Is InvalidOperationException
            DeletePartial(partialPath)
            Return New BackupResult With {.ErrorMessage = "mysqldump could not be started: " & ex.Message}
        End Try

        Dim size = If(File.Exists(partialPath), New FileInfo(partialPath).Length, 0L)
        If exitCode <> 0 OrElse size = 0 Then
            DeletePartial(partialPath)
            Dim firstLine = errorText.Split({vbCr, vbLf}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
            Return New BackupResult With {
                .ErrorMessage = "The backup failed. Please make sure MySQL is running in XAMPP." &
                                If(firstLine Is Nothing, "", vbCrLf & vbCrLf & "Details: " & firstLine)
            }
        End If

        Try
            File.Move(partialPath, filePath, overwrite:=True)
        Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
            DeletePartial(partialPath)
            Return New BackupResult With {.ErrorMessage = "The backup file could not be saved: " & ex.Message}
        End Try

        AuditService.Log(AuditActions.Insert, AuditTable, Nothing,
                         "Backup of " & cs.Database & " saved to " & filePath & " (" & FormatSize(size) & ")")
        Return New BackupResult With {.FilePath = filePath, .SizeBytes = size}
    End Function

    ''' <summary>One past backup (read back from its audit_log row).</summary>
    Public Class BackupRecord
        Public Property CreatedAt As Date
        Public Property FilePath As String = ""
        Public Property SizeText As String = ""
        Public Property Username As String = ""
        Public ReadOnly Property FileName As String
            Get
                Return Path.GetFileName(FilePath)
            End Get
        End Property
        Public ReadOnly Property FileExists As Boolean
            Get
                Return FilePath <> "" AndAlso File.Exists(FilePath)
            End Get
        End Property
    End Class

    ''' <summary>Recent backups, newest first (from audit_log).</summary>
    Public Shared Function GetHistory(Optional limit As Integer = 20) As List(Of BackupRecord)
        If Not CanBackup() Then Return New List(Of BackupRecord)
        Return AuditLogRepository.GetFiltered(New AuditLogFilter With {.TableName = AuditTable}, limit).
               Select(AddressOf ToRecord).ToList()
    End Function

    ''' <summary>Details look like "Backup of biztracker_db saved to C:\...\file.sql (12.3 KB)".</summary>
    Private Shared Function ToRecord(entry As AuditLogEntry) As BackupRecord
        Dim record As New BackupRecord With {.CreatedAt = entry.CreatedAt, .Username = AuditLogService.UserLabel(entry)}
        Dim details = If(entry.Details, "")
        Dim start = details.IndexOf(" saved to ", StringComparison.Ordinal)
        Dim sizeStart = details.LastIndexOf(" (", StringComparison.Ordinal)
        If start >= 0 AndAlso sizeStart > start Then
            record.FilePath = details.Substring(start + 10, sizeStart - start - 10)
            record.SizeText = details.Substring(sizeStart + 2).TrimEnd(")"c)
        Else
            record.FilePath = details
        End If
        Return record
    End Function

    ''' <summary>Opens Windows Explorer with the backup file selected. Returns an error message, or Nothing.</summary>
    Public Shared Function ShowInFolder(filePath As String) As String
        If String.IsNullOrEmpty(filePath) OrElse Not File.Exists(filePath) Then
            Return "The file is no longer there. It may have been moved or deleted."
        End If
        Try
            Process.Start(New ProcessStartInfo("explorer.exe", "/select,""" & filePath & """") With {.UseShellExecute = True})
            Return Nothing
        Catch ex As ComponentModel.Win32Exception
            Return "Windows Explorer could not be opened: " & ex.Message
        End Try
    End Function

    ''' <summary>"12.4 KB", "1.2 MB"</summary>
    Public Shared Function FormatSize(bytes As Long) As String
        If bytes >= 1024L * 1024L Then Return (bytes / 1024.0 / 1024.0).ToString("0.0") & " MB"
        Return Math.Max(0.1, bytes / 1024.0).ToString("0.0") & " KB"
    End Function

    Private Shared Sub DeletePartial(filePath As String)
        Try
            If File.Exists(filePath) Then File.Delete(filePath)
        Catch ex As IOException
            ' Leave it - the user is told the backup failed.
        Catch ex As UnauthorizedAccessException
        End Try
    End Sub

End Class
