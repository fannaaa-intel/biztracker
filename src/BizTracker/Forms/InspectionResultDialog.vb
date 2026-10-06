''' <summary>
''' Records one department's inspection result: department, Passed / Failed, inspector and findings,
''' with a live note on what the result will do (e.g. complete the inspection and issue the certificate).
''' Saving is done by InspectionService.RecordResult.
''' </summary>
Public Class InspectionResultDialog
    Inherits ModernDialog

    Private ReadOnly inspection As Inspection
    Private ReadOnly items As List(Of InspectionItem)
    Private ReadOnly cboDepartment As New ComboBox()
    Private ReadOnly cboResult As New ComboBox()
    Private ReadOnly txtInspector As New TextBox()
    Private ReadOnly txtFindings As New TextBox()
    Private ReadOnly errDepartment As Label
    Private ReadOnly errResult As Label
    Private ReadOnly errInspector As Label
    Private ReadOnly errFindings As Label
    Private ReadOnly noteBox As New RoundedPanel()
    Private ReadOnly lblNote As New Label()

    ''' <summary>What happened (after OK).</summary>
    Public Property Outcome As InspectionResultOutcome

    ''' <summary>The department that was recorded (after OK).</summary>
    Public ReadOnly Property Department As String
        Get
            Return If(cboDepartment.SelectedItem?.ToString(), "")
        End Get
    End Property

    Public Sub New(i As Inspection, departmentItems As List(Of InspectionItem), Optional preselect As String = "")
        inspection = i
        items = departmentItems
        ClientSize = New Size(560, 600)
        SetTitle("Record Result", i.ReferenceNo & "  ·  " & i.BusinessName)

        ' --- Summary box ---
        Dim info As New RoundedPanel With {.BackColor = Theme.AccentSoft, .ShowShadow = False, .Radius = 10}
        info.SetBounds(24, 18, 512, 64)
        AddInfo(info, "Inspection date", i.ScheduleDate.ToString("MMM d, yyyy h:mm tt"), 16, 10, 200)
        AddInfo(info, "Status", i.Status, 226, 10, 140)
        AddInfo(info, "Departments", InspectionService.GetPassedCount(items) & " of 4 passed", 376, 10, 124)
        Body.Controls.Add(info)

        ' --- Department + result ---
        cboDepartment.DropDownStyle = ComboBoxStyle.DropDownList
        cboDepartment.FlatStyle = FlatStyle.Flat
        cboDepartment.Font = Theme.InputFont
        For Each item In InspectionService.GetRecordableItems(items)
            cboDepartment.Items.Add(item.Department)
        Next
        cboDepartment.SelectedItem = preselect
        If cboDepartment.SelectedIndex < 0 AndAlso cboDepartment.Items.Count > 0 Then cboDepartment.SelectedIndex = 0
        UiHelper.StyleComboBox(cboDepartment)
        errDepartment = AddField("Department", UiHelper.CreateComboBox(cboDepartment, 29), 24, 98, 246)

        cboResult.DropDownStyle = ComboBoxStyle.DropDownList
        cboResult.FlatStyle = FlatStyle.Flat
        cboResult.Font = Theme.InputFont
        cboResult.Items.AddRange({InspectionService.Passed, InspectionService.Failed})
        cboResult.SelectedIndex = 0
        UiHelper.StyleComboBox(cboResult)
        errResult = AddField("Result", UiHelper.CreateComboBox(cboResult, 29), 290, 98, 246)

        txtInspector.Font = Theme.InputFont
        txtInspector.BorderStyle = BorderStyle.FixedSingle
        txtInspector.MaxLength = 150
        txtInspector.PlaceholderText = "e.g. Insp. M. Del Rosario (BFP)"
        errInspector = AddField("Inspector", txtInspector, 24, 176, 512)

        ' Live note: what this result will do
        noteBox.ShowShadow = False
        noteBox.Radius = 10
        noteBox.Padding = New Padding(14, 0, 14, 0)
        noteBox.SetBounds(24, 254, 512, 52)
        lblNote.Dock = DockStyle.Fill
        lblNote.TextAlign = ContentAlignment.MiddleLeft
        lblNote.Font = Theme.SmallBoldFont
        lblNote.AutoEllipsis = True
        noteBox.Controls.Add(lblNote)
        Body.Controls.Add(noteBox)

        txtFindings.Multiline = True
        txtFindings.Font = Theme.InputFont
        txtFindings.BorderStyle = BorderStyle.FixedSingle
        txtFindings.Height = 100
        txtFindings.MaxLength = 1000
        txtFindings.PlaceholderText = "What was checked, deficiencies found and what must be corrected..."
        errFindings = AddField("Findings", txtFindings, 24, 318, 512)

        ' --- Buttons ---
        Dim save = AddFooterButton("Save Result", "primary")
        Dim cancel = AddFooterButton("Cancel")
        AddHandler save.Click, AddressOf Save_Click
        AddHandler cancel.Click, Sub() DialogResult = DialogResult.Cancel
        CancelButton = cancel

        AddHandler cboDepartment.SelectedIndexChanged, Sub()
                                                           errDepartment.Text = ""
                                                           FillInspector()
                                                           UpdateNote()
                                                       End Sub
        AddHandler cboResult.SelectedIndexChanged, Sub()
                                                       errResult.Text = ""
                                                       errFindings.Text = ""
                                                       UpdateNote()
                                                   End Sub
        AddHandler txtInspector.TextChanged, Sub() errInspector.Text = ""
        AddHandler txtFindings.TextChanged, Sub() errFindings.Text = ""
        FillInspector()
        UpdateNote()
    End Sub

    Private ReadOnly Property SelectedItem As InspectionItem
        Get
            Return items.FirstOrDefault(Function(x) x.Department = Department)
        End Get
    End Property

    ''' <summary>Suggests the inspector: who inspected this department before, or the logged-in Inspector.</summary>
    Private Sub FillInspector()
        Dim item = SelectedItem
        If item IsNot Nothing AndAlso item.InspectorName <> "" Then
            txtInspector.Text = item.InspectorName
        ElseIf Session.Role = Roles.Inspector Then
            txtInspector.Text = Session.FullName
        End If
    End Sub

    Private Sub UpdateNote()
        Dim item = SelectedItem
        Dim result = If(cboResult.SelectedItem?.ToString(), "")
        Dim text As String
        Dim good As Boolean
        If item Is Nothing Then
            text = "All departments have passed."
            good = True
        ElseIf result = InspectionService.Failed Then
            text = item.Department & " will be marked Failed. The inspection goes For Re-inspection once no department is pending."
            good = False
        Else
            Dim passedAfter = InspectionService.GetPassedCount(items) + 1
            If passedAfter >= InspectionDepartments.All.Length Then
                text = "All 4 departments will have passed: the inspection is completed and the certificate is issued."
            Else
                text = item.Department & " passes - " & passedAfter & " of 4 departments passed."
            End If
            If item.Department = InspectionService.FireDepartment Then text &= " The Fire endorsement of the business permit is updated."
            good = True
        End If
        If item IsNot Nothing AndAlso item.Result = InspectionService.Failed Then text = "Re-inspection. " & text
        noteBox.BackColor = If(good, Theme.StatusGreenSoft, Theme.StatusAmberSoft)
        lblNote.BackColor = noteBox.BackColor
        lblNote.ForeColor = If(good, Theme.StatusGreen, Theme.StatusAmber)
        lblNote.Text = text
        Tips.SetToolTip(lblNote, text)
        noteBox.Invalidate()
    End Sub

    Private Sub Save_Click(sender As Object, e As EventArgs)
        errDepartment.Text = "" : errResult.Text = "" : errInspector.Text = "" : errFindings.Text = ""
        If SelectedItem Is Nothing Then
            errDepartment.Text = "Choose a department."
            Return
        End If
        Dim result = If(cboResult.SelectedItem?.ToString(), "")
        Dim errors = InspectionService.ValidateResult(result, txtFindings.Text, txtInspector.Text)
        If errors.Count > 0 Then
            If errors.ContainsKey("result") Then errResult.Text = errors("result")
            If errors.ContainsKey("inspector") Then errInspector.Text = errors("inspector")
            If errors.ContainsKey("findings") Then errFindings.Text = errors("findings")
            Return
        End If
        Cursor = Cursors.WaitCursor
        Dim saved = InspectionService.RecordResult(inspection, Department, result, txtFindings.Text, txtInspector.Text)
        Cursor = Cursors.Default
        If saved.ErrorMessage IsNot Nothing Then
            UiHelper.ShowWarning(saved.ErrorMessage)
            Return
        End If
        Outcome = saved
        DialogResult = DialogResult.OK
    End Sub

End Class
