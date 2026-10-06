''' <summary>One row of the employees table.</summary>
Public Class Employee
    Public Property EmployeeId As Integer
    Public Property BusinessId As Integer
    Public Property FullName As String = ""
    Public Property Position As String = ""
    Public Property Category As String = "Food Handler"   ' Food Handler / Non-Food
    Public Property IsActive As Boolean = True
    Public Property CreatedAt As Date
End Class
