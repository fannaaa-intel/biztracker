''' <summary>
''' Read-only "Policies & Requirements" of one service (Revision 4D), shown in a built-in browser.
''' The page comes from PolicyService (template + current settings). "Open in Browser" saves it so it can be printed.
''' </summary>
Public Class PoliciesDialog
    Inherits ModernDialog

    Private ReadOnly screen As AppScreen
    Private ReadOnly browser As New WebBrowser()

    Public Sub New(forScreen As AppScreen)
        screen = forScreen
        ClientSize = New Size(820, 660)
        SetTitle(PolicyService.GetTitle(screen), "Policies & Requirements: legal basis, requirements, validity, deadlines and penalties")

        browser.Dock = DockStyle.Fill
        browser.AllowWebBrowserDrop = False
        browser.IsWebBrowserContextMenuEnabled = False
        browser.WebBrowserShortcutsEnabled = True        ' Ctrl+C to copy text
        browser.ScriptErrorsSuppressed = True
        Body.Padding = New Padding(0)
        Body.Controls.Add(browser)

        Dim close = AddFooterButton("Close", "primary")
        AddHandler close.Click, Sub() DialogResult = DialogResult.Cancel
        CancelButton = close
        Dim openButton = AddFooterButton("Open in Browser / Print")
        AddHandler openButton.Click, AddressOf Open_Click
        Tips.SetToolTip(openButton, "Open this page in your browser to print it or save it as PDF")
    End Sub

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        Try
            Dim html = PolicyService.BuildHtml(screen)
            browser.DocumentText = If(html, "<p style=""font-family:Segoe UI"">There is no policy page for this screen.</p>")
        Catch ex As Exception
            browser.DocumentText = "<p style=""font-family:Segoe UI"">The policies could not be loaded: " &
                                   Net.WebUtility.HtmlEncode(ex.Message) & "</p>"
        End Try
    End Sub

    Private Sub Open_Click(sender As Object, e As EventArgs)
        Dim problem = PolicyService.OpenInBrowser(screen)
        If problem IsNot Nothing Then UiHelper.ShowWarning(problem)
    End Sub

End Class
