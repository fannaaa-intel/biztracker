<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class StartupForm
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        lblTitle = New Label()
        btnTestConnection = New Button()
        lblStatus = New Label()
        SuspendLayout()
        '
        'lblTitle
        '
        lblTitle.AutoSize = True
        lblTitle.Location = New Point(30, 30)
        lblTitle.Name = "lblTitle"
        lblTitle.Size = New Size(300, 30)
        lblTitle.TabIndex = 0
        lblTitle.Text = "BizTracker - Setup Check"
        '
        'btnTestConnection
        '
        btnTestConnection.FlatStyle = FlatStyle.Flat
        btnTestConnection.Location = New Point(30, 90)
        btnTestConnection.Name = "btnTestConnection"
        btnTestConnection.Size = New Size(180, 40)
        btnTestConnection.TabIndex = 1
        btnTestConnection.Text = "Test Connection"
        btnTestConnection.UseVisualStyleBackColor = False
        '
        'lblStatus
        '
        lblStatus.AutoSize = True
        lblStatus.Location = New Point(30, 150)
        lblStatus.Name = "lblStatus"
        lblStatus.Size = New Size(120, 20)
        lblStatus.TabIndex = 2
        lblStatus.Text = "Not tested yet"
        '
        'StartupForm
        '
        AutoScaleDimensions = New SizeF(8.0F, 20.0F)
        AutoScaleMode = AutoScaleMode.Font
        ClientSize = New Size(480, 220)
        Controls.Add(lblTitle)
        Controls.Add(btnTestConnection)
        Controls.Add(lblStatus)
        FormBorderStyle = FormBorderStyle.FixedSingle
        MaximizeBox = False
        Name = "StartupForm"
        StartPosition = FormStartPosition.CenterScreen
        Text = "BizTracker"
        ResumeLayout(False)
        PerformLayout()
    End Sub

    Friend WithEvents lblTitle As Label
    Friend WithEvents btnTestConnection As Button
    Friend WithEvents lblStatus As Label

End Class
