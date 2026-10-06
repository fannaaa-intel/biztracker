''' <summary>
''' TEMPORARY startup form for Phase 1 only.
''' Lets us confirm that the app can reach the MySQL server.
''' Will be replaced by LoginForm in Phase 4.
''' </summary>
Public Class StartupForm

    Private Sub StartupForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ' Apply theme colors/fonts.
        BackColor = Theme.ContentBackground
        lblTitle.Font = Theme.TitleFont
        lblTitle.ForeColor = Theme.TextOnBlue
        lblStatus.Font = Theme.BodyFont
        lblStatus.ForeColor = Theme.TextDark
        btnTestConnection.Font = Theme.BodyBoldFont
        btnTestConnection.BackColor = Theme.ButtonPrimary
        btnTestConnection.ForeColor = Theme.ButtonPrimaryText
    End Sub

    Private Sub btnTestConnection_Click(sender As Object, e As EventArgs) Handles btnTestConnection.Click
        Cursor = Cursors.WaitCursor
        Dim version = Db.GetServerVersion()   ' shows its own friendly error on failure
        Cursor = Cursors.Default

        If version IsNot Nothing Then
            lblStatus.Text = "Connected to MySQL server " & version
            lblStatus.ForeColor = Theme.StatusGreen
            MessageBox.Show("Connected to MySQL server." & vbCrLf & "Server version: " & version,
                            "Test Connection", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Else
            lblStatus.Text = "Not connected"
            lblStatus.ForeColor = Theme.StatusRed
        End If
    End Sub

End Class
