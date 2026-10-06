''' <summary>
''' Database access for the businesses table.
''' Deleting a business is a SOFT delete (is_active = 0).
''' </summary>
Public NotInheritable Class BusinessRepository

    Private Sub New()
    End Sub

    Private Const SelectSql As String = "SELECT * FROM businesses "

    ''' <summary>All businesses, sorted by name. Inactive ones only if asked.</summary>
    Public Shared Function GetAll(Optional includeInactive As Boolean = False) As List(Of Business)
        Dim sql = SelectSql &
                  If(includeInactive, "", "WHERE is_active = 1 ") &
                  "ORDER BY business_name"
        Return ToList(Db.GetDataTable(sql))
    End Function

    Public Shared Function GetById(businessId As Integer) As Business
        Dim sql = SelectSql & "WHERE business_id = @id"
        Return ToList(Db.GetDataTable(sql, Db.P("@id", businessId))).FirstOrDefault()
    End Function

    ''' <summary>Inserts a business and returns its new business_id (-1 on error).</summary>
    Public Shared Function Insert(b As Business) As Integer
        Const sql As String =
            "INSERT INTO businesses (business_name, owner_name, address, barangay, business_type, " &
            "  line_of_business, is_food_business, dti_sec_no, tin, contact_no, email, is_active) " &
            "VALUES (@name, @owner, @address, @barangay, @type, @line, @food, @dti, @tin, @contact, @email, @active)"
        Return Db.ExecuteInsert(sql, ToParameters(b))
    End Function

    Public Shared Function Update(b As Business) As Boolean
        Const sql As String =
            "UPDATE businesses SET business_name = @name, owner_name = @owner, address = @address, " &
            "  barangay = @barangay, business_type = @type, line_of_business = @line, " &
            "  is_food_business = @food, dti_sec_no = @dti, tin = @tin, contact_no = @contact, " &
            "  email = @email, is_active = @active " &
            "WHERE business_id = @id"
        Dim parameters = ToParameters(b).Append(Db.P("@id", b.BusinessId)).ToArray()
        Return Db.ExecuteNonQuery(sql, parameters) > 0
    End Function

    ''' <summary>Soft delete: the business is hidden but its history stays.</summary>
    Public Shared Function SoftDelete(businessId As Integer) As Boolean
        Return Db.ExecuteNonQuery("UPDATE businesses SET is_active = 0 WHERE business_id = @id",
                                  Db.P("@id", businessId)) > 0
    End Function

    ' ---------------- helpers ----------------

    Private Shared Function ToParameters(b As Business) As MySql.Data.MySqlClient.MySqlParameter()
        Return {
            Db.P("@name", b.BusinessName.Trim()),
            Db.P("@owner", b.OwnerName.Trim()),
            Db.P("@address", b.Address.Trim()),
            Db.P("@barangay", b.Barangay.Trim()),
            Db.P("@type", b.BusinessType),
            Db.P("@line", b.LineOfBusiness.Trim()),
            Db.P("@food", b.IsFoodBusiness),
            Db.P("@dti", Db.NullIfEmpty(b.DtiSecNo)),
            Db.P("@tin", Db.NullIfEmpty(b.Tin)),
            Db.P("@contact", Db.NullIfEmpty(b.ContactNo)),
            Db.P("@email", Db.NullIfEmpty(b.Email)),
            Db.P("@active", b.IsActive)
        }
    End Function

    Private Shared Function ToList(table As DataTable) As List(Of Business)
        Return table.Rows.Cast(Of DataRow)().Select(AddressOf Map).ToList()
    End Function

    Private Shared Function Map(row As DataRow) As Business
        Return New Business With {
            .BusinessId = row.GetInt("business_id"),
            .BusinessName = row.GetString("business_name"),
            .OwnerName = row.GetString("owner_name"),
            .Address = row.GetString("address"),
            .Barangay = row.GetString("barangay"),
            .BusinessType = row.GetString("business_type"),
            .LineOfBusiness = row.GetString("line_of_business"),
            .IsFoodBusiness = row.GetBool("is_food_business"),
            .DtiSecNo = row.GetString("dti_sec_no"),
            .Tin = row.GetString("tin"),
            .ContactNo = row.GetString("contact_no"),
            .Email = row.GetString("email"),
            .IsActive = row.GetBool("is_active"),
            .CreatedAt = row.GetDate("created_at")
        }
    End Function

End Class
