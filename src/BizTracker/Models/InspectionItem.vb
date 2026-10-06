''' <summary>One row of the inspection_items table (one department's result for an inspection).</summary>
Public Class InspectionItem
    Public Property ItemId As Integer
    Public Property InspectionId As Integer
    Public Property Department As String = ""       ' Structural / Electrical / Mechanical / Fire
    Public Property Result As String = "Pending"    ' Pending / Passed / Failed
    Public Property Findings As String = ""
    Public Property InspectorName As String = ""
    Public Property InspectedAt As Date?
End Class
