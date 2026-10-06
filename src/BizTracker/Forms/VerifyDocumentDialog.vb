''' <summary>
''' "Verify Document": type the reference number (and, optionally, the verification code) printed on a
''' document and see whether it is a genuine, valid issued document. No SQL here: DocumentService.Verify.
''' </summary>
Public Class VerifyDocumentDialog
    Inherits ModernDialog

    Private ReadOnly txtRef As New TextBox()
    Private ReadOnly txtCode As New TextBox()
    Private ReadOnly lblRefError As Label
    Private ReadOnly resultBox As New Panel()
    Private ReadOnly resultIcon As New IconButton()
    Private ReadOnly lblResultTitle As New Label()
    Private ReadOnly lblResultMessage As New Label()
    Private ReadOnly lblHint As New Label()
    Private ReadOnly detailLabels As New List(Of Label)

    ''' <summary>Status of the last check (Valid / Expired / Not Found / Code Mismatch, or "").</summary>
    Public Property ResultStatus As String = ""

    Public Sub New(Optional referenceNo As String = Nothing)
        ClientSize = New Size(560, 470)
        SetTitle("Verify Document", "Check a printed permit, certificate or receipt against the records")

        For Each t In {txtRef, txtCode}
            t.Font = Theme.InputFont
            t.BorderStyle = BorderStyle.FixedSingle
            t.CharacterCasing = CharacterCasing.Upper
        Next
        txtRef.MaxLength = 30
        txtCode.MaxLength = 11
        txtRef.Text = If(referenceNo, "")
        lblRefError = AddField("Reference number", txtRef, 24, 18, 300)
        AddField("Verification code (optional)", txtCode, 340, 18, 196)

        lblHint.Text = "Both are printed at the bottom of every document, e.g. MP-2026-00001 and 7F3A2-9C01B."
        lblHint.Font = Theme.SmallFont
        lblHint.ForeColor = Theme.TextMuted
        lblHint.AutoEllipsis = True
        lblHint.SetBounds(24, 104, 512, 20)
        Body.Controls.Add(lblHint)

        ' Result card (hidden until the first check)
        resultBox.SetBounds(24, 134, 512, 180)
        resultBox.Visible = False
        resultIcon.IconSize = 14.0F
        resultIcon.Cursor = Cursors.Default
        resultIcon.SetBounds(16, 16, 40, 40)
        lblResultTitle.Font = Theme.SubtitleFont
        lblResultTitle.AutoEllipsis = True
        lblResultTitle.SetBounds(68, 14, 430, 26)
        lblResultMessage.Font = Theme.BodyFont
        lblResultMessage.ForeColor = Theme.TextDark
        lblResultMessage.AutoEllipsis = True
        lblResultMessage.SetBounds(68, 42, 430, 40)
        resultBox.Controls.AddRange({resultIcon, lblResultTitle, lblResultMessage})
        For i = 0 To 3
            Dim lbl As New Label With {.Font = Theme.SmallFont, .ForeColor = Theme.TextDark, .AutoEllipsis = True}
            lbl.SetBounds(68 + (i Mod 2) * 216, 92 + (i \ 2) * 40, 210, 36)
            detailLabels.Add(lbl)
            resultBox.Controls.Add(lbl)
        Next
        Body.Controls.Add(resultBox)

        Dim btnVerify = AddFooterButton("Verify", "primary")
        Dim btnClose = AddFooterButton("Close")
        AddHandler btnVerify.Click, Sub() RunVerify()
        AddHandler btnClose.Click, Sub() DialogResult = DialogResult.Cancel
        AcceptButton = btnVerify
        CancelButton = btnClose
    End Sub

    ''' <summary>Checks the typed reference number / code and shows the result.</summary>
    Public Sub RunVerify()
        lblRefError.Text = ""
        Dim result = DocumentService.Verify(txtRef.Text, txtCode.Text)
        ResultStatus = result.Status
        If result.Status = "" Then
            lblRefError.Text = result.Message
            resultBox.Visible = False
            txtRef.Focus()
            Return
        End If

        Dim color As Color, soft As Color, glyph As String, heading As String
        Select Case result.Status
            Case DocumentService.VerifyValid
                color = Theme.StatusGreen : soft = Theme.StatusGreenSoft : glyph = Icons.CheckMark : heading = "Genuine and valid"
            Case DocumentService.VerifyExpired
                color = Theme.StatusAmber : soft = Theme.StatusAmberSoft : glyph = Icons.Warning : heading = "Genuine but expired"
            Case DocumentService.VerifyMismatch
                color = Theme.StatusRed : soft = Theme.StatusRedSoft : glyph = Icons.Warning : heading = "Code does not match"
            Case Else
                color = Theme.StatusRed : soft = Theme.StatusRedSoft : glyph = Icons.Close : heading = "Not found"
        End Select
        resultBox.BackColor = soft
        resultIcon.BackColor = soft
        resultIcon.CircleColor = Color.White
        resultIcon.IconColor = color
        resultIcon.Glyph = glyph
        lblResultTitle.Text = heading
        lblResultTitle.ForeColor = color
        lblResultMessage.Text = result.Message
        Tips.SetToolTip(lblResultMessage, result.Message)

        Dim d = result.Document
        Dim details As String() = If(d Is Nothing, {"", "", "", ""},
            {"Document" & vbCrLf & d.Title, "Issued to" & vbCrLf & d.Holder,
             "Business" & vbCrLf & d.BusinessName,
             "Issued · valid until" & vbCrLf & UiHelper.FormatDate(d.IssueDate) & " · " &
             If(d.ValidUntil.HasValue, UiHelper.FormatDate(d.ValidUntil), "no expiry")})
        For i = 0 To 3
            detailLabels(i).Text = details(i)
            detailLabels(i).Visible = details(i) <> ""
            Tips.SetToolTip(detailLabels(i), details(i).Replace(vbCrLf, ": "))
        Next
        resultBox.Height = If(d Is Nothing, 96, 180)
        resultBox.Visible = True
    End Sub

    ''' <summary>For tests: fills the fields and runs the check.</summary>
    Public Sub VerifyText(referenceNo As String, Optional code As String = "")
        txtRef.Text = referenceNo
        txtCode.Text = code
        RunVerify()
    End Sub

End Class
