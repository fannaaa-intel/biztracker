Imports System.Reflection

''' <summary>
''' Fills the SettingsRepository cache with the seeded rates (database/biztracker_db.sql), so the
''' tests never open a database connection and always use the same numbers.
''' </summary>
Public Module TestSettings

    Public Sub UseSeededRates()
        Dim rates As New Dictionary(Of String, String) From {
            {"bp_renewal_deadline", "01-20"},
            {"bp_surcharge_rate", "0.25"},
            {"bp_interest_rate_monthly", "0.02"},
            {"bp_interest_max_months", "36"},
            {"rpt_basic_rate", "0.01"},
            {"rpt_sef_rate", "0.01"},
            {"rpt_penalty_rate_monthly", "0.02"},
            {"rpt_penalty_max", "0.72"},
            {"expiry_warning_days", "30"},
            {"arta_simple_days", "3"},
            {"arta_complex_days", "7"},
            {"arta_technical_days", "20"}
        }
        GetType(SettingsRepository).GetField("_cache", BindingFlags.NonPublic Or BindingFlags.Static).SetValue(Nothing, rates)
    End Sub

End Module
