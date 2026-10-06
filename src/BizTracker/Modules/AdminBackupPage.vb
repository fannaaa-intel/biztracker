''' <summary>
''' Admin screen, "Backup" tab: what gets backed up and how to restore it (left), and the
''' backup history from the audit log (right). Toolbar: Back Up Now (asks where to save the .sql file).
''' The work is done by BackupService (XAMPP's mysqldump).
''' </summary>
Public Class AdminBackupPage
    Inherits AdminPage

    Private ReadOnly btnBackup As Button
    Private ReadOnly header As New DetailHeader()
    Private ReadOnly infoList As New WheelScrollPanel()
    Private ReadOnly historyList As New WheelScrollPanel()
    Private ReadOnly lblHistoryEmpty As New Label()
    Private history As New List(Of BackupService.BackupRecord)

    ''' <summary>Tests set this to back up without the Save dialog.</summary>
    Public Property TestFilePath As String

    Public Sub New(bar As ModuleToolbar)
        MyBase.New(bar)
        btnBackup = AddAction("Back Up Now", "Back Up", "primary", AddressOf Backup_Click)
        Controls.Add(TwoColumns(BuildInfoCard(), BuildHistoryCard(), 52))
    End Sub

    Public ReadOnly Property HistoryCount As Integer
        Get
            Return history.Count
        End Get
    End Property

    Private Function BuildInfoCard() As Control
        Dim card = CreateCard(New Padding(18, 14, 18, 16))
        Dim gap As New Panel With {.Dock = DockStyle.Top, .Height = 8, .BackColor = Color.White}
        infoList.Dock = DockStyle.Fill
        infoList.BackColor = Color.White
        card.Controls.Add(infoList)
        card.Controls.Add(gap)
        card.Controls.Add(header)
        Return card
    End Function

    Private Function BuildHistoryCard() As Control
        Dim card = CreateCard(New Padding(18, 14, 18, 16))
        Dim head As New Panel With {.Dock = DockStyle.Top, .Height = Dpi(44), .BackColor = Color.White}
        Dim title As New Label With {.Text = "Backup History", .Font = Theme.SubtitleFont, .ForeColor = Theme.TextDark,
                                     .AutoSize = True, .Location = New Point(0, Dpi(6)), .BackColor = Color.White}
        head.Controls.Add(title)
        historyList.Dock = DockStyle.Fill
        historyList.BackColor = Color.White
        StyleEmptyLabel(lblHistoryEmpty)
        card.Controls.Add(lblHistoryEmpty)
        card.Controls.Add(historyList)
        card.Controls.Add(head)
        Return card
    End Function

    Public Overrides Sub RefreshData()
        history = BackupService.GetHistory()
        ShowInfo()
        ShowHistory()
    End Sub

    Private Sub ShowInfo()
        Dim blocker = BackupService.GetBackupBlocker()
        Dim tool = BackupService.FindMysqldump()
        header.SetText("Database Backup", If(blocker Is Nothing, "Ready", "Not Available"),
                       "Saves every table and record to one .sql file")
        Dim last = history.FirstOrDefault()
        Dim rows As New List(Of Control) From {
            InfoRow(Tips, "Database", BackupService.DatabaseName(), "Server", BackupService.ServerName()),
            InfoRow(Tips, "Backup tool", If(tool, "mysqldump.exe not found"),
                    "Last backup", If(last Is Nothing, "Never", last.CreatedAt.ToString("MMM d, yyyy h:mm tt"))),
            HeadingRow("How it works")
        }
        If blocker IsNot Nothing Then rows.Add(NoteRow(Tips, blocker, Theme.StatusRed))
        rows.Add(NoteRow(Tips, "1. Click ""Back Up Now"" and choose a folder (a USB drive or cloud folder is safest).", Theme.TextDark))
        rows.Add(NoteRow(Tips, "2. Keep at least one recent copy outside this computer.", Theme.TextDark))
        rows.Add(NoteRow(Tips, "To restore: phpMyAdmin → Import → choose the .sql file.", Theme.SidebarBlue))
        If last IsNot Nothing AndAlso (Date.Today - last.CreatedAt.Date).TotalDays > 7 Then
            rows.Add(NoteRow(Tips, "The last backup is more than a week old. Back up again soon.", Theme.StatusAmber))
        ElseIf last Is Nothing Then
            rows.Add(NoteRow(Tips, "No backup has been made yet.", Theme.StatusAmber))
        End If
        infoList.SetRows(rows)

        btnBackup.Enabled = blocker Is Nothing
        Toolbar.SetTip(btnBackup, If(blocker, "Save a full copy of " & BackupService.DatabaseName() & " as a .sql file"))
    End Sub

    Private Sub ShowHistory()
        lblHistoryEmpty.Visible = history.Count = 0
        If history.Count = 0 Then
            lblHistoryEmpty.Text = "No backups yet." & vbCrLf & "Click ""Back Up Now"" to save the first one."
            lblHistoryEmpty.BringToFront()
        End If
        Dim rows As New List(Of Control)
        For Each record In history
            Dim r = record
            Dim links = If(r.FileExists, {New RowLink("Show file", Theme.SidebarBlue, Sub() ShowFile(r.FilePath))}, Nothing)
            rows.Add(ListRow(Tips, If(r.FileName = "", "Backup", r.FileName),
                             r.CreatedAt.ToString("MMM d, yyyy h:mm tt") & If(r.SizeText = "", "", " · " & r.SizeText) & " · " & r.Username,
                             If(r.FileExists, "", "Moved"), links))
        Next
        historyList.SetRows(rows)
    End Sub

    Private Sub ShowFile(path As String)
        Dim problem = BackupService.ShowInFolder(path)
        If problem IsNot Nothing Then UiHelper.ShowWarning(problem, "Cannot show the file")
    End Sub

    Private Sub Backup_Click(sender As Object, e As EventArgs)
        Dim target = TestFilePath
        If String.IsNullOrEmpty(target) Then
            Using dlg As New SaveFileDialog With {
                .Title = "Save database backup", .Filter = "SQL backup (*.sql)|*.sql", .DefaultExt = "sql",
                .FileName = BackupService.DefaultFileName(), .OverwritePrompt = True, .AddExtension = True
            }
                If dlg.ShowDialog(OwnerWindow) <> DialogResult.OK Then Return
                target = dlg.FileName
            End Using
        End If
        Dim form = FindForm()
        If form IsNot Nothing Then form.Cursor = Cursors.WaitCursor
        Dim result = BackupService.Backup(target)
        If form IsNot Nothing Then form.Cursor = Cursors.Default
        If result.ErrorMessage IsNot Nothing Then
            UiHelper.ShowError(result.ErrorMessage, "Backup failed")
            Return
        End If
        RefreshData()
        OnDataChanged()
        If String.IsNullOrEmpty(TestFilePath) Then
            UiHelper.ShowSuccess("The database was saved to:" & vbCrLf & result.FilePath & "  (" & BackupService.FormatSize(result.SizeBytes) & ")" & vbCrLf & vbCrLf &
                                 "Next: copy this file to a USB drive or cloud folder. To restore it, import it in phpMyAdmin.", "Backup saved")
        End If
    End Sub

End Class
