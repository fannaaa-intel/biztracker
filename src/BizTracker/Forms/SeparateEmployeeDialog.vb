''' <summary>
''' Marks an employee as separated (resigned / contract ended): asks for the separation date and an optional reason.
''' The employee leaves the roster but keeps their certificate history. Saving is done by HealthCertificateService.
''' </summary>
Public Class SeparateEmployeeDialog
    Inherits ModernDialog

    Private ReadOnly employee As Employee
    Private ReadOnly dtpDate As New DateTimePicker()
    Private ReadOnly txtReason As New TextBox()
    Private ReadOnly errDate As Label

    Public Sub New(business As Business, emp As Employee)
        employee = emp
        ClientSize = New Size(560, 420)
        SetTitle("Mark as Separated", emp.FullName & "  ·  " & business.BusinessName)

        ' --- Summary box ---
        Dim info As New RoundedPanel With {.BackColor = Theme.AccentSoft, .ShowShadow = False, .Radius = 10}
        info.SetBounds(24, 18, 512, 64)
        AddInfo(info, "Position", emp.Position & " · " & emp.Category, 16, 10, 230)
        AddInfo(info, "Date hired", If(emp.DateHired.HasValue, UiHelper.FormatDate(emp.DateHired.Value), "Not recorded"), 256, 10, 120)
        AddInfo(info, "Service", If(emp.DateHired.HasValue, HealthCertificateService.GetLengthOfService(emp), "—"), 386, 10, 114)
        Body.Controls.Add(info)

        ' --- Fields ---
        dtpDate.Format = DateTimePickerFormat.Custom
        dtpDate.CustomFormat = "MMM dd, yyyy"
        dtpDate.MaxDate = Date.Today
        If emp.DateHired.HasValue AndAlso emp.DateHired.Value.Date <= Date.Today Then dtpDate.MinDate = emp.DateHired.Value.Date
        dtpDate.Value = Date.Today
        dtpDate.Font = Theme.InputFont
        dtpDate.Height = 32
        errDate = AddField("Last day of work (separation date)", dtpDate, 24, 98, 246)

        txtReason.Font = Theme.InputFont
        txtReason.BorderStyle = BorderStyle.FixedSingle
        txtReason.MaxLength = 150
        txtReason.PlaceholderText = "e.g. Resigned, end of contract"
        AddField("Reason (optional, kept in the audit log)", txtReason, 24, 176, 512)

        Dim note As New Label With {
            .Text = "The employee is removed from the list, the cards and the alerts. Their certificate history is kept, " &
                    "and they can be reactivated from Employees Summary if rehired.",
            .Font = Theme.SmallFont, .ForeColor = Theme.TextMuted, .AutoSize = False
        }
        note.SetBounds(24, 254, 512, 40)
        Body.Controls.Add(note)

        ' --- Buttons ---
        Dim save = AddFooterButton("Mark as Separated", "danger")
        Dim cancel = AddFooterButton("Cancel")
        AddHandler save.Click, AddressOf Save_Click
        AddHandler cancel.Click, Sub() DialogResult = DialogResult.Cancel
        CancelButton = cancel
        AddHandler dtpDate.ValueChanged, Sub() errDate.Text = ""
    End Sub

    ''' <summary>The separation date that was saved (after OK).</summary>
    Public ReadOnly Property SeparatedOn As Date
        Get
            Return dtpDate.Value.Date
        End Get
    End Property

    Private Sub Save_Click(sender As Object, e As EventArgs)
        errDate.Text = ""
        Dim problem = HealthCertificateService.ValidateSeparation(employee, dtpDate.Value.Date)
        If problem IsNot Nothing Then
            errDate.Text = problem
            Return
        End If
        Cursor = Cursors.WaitCursor
        problem = HealthCertificateService.DeactivateEmployee(employee, dtpDate.Value.Date, txtReason.Text)
        Cursor = Cursors.Default
        If problem IsNot Nothing Then
            UiHelper.ShowWarning(problem)
            Return
        End If
        DialogResult = DialogResult.OK
    End Sub

End Class
