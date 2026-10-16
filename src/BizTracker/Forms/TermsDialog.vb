''' <summary>
''' Terms and Conditions shown right after login when the user has not accepted the current version (Revision 4E).
''' "Accept" is enabled only after ticking "I have read and agree". OK = accepted (saved by TermsService);
''' Cancel = declined (the caller logs the user out).
''' </summary>
Public Class TermsDialog
    Inherits ModernDialog

    Private ReadOnly browser As New WebBrowser()
    Private ReadOnly chkAgree As New CheckBox()
    Private ReadOnly btnAccept As Button

    Public Sub New(fullName As String)
        ClientSize = New Size(820, 680)
        ControlBox = False                  ' must choose Accept or Decline
        SetTitle("Terms and Conditions", "Hello " & fullName & ". Please read and accept the terms (version " &
                 TermsService.CurrentVersion & ") to continue.")

        browser.Dock = DockStyle.Fill
        browser.AllowWebBrowserDrop = False
        browser.IsWebBrowserContextMenuEnabled = False
        browser.ScriptErrorsSuppressed = True

        ' Agree check box under the text
        Dim agreeBar As New Panel With {.Dock = DockStyle.Bottom, .Height = 44, .BackColor = Color.White,
                                        .Padding = New Padding(24, 10, 24, 0)}
        chkAgree.Text = "I have read and agree to the Terms and Conditions, including the processing of my personal information (RA 10173)."
        chkAgree.Font = Theme.BodyBoldFont
        chkAgree.ForeColor = Theme.TextDark
        chkAgree.AutoSize = False
        chkAgree.AutoEllipsis = True
        chkAgree.Dock = DockStyle.Fill
        agreeBar.Controls.Add(chkAgree)
        Tips.SetToolTip(chkAgree, chkAgree.Text)

        Body.Padding = New Padding(0)
        Body.Controls.Add(browser)
        Body.Controls.Add(agreeBar)

        btnAccept = AddFooterButton("Accept", "primary")
        Dim decline = AddFooterButton("Decline")
        AddHandler btnAccept.Click, AddressOf Accept_Click
        AddHandler decline.Click, Sub() DialogResult = DialogResult.Cancel
        CancelButton = decline
        UiHelper.SetAvailable(btnAccept, "Tick ""I have read and agree"" first.")
        AddHandler chkAgree.CheckedChanged,
            Sub() UiHelper.SetAvailable(btnAccept, If(chkAgree.Checked, Nothing, "Tick ""I have read and agree"" first."))
    End Sub

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        Try
            browser.DocumentText = TermsService.BuildHtml()
        Catch ex As Exception
            browser.DocumentText = "<p style=""font-family:Segoe UI"">The terms could not be loaded: " &
                                   Net.WebUtility.HtmlEncode(ex.Message) & "</p>"
        End Try
    End Sub

    Private Sub Accept_Click(sender As Object, e As EventArgs)
        If Not chkAgree.Checked Then Return
        Dim problem = TermsService.Accept()
        If problem IsNot Nothing Then
            UiHelper.ShowWarning(problem)
            Return
        End If
        DialogResult = DialogResult.OK
    End Sub

End Class
