Imports System.IO
Imports System.Text.RegularExpressions
Imports Xunit

''' <summary>Policies & Requirements pages (Revision 4D): placeholders are filled from the settings.</summary>
Public Class PolicyServiceTests

    <Theory>
    <InlineData("0.25", "pct", "25%")>
    <InlineData("0.02", "pct", "2%")>
    <InlineData("0.725", "pct", "72.5%")>
    <InlineData("01-20", "monthday", "January 20")>
    <InlineData("36", "", "36")>
    <InlineData("abc", "pct", "abc")>          ' not a number: shown as is
    Public Sub FormatValue(raw As String, format As String, expected As String)
        Assert.Equal(expected, PolicyService.FormatValue(raw, format))
    End Sub

    <Fact>
    Public Sub FillPlaceholders_EncodesValues_AndEmptiesUnknownKeys()
        Dim values As New Dictionary(Of String, String) From {{"bp_surcharge_rate", "0.25"}, {"lgu_name", "A & B"}}
        Dim html = PolicyService.FillPlaceholders("{{lgu_name}}: {{bp_surcharge_rate|pct}} [{{missing}}]",
                                                  Function(k) If(values.ContainsKey(k), values(k), Nothing))
        Assert.Equal("A &amp; B: 25% []", html)
    End Sub

    ''' <summary>The repository folder (tests run from tests/BizTracker.Tests/bin/...).</summary>
    Private Shared Function RepoRoot() As String
        Dim dir = New DirectoryInfo(AppContext.BaseDirectory)
        While dir IsNot Nothing AndAlso Not File.Exists(Path.Combine(dir.FullName, "CLAUDE.md"))
            dir = dir.Parent
        End While
        Return dir?.FullName
    End Function

    <Fact>
    Public Sub EveryPlaceholder_IsASeededSetting()
        Dim root = RepoRoot()
        Assert.NotNull(root)
        Dim seed = File.ReadAllText(Path.Combine(root, "database", "biztracker_db.sql"))
        Dim folder = Path.Combine(root, "src", "BizTracker", "Templates", "policies")
        Dim screens = {AppScreen.BusinessPermits, AppScreen.SanitaryPermits, AppScreen.HealthCertificates,
                       AppScreen.RealPropertyTax, AppScreen.AnnualInspections, AppScreen.ConstructionPermits}
        For Each screen In screens
            Assert.True(PolicyService.HasPolicy(screen))
            Dim text = File.ReadAllText(Path.Combine(folder, PolicyService.GetFileName(screen)))
            For Each m As Match In Regex.Matches(text, "\{\{(\w+)(?:\|(\w+))?\}\}")
                Assert.True(seed.Contains("('" & m.Groups(1).Value & "'"),
                            PolicyService.GetFileName(screen) & " uses {{" & m.Groups(1).Value & "}}, which is not a seeded setting")
            Next
        Next
        Assert.False(PolicyService.HasPolicy(AppScreen.Dashboard))
    End Sub

End Class
