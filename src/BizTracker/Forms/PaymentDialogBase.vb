Imports System.Globalization

''' <summary>
''' Shared parts of every Record Payment dialog (RPT and business tax):
''' payment date, amount (defaults to the full balance), method, reference no. (not for cash),
''' the OR number typed from the AF 51 booklet, remarks, the breakdown panel helpers, the colored note
''' and Save with field errors. A module dialog adds its summary box and breakdown lines, then calls
''' FinishSetup(), and implements the four MustOverride members.
''' Layout (100% coordinates, 600 wide): fields take 3 rows of 74 px from "top" (BuildPaymentFields).
''' </summary>
Public MustInherit Class PaymentDialogBase
    Inherits ModernDialog

    Protected ReadOnly dtpDate As New DateTimePicker()
    Protected ReadOnly txtAmount As New TextBox()
    Protected ReadOnly cboMethod As New ComboBox()
    Protected ReadOnly txtOr As New TextBox()
    Protected ReadOnly txtReference As New TextBox()
    Protected ReadOnly txtRemarks As New TextBox()
    Private errDate As Label
    Private errAmount As Label
    Private errMethod As Label
    Private errOrNo As Label
    Private errReference As Label
    Private errRemarks As Label
    Private ReadOnly referenceControls As New List(Of Control)
    Private lblReferenceCaption As Label
    Private ReadOnly noteBox As New RoundedPanel()
    Private ReadOnly lblNote As New Label()

    ''' <summary>True once the cashier typed an amount (then a date change no longer replaces it).</summary>
    Private amountTouched As Boolean
    Private settingAmount As Boolean

    ' ==================== What each module provides ====================

    ''' <summary>The full balance due on the chosen payment date (the default amount).</summary>
    Protected MustOverride Function TotalDueOn(payDate As Date) As Decimal

    ''' <summary>Refreshes the module's breakdown lines and calls ShowAmountNote.</summary>
    Protected MustOverride Sub UpdateBreakdown()

    ''' <summary>Field errors (keys "date", "amount", "method", "reference", "or", "remarks").</summary>
    Protected MustOverride Function ValidateInput(input As PaymentInput) As Dictionary(Of String, String)

    ''' <summary>Saves the payment. Returns an error message, or Nothing when saved.</summary>
    Protected MustOverride Function SavePayment(input As PaymentInput) As String

    ' ==================== Building ====================

    ''' <summary>Date / amount / method, OR / reference, remarks - 3 rows starting at "top". Returns the y below them.</summary>
    Protected Function BuildPaymentFields(top As Integer, earliest As Date) As Integer
        dtpDate.Format = DateTimePickerFormat.Custom
        dtpDate.CustomFormat = "MMM dd, yyyy"
        dtpDate.MinDate = If(earliest > Date.Today, Date.Today, earliest.Date)
        dtpDate.MaxDate = Date.Today
        dtpDate.Value = Date.Today
        dtpDate.Font = Theme.InputFont
        dtpDate.Height = 32
        errDate = AddField("Payment date", dtpDate, 24, top, 190)

        StyleText(txtAmount, "", "0.00", 15)
        txtAmount.TextAlign = HorizontalAlignment.Right
        errAmount = AddField("Amount received (₱)", txtAmount, 234, top, 150)

        cboMethod.DropDownStyle = ComboBoxStyle.DropDownList
        cboMethod.FlatStyle = FlatStyle.Flat
        cboMethod.Font = Theme.InputFont
        UiHelper.StyleComboBox(cboMethod)
        cboMethod.Items.AddRange(PaymentMethods.All)
        cboMethod.SelectedItem = PaymentMethods.Cash
        errMethod = AddField("Payment method", UiHelper.CreateComboBox(cboMethod, 29), 404, top, 172)

        Dim lastOr = OfficialReceiptRepository.GetLastOrNo()
        StyleText(txtOr, "", If(lastOr = "", "e.g. 7001033", "Last used: " & lastOr), PaymentRules.MaxOrNoLength)
        errOrNo = AddField("OR No. (AF 51)", txtOr, 24, top + 74, 190)
        Tips.SetToolTip(txtOr, "Type the number printed on the Official Receipt (AF 51) you hand to the payer")

        Dim refStart = Body.Controls.Count
        StyleText(txtReference, "", "", PaymentRules.MaxReferenceLength)
        errReference = AddField("Reference no.", txtReference, 234, top + 74, 342)
        For i = refStart To Body.Controls.Count - 1
            referenceControls.Add(Body.Controls(i))
        Next
        lblReferenceCaption = DirectCast(referenceControls(0), Label)

        StyleText(txtRemarks, "", "Optional, e.g. partial payment - will pay the rest next week", PaymentRules.MaxRemarksLength)
        errRemarks = AddField("Remarks", txtRemarks, 24, top + 148, 552)
        Return top + 224
    End Function

    ''' <summary>A light-gray rounded panel for the breakdown lines.</summary>
    Protected Function AddBreakdownPanel(top As Integer, height As Integer) As RoundedPanel
        Dim breakdown As New RoundedPanel With {.BackColor = Theme.SoftBackground, .ShowShadow = False, .Radius = 10}
        breakdown.SetBounds(24, top, 552, height)
        Dim divider As New Panel With {.BackColor = Theme.Divider}
        divider.SetBounds(276, 12, 1, height - 24)
        breakdown.Controls.Add(divider)
        Body.Controls.Add(breakdown)
        Return breakdown
    End Function

    ''' <summary>One caption (left) / value (right-aligned) line of the breakdown, 252 wide at "left".</summary>
    Protected Sub AddBreakdownLine(parent As Control, caption As String, valueLabel As Label, left As Integer, top As Integer, bold As Boolean)
        Dim cap As New Label With {
            .Text = caption, .Font = If(bold, Theme.BodyBoldFont, Theme.BodyFont), .ForeColor = If(bold, Theme.TextDark, Theme.TextMuted),
            .AutoSize = False, .AutoEllipsis = True, .BackColor = parent.BackColor, .TextAlign = ContentAlignment.MiddleLeft
        }
        cap.SetBounds(left, top, 152, 26)
        Tips.SetToolTip(cap, caption)
        valueLabel.Font = If(bold, Theme.BodyBoldFont, Theme.BodyFont)
        valueLabel.ForeColor = Theme.TextDark
        valueLabel.AutoSize = False
        valueLabel.AutoEllipsis = True
        valueLabel.BackColor = parent.BackColor
        valueLabel.TextAlign = ContentAlignment.MiddleRight
        valueLabel.SetBounds(left + 152, top, 100, 26)
        parent.Controls.AddRange({cap, valueLabel})
    End Sub

    ''' <summary>The colored note under the breakdown.</summary>
    Protected Sub AddNoteBox(top As Integer)
        noteBox.ShowShadow = False
        noteBox.Radius = 10
        noteBox.Padding = New Padding(14, 0, 14, 0)
        noteBox.SetBounds(24, top, 552, 38)
        lblNote.Dock = DockStyle.Fill
        lblNote.TextAlign = ContentAlignment.MiddleLeft
        lblNote.Font = Theme.SmallBoldFont
        lblNote.AutoEllipsis = True
        noteBox.Controls.Add(lblNote)
        Body.Controls.Add(noteBox)
    End Sub

    ''' <summary>Footer buttons, event handlers, the default amount (full balance) and the first breakdown.</summary>
    Protected Sub FinishSetup()
        Dim save = AddFooterButton("Record Payment", "primary")
        Dim cancel = AddFooterButton("Cancel")
        AddHandler save.Click, AddressOf Save_Click
        AddHandler cancel.Click, Sub() DialogResult = DialogResult.Cancel
        AcceptButton = save
        CancelButton = cancel

        AddHandler dtpDate.ValueChanged, Sub()
                                             errDate.Text = ""
                                             If Not amountTouched Then SetAmount(TotalDueOn(PaymentDate))
                                             UpdateBreakdown()
                                         End Sub
        AddHandler txtAmount.TextChanged, Sub()
                                              errAmount.Text = ""
                                              If Not settingAmount Then amountTouched = True
                                              UpdateBreakdown()
                                          End Sub
        AddHandler txtAmount.KeyPress, Sub(s, e) e.Handled = Not (Char.IsDigit(e.KeyChar) OrElse Char.IsControl(e.KeyChar) OrElse
                                                                  e.KeyChar = "."c OrElse e.KeyChar = ","c)
        AddHandler cboMethod.SelectedIndexChanged, Sub()
                                                       errMethod.Text = ""
                                                       ShowReferenceField()
                                                   End Sub
        AddHandler txtOr.TextChanged, Sub() errOrNo.Text = ""
        AddHandler txtReference.TextChanged, Sub() errReference.Text = ""
        AddHandler txtRemarks.TextChanged, Sub() errRemarks.Text = ""

        SetAmount(TotalDueOn(PaymentDate))       ' default: the full remaining balance
        ShowReferenceField()
        UpdateBreakdown()
    End Sub

    ' ==================== Values ====================

    Protected ReadOnly Property PaymentDate As Date
        Get
            Return dtpDate.Value.Date
        End Get
    End Property

    ''' <summary>The typed amount (commas allowed as thousands separators); -1 when it is not a number.</summary>
    Protected ReadOnly Property AmountValue As Decimal
        Get
            Dim value As Decimal
            If Decimal.TryParse(txtAmount.Text.Replace(",", "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, value) Then Return value
            Return -1D
        End Get
    End Property

    Protected ReadOnly Property SelectedMethod As String
        Get
            Return If(cboMethod.SelectedItem?.ToString(), "")
        End Get
    End Property

    ''' <summary>
    ''' The colored note: amount missing, more than the balance, pays it in full, or a partial payment.
    ''' "what" names what is being paid ("Q2 2026", "BP-2027-00003").
    ''' </summary>
    Protected Sub ShowAmountNote(totalDue As Decimal, completes As Boolean, remainingAfter As Decimal, what As String,
                                 chargesRunning As Boolean, chargesIncluded As Boolean)
        Dim amount = AmountValue
        Dim color As Color, soft As Color, text As String
        If amount <= 0D Then
            color = Theme.StatusAmber : soft = Theme.StatusAmberSoft
            text = "Enter the amount received."
        ElseIf amount > totalDue Then
            color = Theme.StatusRed : soft = Theme.StatusRedSoft
            text = "More than the balance due (" & UiHelper.FormatMoney(totalDue) & ") - give change instead."
        ElseIf completes Then
            color = Theme.StatusGreen : soft = Theme.StatusGreenSoft
            text = "This pays " & what & " in full" & If(chargesIncluded, " (late charges included).", ".")
        Else
            color = Theme.StatusAmber : soft = Theme.StatusAmberSoft
            text = "Partial payment: " & UiHelper.FormatMoney(remainingAfter) & " of " & what & " will still be due" &
                   If(chargesRunning, " (charges keep running).", ".")
        End If
        noteBox.BackColor = soft
        lblNote.BackColor = soft
        lblNote.ForeColor = color
        lblNote.Text = text
        Tips.SetToolTip(lblNote, text)
        noteBox.Invalidate()
    End Sub

    ' ==================== Helpers ====================

    Private Shared Sub StyleText(box As TextBox, value As String, placeholder As String, maxLength As Integer)
        box.Font = Theme.InputFont
        box.BorderStyle = BorderStyle.FixedSingle
        box.MaxLength = maxLength
        box.Text = value
        box.PlaceholderText = placeholder
    End Sub

    Private Sub SetAmount(value As Decimal)
        settingAmount = True
        txtAmount.Text = value.ToString("0.00", CultureInfo.InvariantCulture)
        settingAmount = False
    End Sub

    ''' <summary>Reference no. only for Check / GCash / Bank Transfer, with a caption that says what to type.</summary>
    Private Sub ShowReferenceField()
        Dim needs = PaymentMethods.NeedsReference(SelectedMethod)
        For Each c In referenceControls
            c.Visible = needs
        Next
        Select Case SelectedMethod
            Case PaymentMethods.Check
                lblReferenceCaption.Text = "Check no. (and bank)"
                txtReference.PlaceholderText = "e.g. 0004512 - Landbank"
            Case PaymentMethods.GCash
                lblReferenceCaption.Text = "GCash reference no."
                txtReference.PlaceholderText = "13-digit reference from the GCash receipt"
            Case PaymentMethods.BankTransfer
                lblReferenceCaption.Text = "Bank transaction reference"
                txtReference.PlaceholderText = "Reference from the deposit slip or transfer"
        End Select
    End Sub

    Private Sub Save_Click(sender As Object, e As EventArgs)
        Dim map As New Dictionary(Of String, Label) From {
            {"date", errDate}, {"amount", errAmount}, {"method", errMethod}, {"or", errOrNo},
            {"reference", errReference}, {"remarks", errRemarks}}
        For Each lbl In map.Values
            lbl.Text = ""
        Next
        Dim input As New PaymentInput With {
            .PaymentDate = PaymentDate, .Amount = AmountValue, .Method = SelectedMethod,
            .ReferenceNo = txtReference.Text, .OrNo = txtOr.Text, .Remarks = txtRemarks.Text
        }
        Dim errors = ValidateInput(input)
        If AmountValue < 0D Then errors("amount") = "Numbers only, e.g. 125.00"
        If errors.Count > 0 Then
            For Each kv In errors
                Dim lbl As Label = Nothing
                If map.TryGetValue(kv.Key, lbl) Then
                    lbl.Text = kv.Value
                    Tips.SetToolTip(lbl, kv.Value)
                End If
            Next
            Return
        End If

        Cursor = Cursors.WaitCursor
        Dim problem = SavePayment(input)
        Cursor = Cursors.Default
        If problem IsNot Nothing Then
            UiHelper.ShowWarning(problem)
            Return
        End If
        DialogResult = DialogResult.OK
    End Sub

End Class
