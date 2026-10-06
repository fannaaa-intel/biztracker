' Placeholder pages, one per sidebar item. Each phase replaces its placeholder with the real
' module (and moves it to its own file, e.g. Modules/BusinessPermitView.vb).

Public Class SettingsView
    Inherits ModuleView
    Public Sub New()
        ShowPlaceholder(Icons.Settings, "Settings",
                        "User management, business registry, rates and deadlines, audit log " &
                        "viewer and database backup.",
                        "Coming in Phase 14")
    End Sub
End Class
