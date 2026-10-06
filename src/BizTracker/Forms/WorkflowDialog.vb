''' <summary>
''' Generic "Update Status / Advance Stage" dialog used by the module screens:
''' shows Current -> Next, a green "ready" or amber "blocked" box, and buttons to move to the
''' next step only (or reject with a reason). The rules live in the module's service; this
''' dialog just calls the advance / reject functions it is given (they return an error message or Nothing).
''' </summary>
Public Class WorkflowDialog
    Inherits ModernDialog

    Private ReadOnly advance As Func(Of String)
    Private ReadOnly reject As Func(Of String, String)
    Private ReadOnly rejectTitle As String

    ''' <param name="nextStatus">Next status/stage, or Nothing when already final.</param>
    ''' <param name="blocker">Why it cannot advance yet (Nothing = ready).</param>
    ''' <param name="readyMessage">Shown in the green box when ready.</param>
    ''' <param name="advanceAction">Moves to the next step; returns an error message or Nothing.</param>
    ''' <param name="rejectAction">Rejects with a reason; Nothing hides the Reject button.</param>
    Public Sub New(title As String, subtitle As String, currentStatus As String, nextStatus As String,
                   blocker As String, readyMessage As String, advanceAction As Func(Of String),
                   Optional rejectAction As Func(Of String, String) = Nothing, Optional rejectHeading As String = "Reject",
                   Optional advanceText As String = "")
        advance = advanceAction
        reject = rejectAction
        rejectTitle = rejectHeading
        ClientSize = New Size(560, 310)
        SetTitle(title, subtitle)

        ' --- Current -> Next ---
        Dim lblFrom As New Label With {.Text = "Current", .Font = Theme.SmallFont, .ForeColor = Theme.TextMuted, .AutoSize = True,
                                       .Location = New Point(24, 18)}
        Dim badgeFrom As New StatusBadge With {.Text = currentStatus, .Location = New Point(24, 40)}
        Dim arrow As New Label With {.Text = ChrW(&HE72A), .Font = Theme.IconFont(12.0F), .ForeColor = Theme.TextMuted,
                                     .AutoSize = True, .Location = New Point(badgeFrom.Right + 16, 43)}
        Dim lblTo As New Label With {.Text = "Next", .Font = Theme.SmallFont, .ForeColor = Theme.TextMuted, .AutoSize = True,
                                     .Location = New Point(arrow.Right + 40, 18)}
        Dim badgeTo As New StatusBadge With {.Text = If(nextStatus, "—"), .Location = New Point(arrow.Right + 40, 40)}
        Body.Controls.AddRange({lblFrom, badgeFrom, arrow, lblTo, badgeTo})

        ' --- Readiness box (green = ready, amber = blocked) ---
        Dim ready = blocker Is Nothing AndAlso nextStatus IsNot Nothing
        Dim box As New RoundedPanel With {
            .ShowShadow = False, .Radius = 10, .Padding = New Padding(14, 0, 14, 0),
            .BackColor = If(ready, Theme.StatusGreenSoft, Theme.StatusAmberSoft)
        }
        box.SetBounds(24, 82, 512, 56)
        Dim lblBox As New Label With {
            .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Font = Theme.SmallBoldFont,
            .ForeColor = If(ready, Theme.StatusGreen, Theme.StatusAmber), .BackColor = box.BackColor,
            .AutoEllipsis = True, .Text = If(blocker, readyMessage)
        }
        Tips.SetToolTip(lblBox, lblBox.Text)
        box.Controls.Add(lblBox)
        Body.Controls.Add(box)

        ' --- Footer ---
        Dim btnAdvance = AddFooterButton(If(advanceText <> "", advanceText,
                                            If(nextStatus Is Nothing, "Advance", "Move to " & nextStatus)), "primary")
        btnAdvance.Enabled = ready
        AddHandler btnAdvance.Click, AddressOf Advance_Click
        If reject IsNot Nothing Then
            Dim btnReject = AddFooterButton("Reject…", "danger")
            AddHandler btnReject.Click, AddressOf Reject_Click
        End If
        Dim btnCancel = AddFooterButton("Cancel")
        AddHandler btnCancel.Click, Sub() DialogResult = DialogResult.Cancel
        CancelButton = btnCancel
    End Sub

    Private Sub Advance_Click(sender As Object, e As EventArgs)
        Cursor = Cursors.WaitCursor
        Dim problem = advance()
        Cursor = Cursors.Default
        If problem IsNot Nothing Then
            UiHelper.ShowWarning(problem)
            Return
        End If
        DialogResult = DialogResult.OK
    End Sub

    Private Sub Reject_Click(sender As Object, e As EventArgs)
        Dim reason = PromptDialog.Ask(Me, rejectTitle, "This record will be marked Rejected.", "Reason for rejecting",
                                      True, "Reject", "danger")
        If reason Is Nothing Then Return
        Dim problem = reject(reason)
        If problem IsNot Nothing Then
            UiHelper.ShowWarning(problem)
            Return
        End If
        DialogResult = DialogResult.OK
    End Sub

End Class
