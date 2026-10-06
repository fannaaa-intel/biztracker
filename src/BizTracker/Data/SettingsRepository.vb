Imports System.Globalization

''' <summary>
''' Database access for the settings table (rates, warning days, LGU name, ...).
''' Values are cached in memory after the first read; Update() and Reload() refresh the cache.
''' </summary>
Public NotInheritable Class SettingsRepository

    Private Sub New()
    End Sub

    Private Shared _cache As Dictionary(Of String, String)

    Public Shared Function GetAll() As List(Of Setting)
        Dim table = Db.GetDataTable("SELECT setting_key, setting_value, description FROM settings ORDER BY setting_key")
        Return table.Rows.Cast(Of DataRow)().Select(
            Function(row) New Setting With {
                .SettingKey = row.GetString("setting_key"),
                .SettingValue = row.GetString("setting_value"),
                .Description = row.GetString("description")
            }).ToList()
    End Function

    ''' <summary>Re-reads all settings from the database into the cache.</summary>
    Public Shared Sub Reload()
        Dim all = GetAll()
        ' If the database could not be read, don't cache an empty list - try again next time.
        _cache = If(all.Count = 0, Nothing, all.ToDictionary(Function(s) s.SettingKey, Function(s) s.SettingValue))
    End Sub

    ''' <summary>Returns a setting's text value, or defaultValue if the key is missing.</summary>
    Public Shared Function GetValue(key As String, Optional defaultValue As String = "") As String
        If _cache Is Nothing Then Reload()
        Dim value As String = Nothing
        If _cache IsNot Nothing AndAlso _cache.TryGetValue(key, value) Then Return value
        Return defaultValue
    End Function

    Public Shared Function GetInt(key As String, defaultValue As Integer) As Integer
        Dim result As Integer
        If Integer.TryParse(GetValue(key), NumberStyles.Integer, CultureInfo.InvariantCulture, result) Then Return result
        Return defaultValue
    End Function

    ''' <summary>Rates are stored as decimals, e.g. "0.25" for 25%.</summary>
    Public Shared Function GetDecimal(key As String, defaultValue As Decimal) As Decimal
        Dim result As Decimal
        If Decimal.TryParse(GetValue(key), NumberStyles.Number, CultureInfo.InvariantCulture, result) Then Return result
        Return defaultValue
    End Function

    Public Shared Function Update(key As String, value As String) As Boolean
        Dim ok = Db.ExecuteNonQuery("UPDATE settings SET setting_value = @value WHERE setting_key = @key",
                                    Db.P("@value", value.Trim()), Db.P("@key", key)) > 0
        _cache = Nothing   ' force a fresh read next time
        Return ok
    End Function

End Class
