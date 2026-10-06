''' <summary>
''' Reusable document checklist for ANY module (Business Permit, Sanitary, Construction, ...).
''' Call LoadFor(...) with the business, module and record. Applicants (owner or staff on their
''' behalf) can upload; the module's staff can verify or reject. No scrollbars: the list
''' scrolls with the mouse wheel.
''' </summary>
Public Class RequirementsPanel
    Inherits UserControl

    Private ReadOnly lblSummary As New Label()
    Private ReadOnly list As New WheelScrollPanel()
    Private ReadOnly tips As New ToolTip()

    Private businessId As Integer
    Private moduleName As String = ""
    Private relatedId As Integer?
    Private screen As AppScreen
    Private canUpload As Boolean
    Private canVerify As Boolean
    Private currentDocs As New List(Of Requirement)

    ''' <summary>Raised after an upload / verify / reject so the parent can refresh its status.</summary>
    Public Event RequirementsChanged As EventHandler

    Public Sub New()
        AutoScaleDimensions = New SizeF(96.0F, 96.0F)
        AutoScaleMode = AutoScaleMode.Dpi
        BackColor = Color.White
        Font = Theme.BodyFont

        lblSummary.Dock = DockStyle.Top
        lblSummary.Height = Dpi(26)
        lblSummary.Font = Theme.SmallFont
        lblSummary.ForeColor = Theme.TextMuted
        lblSummary.TextAlign = ContentAlignment.MiddleLeft
        lblSummary.AutoEllipsis = True
        list.Dock = DockStyle.Fill
        list.BackColor = Color.White
        AddHandler list.Scrolled, Sub() UpdateSummary()

        Controls.Add(list)
        Controls.Add(lblSummary)
    End Sub

    ''' <summary>
    ''' Shows the checklist for one record. allowUpload: the applicant side may upload
    ''' (e.g. owner of this business, application not yet final).
    ''' </summary>
    Public Sub LoadFor(bizId As Integer, moduleTitle As String, recordId As Integer?, moduleScreen As AppScreen,
                       allowUpload As Boolean, allowVerify As Boolean)
        businessId = bizId
        moduleName = moduleTitle
        relatedId = recordId
        screen = moduleScreen
        canUpload = allowUpload AndAlso AccessService.CanSeeBusiness(bizId)
        canVerify = allowVerify AndAlso AccessService.CanManage(moduleScreen)
        RefreshList()
    End Sub

    ''' <summary>Shows a message instead of a list (e.g. "Select an application").</summary>
    Public Sub ShowMessage(message As String)
        moduleName = ""
        currentDocs = New List(Of Requirement)()
        list.SetRows(New List(Of Control)())
        lblSummary.Text = message
    End Sub

    Public Sub RefreshList()
        If moduleName = "" Then Return
        currentDocs = RequirementRepository.GetByModule(businessId, moduleName, relatedId)
        list.SetRows(currentDocs.Select(Function(r) CreateRow(r)).ToList())
        UpdateSummary()
    End Sub

    Private Sub UpdateSummary()
        If moduleName = "" Then Return
        Dim docs = currentDocs
        If docs.Count = 0 Then
            lblSummary.Text = "No documents required for this record."
            Return
        End If
        Dim verified = docs.Where(Function(r) r.Status = "Verified").Count()
        lblSummary.Text = verified & " of " & docs.Count & " documents verified"
    End Sub

    ''' <summary>One checklist row: name + detail on the left, status badge and actions on the right.</summary>
    Private Function CreateRow(r As Requirement) As Control
        Dim row As New Panel With {.Height = Dpi(52), .BackColor = Color.White}
        AddHandler row.Paint,
            Sub(s, e)
                Using pen As New Pen(Theme.Divider)
                    e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1)
                End Using
            End Sub

        ' Right side: actions + badge (laid out right-to-left)
        Dim right As New FlowLayoutPanel With {
            .Dock = DockStyle.Right, .AutoSize = True, .WrapContents = False,
            .FlowDirection = FlowDirection.RightToLeft, .BackColor = Color.White, .Padding = New Padding(0, Dpi(12), 0, 0)
        }
        If canVerify AndAlso r.Status = "Submitted" Then
            right.Controls.Add(MakeLink("Reject", Sub() RejectDoc(r)))
            right.Controls.Add(MakeLink("Verify", Sub() VerifyDoc(r)))
        End If
        ' A reviewer deciding a submitted file gets Verify / Reject instead of Replace (keeps the row readable)
        Dim reviewing = canVerify AndAlso r.Status = "Submitted"
        If canUpload AndAlso r.Status <> "Verified" AndAlso Not reviewing Then
            right.Controls.Add(MakeLink(If(String.IsNullOrEmpty(r.FilePath), "Upload", "Replace"), Sub() UploadDoc(r)))
        End If
        If Not String.IsNullOrEmpty(r.FilePath) Then
            right.Controls.Add(MakeLink("View", Sub() ViewDoc(r)))
        End If
        right.Controls.Add(New StatusBadge With {.Text = r.Status, .Height = Dpi(24), .Margin = New Padding(Dpi(8), Dpi(1), Dpi(4), 0)})

        ' Left side: document name + one line of detail
        Dim left As New Panel With {.Dock = DockStyle.Fill, .BackColor = Color.White}
        Dim lblName As New Label With {
            .Text = r.DocumentName, .Font = Theme.BodyBoldFont, .ForeColor = Theme.TextDark,
            .Dock = DockStyle.Top, .Height = Dpi(26), .AutoEllipsis = True, .TextAlign = ContentAlignment.BottomLeft
        }
        Dim lblDetail As New Label With {
            .Text = DetailText(r), .Font = Theme.SmallFont, .AutoEllipsis = True, .Dock = DockStyle.Top, .Height = Dpi(20),
            .ForeColor = If(r.Status = "Rejected", Theme.StatusRed, Theme.TextMuted)
        }
        tips.SetToolTip(lblName, r.DocumentName)
        tips.SetToolTip(lblDetail, lblDetail.Text)
        left.Controls.Add(lblDetail)
        left.Controls.Add(lblName)

        row.Controls.Add(left)
        row.Controls.Add(right)
        Return row
    End Function

    Private Shared Function DetailText(r As Requirement) As String
        Select Case r.Status
            Case "Verified"
                Return "Verified" & If(r.VerifiedByName <> "", " by " & r.VerifiedByName, "") & " · " & UiHelper.FormatDate(r.VerifiedAt)
            Case "Rejected"
                Return "Rejected: " & If(r.Remarks <> "", r.Remarks, "please upload a new copy")
            Case "Submitted"
                Return "Uploaded " & UiHelper.FormatDate(r.UploadedAt) & " · waiting for verification"
            Case Else
                Return "Not uploaded yet"
        End Select
    End Function

    Private Function MakeLink(text As String, action As Action) As LinkLabel
        Dim link As New LinkLabel With {
            .Text = text, .AutoSize = True, .Font = Theme.SmallBoldFont, .LinkColor = Theme.SidebarBlue,
            .ActiveLinkColor = Theme.SidebarActive, .LinkBehavior = LinkBehavior.HoverUnderline,
            .Margin = New Padding(Dpi(10), Dpi(4), 0, 0), .BackColor = Color.White
        }
        If text = "Reject" Then link.LinkColor = Theme.StatusRed
        AddHandler link.LinkClicked, Sub() action()
        Return link
    End Function

    ' ---------------- actions ----------------

    Private Sub UploadDoc(r As Requirement)
        Using dlg As New OpenFileDialog With {
            .Title = "Upload " & r.DocumentName, .Filter = RequirementService.FileDialogFilter, .Multiselect = False}
            If dlg.ShowDialog(Me) <> DialogResult.OK Then Return
            Dim problem = RequirementService.Upload(r, dlg.FileName)
            If problem IsNot Nothing Then
                UiHelper.ShowWarning(problem, "Upload")
                Return
            End If
        End Using
        Changed()
    End Sub

    Private Sub ViewDoc(r As Requirement)
        Dim problem = RequirementService.OpenFile(r)
        If problem IsNot Nothing Then UiHelper.ShowWarning(problem, "Open document")
    End Sub

    Private Sub VerifyDoc(r As Requirement)
        Dim problem = RequirementService.SetVerification(r, screen, True)
        If problem IsNot Nothing Then
            UiHelper.ShowWarning(problem)
            Return
        End If
        Changed()
    End Sub

    Private Sub RejectDoc(r As Requirement)
        Dim reason = PromptDialog.Ask(FindForm(), "Reject Document", r.DocumentName,
                                      "Reason (shown to the applicant)", True, "Reject", "danger")
        If reason Is Nothing Then Return
        Dim problem = RequirementService.SetVerification(r, screen, False, reason)
        If problem IsNot Nothing Then
            UiHelper.ShowWarning(problem)
            Return
        End If
        Changed()
    End Sub

    Private Sub Changed()
        RefreshList()
        RaiseEvent RequirementsChanged(Me, EventArgs.Empty)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then tips.Dispose()
        MyBase.Dispose(disposing)
    End Sub

End Class
