''' <summary>
''' Database access for the employees table.
''' Employees are deactivated (is_active = 0), never deleted, so their certificates stay on record.
''' </summary>
Public NotInheritable Class EmployeeRepository

    Private Sub New()
    End Sub

    Private Const SelectSql As String = "SELECT * FROM employees "

    Public Shared Function GetAll(Optional includeInactive As Boolean = False) As List(Of Employee)
        Dim sql = SelectSql & If(includeInactive, "", "WHERE is_active = 1 ") & "ORDER BY business_id, full_name"
        Return ToList(Db.GetDataTable(sql))
    End Function

    Public Shared Function GetById(employeeId As Integer) As Employee
        Return ToList(Db.GetDataTable(SelectSql & "WHERE employee_id = @id", Db.P("@id", employeeId))).FirstOrDefault()
    End Function

    Public Shared Function GetByBusinessId(businessId As Integer, Optional includeInactive As Boolean = False) As List(Of Employee)
        Dim sql = SelectSql & "WHERE business_id = @bid " &
                  If(includeInactive, "", "AND is_active = 1 ") & "ORDER BY full_name"
        Return ToList(Db.GetDataTable(sql, Db.P("@bid", businessId)))
    End Function

    ''' <summary>Returns the new employee_id, or -1 on error.</summary>
    Public Shared Function Insert(e As Employee) As Integer
        Const sql As String =
            "INSERT INTO employees (business_id, full_name, position, category, is_active) " &
            "VALUES (@bid, @name, @position, @category, @active)"
        Return Db.ExecuteInsert(sql,
            Db.P("@bid", e.BusinessId),
            Db.P("@name", e.FullName.Trim()),
            Db.P("@position", e.Position.Trim()),
            Db.P("@category", e.Category),
            Db.P("@active", e.IsActive))
    End Function

    Public Shared Function Update(e As Employee) As Boolean
        Const sql As String =
            "UPDATE employees SET full_name = @name, position = @position, category = @category, is_active = @active " &
            "WHERE employee_id = @id"
        Return Db.ExecuteNonQuery(sql,
            Db.P("@name", e.FullName.Trim()),
            Db.P("@position", e.Position.Trim()),
            Db.P("@category", e.Category),
            Db.P("@active", e.IsActive),
            Db.P("@id", e.EmployeeId)) > 0
    End Function

    ''' <summary>Soft delete.</summary>
    Public Shared Function Deactivate(employeeId As Integer) As Boolean
        Return Db.ExecuteNonQuery("UPDATE employees SET is_active = 0 WHERE employee_id = @id",
                                  Db.P("@id", employeeId)) > 0
    End Function

    ' ---------------- helpers ----------------

    Private Shared Function ToList(table As DataTable) As List(Of Employee)
        Return table.Rows.Cast(Of DataRow)().Select(AddressOf Map).ToList()
    End Function

    Private Shared Function Map(row As DataRow) As Employee
        Return New Employee With {
            .EmployeeId = row.GetInt("employee_id"),
            .BusinessId = row.GetInt("business_id"),
            .FullName = row.GetString("full_name"),
            .Position = row.GetString("position"),
            .Category = row.GetString("category"),
            .IsActive = row.GetBool("is_active"),
            .CreatedAt = row.GetDate("created_at")
        }
    End Function

End Class
