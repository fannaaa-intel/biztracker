''' <summary>
''' Database access for the requirements table - the document checklist shared by all six modules.
''' A requirement belongs to a business + module + related record (e.g. permit_id for Business Permit).
''' </summary>
Public NotInheritable Class RequirementRepository

    Private Sub New()
    End Sub

    Private Const SelectSql As String =
        "SELECT r.*, u.full_name AS verified_by_name FROM requirements r " &
        "LEFT JOIN users u ON u.user_id = r.verified_by "

    Public Shared Function GetById(requirementId As Integer) As Requirement
        Return ToList(Db.GetDataTable(SelectSql & "WHERE r.requirement_id = @id",
                                      Db.P("@id", requirementId))).FirstOrDefault()
    End Function

    Public Shared Function GetByBusinessId(businessId As Integer) As List(Of Requirement)
        Return ToList(Db.GetDataTable(SelectSql & "WHERE r.business_id = @bid ORDER BY r.module, r.requirement_id",
                                      Db.P("@bid", businessId)))
    End Function

    ''' <summary>
    ''' The checklist for one record of one module, e.g. ("Business Permit", permit 2).
    ''' Pass relatedId = Nothing for requirements not tied to a specific record.
    ''' </summary>
    Public Shared Function GetByModule(businessId As Integer, moduleName As String, relatedId As Integer?) As List(Of Requirement)
        Dim sql = SelectSql & "WHERE r.business_id = @bid AND r.module = @module AND " &
                  If(relatedId.HasValue, "r.related_id = @rid", "r.related_id IS NULL") &
                  " ORDER BY r.requirement_id"
        Return ToList(Db.GetDataTable(sql,
            Db.P("@bid", businessId), Db.P("@module", moduleName), Db.P("@rid", relatedId)))
    End Function

    ''' <summary>Adds a checklist item (usually "Pending Upload"). Returns new id or -1.</summary>
    Public Shared Function Insert(r As Requirement) As Integer
        Const sql As String =
            "INSERT INTO requirements (business_id, module, related_id, document_name, file_path, status, " &
            "  uploaded_at, remarks) " &
            "VALUES (@bid, @module, @rid, @doc, @path, @status, @uploaded, @remarks)"
        Return Db.ExecuteInsert(sql,
            Db.P("@bid", r.BusinessId),
            Db.P("@module", r.ModuleName),
            Db.P("@rid", r.RelatedId),
            Db.P("@doc", r.DocumentName.Trim()),
            Db.P("@path", Db.NullIfEmpty(r.FilePath)),
            Db.P("@status", r.Status),
            Db.P("@uploaded", r.UploadedAt),
            Db.P("@remarks", Db.NullIfEmpty(r.Remarks)))
    End Function

    ''' <summary>
    ''' Records an uploaded file (relative path). Status goes back to "Submitted"
    ''' and any previous verification is cleared.
    ''' </summary>
    Public Shared Function SaveUpload(requirementId As Integer, relativePath As String) As Boolean
        Const sql As String =
            "UPDATE requirements SET file_path = @path, status = 'Submitted', uploaded_at = NOW(), " &
            "  verified_by = NULL, verified_at = NULL, remarks = NULL " &
            "WHERE requirement_id = @id"
        Return Db.ExecuteNonQuery(sql, Db.P("@path", relativePath), Db.P("@id", requirementId)) > 0
    End Function

    ''' <summary>Staff marks a document "Verified" or "Rejected".</summary>
    Public Shared Function SetVerification(requirementId As Integer, status As String,
                                           userId As Integer?, remarks As String) As Boolean
        Const sql As String =
            "UPDATE requirements SET status = @status, verified_by = @uid, verified_at = NOW(), remarks = @remarks " &
            "WHERE requirement_id = @id"
        Return Db.ExecuteNonQuery(sql,
            Db.P("@status", status),
            Db.P("@uid", userId),
            Db.P("@remarks", Db.NullIfEmpty(remarks)),
            Db.P("@id", requirementId)) > 0
    End Function

    Public Shared Function Delete(requirementId As Integer) As Boolean
        Return Db.ExecuteNonQuery("DELETE FROM requirements WHERE requirement_id = @id",
                                  Db.P("@id", requirementId)) > 0
    End Function

    ' ---------------- helpers ----------------

    Private Shared Function ToList(table As DataTable) As List(Of Requirement)
        Return table.Rows.Cast(Of DataRow)().Select(AddressOf Map).ToList()
    End Function

    Private Shared Function Map(row As DataRow) As Requirement
        Return New Requirement With {
            .RequirementId = row.GetInt("requirement_id"),
            .BusinessId = row.GetInt("business_id"),
            .ModuleName = row.GetString("module"),
            .RelatedId = row.GetNullableInt("related_id"),
            .DocumentName = row.GetString("document_name"),
            .FilePath = row.GetString("file_path"),
            .Status = row.GetString("status"),
            .UploadedAt = row.GetNullableDate("uploaded_at"),
            .VerifiedBy = row.GetNullableInt("verified_by"),
            .VerifiedAt = row.GetNullableDate("verified_at"),
            .Remarks = row.GetString("remarks"),
            .VerifiedByName = row.GetString("verified_by_name")
        }
    End Function

End Class
