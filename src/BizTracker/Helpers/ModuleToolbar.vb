''' <summary>
''' The toolbar at the top of every module screen (same look as Business Permits):
'''   left  : business selector (staff) or the owner's own business (fixed),
'''           or any other control (e.g. the Admin screen's tabs) given to the second constructor
'''   right : action buttons; their labels switch to short versions when the window is narrow.
''' Raises BusinessChanged when another business is chosen.
''' </summary>
Public Class ModuleToolbar
    Inherits Panel

    ''' <summary>Last business picked per screen, so a module re-opens on the same business.</summary>
    Private Shared ReadOnly lastBusiness As New Dictionary(Of String, Integer)

    Private ReadOnly rememberKey As String
    Private ReadOnly businessBox As New RoundedPanel()
    Private ReadOnly cboBusiness As New ComboBox()
    Private ReadOnly lblBusiness As New Label()
    Private ReadOnly actions As New FlowLayoutPanel()
    ''' <summary>The control on the left: the business box, or a custom control.</summary>
    Private ReadOnly leftControl As Control
    Private ReadOnly leftMin As Integer = 220
    Private ReadOnly leftMax As Integer = 380
    Private ReadOnly buttonTexts As New Dictionary(Of Button, String())   ' {full, short}
    Private ReadOnly tips As New ToolTip()
    Private loading As Boolean

    ''' <summary>The selected business (Nothing if none).</summary>
    Public Property Business As Business

    Public Event BusinessChanged As EventHandler

    ''' <param name="key">A name for the screen (e.g. "rpt") used to remember the last business.</param>
    Public Sub New(key As String)
        rememberKey = key
        leftControl = businessBox
        Dock = DockStyle.Fill
        BackColor = Theme.ContentBackground
        Margin = New Padding(0)

        businessBox.ShowShadow = False
        businessBox.Radius = 10
        businessBox.Padding = New Padding(40, 0, 10, 0)
        businessBox.SetBounds(0, 2, 340, 44)
        Dim icon As New Label With {
            .Text = Icons.Contact, .Font = Theme.IconFont(12.0F), .ForeColor = Theme.SidebarBlue,
            .BackColor = Color.White, .TextAlign = ContentAlignment.MiddleCenter, .AutoSize = False
        }
        icon.SetBounds(10, 2, 26, 40)
        businessBox.Controls.Add(icon)

        If Session.IsOwner Then
            lblBusiness.Font = Theme.BodyBoldFont
            lblBusiness.ForeColor = Theme.TextDark
            lblBusiness.BackColor = Color.White
            lblBusiness.AutoEllipsis = True
            lblBusiness.TextAlign = ContentAlignment.MiddleLeft
            lblBusiness.Dock = DockStyle.Fill
            businessBox.Controls.Add(lblBusiness)
        Else
            cboBusiness.DropDownStyle = ComboBoxStyle.DropDownList
            cboBusiness.FlatStyle = FlatStyle.Flat
            cboBusiness.Font = Theme.InputFont
            cboBusiness.BackColor = Color.White
            cboBusiness.ForeColor = Theme.TextDark
            UiHelper.StyleComboBox(cboBusiness)
            businessBox.Controls.Add(cboBusiness)
            AddHandler businessBox.Resize,
                Sub()
                    cboBusiness.Width = businessBox.ClientSize.Width - businessBox.Padding.Horizontal
                    cboBusiness.Location = New Point(businessBox.Padding.Left, (businessBox.ClientSize.Height - cboBusiness.Height) \ 2)
                End Sub
            AddHandler cboBusiness.SelectedIndexChanged, AddressOf Combo_Changed
            tips.SetToolTip(cboBusiness, "Choose a business")
        End If
        Controls.Add(businessBox)
        AddActionsPanel()
    End Sub

    ''' <summary>
    ''' A toolbar without a business selector: "left" (e.g. SegmentedTabs) takes the left side and is
    ''' sized between minWidth and maxWidth (100% pixels) next to the action buttons.
    ''' </summary>
    Public Sub New(left As Control, minWidth As Integer, maxWidth As Integer)
        rememberKey = ""
        leftControl = left
        leftMin = minWidth
        leftMax = maxWidth
        Dock = DockStyle.Fill
        BackColor = Theme.ContentBackground
        Margin = New Padding(0)
        Controls.Add(left)          ' the caller sets its top and height
        AddActionsPanel()
    End Sub

    Private Sub AddActionsPanel()
        actions.Dock = DockStyle.Right
        actions.AutoSize = True
        actions.WrapContents = False
        actions.FlowDirection = FlowDirection.LeftToRight
        actions.Padding = New Padding(0, 3, 0, 0)
        actions.BackColor = Theme.ContentBackground
        Controls.Add(actions)
    End Sub

    ''' <summary>
    ''' Adds an action button (call in left-to-right order). style: "primary" / "secondary" / "danger".
    ''' Hidden buttons (isVisible = False) take no space.
    ''' </summary>
    Public Function AddAction(fullText As String, shortText As String, style As String, handler As EventHandler,
                              Optional isVisible As Boolean = True, Optional tip As String = "") As Button
        Dim btn As New Button With {.Height = 40, .Margin = New Padding(8, 0, 0, 0)}
        Select Case style
            Case "primary" : UiHelper.StylePrimaryButton(btn)
            Case "danger" : UiHelper.StyleDangerButton(btn)
            Case Else : UiHelper.StyleSecondaryButton(btn)
        End Select
        btn.Visible = isVisible
        buttonTexts(btn) = {fullText, shortText}
        SetButtonText(btn, fullText)
        AddHandler btn.Click, handler
        tips.SetToolTip(btn, If(tip <> "", tip, fullText))
        actions.Controls.Add(btn)
        Return btn
    End Function

    ''' <summary>Changes a button's labels (e.g. "Put On Hold" / "Resume"), keeping the narrow-window behavior.</summary>
    Public Sub SetActionText(btn As Button, fullText As String, shortText As String)
        Dim current As String() = Nothing
        If buttonTexts.TryGetValue(btn, current) AndAlso current(0) = fullText AndAlso current(1) = shortText Then Return
        buttonTexts(btn) = {fullText, shortText}
        FitToolbar()
    End Sub

    ''' <summary>Re-fits the labels after buttons were shown or hidden (e.g. when switching tabs).</summary>
    Public Sub Refit()
        FitToolbar()
    End Sub

    ''' <summary>Changes a button's tooltip (e.g. to explain why it is disabled).</summary>
    Public Sub SetTip(btn As Button, text As String)
        tips.SetToolTip(btn, text)
    End Sub

    Private Sub SetButtonText(btn As Button, text As String)
        btn.Text = text
        btn.Width = TextRenderer.MeasureText(text, btn.Font).Width + CInt(32 * DeviceDpi / 96.0)
    End Sub

    Protected Overrides Sub OnResize(eventargs As EventArgs)
        MyBase.OnResize(eventargs)
        FitToolbar()
    End Sub

    ''' <summary>Uses short button labels and a narrower business box when space is tight.</summary>
    Private Sub FitToolbar()
        If actions Is Nothing Then Return
        Dim gap = CInt(16 * DeviceDpi / 96.0)
        Dim minBox = CInt(leftMin * DeviceDpi / 96.0)
        Dim maxBox = CInt(leftMax * DeviceDpi / 96.0)
        For Each useShort In {False, True}
            For Each kv In buttonTexts
                SetButtonText(kv.Key, kv.Value(If(useShort, 1, 0)))
            Next
            actions.PerformLayout()
            If ClientSize.Width - actions.PreferredSize.Width - gap >= minBox Then Exit For
        Next
        Dim room = ClientSize.Width - actions.PreferredSize.Width - gap
        If leftControl Is businessBox Then
            leftControl.Width = Math.Max(minBox, Math.Min(maxBox, room))
        Else
            ' A custom control (e.g. tabs) never slides under the buttons: it gets narrower instead
            leftControl.Width = Math.Max(CInt(120 * DeviceDpi / 96.0), Math.Min(maxBox, room))
        End If
    End Sub

    ''' <summary>Fills the business list (staff) or shows the owner's business, then raises BusinessChanged.</summary>
    Public Sub LoadBusinesses()
        If Session.IsOwner Then
            Business = If(Session.BusinessId.HasValue, BusinessRepository.GetById(Session.BusinessId.Value), Nothing)
            lblBusiness.Text = If(Business?.BusinessName, "No business linked")
            tips.SetToolTip(lblBusiness, lblBusiness.Text)
            RaiseEvent BusinessChanged(Me, EventArgs.Empty)
            Return
        End If

        loading = True
        Dim list = BusinessRepository.GetAll()
        cboBusiness.Items.Clear()
        cboBusiness.Items.AddRange(list.Cast(Of Object)().ToArray())
        Dim lastId As Integer = 0
        lastBusiness.TryGetValue(rememberKey, lastId)
        Dim requested = ModuleView.TakePendingBusiness()     ' e.g. opened from the dashboard
        If requested.HasValue Then lastId = requested.Value
        Dim index = list.FindIndex(Function(b) b.BusinessId = lastId)
        loading = False
        If list.Count > 0 Then
            cboBusiness.SelectedIndex = Math.Max(0, index)   ' raises BusinessChanged
        Else
            Business = Nothing
            RaiseEvent BusinessChanged(Me, EventArgs.Empty)
        End If
    End Sub

    Private Sub Combo_Changed(sender As Object, e As EventArgs)
        If loading Then Return
        Business = TryCast(cboBusiness.SelectedItem, Business)
        If Business IsNot Nothing Then lastBusiness(rememberKey) = Business.BusinessId
        RaiseEvent BusinessChanged(Me, EventArgs.Empty)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then tips.Dispose()
        MyBase.Dispose(disposing)
    End Sub

End Class
