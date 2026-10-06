''' <summary>
''' All colors, fonts and icons used by the app, in one place (matches the Figma prototype).
''' Forms and UserControls should use these instead of hard-coding colors.
''' </summary>
Public NotInheritable Class Theme

    Private Sub New()
    End Sub

    ' ---------- Layout colors ----------
    Public Shared ReadOnly SidebarBlue As Color = ColorTranslator.FromHtml("#0B6E99")
    Public Shared ReadOnly SidebarBlueDark As Color = ColorTranslator.FromHtml("#074A68")     ' gradients
    Public Shared ReadOnly SidebarHover As Color = ColorTranslator.FromHtml("#1580AE")
    Public Shared ReadOnly SidebarActive As Color = ColorTranslator.FromHtml("#085A7E")
    Public Shared ReadOnly SidebarTextMuted As Color = ColorTranslator.FromHtml("#B9DCEC")
    Public Shared ReadOnly ContentBackground As Color = ColorTranslator.FromHtml("#7FBFE0")
    Public Shared ReadOnly CardBackground As Color = Color.White
    Public Shared ReadOnly CardCornerRadius As Integer = 14
    Public Shared ReadOnly TopBarBackground As Color = Color.White
    Public Shared ReadOnly Divider As Color = ColorTranslator.FromHtml("#E5E7EB")
    Public Shared ReadOnly SoftBackground As Color = ColorTranslator.FromHtml("#F3F7FA")       ' hover on white
    Public Shared ReadOnly AccentSoft As Color = ColorTranslator.FromHtml("#E1F1F8")          ' light blue chips/icons

    ' ---------- Text colors ----------
    Public Shared ReadOnly TextDark As Color = ColorTranslator.FromHtml("#1F2937")
    Public Shared ReadOnly TextMuted As Color = ColorTranslator.FromHtml("#6B7280")
    Public Shared ReadOnly TextOnBlue As Color = Color.White

    ' ---------- Inputs ----------
    Public Shared ReadOnly InputBorder As Color = ColorTranslator.FromHtml("#D1D5DB")
    Public Shared ReadOnly InputFocusBorder As Color = SidebarBlue

    ' ---------- Buttons ----------
    Public Shared ReadOnly ButtonPrimary As Color = SidebarBlue
    Public Shared ReadOnly ButtonPrimaryHover As Color = SidebarHover
    Public Shared ReadOnly ButtonPrimaryPressed As Color = SidebarActive
    Public Shared ReadOnly ButtonPrimaryText As Color = Color.White

    ' ---------- Status colors ----------
    ' Green = Valid / Approved / Paid
    Public Shared ReadOnly StatusGreen As Color = ColorTranslator.FromHtml("#16A34A")
    ' Amber = Pending / Expiring Soon
    Public Shared ReadOnly StatusAmber As Color = ColorTranslator.FromHtml("#D97706")
    ' Red = Expired / Overdue / Rejected
    Public Shared ReadOnly StatusRed As Color = ColorTranslator.FromHtml("#DC2626")
    ' Gray = anything else (e.g. Draft, Not Applicable)
    Public Shared ReadOnly StatusGray As Color = ColorTranslator.FromHtml("#6B7280")

    ' Light backgrounds for status badges (pills)
    Public Shared ReadOnly StatusGreenSoft As Color = ColorTranslator.FromHtml("#DCFCE7")
    Public Shared ReadOnly StatusAmberSoft As Color = ColorTranslator.FromHtml("#FEF3C7")
    Public Shared ReadOnly StatusRedSoft As Color = ColorTranslator.FromHtml("#FEE2E2")
    Public Shared ReadOnly StatusGraySoft As Color = ColorTranslator.FromHtml("#F3F4F6")

    ' ---------- Fonts ----------
    Public Const FontFamilyName As String = "Segoe UI"
    Public Const IconFontName As String = "Segoe MDL2 Assets"   ' built into Windows 10/11

    Public Shared ReadOnly DisplayFont As New Font(FontFamilyName, 24.0F, FontStyle.Bold)
    Public Shared ReadOnly HeadingFont As New Font(FontFamilyName, 20.0F, FontStyle.Bold)
    Public Shared ReadOnly BrandFont As New Font(FontFamilyName, 14.0F, FontStyle.Bold)
    Public Shared ReadOnly TitleFont As New Font(FontFamilyName, 16.0F, FontStyle.Bold)
    Public Shared ReadOnly SubtitleFont As New Font(FontFamilyName, 12.0F, FontStyle.Bold)
    Public Shared ReadOnly BodyFont As New Font(FontFamilyName, 10.0F, FontStyle.Regular)
    Public Shared ReadOnly BodyBoldFont As New Font(FontFamilyName, 10.0F, FontStyle.Bold)
    Public Shared ReadOnly InputFont As New Font(FontFamilyName, 11.0F, FontStyle.Regular)
    Public Shared ReadOnly SmallFont As New Font(FontFamilyName, 8.5F, FontStyle.Regular)
    Public Shared ReadOnly SmallBoldFont As New Font(FontFamilyName, 8.5F, FontStyle.Bold)
    Public Shared ReadOnly SidebarFont As New Font(FontFamilyName, 10.0F, FontStyle.Regular)
    Public Shared ReadOnly SidebarActiveFont As New Font(FontFamilyName, 10.0F, FontStyle.Bold)

    Private Shared ReadOnly _iconFonts As New Dictionary(Of Single, Font)

    ''' <summary>Icon font at a given point size (cached so we don't create fonts on every paint).</summary>
    Public Shared Function IconFont(size As Single) As Font
        Dim f As Font = Nothing
        If Not _iconFonts.TryGetValue(size, f) Then
            f = New Font(IconFontName, size, FontStyle.Regular)
            _iconFonts(size) = f
        End If
        Return f
    End Function

    ''' <summary>Returns the color for a status word (Valid, Pending, Expired, ...).</summary>
    Public Shared Function StatusColor(status As String) As Color
        Select Case StatusGroup(status)
            Case 1 : Return StatusGreen
            Case 2 : Return StatusAmber
            Case 3 : Return StatusRed
            Case Else : Return StatusGray
        End Select
    End Function

    ''' <summary>Light background color for a status badge.</summary>
    Public Shared Function StatusSoftColor(status As String) As Color
        Select Case StatusGroup(status)
            Case 1 : Return StatusGreenSoft
            Case 2 : Return StatusAmberSoft
            Case 3 : Return StatusRedSoft
            Case Else : Return StatusGraySoft
        End Select
    End Function

    ''' <summary>1 = green, 2 = amber, 3 = red, 0 = gray.</summary>
    Private Shared Function StatusGroup(status As String) As Integer
        Select Case If(status, "").Trim().ToLowerInvariant()
            Case "valid", "approved", "paid", "issued", "endorsed", "passed", "verified", "completed", "active", "ready"
                Return 1
            Case "pending", "expiring soon", "submitted", "under review", "for assessment",
                 "assessed", "scheduled", "in progress", "pending upload", "due soon",
                 "lab analysis", "for inspection", "on hold", "unpaid", "late"
                Return 2
            Case "expired", "overdue", "rejected", "failed", "for re-inspection", "re-inspection", "cancelled", "not issued", "not available"
                Return 3
            Case Else
                Return 0
        End Select
    End Function

