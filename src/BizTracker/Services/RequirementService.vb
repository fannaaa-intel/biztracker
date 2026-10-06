Imports System.IO

''' <summary>
''' Uploading, opening and verifying requirement documents (shared by all modules).
''' Files are COPIED into uploads/&lt;business_id&gt;/ and only the relative path is saved (no BLOBs).
''' </summary>
Public NotInheritable Class RequirementService

    Private Sub New()
    End Sub

    Public Shared ReadOnly AllowedExtensions As String() = {".pdf", ".jpg", ".jpeg", ".png"}
    Public Const MaxFileBytes As Long = 10L * 1024 * 1024   ' 10 MB
    Public Const FileDialogFilter As String = "Documents (*.pdf;*.jpg;*.jpeg;*.png)|*.pdf;*.jpg;*.jpeg;*.png"

    ''' <summary>
    ''' Folder that holds uploads. Uses the "upload_folder" setting: an absolute path is used as-is;
    ''' a relative name is placed next to BizTracker.sln when running from source, else next to the .exe.
    ''' </summary>
    Public Shared Function GetUploadsRoot() As String
        Dim folder = SettingsRepository.GetValue("upload_folder", "uploads")
        If Path.IsPathRooted(folder) Then Return folder

        Dim dir = New DirectoryInfo(AppContext.BaseDirectory)
        While dir IsNot Nothing
            If File.Exists(Path.Combine(dir.FullName, "BizTracker.sln")) Then Return Path.Combine(dir.FullName, folder)
            dir = dir.Parent
        End While
        Return Path.Combine(AppContext.BaseDirectory, folder)
    End Function

    ''' <summary>Absolute path of a stored relative path like "uploads/1/file.pdf".</summary>
    Public Shared Function GetFullPath(relativePath As String) As String
        Dim root = GetUploadsRoot()
        ' Stored paths start with the uploads folder name - strip it and join with the real root
        Dim parts = relativePath.Replace("\"c, "/"c).Split("/"c).ToList()
        If parts.Count > 1 Then parts.RemoveAt(0)
        Return Path.Combine({root}.Concat(parts).ToArray())
    End Function

    ''' <summary>Checks a file before upload. Returns an error message or Nothing.</summary>
    Public Shared Function ValidateFile(sourcePath As String) As String
        If Not File.Exists(sourcePath) Then Return "The selected file no longer exists."
        If Not AllowedExtensions.Contains(Path.GetExtension(sourcePath).ToLowerInvariant()) Then
            Return "Only PDF, JPG and PNG files can be uploaded."
        End If
        Dim size = New FileInfo(sourcePath).Length
        If size = 0 Then Return "The selected file is empty."
        If size > MaxFileBytes Then Return "The file is larger than 10 MB."
        Return Nothing
    End Function

    ''' <summary>
    ''' Copies the file into uploads/&lt;business_id&gt;/ and marks the requirement "Submitted".
    ''' Owners can upload for their own business; staff can upload on behalf of an applicant.
    ''' Returns an error message, or Nothing on success.
    ''' </summary>
    Public Shared Function Upload(r As Requirement, sourcePath As String) As String
        If Not AccessService.CanSeeBusiness(r.BusinessId) Then Return "You cannot upload for this business."
        If r.Status = "Verified" Then Return "This document is already verified."
        Dim problem = ValidateFile(sourcePath)
        If problem IsNot Nothing Then Return problem

        Try
            Dim folderName = SettingsRepository.GetValue("upload_folder", "uploads")
            If Path.IsPathRooted(folderName) Then folderName = "uploads"
            Dim targetDir = Path.Combine(GetUploadsRoot(), r.BusinessId.ToString())
            Directory.CreateDirectory(targetDir)

            ' Safe, unique file name: requirementId_timestamp_originalname.ext
            Dim original = Path.GetFileNameWithoutExtension(sourcePath)
            Dim safe = New String(original.Select(Function(c) If(Char.IsLetterOrDigit(c) OrElse c = "-"c, c, "_"c)).ToArray())
            If safe.Length > 40 Then safe = safe.Substring(0, 40)
            Dim fileName = r.RequirementId & "_" & Date.Now.ToString("yyyyMMddHHmmss") & "_" & safe &
                           Path.GetExtension(sourcePath).ToLowerInvariant()
            File.Copy(sourcePath, Path.Combine(targetDir, fileName), overwrite:=False)

            Dim relative = folderName & "/" & r.BusinessId & "/" & fileName
            If Not RequirementRepository.SaveUpload(r.RequirementId, relative) Then Return "The upload could not be saved."
            AuditService.Log(AuditActions.Upload, "requirements", r.RequirementId,
                             "Uploaded '" & r.DocumentName & "' (" & r.ModuleName & ")")
            Return Nothing
        Catch ex As IOException
            Return "The file could not be copied: " & ex.Message
        Catch ex As UnauthorizedAccessException
            Return "No permission to save the file in the uploads folder."
        End Try
    End Function

    ''' <summary>Opens the uploaded file with the default program. Returns an error message or Nothing.</summary>
    Public Shared Function OpenFile(r As Requirement) As String
        If String.IsNullOrEmpty(r.FilePath) Then Return "No file has been uploaded yet."
        Dim full = GetFullPath(r.FilePath)
        If Not File.Exists(full) Then Return "The file '" & r.FilePath & "' was not found in the uploads folder."
        Try
            Process.Start(New ProcessStartInfo(full) With {.UseShellExecute = True})
            Return Nothing
        Catch ex As Exception
            Return "The file could not be opened: " & ex.Message
        End Try
    End Function

    ''' <summary>Staff marks a submitted document Verified or Rejected (reason required for Rejected).</summary>
    Public Shared Function SetVerification(r As Requirement, screen As AppScreen, verified As Boolean,
                                           Optional remarks As String = "") As String
        If Not AccessService.CanManage(screen) Then Return "You are not allowed to verify documents."
        If r.Status <> "Submitted" Then Return "Only submitted documents can be verified or rejected."
        If Not verified AndAlso String.IsNullOrWhiteSpace(remarks) Then Return "Please enter the reason for rejecting."
        Dim status = If(verified, "Verified", "Rejected")
        If Not RequirementRepository.SetVerification(r.RequirementId, status, Session.UserId, remarks) Then
            Return "The change could not be saved."
        End If
        AuditService.Log(AuditActions.StatusChange, "requirements", r.RequirementId,
                         "'" & r.DocumentName & "' -> " & status)
        Return Nothing
    End Function

End Class
