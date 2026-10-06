''' <summary>
''' Schedule a new annual inspection, move a scheduled one, or set the re-inspection date
''' (date + time). All rules and saving are done by InspectionService.
''' </summary>
Public Class ScheduleInspectionDialog
    Inherits ModernDialog

    Public Enum DialogMode
        NewInspection
        Reschedule
        Reinspection
    End Enum

    Private ReadOnly mode As DialogMode
    Private ReadOnly inspection As Inspection
    Private ReadOnly dtpDate As New DateTimePicker()
    Private ReadOnly dtpTime As New DateTimePicker()
    Private ReadOnly errDate As Label

    ''' <summary>The saved inspection id (after OK).</summary>
    Public ReadOnly Property InspectionId As Integer
        Get
            Return inspection.InspectionId
        End Get
    End Property

    Public Sub New(dialogMode As DialogMode, biz As Business, Optional existing As Inspection = Nothing,
                   Optional failedDepartments As IEnumerable(Of String) = Nothing, Optional suggestedDate As Date? = Nothing)
        mode = dialogMode
        inspection = If(existing, New Inspection With {.BusinessId = biz.BusinessId, .BusinessName = biz.BusinessName})
        ClientSize = New Size(560, 400)
        Select Case mode
            Case DialogMode.NewInspection
                SetTitle("Schedule Inspection", "Annual joint inspection of " & biz.BusinessName)
            Case DialogMode.Reschedule
                SetTitle("Reschedule Inspection", inspection.ReferenceNo & "  ·  " & biz.BusinessName)
            Case Else
                SetTitle("Schedule Re-inspection", inspection.ReferenceNo & "  ·  " & biz.BusinessName)
        End Select

        ' --- Summary box ---
        Dim info As New RoundedPanel With {.BackColor = Theme.AccentSoft, .ShowShadow = False, .Radius = 10}
        info.SetBounds(24, 18, 512, 64)
        AddInfo(info, "Business", biz.BusinessName, 16, 10, 200)
        AddInfo(info, "Reference", If(inspection.InspectionId = 0, "New", inspection.ReferenceNo), 226, 10, 124)
        AddInfo(info, "Status", If(inspection.InspectionId = 0, "Scheduled", inspection.Status), 360, 10, 140)
        Body.Controls.Add(info)

        ' --- Date + time (default: the current date, else the next working day at 9:00 AM) ---
        Dim current As Date
        Select Case mode
            Case DialogMode.Reschedule : current = inspection.ScheduleDate
            Case DialogMode.Reinspection : current = If(inspection.ReinspectionDate, Date.Today.AddDays(14).AddHours(9))
            Case Else : current = If(suggestedDate, Date.Today.AddDays(7).AddHours(9))
        End Select
        Dim minDate = If(mode = DialogMode.Reinspection, Date.Today.AddDays(1), Date.Today)
        If current.Date < minDate Then current = minDate.AddHours(9)

        dtpDate.Format = DateTimePickerFormat.Custom
        dtpDate.CustomFormat = "ddd, MMM dd, yyyy"
        dtpDate.MinDate = minDate
        dtpDate.MaxDate = Date.Today.AddYears(1)
        dtpDate.Value = current.Date
        dtpDate.Font = Theme.InputFont
        dtpDate.Height = 32
        errDate = AddField(If(mode = DialogMode.Reinspection, "Re-inspection date", "Inspection date"), dtpDate, 24, 98, 300)

        dtpTime.Format = DateTimePickerFormat.Custom
        dtpTime.CustomFormat = "h:mm tt"
        dtpTime.ShowUpDown = True
        dtpTime.Value = Date.Today.Add(current.TimeOfDay)
        dtpTime.Font = Theme.InputFont
        dtpTime.Height = 32
        AddField("Time", dtpTime, 340, 98, 196)

        Dim noteText As String
        If mode = DialogMode.Reinspection Then
            Dim failed = If(failedDepartments, Enumerable.Empty(Of String)()).ToList()
            noteText = "Departments to re-inspect: " & If(failed.Count = 0, "the failed ones", String.Join(", ", failed)) &
                       ". The business should correct the deficiencies before this date."
        Else
            noteText = "The joint team inspects Structural, Electrical, Mechanical and Fire safety. " &
                       "Visits are held between 7:00 AM and 5:00 PM."
        End If
        Dim noteBox As New RoundedPanel With {.BackColor = Theme.SoftBackground, .ShowShadow = False, .Radius = 10,
                                              .Padding = New Padding(14, 0, 14, 0)}
        noteBox.SetBounds(24, 178, 512, 56)
        Dim note As New Label With {.Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Font = Theme.SmallFont,
                                    .ForeColor = Theme.TextMuted, .BackColor = noteBox.BackColor, .AutoEllipsis = True, .Text = noteText}
        Tips.SetToolTip(note, noteText)
        noteBox.Controls.Add(note)
        Body.Controls.Add(noteBox)

        ' --- Buttons ---
        Dim save = AddFooterButton(If(mode = DialogMode.NewInspection, "Schedule Inspection",
                                      If(mode = DialogMode.Reschedule, "Save New Date", "Set Re-inspection")), "primary")
        Dim cancel = AddFooterButton("Cancel")
        AddHandler save.Click, AddressOf Save_Click
        AddHandler cancel.Click, Sub() DialogResult = DialogResult.Cancel
        AcceptButton = save
        CancelButton = cancel
        AddHandler dtpDate.ValueChanged, Sub() errDate.Text = ""
        AddHandler dtpTime.ValueChanged, Sub() errDate.Text = ""
    End Sub

    ''' <summary>The chosen date + time (seconds dropped).</summary>
    Private ReadOnly Property Chosen As Date
        Get
            Dim t = dtpTime.Value.TimeOfDay
            Return dtpDate.Value.Date.Add(New TimeSpan(t.Hours, t.Minutes, 0))
        End Get
    End Property

    Private Sub Save_Click(sender As Object, e As EventArgs)
        errDate.Text = ""
        Dim visitAt = Chosen
        Dim errors = InspectionService.ValidateSchedule(visitAt, allowToday:=mode <> DialogMode.Reinspection)
        If errors.ContainsKey("date") Then
            errDate.Text = errors("date")
            Return
        End If

        Cursor = Cursors.WaitCursor
        Dim problem As String
        Select Case mode
            Case DialogMode.NewInspection
                inspection.ScheduleDate = visitAt
                problem = InspectionService.ScheduleInspection(inspection)
            Case DialogMode.Reschedule
                problem = InspectionService.Reschedule(inspection, visitAt)
            Case Else
                problem = InspectionService.ScheduleReinspection(inspection, visitAt)
        End Select
        Cursor = Cursors.Default
        If problem IsNot Nothing Then
            UiHelper.ShowWarning(problem)
            Return
        End If
        DialogResult = DialogResult.OK
    End Sub

End Class
