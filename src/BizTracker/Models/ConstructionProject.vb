''' <summary>One row of the construction_projects table.</summary>
Public Class ConstructionProject
    Public Property ProjectId As Integer
    Public Property BusinessId As Integer
    Public Property ReferenceNo As String = ""      ' CP-YYYY-00001
    Public Property ProjectTitle As String = ""
    ' New Construction / Renovation / Extension / Renovation & Extension / Demolition
    Public Property ProjectType As String = "New Construction"
    ' Locational -> Building Permit -> Construction -> Occupancy -> Completed
    Public Property CurrentStage As String = "Locational"
    Public Property Status As String = "Active"     ' Active / On Hold / Completed / Rejected
    Public Property EstimatedCost As Decimal?
    Public Property DateFiled As Date
    Public Property CreatedAt As Date

    ' Display only (filled from a JOIN, not saved)
    Public Property BusinessName As String = ""
End Class
