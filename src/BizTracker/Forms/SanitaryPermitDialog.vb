''' <summary>
''' New / Renew / Edit / View a sanitary permit application.
''' All rules and saving are done by SanitaryPermitService.
''' </summary>
Public Class SanitaryPermitDialog
    Inherits ModernDialog

    Public Enum DialogMode
        NewApplication
        Renewal
        Edit
        View
    End Enum

    Private ReadOnly mode As DialogMode
    Private ReadOnly permit As SanitaryPermit
    Private ReadOnly cboCategory As New ComboBox()
    Private ReadOnly dtpFiled As New DateTimePicker()
    Private ReadOnly errCategory As Label
    Private ReadOnly errFiled As Label

    ''' <summary>The saved sanitary_id (after OK).</summary>
    Public ReadOnly Property SanitaryId As Integer
        Get
            Return permit.SanitaryId
        End Get
    End Property

    Public Sub New(dialogMode As DialogMode, business As Business, Optional existing As SanitaryPermit = Nothing)
        mode = dialogMode
        ClientSize = New Size(560, 400)
        permit = If(existing, New SanitaryPermit With {
            .BusinessId = business.BusinessId, .PermitYear = Date.Today.Year, .DateFiled = Date.Today,
            .Category = If(business.IsFoodBusiness, "Food", "Non-Food")})

        Select Case mode
            Case DialogMode.NewApplication
                SetTitle("New Sanitary Permit Application", "Apply for a " & permit.PermitYear & " sanitary permit for " & business.BusinessName)
            Case DialogMode.Renewal
                SetTitle("Renew Sanitary Permit", "File the " & permit.PermitYear & " renewal for " & business.BusinessName)
            Case DialogMode.Edit
                SetTitle("Edit Application", permit.PermitNo & "  ·  " & business.BusinessName)
            Case Else
                SetTitle("Application Details", permit.PermitNo & "  ·  " & business.BusinessName)
        End Select

        ' --- Summary box ---
        Dim info As New RoundedPanel With {.BackColor = Theme.AccentSoft, .ShowShadow = False, .Radius = 10}
        info.SetBounds(24, 18, 512, 64)
        AddInfo(info, "Business", business.BusinessName, 16, 10, 250)
        AddInfo(info, "Permit year", permit.PermitYear.ToString(), 280, 10, 90)
        AddInfo(info, "Status", If(permit.SanitaryId = 0, "New", permit.Status), 380, 10, 120)
        Body.Controls.Add(info)

        ' --- Fields ---
        cboCategory.DropDownStyle = ComboBoxStyle.DropDownList
        cboCategory.FlatStyle = FlatStyle.Flat
        cboCategory.Font = Theme.InputFont
        cboCategory.Items.AddRange(SanitaryPermitService.Categories)
        cboCategory.SelectedItem = permit.Category
        If cboCategory.SelectedIndex < 0 Then cboCategory.SelectedIndex = 0
        UiHelper.StyleComboBox(cboCategory)
        errCategory = AddField("Category", UiHelper.CreateComboBox(cboCategory, 29), 24, 98, 246)

        dtpFiled.Format = DateTimePickerFormat.Custom
        dtpFiled.CustomFormat = "MMM dd, yyyy"
        dtpFiled.MinDate = New Date(permit.PermitYear, 1, 1)
        dtpFiled.MaxDate = If(permit.PermitYear = Date.Today.Year, Date.Today, New Date(permit.PermitYear, 12, 31))
        dtpFiled.Value = If(permit.DateFiled < dtpFiled.MinDate OrElse permit.DateFiled > dtpFiled.MaxDate, dtpFiled.MaxDate, permit.DateFiled)
        dtpFiled.Font = Theme.InputFont
        dtpFiled.Height = 32
        errFiled = AddField("Date filed", dtpFiled, 290, 98, 246)

        Dim note As New Label With {
            .Font = Theme.SmallFont, .ForeColor = Theme.TextMuted, .AutoSize = False, .AutoEllipsis = True,
            .Text = "Prerequisites: " & String.Join(", ", SanitaryPermitService.Prerequisites) &
                    ". The permit is valid until December 31 of the year it is issued."
        }
        note.SetBounds(24, 176, 512, 40)
        Tips.SetToolTip(note, note.Text)
        Body.Controls.Add(note)

        ' --- Buttons ---
        If mode = DialogMode.View Then
            Dim close = AddFooterButton("Close", "primary")
            AddHandler close.Click, Sub() DialogResult = DialogResult.Cancel
            CancelButton = close
            cboCategory.Enabled = False
            dtpFiled.Enabled = False
        Else
            Dim save = AddFooterButton(If(mode = DialogMode.Edit, "Save Changes", "Submit Application"), "primary")
            Dim cancel = AddFooterButton("Cancel")
            AddHandler save.Click, AddressOf Save_Click
            AddHandler cancel.Click, Sub() DialogResult = DialogResult.Cancel
            AcceptButton = save
            CancelButton = cancel
        End If
        AddHandler cboCategory.SelectedIndexChanged, Sub() errCategory.Text = ""
        AddHandler dtpFiled.ValueChanged, Sub() errFiled.Text = ""
    End Sub

    Private Sub Save_Click(sender As Object, e As EventArgs)
        errCategory.Text = "" : errFiled.Text = ""
        Dim candidate As New SanitaryPermit With {
            .SanitaryId = permit.SanitaryId, .BusinessId = permit.BusinessId, .PermitNo = permit.PermitNo,
            .PermitYear = permit.PermitYear, .Status = permit.Status, .Category = If(cboCategory.SelectedItem?.ToString(), ""),
            .DateFiled = dtpFiled.Value.Date, .InspectionDate = permit.InspectionDate, .InspectionScore = permit.InspectionScore,
            .InspectorName = permit.InspectorName, .Findings = permit.Findings, .DateIssued = permit.DateIssued,
            .ValidUntil = permit.ValidUntil
        }
        Dim errors = SanitaryPermitService.Validate(candidate)
        If errors.Count > 0 Then
            If errors.ContainsKey("category") Then errCategory.Text = errors("category")
            If errors.ContainsKey("filed") Then errFiled.Text = errors("filed")
            Return
        End If
        If mode <> DialogMode.Edit Then
            Dim blocker = SanitaryPermitService.GetFilingBlocker(permit.BusinessId)
            If blocker IsNot Nothing Then
                UiHelper.ShowWarning(blocker)
                Return
            End If
        End If

        Cursor = Cursors.WaitCursor
        Dim ok As Boolean
        If mode = DialogMode.Edit Then
            ok = SanitaryPermitService.UpdateApplication(candidate)
        Else
            ok = SanitaryPermitService.FileApplication(candidate) > 0
        End If
        Cursor = Cursors.Default
        If Not ok Then
            UiHelper.ShowError("The application could not be saved. Please check the details and try again.")
            Return
        End If
        permit.SanitaryId = candidate.SanitaryId
        DialogResult = DialogResult.OK
    End Sub

End Class
