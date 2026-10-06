''' <summary>Database access for the properties table (real property for RPT).</summary>
Public NotInheritable Class PropertyRepository

    Private Sub New()
    End Sub

    Private Const SelectSql As String =
        "SELECT p.*, b.business_name FROM properties p " &
        "JOIN businesses b ON b.business_id = p.business_id "

    Public Shared Function GetAll() As List(Of RealProperty)
        Return ToList(Db.GetDataTable(SelectSql & "WHERE b.is_active = 1 ORDER BY b.business_name, p.pin"))
    End Function

    Public Shared Function GetById(propertyId As Integer) As RealProperty
        Return ToList(Db.GetDataTable(SelectSql & "WHERE p.property_id = @id", Db.P("@id", propertyId))).FirstOrDefault()
    End Function

    Public Shared Function GetByBusinessId(businessId As Integer) As List(Of RealProperty)
        Return ToList(Db.GetDataTable(SelectSql & "WHERE p.business_id = @bid ORDER BY p.pin", Db.P("@bid", businessId)))
    End Function

    ''' <summary>Returns the new property_id, or -1 on error (e.g. duplicate PIN / TD No.).</summary>
    Public Shared Function Insert(p As RealProperty) As Integer
        Const sql As String =
            "INSERT INTO properties (business_id, pin, td_no, location, property_type, classification, assessed_value) " &
            "VALUES (@bid, @pin, @td, @location, @type, @class, @value)"
        Return Db.ExecuteInsert(sql,
            Db.P("@bid", p.BusinessId),
            Db.P("@pin", p.Pin.Trim()),
            Db.P("@td", p.TdNo.Trim()),
            Db.P("@location", p.Location.Trim()),
            Db.P("@type", p.PropertyType),
            Db.P("@class", p.Classification),
            Db.P("@value", p.AssessedValue))
    End Function

    Public Shared Function Update(p As RealProperty) As Boolean
        Const sql As String =
            "UPDATE properties SET pin = @pin, td_no = @td, location = @location, property_type = @type, " &
            "  classification = @class, assessed_value = @value " &
            "WHERE property_id = @id"
        Return Db.ExecuteNonQuery(sql,
            Db.P("@pin", p.Pin.Trim()),
            Db.P("@td", p.TdNo.Trim()),
            Db.P("@location", p.Location.Trim()),
            Db.P("@type", p.PropertyType),
            Db.P("@class", p.Classification),
            Db.P("@value", p.AssessedValue),
            Db.P("@id", p.PropertyId)) > 0
    End Function

    ''' <summary>True if another property already uses this PIN (ignorePropertyId = the one being edited).</summary>
    Public Shared Function PinExists(pin As String, Optional ignorePropertyId As Integer = 0) As Boolean
        Dim count = Db.ExecuteScalar("SELECT COUNT(*) FROM properties WHERE pin = @pin AND property_id <> @id",
                                     Db.P("@pin", pin.Trim()), Db.P("@id", ignorePropertyId))
        Return count IsNot Nothing AndAlso Convert.ToInt32(count) > 0
    End Function

    ''' <summary>True if another property already uses this Tax Declaration No.</summary>
    Public Shared Function TdNoExists(tdNo As String, Optional ignorePropertyId As Integer = 0) As Boolean
        Dim count = Db.ExecuteScalar("SELECT COUNT(*) FROM properties WHERE td_no = @td AND property_id <> @id",
                                     Db.P("@td", tdNo.Trim()), Db.P("@id", ignorePropertyId))
        Return count IsNot Nothing AndAlso Convert.ToInt32(count) > 0
    End Function

    ' ---------------- helpers ----------------

    Private Shared Function ToList(table As DataTable) As List(Of RealProperty)
        Return table.Rows.Cast(Of DataRow)().Select(AddressOf Map).ToList()
    End Function

    Private Shared Function Map(row As DataRow) As RealProperty
        Return New RealProperty With {
            .PropertyId = row.GetInt("property_id"),
            .BusinessId = row.GetInt("business_id"),
            .Pin = row.GetString("pin"),
            .TdNo = row.GetString("td_no"),
            .Location = row.GetString("location"),
            .PropertyType = row.GetString("property_type"),
            .Classification = row.GetString("classification"),
            .AssessedValue = row.GetDecimal("assessed_value"),
            .CreatedAt = row.GetDate("created_at"),
            .BusinessName = row.GetString("business_name")
        }
    End Function

End Class
