' Exact ENUM strings from database/biztracker_db.sql, so code never misspells them.

''' <summary>users.role values.</summary>
Public NotInheritable Class Roles
    Private Sub New()
    End Sub
    Public Const Admin As String = "Admin"
    Public Const BPLO As String = "BPLO"
    Public Const Health As String = "Health"
    Public Const Assessor As String = "Assessor"
    Public Const Building As String = "Building"
    Public Const Inspector As String = "Inspector"
    Public Const Owner As String = "Owner"

    Public Shared ReadOnly All As String() = {Admin, BPLO, Health, Assessor, Building, Inspector, Owner}
End Class

''' <summary>requirements.module / notifications.module values.</summary>
Public NotInheritable Class ModuleNames
    Private Sub New()
    End Sub
    Public Const BusinessPermit As String = "Business Permit"
    Public Const SanitaryPermit As String = "Sanitary Permit"
    Public Const HealthCertificate As String = "Health Certificate"
    Public Const RealPropertyTax As String = "Real Property Tax"
    Public Const AnnualInspection As String = "Annual Inspection"
    Public Const ConstructionPermit As String = "Construction Permit"

    Public Shared ReadOnly All As String() =
        {BusinessPermit, SanitaryPermit, HealthCertificate, RealPropertyTax, AnnualInspection, ConstructionPermit}
End Class

''' <summary>audit_log.action values.</summary>
Public NotInheritable Class AuditActions
    Private Sub New()
    End Sub
    Public Const Login As String = "LOGIN"
    Public Const Logout As String = "LOGOUT"
    Public Const Insert As String = "INSERT"
    Public Const Update As String = "UPDATE"
    Public Const Delete As String = "DELETE"
    Public Const StatusChange As String = "STATUS_CHANGE"
    Public Const Upload As String = "UPLOAD"
    Public Const Print As String = "PRINT"
End Class

''' <summary>clearance_endorsements.office values (one row per office per business permit).</summary>
Public NotInheritable Class EndorsementOffices
    Private Sub New()
    End Sub
    Public Const Barangay As String = "Barangay"
    Public Const Sanitary As String = "Sanitary"
    Public Const RPT As String = "RPT"
    Public Const Fire As String = "Fire"
    Public Const Zoning As String = "Zoning"

    Public Shared ReadOnly All As String() = {Barangay, Sanitary, RPT, Fire, Zoning}
End Class

''' <summary>inspection_items.department values (one row per department per inspection).</summary>
Public NotInheritable Class InspectionDepartments
    Private Sub New()
    End Sub
    Public Shared ReadOnly All As String() = {"Structural", "Electrical", "Mechanical", "Fire"}
End Class

''' <summary>construction_clearances.clearance_type values (one row per type per project).</summary>
Public NotInheritable Class ClearanceTypes
    Private Sub New()
    End Sub
    Public Const Locational As String = "Locational"
    Public Const FSEC As String = "FSEC"
    Public Const Building As String = "Building"
    Public Const Occupancy As String = "Occupancy"

    Public Shared ReadOnly All As String() = {Locational, FSEC, Building, Occupancy}
End Class
