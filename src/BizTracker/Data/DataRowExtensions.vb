Imports System.Runtime.CompilerServices

''' <summary>
''' Null-safe helpers for reading DataRow values in repositories.
''' Example: row.GetInt("business_id"), row.GetNullableDate("date_issued").
''' SQL NULL becomes Nothing (for nullable types) or a safe default.
''' </summary>
Public Module DataRowExtensions

    <Extension()>
    Public Function GetInt(row As DataRow, column As String) As Integer
        If row.IsNull(column) Then Return 0
        Return Convert.ToInt32(row(column))
    End Function

    <Extension()>
    Public Function GetNullableInt(row As DataRow, column As String) As Integer?
        If row.IsNull(column) Then Return Nothing
        Return Convert.ToInt32(row(column))
    End Function

    <Extension()>
    Public Function GetString(row As DataRow, column As String) As String
        If row.IsNull(column) Then Return ""
        Return row(column).ToString()
    End Function

    <Extension()>
    Public Function GetDecimal(row As DataRow, column As String) As Decimal
        If row.IsNull(column) Then Return 0D
        Return Convert.ToDecimal(row(column))
    End Function

    <Extension()>
    Public Function GetNullableDecimal(row As DataRow, column As String) As Decimal?
        If row.IsNull(column) Then Return Nothing
        Return Convert.ToDecimal(row(column))
    End Function

    <Extension()>
    Public Function GetDate(row As DataRow, column As String) As Date
        If row.IsNull(column) Then Return Date.MinValue
        Return Convert.ToDateTime(row(column))
    End Function

    <Extension()>
    Public Function GetNullableDate(row As DataRow, column As String) As Date?
        If row.IsNull(column) Then Return Nothing
        Return Convert.ToDateTime(row(column))
    End Function

    ''' <summary>TINYINT(1) columns (is_active, is_read, ...) come back as Boolean.</summary>
    <Extension()>
    Public Function GetBool(row As DataRow, column As String) As Boolean
        If row.IsNull(column) Then Return False
        Return Convert.ToBoolean(row(column))
    End Function

End Module
