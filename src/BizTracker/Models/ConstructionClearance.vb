''' <summary>One row of the construction_clearances table.</summary>
Public Class ConstructionClearance
    Public Property ClearanceId As Integer
    Public Property ProjectId As Integer
    Public Property ClearanceType As String = ""    ' Locational / FSEC / Building / Occupancy
    Public Property ClearanceNo As String = ""      ' set when Approved
    Public Property Status As String = "Pending"    ' Pending / Approved / Rejected
    Public Property ApprovedBy As Integer?
    Public Property ApprovedAt As Date?
    Public Property Remarks As String = ""

    ' Display only (filled from a JOIN, not saved)
    Public Property ApprovedByName As String = ""
End Class
