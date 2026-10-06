Imports System.Globalization
Imports System.IO

''' <summary>How a setting's value is checked and shown.</summary>
Public Enum SettingKind
    Text
    WholeNumber
    Rate          ' decimal 0-1, e.g. 0.25 = 25%
    MonthDay      ' MM-DD, e.g. 01-20
    Folder        ' a folder name/path for uploads
End Enum

''' <summary>Description of one settings key: friendly name, kind, allowed range, unit and help text.</summary>
Public Class SettingInfo
    Public Property Key As String = ""
    Public Property Label As String = ""
    Public Property Kind As SettingKind = SettingKind.Text
    Public Property MinValue As Decimal
    Public Property MaxValue As Decimal
    Public Property Unit As String = ""
    Public Property Hint As String = ""
End Class

''' <summary>
''' Settings screen rules (Admin only): rates, deadlines and names are kept in the settings table
''' so they can change without code edits. Every value is validated by its kind before saving,
''' the settings cache is reloaded, and the change (old -> new) is written to audit_log.
''' </summary>
Public NotInheritable Class SettingsService

    Private Sub New()
    End Sub

    Private Const TableName As String = "settings"
    Private Const NotAllowed As String = "Only an Admin can change settings."

    Private Shared ReadOnly Infos As SettingInfo() = {
        New SettingInfo With {.Key = "lgu_name", .Label = "LGU name", .Kind = SettingKind.Text,
                              .Hint = "Printed on every permit and certificate, e.g. Municipality of Aparri."},
        New SettingInfo With {.Key = "province", .Label = "Province", .Kind = SettingKind.Text,
                              .Hint = "Printed under the LGU name, e.g. Cagayan."},
        New SettingInfo With {.Key = "expiry_warning_days", .Label = "Expiring Soon warning", .Kind = SettingKind.WholeNumber,
                              .MinValue = 1, .MaxValue = 365, .Unit = "days",
                              .Hint = "Days before an expiry or due date that a record shows Expiring Soon (1-365)."},
        New SettingInfo With {.Key = "bp_renewal_deadline", .Label = "Business permit renewal deadline", .Kind = SettingKind.MonthDay,
                              .Hint = "Month and day in MM-DD format, e.g. 01-20 for January 20."},
        New SettingInfo With {.Key = "bp_surcharge_rate", .Label = "Late renewal surcharge", .Kind = SettingKind.Rate,
                              .MinValue = 0, .MaxValue = 1, .Hint = "Enter 0.25 or 25% (0-100%)."},
        New SettingInfo With {.Key = "bp_interest_rate_monthly", .Label = "Monthly interest (business tax)", .Kind = SettingKind.Rate,
                              .MinValue = 0, .MaxValue = 1, .Hint = "Enter 0.02 or 2% per month (0-100%)."},
        New SettingInfo With {.Key = "bp_interest_max_months", .Label = "Interest limit", .Kind = SettingKind.WholeNumber,
                              .MinValue = 1, .MaxValue = 120, .Unit = "months", .Hint = "Maximum months of interest (1-120)."},
        New SettingInfo With {.Key = "rpt_basic_rate", .Label = "RPT basic tax rate", .Kind = SettingKind.Rate,
                              .MinValue = 0, .MaxValue = 1, .Hint = "Enter 0.01 or 1% of the assessed value per year."},
        New SettingInfo With {.Key = "rpt_sef_rate", .Label = "RPT SEF rate", .Kind = SettingKind.Rate,
                              .MinValue = 0, .MaxValue = 1, .Hint = "Special Education Fund. Enter 0.01 or 1% per year."},
        New SettingInfo With {.Key = "rpt_penalty_rate_monthly", .Label = "RPT monthly penalty", .Kind = SettingKind.Rate,
                              .MinValue = 0, .MaxValue = 1, .Hint = "Enter 0.02 or 2% per month late (0-100%)."},
        New SettingInfo With {.Key = "rpt_penalty_max", .Label = "RPT penalty limit", .Kind = SettingKind.Rate,
                              .MinValue = 0, .MaxValue = 1, .Hint = "Maximum total penalty. Enter 0.72 or 72%."},
        New SettingInfo With {.Key = "hc_validity_months", .Label = "Health certificate validity", .Kind = SettingKind.WholeNumber,
                              .MinValue = 1, .MaxValue = 60, .Unit = "months", .Hint = "Months a health certificate stays valid (1-60)."},
        New SettingInfo With {.Key = "sp_passing_score", .Label = "Sanitary passing score", .Kind = SettingKind.WholeNumber,
                              .MinValue = 0, .MaxValue = 100, .Unit = "/ 100", .Hint = "Minimum inspection score to issue a sanitary permit (0-100)."},
        New SettingInfo With {.Key = "upload_folder", .Label = "Upload folder", .Kind = SettingKind.Folder,
                              .Hint = "Folder for uploaded requirement files, relative to the app (e.g. uploads)."}
    }

    Public Shared Function CanManage() As Boolean
        Return Session.IsAdmin
    End Function

    ''' <summary>All settings rows (empty for non-admins).</summary>
    Public Shared Function GetSettings() As List(Of Setting)
        If Not CanManage() Then Return New List(Of Setting)
        Return SettingsRepository.GetAll()
    End Function

    ''' <summary>The description of a key (unknown keys are plain required text).</summary>
    Public Shared Function GetInfo(key As String) As SettingInfo
        Return If(Infos.FirstOrDefault(Function(i) i.Key = key),
                  New SettingInfo With {.Key = key, .Label = key, .Kind = SettingKind.Text, .Hint = "Any text (required)."})
    End Function

    ''' <summary>Position of a key in the screen's list (known keys in a logical order, others last).</summary>
    Public Shared Function SortOrder(key As String) As Integer
        Dim i = Array.FindIndex(Infos, Function(x) x.Key = key)
        Return If(i < 0, Infos.Length, i)
    End Function

    ''' <summary>"Whole number", "Rate (0-100%)", "Date (MM-DD)", "Folder name", "Text"</summary>
    Public Shared Function KindText(key As String) As String
        Select Case GetInfo(key).Kind
            Case SettingKind.WholeNumber : Return "Whole number"
            Case SettingKind.Rate : Return "Rate (0-100%)"
            Case SettingKind.MonthDay : Return "Date (MM-DD)"
            Case SettingKind.Folder : Return "Folder name"
            Case Else : Return "Text"
        End Select
    End Function

    ''' <summary>The value as people read it: "0.25 (25%)", "01-20 (January 20)", "30 days".</summary>
    Public Shared Function FormatValue(key As String, value As String) As String
        Dim info = GetInfo(key)
        Select Case info.Kind
            Case SettingKind.Rate
                Dim rate As Decimal
                If Decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, rate) Then
                    Return value & " (" & (rate * 100D).ToString("0.##", CultureInfo.InvariantCulture) & "%)"
                End If
            Case SettingKind.MonthDay
                Dim d As Date
                If Date.TryParseExact("2001-" & value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, d) Then
                    Return value & " (" & d.ToString("MMMM d", CultureInfo.InvariantCulture) & ")"
                End If
            Case SettingKind.WholeNumber
                If info.Unit <> "" Then Return value & " " & info.Unit
        End Select
        Return value
    End Function

    ''' <summary>
    ''' Checks a new value for a key. Returns an error message, or Nothing when valid;
    ''' normalized receives the value to store (e.g. "25%" -> "0.25", "1-5" -> "01-05").
    ''' </summary>
    Public Shared Function ValidateValue(key As String, input As String, ByRef normalized As String) As String
        Dim info = GetInfo(key)
        Dim text = If(input, "").Trim()
        normalized = text
        If text = "" Then Return "Enter a value."
        If text.Length > 255 Then Return "At most 255 characters."

        Select Case info.Kind
            Case SettingKind.WholeNumber
                Dim n As Integer
                If Not Integer.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, n) Then Return "Enter a whole number."
                If n < info.MinValue OrElse n > info.MaxValue Then Return "Enter a number from " & info.MinValue & " to " & info.MaxValue & "."
                normalized = n.ToString(CultureInfo.InvariantCulture)

            Case SettingKind.Rate
                Dim isPercent = text.EndsWith("%")
                Dim rate As Decimal
                If Not Decimal.TryParse(text.TrimEnd("%"c).Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, rate) Then
                    Return "Enter a rate such as 0.25 or 25%."
                End If
                If isPercent Then rate /= 100D
                If rate < info.MinValue OrElse rate > info.MaxValue Then Return "The rate must be from 0 to 1 (0% to 100%)."
                normalized = rate.ToString("0.####", CultureInfo.InvariantCulture)

            Case SettingKind.MonthDay
                Dim parts = text.Split("-"c)
                Dim m, d As Integer
                If parts.Length <> 2 OrElse Not Integer.TryParse(parts(0), m) OrElse Not Integer.TryParse(parts(1), d) OrElse
                   m < 1 OrElse m > 12 OrElse d < 1 OrElse d > Date.DaysInMonth(2001, m) Then
                    Return "Enter a valid date as MM-DD, e.g. 01-20."
                End If
                normalized = m.ToString("00") & "-" & d.ToString("00")

            Case SettingKind.Folder
                If text.IndexOfAny(Path.GetInvalidPathChars()) >= 0 OrElse text.IndexOfAny({"*"c, "?"c, """"c, "<"c, ">"c, "|"c}) >= 0 Then
                    Return "The folder name contains characters that are not allowed."
                End If
        End Select
        Return Nothing
    End Function

    ''' <summary>Saves a new value (validated, cache reloaded, audited). Returns an error message, or Nothing when saved.</summary>
    Public Shared Function UpdateSetting(key As String, input As String) As String
        If Not CanManage() Then Return NotAllowed
        Dim current = SettingsRepository.GetAll().FirstOrDefault(Function(s) s.SettingKey = key)
        If current Is Nothing Then Return "This setting no longer exists."
        Dim normalized As String = Nothing
        Dim problem = ValidateValue(key, input, normalized)
        If problem IsNot Nothing Then Return problem
        If normalized = current.SettingValue Then Return Nothing          ' nothing changed
        If Not SettingsRepository.Update(key, normalized) Then Return "The setting could not be saved. Please try again."
        SettingsRepository.Reload()
        AuditService.Log(AuditActions.Update, TableName, Nothing, key & ": " & current.SettingValue & " -> " & normalized)
        Return Nothing
    End Function

End Class
