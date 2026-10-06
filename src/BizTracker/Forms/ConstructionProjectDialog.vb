''' <summary>
''' New / Edit / View a construction project (title, type, estimated cost).
''' All rules and saving are done by ConstructionService.
''' </summary>
Public Class ConstructionProjectDialog
    Inherits ModernDialog

    Public Enum DialogMode
        NewProject
        Edit
        View
    End Enum

    Private ReadOnly mode As DialogMode
    Private ReadOnly project As ConstructionProject
    Private ReadOnly txtTitle As New TextBox()
    Private ReadOnly cboType As New ComboBox()
    Private ReadOnly txtCost As New TextBox()
    Private ReadOnly errTitle As Label
    Private ReadOnly errType As Label
    Private ReadOnly errCost As Label

    ''' <summary>The saved project id (after OK).</summary>
    Public ReadOnly Property ProjectId As Integer
        Get
            Return project.ProjectId
        End Get
    End Property

    Public Sub New(dialogMode As DialogMode, biz As Business, Optional existing As ConstructionProject = Nothing)
        mode = dialogMode
        project = If(existing, New ConstructionProject With {.BusinessId = biz.BusinessId, .DateFiled = Date.Today})
        ClientSize = New Size(560, 450)
        Select Case mode
            Case DialogMode.NewProject
                SetTitle("New Construction Project", "File a construction permit application for " & biz.BusinessName)
            Case DialogMode.Edit
                SetTitle("Edit Project", project.ReferenceNo & "  ·  " & biz.BusinessName)
            Case Else
                SetTitle("Project Details", project.ReferenceNo & "  ·  " & biz.BusinessName)
        End Select

        ' --- Summary box ---
        Dim info As New RoundedPanel With {.BackColor = Theme.AccentSoft, .ShowShadow = False, .Radius = 10}
        info.SetBounds(24, 18, 512, 64)
        AddInfo(info, "Business", biz.BusinessName, 16, 10, 220)
        AddInfo(info, "Stage", If(project.ProjectId = 0, ConstructionService.StageLocational, project.CurrentStage), 246, 10, 130)
        AddInfo(info, "Status", If(project.ProjectId = 0, "New", project.Status), 386, 10, 114)
        Body.Controls.Add(info)

        ' --- Fields ---
        txtTitle.Font = Theme.InputFont
        txtTitle.BorderStyle = BorderStyle.FixedSingle
        txtTitle.MaxLength = 200
        txtTitle.Text = project.ProjectTitle
        txtTitle.PlaceholderText = "e.g. Two-storey commercial building"
        errTitle = AddField("Project title", txtTitle, 24, 98, 512)

        cboType.DropDownStyle = ComboBoxStyle.DropDownList
        cboType.FlatStyle = FlatStyle.Flat
        cboType.Font = Theme.InputFont
        cboType.Items.AddRange(ConstructionService.ProjectTypes)
        cboType.SelectedItem = project.ProjectType
        If cboType.SelectedIndex < 0 Then cboType.SelectedIndex = 0
        UiHelper.StyleComboBox(cboType)
        errType = AddField("Type of project", UiHelper.CreateComboBox(cboType, 29), 24, 176, 246)

        txtCost.Font = Theme.InputFont
        txtCost.BorderStyle = BorderStyle.FixedSingle
        txtCost.MaxLength = 16
        txtCost.Text = If(project.EstimatedCost.HasValue, project.EstimatedCost.Value.ToString("0.00"), "")
        txtCost.PlaceholderText = "Optional, e.g. 850000"
        errCost = AddField("Estimated cost (₱)", txtCost, 290, 176, 246)

        Dim note As New Label With {
            .Font = Theme.SmallFont, .ForeColor = Theme.TextMuted, .AutoSize = False, .AutoEllipsis = True,
            .Text = If(mode = DialogMode.NewProject,
                       "After filing, upload the technical documents: " & String.Join(", ", ConstructionService.DefaultDocuments) & ".",
                       "Filed " & UiHelper.FormatDate(project.DateFiled) & ". The stage and clearances are updated from the project screen.")
        }
        note.SetBounds(24, 256, 512, 40)
        Tips.SetToolTip(note, note.Text)
        Body.Controls.Add(note)

        ' --- Buttons ---
        If mode = DialogMode.View Then
            Dim close = AddFooterButton("Close", "primary")
            AddHandler close.Click, Sub() DialogResult = DialogResult.Cancel
            CancelButton = close
            txtTitle.ReadOnly = True
            cboType.Enabled = False
            txtCost.ReadOnly = True
        Else
            Dim save = AddFooterButton(If(mode = DialogMode.NewProject, "File Project", "Save Changes"), "primary")
            Dim cancel = AddFooterButton("Cancel")
            AddHandler save.Click, AddressOf Save_Click
            AddHandler cancel.Click, Sub() DialogResult = DialogResult.Cancel
            AcceptButton = save
            CancelButton = cancel
        End If
        AddHandler txtTitle.TextChanged, Sub() errTitle.Text = ""
        AddHandler cboType.SelectedIndexChanged, Sub() errType.Text = ""
        AddHandler txtCost.TextChanged, Sub() errCost.Text = ""
    End Sub

    Private Sub Save_Click(sender As Object, e As EventArgs)
        errTitle.Text = "" : errType.Text = "" : errCost.Text = ""
        Dim cost As Decimal? = Nothing
        Dim costText = txtCost.Text.Replace("₱", "").Replace(",", "").Trim()
        If costText <> "" Then
            Dim value As Decimal
            If Not Decimal.TryParse(costText, Globalization.NumberStyles.Number, Globalization.CultureInfo.InvariantCulture, value) Then
                errCost.Text = "Enter a valid amount, e.g. 850000 (or leave it blank)."
                Return
            End If
            cost = Math.Round(value, 2)
        End If
        Dim candidate As New ConstructionProject With {
            .ProjectId = project.ProjectId, .BusinessId = project.BusinessId, .ReferenceNo = project.ReferenceNo,
            .ProjectTitle = txtTitle.Text.Trim(), .ProjectType = If(cboType.SelectedItem?.ToString(), ""),
            .CurrentStage = project.CurrentStage, .Status = project.Status, .EstimatedCost = cost, .DateFiled = project.DateFiled
        }
        Dim errors = ConstructionService.ValidateProject(candidate)
        If errors.Count > 0 Then
            If errors.ContainsKey("title") Then errTitle.Text = errors("title")
            If errors.ContainsKey("type") Then errType.Text = errors("type")
            If errors.ContainsKey("cost") Then errCost.Text = errors("cost")
            Return
        End If
        Cursor = Cursors.WaitCursor
        Dim problem = If(mode = DialogMode.NewProject, ConstructionService.FileProject(candidate), ConstructionService.UpdateProject(candidate))
        Cursor = Cursors.Default
        If problem IsNot Nothing Then
            UiHelper.ShowWarning(problem)
            Return
        End If
        project.ProjectId = candidate.ProjectId
        DialogResult = DialogResult.OK
    End Sub

End Class
