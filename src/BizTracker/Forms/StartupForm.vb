''' <summary>
''' TEMPORARY startup form (setup check only).
''' Confirms the app can reach biztracker_db and that BCrypt verifies the seeded hash.
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
        Dim row = Db.GetSetupCheck()   ' shows its own friendly error on failure
        Cursor = Cursors.Default

        If row Is Nothing Then
            lblStatus.Text = "Not connected"
            lblStatus.ForeColor = Theme.StatusRed
            Return
        End If

        ' BCrypt check: does "password123" match the seeded admin hash?
        Dim hash = If(row.IsNull("admin_hash"), "", row("admin_hash").ToString())
        Dim bcryptResult As String
        If hash = "" Then
            bcryptResult = "FAILED - user 'admin' not found"
        Else
            Try
                bcryptResult = If(BCrypt.Net.BCrypt.Verify("password123", hash), "OK - password123 matches", "FAILED - hash does not match")
            Catch ex As BCrypt.Net.SaltParseException
                bcryptResult = "FAILED - stored hash is not a valid BCrypt hash"
            End Try
        End If

        lblStatus.Text = "Connected to " & row("db_name").ToString()
        lblStatus.ForeColor = Theme.StatusGreen
        MessageBox.Show("Connected to database '" & row("db_name").ToString() & "'" & vbCrLf &
                        "Server version: " & row("server_version").ToString() & vbCrLf & vbCrLf &
                        "Businesses: " & row("business_count").ToString() & vbCrLf &
                        "Users: " & row("user_count").ToString() & vbCrLf & vbCrLf &
                        "BCrypt check (admin): " & bcryptResult,
                        "Test Connection", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

End Class