End Class

''' <summary>
''' Glyphs from the "Segoe MDL2 Assets" icon font (draw them with Theme.IconFont).
''' </summary>
Public NotInheritable Class Icons
    Private Sub New()
    End Sub
    Public Const Home As String = ChrW(&HE80F)
    Public Const Document As String = ChrW(&HE8A5)
    Public Const CheckShield As String = ChrW(&HE73E)
    Public Const Bank As String = ChrW(&HE825)
    Public Const Health As String = ChrW(&HE95E)
    Public Const Search As String = ChrW(&HE721)
    Public Const Repair As String = ChrW(&HE90F)
    Public Const Settings As String = ChrW(&HE713)
    Public Const Bell As String = ChrW(&HEA8F)
    Public Const SignOut As String = ChrW(&HE7E8)
    Public Const Close As String = ChrW(&HE8BB)
    Public Const Contact As String = ChrW(&HE77B)
    Public Const Lock As String = ChrW(&HE72E)
    Public Const Info As String = ChrW(&HE946)
    Public Const Warning As String = ChrW(&HE7BA)
    Public Const CheckMark As String = ChrW(&HE73E)
    Public Const City As String = ChrW(&HE80F)
    Public Const Calendar As String = ChrW(&HE787)
    Public Const Build As String = ChrW(&HE8F1)
    Public Const People As String = ChrW(&HE716)
    Public Const Save As String = ChrW(&HE74E)
    Public Const History As String = ChrW(&HE81C)
End Class
