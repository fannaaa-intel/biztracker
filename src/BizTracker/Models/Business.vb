''' <summary>One row of the businesses table.</summary>
Public Class Business
    Public Property BusinessId As Integer
    Public Property BusinessName As String = ""
    Public Property OwnerName As String = ""
    Public Property Address As String = ""
    Public Property Barangay As String = ""
    Public Property BusinessType As String = "Sole Proprietorship"   ' Sole Proprietorship / Partnership / Corporation / Cooperative
    Public Property LineOfBusiness As String = ""
    Public Property IsFoodBusiness As Boolean
    Public Property DtiSecNo As String = ""
    Public Property Tin As String = ""
    Public Property ContactNo As String = ""
    Public Property Email As String = ""
    Public Property IsActive As Boolean = True
    Public Property CreatedAt As Date

    ''' <summary>Shown in combo boxes.</summary>
    Public Overrides Function ToString() As String
        Return BusinessName
    End Function
End Class
