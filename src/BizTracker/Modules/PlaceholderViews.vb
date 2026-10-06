' Placeholder pages, one per sidebar item. Each phase replaces its placeholder with the real
' module (and moves it to its own file, e.g. Modules/BusinessPermitView.vb).

Public Class DashboardView
    Inherits ModuleView
    Public Sub New()
        ShowPlaceholder(Icons.Home, "Welcome, " & Session.FullName,
                        "Your compliance overview across all six services will appear here: " &
                        "items that need attention, live status per module, and quick links.",
                        "Coming in Phase 11")
    End Sub
End Class

Public Class SanitaryPermitView
    Inherits ModuleView
    Public Sub New()
        ShowPlaceholder(Icons.CheckShield, "Sanitary Permits",
                        "Applications, laboratory prerequisites, on-site inspection scores and " &
                        "issuance of sanitary permits (valid until December 31).",
                        "Coming in Phase 7")
    End Sub
End Class

Public Class RealPropertyTaxView
    Inherits ModuleView
    Public Sub New()
        ShowPlaceholder(Icons.Bank, "Real Property Tax",
                        "Property ledger, yearly assessments (basic + SEF), quarterly payments " &
                        "with official receipts, penalties and tax clearance.",
                        "Coming in Phase 8")
    End Sub
End Class

Public Class HealthCertificateView
    Inherits ModuleView
    Public Sub New()
        ShowPlaceholder(Icons.Health, "Health Certificates",
                        "Employee health certificates with live Valid / Expiring Soon / Expired " &
                        "status, compliance rate and one-click renewals.",
                        "Coming in Phase 6")
    End Sub
End Class

Public Class AnnualInspectionView
    Inherits ModuleView
    Public Sub New()
        ShowPlaceholder(Icons.Search, "Annual Inspections",
                        "Joint inspection schedule and the Structural, Electrical, Mechanical and " &
                        "Fire checklist, with re-inspection and certificate issuance.",
                        "Coming in Phase 9")
    End Sub
End Class

Public Class ConstructionPermitView
    Inherits ModuleView
    Public Sub New()
        ShowPlaceholder(Icons.Repair, "Construction Permit",
                        "Project pipeline from Locational/Zoning to Building Permit, Construction " &
                        "and Occupancy, with clearances and technical documents.",
                        "Coming in Phase 10")
    End Sub
End Class

Public Class SettingsView
    Inherits ModuleView
    Public Sub New()
        ShowPlaceholder(Icons.Settings, "Settings",
                        "User management, business registry, rates and deadlines, audit log " &
                        "viewer and database backup.",
                        "Coming in Phase 14")
    End Sub
End Class
