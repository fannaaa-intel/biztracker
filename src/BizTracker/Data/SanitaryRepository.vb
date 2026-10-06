Imports MySql.Data.MySqlClient

''' <summary>Database access for the sanitary_permits table.</summary>
Public NotInheritable Class SanitaryRepository

    Private Sub New()
    End Sub

    Private Const SelectSql As String =
        "SELECT sp.*, b.business_name FROM sanitary_permits sp " &
        "JOIN businesses b ON b.business_id = sp.business_id "

    Private Const OrderSql As String = " ORDER BY sp.permit_year DESC, sp.date_filed DESC, sp.sanitary_id DESC"

    Public Shared Function GetAll() As List(Of SanitaryPermit)
        Return ToList(Db.GetDataTable(SelectSql & "WHERE b.is_active = 1" & OrderSql))
    End Function

    Public Shared Function GetById(sanitaryId As Integer) As SanitaryPermit
        Return ToList(Db.GetDataTable(SelectSql & "WHERE sp.sanitary_id = @id", Db.P("@id", sanitaryId))).FirstOrDefault()
    End Function

    Public Shared Function GetByBusinessId(businessId As Integer) As List(Of SanitaryPermit)
        Return ToList(Db.GetDataTable(SelectSql & "WHERE sp.business_id = @bid" & OrderSql, Db.P("@bid", businessId)))
    End Function

    ''' <summary>Returns the new sanitary_id, or -1 on error.</summary>
    Public Shared Function Insert(s As SanitaryPermit) As Integer
        Const sql As String =
            "INSERT INTO sanitary_permits (business_id, permit_no, permit_year, category, status, inspection_date, " &
            "  inspection_score, inspector_name, findings, date_filed, date_issued, valid_until) " &
            "VALUES (@bid, @no, @year, @category, @status, @inspDate, @score, @inspector, @findings, " &
            "  @filed, @issued, @valid)"
        Return Db.ExecuteInsert(sql, ToParameters(s).Concat({
                                    Db.P("@bid", s.BusinessId), Db.P("@no", s.PermitNo)}).ToArray())
    End Function

    ''' <summary>Updates every field except business and permit number.</summary>
    Public Shared Function Update(s As SanitaryPermit) As Boolean
        Const sql As String =
            "UPDATE sanitary_permits SET permit_year = @year, category = @category, status = @status, " &
            "  inspection_date = @inspDate, inspection_score = @score, inspector_name = @inspector, " &
            "  findings = @findings, date_filed = @filed, date_issued = @issued, valid_until = @valid " &
            "WHERE sanitary_id = @id"
        Return Db.ExecuteNonQuery(sql, ToParameters(s).Append(Db.P("@id", s.SanitaryId)).ToArray()) > 0
    End Function

    Public Shared Function UpdateStatus(sanitaryId As Integer, newStatus As String) As Boolean
        Return Db.ExecuteNonQuery("UPDATE sanitary_permits SET status = @status WHERE sanitary_id = @id",
                                  Db.P("@status", newStatus), Db.P("@id", sanitaryId)) > 0
    End Function

    ' ---------------- helpers ----------------

    Private Shared Function ToParameters(s As SanitaryPermit) As MySqlParameter()
        Return {
            Db.P("@year", s.PermitYear),
            Db.P("@category", s.Category),
            Db.P("@status", s.Status),
            Db.P("@inspDate", s.InspectionDate),
            Db.P("@score", s.InspectionScore),
            Db.P("@inspector", Db.NullIfEmpty(s.InspectorName)),
            Db.P("@findings", Db.NullIfEmpty(s.Findings)),
            Db.P("@filed", s.DateFiled.Date),
            Db.P("@issued", s.DateIssued),
            Db.P("@valid", s.ValidUntil)
        }
    End Function

    Private Shared Function ToList(table As DataTable) As List(Of SanitaryPermit)
        Return table.Rows.Cast(Of DataRow)().Select(AddressOf Map).ToList()
    End Function

    Private Shared Function Map(row As DataRow) As SanitaryPermit
        Return New SanitaryPermit With {
            .SanitaryId = row.GetInt("sanitary_id"),
            .BusinessId = row.GetInt("business_id"),
            .PermitNo = row.GetString("permit_no"),
            .PermitYear = row.GetInt("permit_year"),
            .Category = row.GetString("category"),
            .Status = row.GetString("status"),
            .InspectionDate = row.GetNullableDate("inspection_date"),
            .InspectionScore = row.GetNullableInt("inspection_score"),
            .InspectorName = row.GetString("inspector_name"),
            .Findings = row.GetString("findings"),
            .DateFiled = row.GetDate("date_filed"),
            .DateIssued = row.GetNullableDate("date_issued"),
            .ValidUntil = row.GetNullableDate("valid_until"),
            .CreatedAt = row.GetDate("created_at"),
            .BusinessName = row.GetString("business_name")
        }
    End Function

End Class
