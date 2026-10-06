''' <summary>
''' Finds the highest sequence number already used for a reference-number prefix and year.
''' Used only by ReferenceNoService.
''' </summary>
Public NotInheritable Class ReferenceNoRepository

    Private Sub New()
    End Sub

    ''' <summary>
    ''' Returns the last sequence used, e.g. 3 if "BP-2026-00003" is the highest BP number for 2026.
    ''' Returns 0 if none exist yet.
    ''' </summary>
    Public Shared Function GetLastSequence(prefix As String, year As Integer) As Integer
        ' Table and column names cannot be SQL parameters, so they come from this fixed
        ' list in code (never from user input). The prefix/year value IS a parameter.
        Dim tableName As String
        Dim columnName As String
        Select Case prefix
            Case "BP" : tableName = "business_permits" : columnName = "reference_no"
            Case "MP" : tableName = "business_permits" : columnName = "mayors_permit_no"
            Case "SP" : tableName = "sanitary_permits" : columnName = "permit_no"
            Case "HC" : tableName = "health_certificates" : columnName = "certificate_no"
            Case "INS" : tableName = "inspections" : columnName = "reference_no"
            Case "AIC" : tableName = "inspections" : columnName = "certificate_no"
            Case "CP" : tableName = "construction_projects" : columnName = "reference_no"
            Case "LC", "FSEC", "BLDG", "OCC" : tableName = "construction_clearances" : columnName = "clearance_no"
            Case "OR" : tableName = "rpt_payments" : columnName = "or_no"
            Case Else
                Throw New ArgumentException("Unknown reference number prefix: " & prefix)
        End Select

        Dim sql = "SELECT COALESCE(MAX(CAST(SUBSTRING_INDEX(" & columnName & ", '-', -1) AS UNSIGNED)), 0) " &
                  "FROM " & tableName & " WHERE " & columnName & " LIKE @pattern"
        Dim result = Db.ExecuteScalar(sql, Db.P("@pattern", prefix & "-" & year & "-%"))
        If result Is Nothing Then Return 0
        Return Convert.ToInt32(result)
    End Function

End Class
