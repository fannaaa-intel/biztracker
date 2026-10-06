Imports MySql.Data.MySqlClient

''' <summary>
''' Database access for construction_projects and their construction_clearances.
''' A new project always gets 4 Pending clearances (Locational, FSEC, Building, Occupancy).
''' </summary>
Public NotInheritable Class ConstructionRepository

    Private Sub New()
    End Sub

    Private Const SelectSql As String =
        "SELECT cp.*, b.business_name FROM construction_projects cp " &
        "JOIN businesses b ON b.business_id = cp.business_id "

    Private Const OrderSql As String = " ORDER BY cp.date_filed DESC, cp.project_id DESC"

    ' ==================== Projects ====================

    Public Shared Function GetAll() As List(Of ConstructionProject)
        Return ToList(Db.GetDataTable(SelectSql & "WHERE b.is_active = 1" & OrderSql))
    End Function

    Public Shared Function GetById(projectId As Integer) As ConstructionProject
        Return ToList(Db.GetDataTable(SelectSql & "WHERE cp.project_id = @id", Db.P("@id", projectId))).FirstOrDefault()
    End Function

    Public Shared Function GetByBusinessId(businessId As Integer) As List(Of ConstructionProject)
        Return ToList(Db.GetDataTable(SelectSql & "WHERE cp.business_id = @bid" & OrderSql, Db.P("@bid", businessId)))
    End Function

    ''' <summary>
    ''' Inserts a project AND its 4 Pending clearances in one transaction.
    ''' Returns the new project_id, or -1 on error.
    ''' </summary>
    Public Shared Function Insert(p As ConstructionProject) As Integer
        Const projectSql As String =
            "INSERT INTO construction_projects (business_id, reference_no, project_title, project_type, " &
            "  current_stage, status, estimated_cost, date_filed) " &
            "VALUES (@bid, @ref, @title, @type, @stage, @status, @cost, @filed)"
        Const clearanceSql As String =
            "INSERT INTO construction_clearances (project_id, clearance_type, status) VALUES (@pid, @type, 'Pending')"

        Dim newId As Integer = -1
        Dim ok = Db.RunInTransaction(
            Sub(conn, tx)
                newId = Db.TxExecute(conn, tx, projectSql, ToParameters(p).Concat({
                                         Db.P("@bid", p.BusinessId),
                                         Db.P("@ref", p.ReferenceNo)}).ToArray())
                For Each clearanceType In ClearanceTypes.All
                    Db.TxExecute(conn, tx, clearanceSql, Db.P("@pid", newId), Db.P("@type", clearanceType))
                Next
            End Sub)
        Return If(ok, newId, -1)
    End Function

    Public Shared Function Update(p As ConstructionProject) As Boolean
        Const sql As String =
            "UPDATE construction_projects SET project_title = @title, project_type = @type, " &
            "  current_stage = @stage, status = @status, estimated_cost = @cost, date_filed = @filed " &
            "WHERE project_id = @id"
        Return Db.ExecuteNonQuery(sql, ToParameters(p).Append(Db.P("@id", p.ProjectId)).ToArray()) > 0
    End Function

    ' ==================== Clearances ====================

    Public Shared Function GetClearances(projectId As Integer) As List(Of ConstructionClearance)
        Const sql As String =
            "SELECT cc.*, u.full_name AS approved_by_name FROM construction_clearances cc " &
            "LEFT JOIN users u ON u.user_id = cc.approved_by " &
            "WHERE cc.project_id = @pid " &
            "ORDER BY FIELD(cc.clearance_type, 'Locational', 'FSEC', 'Building', 'Occupancy')"
        Dim table = Db.GetDataTable(sql, Db.P("@pid", projectId))
        Return table.Rows.Cast(Of DataRow)().Select(
            Function(row) New ConstructionClearance With {
                .ClearanceId = row.GetInt("clearance_id"),
                .ProjectId = row.GetInt("project_id"),
                .ClearanceType = row.GetString("clearance_type"),
                .ClearanceNo = row.GetString("clearance_no"),
                .Status = row.GetString("status"),
                .ApprovedBy = row.GetNullableInt("approved_by"),
                .ApprovedAt = row.GetNullableDate("approved_at"),
                .Remarks = row.GetString("remarks"),
                .ApprovedByName = row.GetString("approved_by_name")
            }).ToList()
    End Function

    ''' <summary>Saves a clearance decision (status, number, who/when, remarks).</summary>
    Public Shared Function UpdateClearance(c As ConstructionClearance) As Boolean
        Const sql As String =
            "UPDATE construction_clearances SET clearance_no = @no, status = @status, approved_by = @by, " &
            "  approved_at = @at, remarks = @remarks " &
            "WHERE clearance_id = @id"
        Return Db.ExecuteNonQuery(sql,
            Db.P("@no", Db.NullIfEmpty(c.ClearanceNo)),
            Db.P("@status", c.Status),
            Db.P("@by", c.ApprovedBy),
            Db.P("@at", c.ApprovedAt),
            Db.P("@remarks", Db.NullIfEmpty(c.Remarks)),
            Db.P("@id", c.ClearanceId)) > 0
    End Function

    ' ---------------- helpers ----------------

    Private Shared Function ToParameters(p As ConstructionProject) As MySqlParameter()
        Return {
            Db.P("@title", p.ProjectTitle.Trim()),
            Db.P("@type", p.ProjectType),
            Db.P("@stage", p.CurrentStage),
            Db.P("@status", p.Status),
            Db.P("@cost", p.EstimatedCost),
            Db.P("@filed", p.DateFiled.Date)
        }
    End Function

    Private Shared Function ToList(table As DataTable) As List(Of ConstructionProject)
        Return table.Rows.Cast(Of DataRow)().Select(AddressOf Map).ToList()
    End Function

    Private Shared Function Map(row As DataRow) As ConstructionProject
        Return New ConstructionProject With {
            .ProjectId = row.GetInt("project_id"),
            .BusinessId = row.GetInt("business_id"),
            .ReferenceNo = row.GetString("reference_no"),
            .ProjectTitle = row.GetString("project_title"),
            .ProjectType = row.GetString("project_type"),
            .CurrentStage = row.GetString("current_stage"),
            .Status = row.GetString("status"),
            .EstimatedCost = row.GetNullableDecimal("estimated_cost"),
            .DateFiled = row.GetDate("date_filed"),
            .CreatedAt = row.GetDate("created_at"),
            .BusinessName = row.GetString("business_name")
        }
    End Function

End Class
