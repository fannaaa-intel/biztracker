''' <summary>One row of the employees table.</summary>
Public Class Employee
    Public Property EmployeeId As Integer
    Public Property BusinessId As Integer
    Public Property FullName As String = ""
    Public Property Position As String = ""
    Public Property Category As String = "Food Handler"   ' Food Handler / Non-Food
    Public Property IsActive As Boolean = True             ' False = separated (no longer working there)
    Public Property DateHired As Date?
    Public Property DateSeparated As Date?                 ' set when separated, cleared when reactivated
    Public Property CreatedAt As Date
End Class
