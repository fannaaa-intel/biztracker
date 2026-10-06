Imports System.Globalization

''' <summary>
''' Moves a business permit application to its NEXT status only (or rejects it).
''' Shows why a move is blocked, asks for the assessed tax when assessing, and previews
''' late renewal charges. All rules are enforced by BusinessPermitService.
''' </summary>
Public Class UpdateStatusDialog
    Inherits ModernDialog

    Private ReadOnly permit As BusinessPermit
    Private ReadOnly nextStatus As String
    Private ReadOnly txtAssessed As New TextBox()
    Private ReadOnly lblPreview As New Label()
    Private ReadOnly errAssessed As Label
    Private ReadOnly btnAdvance As Button

    Public Sub New(p As BusinessPermit)
        permit = p
        nextStatus = BusinessPermitService.GetNextStatus(p.Status)
        ClientSize = New Size(560, 470)
        SetTitle("Update Status", p.ReferenceNo & "  ·  " & p.BusinessName)

        ' --- Current -> Next ---
        Dim lblFrom As New Label With {.Text = "Current", .Font = Theme.SmallFont, .ForeColor = Theme.TextMuted, .AutoSize = True,
                                       .Location = New Point(24, 18)}
        Dim badgeFrom As New StatusBadge With {.Text = p.Status, .Location = New Point(24, 40)}
        Dim arrow As New Label With {.Text = ChrW(&HE72A), .Font = Theme.IconFont(12.0F), .ForeColor = Theme.TextMuted,
                                     .AutoSize = True, .Location = New Point(badgeFrom.Right + 16, 43)}
        Dim lblTo As New Label With {.Text = "Next", .Font = Theme.SmallFont, .ForeColor = Theme.TextMuted, .AutoSize = True,
                                     .Location = New Point(arrow.Right + 40, 18)}
        Dim badgeTo As New StatusBadge With {.Text = If(nextStatus, "—"), .Location = New Point(arrow.Right + 40, 40)}
        Body.Controls.AddRange({lblFrom, badgeFrom, arrow, lblTo, badgeTo})

        ' --- Readiness box (green = ready, amber = blocked) ---
        Dim blocker = BusinessPermitService.GetAdvanceBlocker(p)
        Dim box As New RoundedPanel With {
            .ShowShadow = False, .Radius = 10, .Padding = New Padding(14, 0, 14, 0),
            .BackColor = If(blocker Is Nothing, Theme.StatusGreenSoft, Theme.StatusAmberSoft)
        }
        box.SetBounds(24, 82, 512, 56)
        Dim lblBox As New Label With {
            .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Font = Theme.SmallBoldFont,
            .ForeColor = If(blocker Is Nothing, Theme.StatusGreen, Theme.StatusAmber), .BackColor = box.BackColor,
            .AutoEllipsis = True,
            .Text = If(blocker, ReadyMessage())
        }
        Tips.SetToolTip(lblBox, lblBox.Text)
        box.Controls.Add(lblBox)
        Body.Controls.Add(box)

        ' --- Assessed tax (only when moving to Assessed) ---
        txtAssessed.Font = Theme.InputFont
        txtAssessed.BorderStyle = BorderStyle.FixedSingle
        txtAssessed.Height = 32
        txtAssessed.PlaceholderText = "0.00"
        errAssessed = AddField("Assessed business tax (₱)", txtAssessed, 24, 154, 240)
        lblPreview.Font = Theme.SmallFont
        lblPreview.ForeColor = Theme.TextMuted
        lblPreview.AutoSize = False
        lblPreview.SetBounds(284, 176, 252, 60)
        Body.Controls.Add(lblPreview)
        Dim needsAmount = nextStatus = BusinessPermitService.Assessed AndAlso blocker Is Nothing
        For Each c In Body.Controls.Cast(Of Control)().Where(Function(x) x.Top >= 150 AndAlso x.Top < 240).ToList()
            c.Visible = needsAmount
        Next
        AddHandler txtAssessed.TextChanged, Sub() UpdatePreview()
        UpdatePreview()

        ' --- Footer ---
        btnAdvance = AddFooterButton(If(nextStatus Is Nothing, "Advance", "Move to " & nextStatus), "primary")
        Dim btnReject = AddFooterButton("Reject…", "danger")
        Dim btnCancel = AddFooterButton("Cancel")
        btnAdvance.Enabled = nextStatus IsNot Nothing AndAlso blocker Is Nothing
        btnReject.Enabled = Not BusinessPermitService.IsFinal(p.Status)
        AddHandler btnAdvance.Click, AddressOf Advance_Click
        AddHandler btnReject.Click, AddressOf Reject_Click
        AddHandler btnCancel.Click, Sub() DialogResult = DialogResult.Cancel
        CancelButton = btnCancel

        ' Shrink the dialog when there is no amount to enter
        If Not needsAmount Then ClientSize = New Size(560, 310)
    End Sub

    Private Function ReadyMessage() As String
        Select Case nextStatus
            Case BusinessPermitService.Assessed
                Return "All 5 offices have endorsed. Enter the assessed tax to continue."
            Case BusinessPermitService.Issued
                Return "Payment received. A Mayor's Permit number will be generated, valid until Dec 31, " &
                       permit.PermitYear & "."
            Case BusinessPermitService.Paid
                Return "Mark as paid: total due " & UiHelper.FormatMoney(BusinessPermitService.GetTotalDue(permit)) &
                       " (interest is updated to today)."
            Case Else
                Return "Ready to move to " & nextStatus & "."
        End Select
    End Function

    Private Function ParseAmount(ByRef amount As Decimal) As Boolean
        Dim text = txtAssessed.Text.Replace("₱", "").Replace(",", "").Trim()
        Return Decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, amount) AndAlso amount > 0
    End Function

    ''' <summary>Live preview of surcharge + interest for late renewals.</summary>
    Private Sub UpdatePreview()
        errAssessed.Text = ""
        Dim amount As Decimal
        If Not ParseAmount(amount) Then
            Dim deadline = BusinessPermitService.GetRenewalDeadline(permit.PermitYear)
            lblPreview.Text = If(permit.ApplicationType = "Renewal" AndAlso Date.Today > deadline,
                                 "Late renewal (deadline " & UiHelper.FormatDate(deadline) & "): surcharge and interest will be added.",
                                 "Renewal deadline: " & UiHelper.FormatDate(deadline))
            Return
        End If
        Dim charges = BusinessPermitService.ComputeLateCharges(permit.ApplicationType, permit.PermitYear, amount, Date.Today)
        If charges.IsLate Then
            lblPreview.Text = "Late by " & charges.MonthsLate & " month(s)" & vbCrLf &
                              "Surcharge " & UiHelper.FormatMoney(charges.Surcharge) & "  ·  Interest " & UiHelper.FormatMoney(charges.Interest) & vbCrLf &
                              "Total due " & UiHelper.FormatMoney(amount + charges.Surcharge + charges.Interest)
            lblPreview.ForeColor = Theme.StatusAmber
        Else
            lblPreview.Text = "On time - no surcharge." & vbCrLf & "Total due " & UiHelper.FormatMoney(amount)
            lblPreview.ForeColor = Theme.TextMuted
        End If
    End Sub

    Private Sub Advance_Click(sender As Object, e As EventArgs)
        Dim amount As Decimal = 0
        If nextStatus = BusinessPermitService.Assessed AndAlso Not ParseAmount(amount) Then
            errAssessed.Text = "Enter the assessed tax (more than 0)."
            txtAssessed.Focus()
            Return
        End If
        Dim problem = BusinessPermitService.Advance(permit, amount)
        If problem IsNot Nothing Then
            UiHelper.ShowWarning(problem)
            Return
        End If
        If permit.Status = BusinessPermitService.Issued Then
            UiHelper.ShowSuccess("Mayor's Permit " & permit.MayorsPermitNo & " was issued." & vbCrLf &
                                 "Valid until " & UiHelper.FormatDate(permit.ValidUntil) & ".", "Permit issued")
        End If
        DialogResult = DialogResult.OK
    End Sub

    Private Sub Reject_Click(sender As Object, e As EventArgs)
        Dim reason = PromptDialog.Ask(Me, "Reject Application", permit.ReferenceNo & " will be marked Rejected.",
                                      "Reason for rejecting", True, "Reject", "danger")
        If reason Is Nothing Then Return
        Dim problem = BusinessPermitService.Reject(permit, reason)
        If problem IsNot Nothing Then
            UiHelper.ShowWarning(problem)
            Return
        End If
        DialogResult = DialogResult.OK
    End Sub

End Class
