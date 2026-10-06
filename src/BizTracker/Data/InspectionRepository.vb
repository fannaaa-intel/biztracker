Imports MySql.Data.MySqlClient

''' <summary>
''' Database access for inspections and their inspection_items.
''' A new inspection always gets 4 Pending items (Structural, Electrical, Mechanical, Fire).
''' </summary>
Public NotInheritable Class InspectionRepository

    Private Sub New()
    End Sub

    Private Const SelectSql As String =
        "SELECT i.*, b.business_name FROM inspections i " &
        "JOIN businesses b ON b.business_id = i.business_id "

    Private Const OrderSql As String = " ORDER BY i.inspection_year DESC, i.schedule_date DESC"

    ' ==================== Inspections ====================

    Public Shared Function GetAll() As List(Of Inspection)
        Return ToList(Db.GetDataTable(SelectSql & "WHERE b.is_active = 1" & OrderSql))
    End Function

    Public Shared Function GetById(inspectionId As Integer) As Inspection
        Return ToList(Db.GetDataTable(SelectSql & "WHERE i.inspection_id = @id", Db.P("@id", inspectionId))).FirstOrDefault()
    End Function

    Public Shared Function GetByBusinessId(businessId As Integer) As List(Of Inspection)
        Return ToList(Db.GetDataTable(SelectSql & "WHERE i.business_id = @bid" & OrderSql, Db.P("@bid", businessId)))
    End Function

    ''' <summary>
    ''' Inserts an inspection AND its 4 Pending department items in one transaction.
    ''' Returns the new inspection_id, or -1 on error.
    ''' </summary>
    Public Shared Function Insert(i As Inspection) As Integer
        Const inspectionSql As String =
            "INSERT INTO inspections (business_id, reference_no, inspection_year, schedule_date, reinspection_date, " &
            "  status, overall_result, certificate_no) " &
            "VALUES (@bid, @ref, @year, @schedule, @reinspect, @status, @result, @cert)"
        Const itemSql As String =
            "INSERT INTO inspection_items (inspection_id, department, result) VALUES (@iid, @dept, 'Pending')"

        Dim newId As Integer = -1
        Dim ok = Db.RunInTransaction(
            Sub(conn, tx)
                newId = Db.TxExecute(conn, tx, inspectionSql, ToParameters(i).Concat({
                                         Db.P("@bid", i.BusinessId),
                                         Db.P("@ref", i.ReferenceNo)}).ToArray())
                For Each dept In InspectionDepartments.All
                    Db.TxExecute(conn, tx, itemSql, Db.P("@iid", newId), Db.P("@dept", dept))
                Next
            End Sub)
        Return If(ok, newId, -1)
    End Function

    ''' <summary>Updates schedule, status, overall result and certificate number.</summary>
    Public Shared Function Update(i As Inspection) As Boolean
        Const sql As String =
            "UPDATE inspections SET inspection_year = @year, schedule_date = @schedule, " &
            "  reinspection_date = @reinspect, status = @status, overall_result = @result, certificate_no = @cert " &
            "WHERE inspection_id = @id"
        Return Db.ExecuteNonQuery(sql, ToParameters(i).Append(Db.P("@id", i.InspectionId)).ToArray()) > 0
    End Function

    ' ==================== Items (one per department) ====================

    Public Shared Function GetItems(inspectionId As Integer) As List(Of InspectionItem)
        Const sql As String =
            "SELECT * FROM inspection_items WHERE inspection_id = @iid " &
            "ORDER BY FIELD(department, 'Structural', 'Electrical', 'Mechanical', 'Fire')"
        Dim table = Db.GetDataTable(sql, Db.P("@iid", inspectionId))
        Return table.Rows.Cast(Of DataRow)().Select(
            Function(row) New InspectionItem With {
                .ItemId = row.GetInt("item_id"),
                .InspectionId = row.GetInt("inspection_id"),
                .Department = row.GetString("department"),
                .Result = row.GetString("result"),
                .Findings = row.GetString("findings"),
                .InspectorName = row.GetString("inspector_name"),
                .InspectedAt = row.GetNullableDate("inspected_at")
            }).ToList()
    End Function

    ''' <summary>Records one department's result, findings and inspector.</summary>
    Public Shared Function UpdateItem(item As InspectionItem) As Boolean
        Const sql As String =
            "UPDATE inspection_items SET result = @result, findings = @findings, inspector_name = @inspector, " &
            "  inspected_at = @at " &
            "WHERE item_id = @id"
        Return Db.ExecuteNonQuery(sql,
            Db.P("@result", item.Result),
            Db.P("@findings", Db.NullIfEmpty(item.Findings)),
            Db.P("@inspector", Db.NullIfEmpty(item.InspectorName)),
            Db.P("@at", item.InspectedAt),
            Db.P("@id", item.ItemId)) > 0
    End Function

    ' ---------------- helpers ----------------

    Private Shared Function ToParameters(i As Inspection) As MySqlParameter()
        Return {
            Db.P("@year", i.InspectionYear),
            Db.P("@schedule", i.ScheduleDate),
            Db.P("@reinspect", i.ReinspectionDate),
            Db.P("@status", i.Status),
            Db.P("@result", i.OverallResult),
            Db.P("@cert", Db.NullIfEmpty(i.CertificateNo))
        }
    End Function

    Private Shared Function ToList(table As DataTable) As List(Of Inspection)
        Return table.Rows.Cast(Of DataRow)().Select(AddressOf Map).ToList()
    End Function

    Private Shared Function Map(row As DataRow) As Inspection
        Return New Inspection With {
            .InspectionId = row.GetInt("inspection_id"),
            .BusinessId = row.GetInt("business_id"),
            .ReferenceNo = row.GetString("reference_no"),
            .InspectionYear = row.GetInt("inspection_year"),
            .ScheduleDate = row.GetDate("schedule_date"),
            .ReinspectionDate = row.GetNullableDate("reinspection_date"),
            .Status = row.GetString("status"),
            .OverallResult = row.GetString("overall_result"),
            .CertificateNo = row.GetString("certificate_no"),
            .CreatedAt = row.GetDate("created_at"),
            .BusinessName = row.GetString("business_name")
        }
    End Function

End Class
