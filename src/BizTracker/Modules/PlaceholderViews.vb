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
