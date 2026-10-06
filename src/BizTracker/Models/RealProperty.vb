''' <summary>
''' One row of the properties table.
''' (Named RealProperty because "Property" is a reserved word in VB.NET.)
''' </summary>
Public Class RealProperty
    Public Property PropertyId As Integer
    Public Property BusinessId As Integer
    Public Property Pin As String = ""              ' Property Index Number
    Public Property TdNo As String = ""             ' Tax Declaration No.
    Public Property Location As String = ""
    Public Property PropertyType As String = "Land" ' Land / Building / Machinery
    Public Property Classification As String = "Commercial"   ' Commercial / Residential / Industrial / Agricultural
    Public Property AssessedValue As Decimal
    Public Property CreatedAt As Date

    ' Display only (filled from a JOIN, not saved)
    Public Property BusinessName As String = ""
End Class
