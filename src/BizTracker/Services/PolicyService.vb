Imports System.Globalization
Imports System.IO
Imports System.Net
Imports System.Text
Imports System.Text.RegularExpressions

''' <summary>
''' "Policies & Requirements" of each service (Revision 4D): legal basis, office, requirements, validity,
''' deadlines and penalties, shown read-only from the module's screen.
''' The text lives in Templates/policies/<file>.html. Placeholders are filled from the settings table, so the
''' rates shown are always the rates the system charges:
'''   {{key}}           the raw value             e.g. {{bp_interest_max_months}} -> 36
'''   {{key|pct}}       a rate as a percent       e.g. {{bp_surcharge_rate|pct}}  -> 25%
'''   {{key|monthday}}  an MM-DD date             e.g. {{bp_renewal_deadline|monthday}} -> January 20
''' Every value is HTML-encoded; unknown keys become empty.
''' </summary>
Public NotInheritable Class PolicyService

    Private Sub New()
    End Sub

    ''' <summary>The screens that have a policy page (the six regulatory services).</summary>
    Public Shared Function HasPolicy(screen As AppScreen) As Boolean
        Return GetFileName(screen) IsNot Nothing
    End Function

    ''' <summary>Template file under Templates/policies/ (Nothing = no policy page for this screen).</summary>
    Public Shared Function GetFileName(screen As AppScreen) As String
        Select Case screen
            Case AppScreen.BusinessPermits : Return "business_permit.html"
            Case AppScreen.SanitaryPermits : Return "sanitary_permit.html"
            Case AppScreen.HealthCertificates : Return "health_certificate.html"
            Case AppScreen.RealPropertyTax : Return "real_property_tax.html"
            Case AppScreen.AnnualInspections : Return "annual_inspection.html"
            Case AppScreen.ConstructionPermits : Return "construction_permit.html"
            Case Else : Return Nothing
        End Select
    End Function

    ''' <summary>Window title, e.g. "Business Permit - Policies & Requirements".</summary>
    Public Shared Function GetTitle(screen As AppScreen) As String
        Select Case screen
            Case AppScreen.BusinessPermits : Return ModuleNames.BusinessPermit
            Case AppScreen.SanitaryPermits : Return ModuleNames.SanitaryPermit
            Case AppScreen.HealthCertificates : Return "Health Certificates"
            Case AppScreen.RealPropertyTax : Return ModuleNames.RealPropertyTax
            Case AppScreen.AnnualInspections : Return ModuleNames.AnnualInspection
            Case Else : Return ModuleNames.ConstructionPermit
        End Select
    End Function

    Private Shared Function PolicyFolder() As String
        Return Path.Combine(ReportService.GetTemplateFolder(), "policies")
    End Function

    ''' <summary>The full HTML page of a screen's policies (Nothing when the screen has none or the user may not open it).</summary>
    Public Shared Function BuildHtml(screen As AppScreen) As String
        Dim file = GetFileName(screen)
        If file Is Nothing OrElse Not AccessService.CanAccess(screen) Then Return Nothing
        Dim content = IO.File.ReadAllText(Path.Combine(PolicyFolder(), file), Encoding.UTF8)
        Dim page = IO.File.ReadAllText(Path.Combine(PolicyFolder(), "_policy_layout.html"), Encoding.UTF8).
                   Replace("{{content}}", content).
                   Replace("{{title}}", WebUtility.HtmlEncode(GetTitle(screen)))
        Return FillPlaceholders(page, Function(key) SettingsRepository.GetValue(key))
    End Function

    ''' <summary>Replaces {{key}}, {{key|pct}} and {{key|monthday}} using getValue (encoded; unknown = empty).</summary>
    Public Shared Function FillPlaceholders(html As String, getValue As Func(Of String, String)) As String
        Return Regex.Replace(html, "\{\{(\w+)(?:\|(\w+))?\}\}",
            Function(m)
                Dim raw = If(getValue(m.Groups(1).Value), "")
                Return WebUtility.HtmlEncode(FormatValue(raw, m.Groups(2).Value))
            End Function)
    End Function

    ''' <summary>"0.25" + "pct" -> "25%"; "01-20" + "monthday" -> "January 20"; otherwise the raw value.</summary>
    Public Shared Function FormatValue(raw As String, format As String) As String
        Select Case format
            Case "pct"
                Dim rate As Decimal
                If Not Decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, rate) Then Return raw
                Return (rate * 100D).ToString("0.##", CultureInfo.InvariantCulture) & "%"
            Case "monthday"
                Dim d As Date
                If Not Date.TryParseExact("2000-" & raw.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, d) Then Return raw
                Return d.ToString("MMMM d", CultureInfo.InvariantCulture)
            Case Else
                Return raw
        End Select
    End Function

    ''' <summary>Saves the page as an HTML file and opens it in the default browser (to print or keep). Returns an error or Nothing.</summary>
    Public Shared Function OpenInBrowser(screen As AppScreen) As String
        Dim html = BuildHtml(screen)
        If html Is Nothing Then Return "There is no policy page for this screen."
        Try
            Dim folder = ReportService.GetOutputFolder()
            Directory.CreateDirectory(folder)
            Dim filePath = Path.Combine(folder, "Policies - " & GetTitle(screen) & ".html")
            IO.File.WriteAllText(filePath, html, New UTF8Encoding(True))
            Process.Start(New ProcessStartInfo(filePath) With {.UseShellExecute = True})
        Catch ex As Exception
            Return "The page could not be opened:" & vbCrLf & ex.Message
        End Try
        Return Nothing
    End Function

End Class
