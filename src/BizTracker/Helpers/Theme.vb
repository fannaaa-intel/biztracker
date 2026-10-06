''' <summary>
''' All colors and fonts used by the app, in one place (matches the Figma prototype).
''' Forms and UserControls should use these instead of hard-coding colors.
''' </summary>
Public NotInheritable Class Theme

    Private Sub New()
    End Sub

    ' ---------- Layout colors ----------
    Public Shared ReadOnly SidebarBlue As Color = ColorTranslator.FromHtml("#0B6E99")
    Public Shared ReadOnly SidebarHover As Color = ColorTranslator.FromHtml("#0D82B5")
    Public Shared ReadOnly SidebarActive As Color = ColorTranslator.FromHtml("#08577A")
    Public Shared ReadOnly ContentBackground As Color = ColorTranslator.FromHtml("#7FBFE0")
    Public Shared ReadOnly CardBackground As Color = Color.White
    Public Shared ReadOnly CardCornerRadius As Integer = 12

    ' ---------- Text colors ----------
    Public Shared ReadOnly TextDark As Color = ColorTranslator.FromHtml("#1F2937")
    Public Shared ReadOnly TextMuted As Color = ColorTranslator.FromHtml("#6B7280")
    Public Shared ReadOnly TextOnBlue As Color = Color.White

    ' ---------- Buttons ----------
    Public Shared ReadOnly ButtonPrimary As Color = SidebarBlue
    Public Shared ReadOnly ButtonPrimaryText As Color = Color.White

    ' ---------- Status colors ----------
    ' Green = Valid / Approved / Paid
    Public Shared ReadOnly StatusGreen As Color = ColorTranslator.FromHtml("#16A34A")
    ' Amber = Pending / Expiring Soon
    Public Shared ReadOnly StatusAmber As Color = ColorTranslator.FromHtml("#F59E0B")
    ' Red = Expired / Overdue / Rejected
    Public Shared ReadOnly StatusRed As Color = ColorTranslator.FromHtml("#DC2626")
    ' Gray = anything else (e.g. Draft, Not Applicable)
    Public Shared ReadOnly StatusGray As Color = ColorTranslator.FromHtml("#6B7280")

    ' ---------- Fonts ----------
    Public Const FontFamilyName As String = "Segoe UI"
    Public Shared ReadOnly TitleFont As New Font(FontFamilyName, 16.0F, FontStyle.Bold)
    Public Shared ReadOnly SubtitleFont As New Font(FontFamilyName, 12.0F, FontStyle.Bold)
    Public Shared ReadOnly BodyFont As New Font(FontFamilyName, 10.0F, FontStyle.Regular)
    Public Shared ReadOnly BodyBoldFont As New Font(FontFamilyName, 10.0F, FontStyle.Bold)
    Public Shared ReadOnly SmallFont As New Font(FontFamilyName, 8.5F, FontStyle.Regular)
    Public Shared ReadOnly SidebarFont As New Font(FontFamilyName, 10.5F, FontStyle.Bold)

    ''' <summary>Returns the color for a status word (Valid, Pending, Expired, ...).</summary>
    Public Shared Function StatusColor(status As String) As Color
        Select Case If(status, "").Trim().ToLowerInvariant()
            Case "valid", "approved", "paid", "issued", "endorsed", "passed", "verified", "completed"
                Return StatusGreen
            Case "pending", "expiring soon", "submitted", "under review", "for assessment",
                 "assessed", "scheduled", "in progress", "pending upload"
                Return StatusAmber
            Case "expired", "overdue", "rejected", "failed", "for re-inspection"
                Return StatusRed
            Case Else
                Return StatusGray
        End Select
    End Function

End Class
