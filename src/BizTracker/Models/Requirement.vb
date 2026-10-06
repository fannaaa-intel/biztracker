''' <summary>One row of the requirements table (a document in a module's checklist).</summary>
Public Class Requirement
    Public Property RequirementId As Integer
    Public Property BusinessId As Integer
    Public Property ModuleName As String = ""       ' column "module" - see ModuleNames constants
    Public Property RelatedId As Integer?           ' permit_id / sanitary_id / project_id ... depending on module
    Public Property DocumentName As String = ""
    Public Property FilePath As String = ""         ' relative path, e.g. uploads/1/file.pdf
    Public Property Status As String = "Pending Upload"   ' Pending Upload / Submitted / Verified / Rejected
    Public Property UploadedAt As Date?
    Public Property VerifiedBy As Integer?
    Public Property VerifiedAt As Date?
    Public Property Remarks As String = ""

    ' Display only (filled from a JOIN, not saved)
    Public Property VerifiedByName As String = ""
End Class
