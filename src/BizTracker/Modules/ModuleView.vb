''' <summary>
''' Base class for every page loaded into MainForm's content area.
''' MainForm docks it to fill the content panel and calls RefreshData() on F5.
''' </summary>
Public Class ModuleView
    Inherits UserControl

    Public Sub New()
        AutoScaleDimensions = New SizeF(96.0F, 96.0F)
        AutoScaleMode = AutoScaleMode.Dpi
        BackColor = Theme.ContentBackground
        Font = Theme.BodyFont
        DoubleBuffered = True
        UiHelper.DisableMnemonics(Me)     ' show "&" in data as-is
    End Sub

    ''' <summary>Optional text shown under the page title in the top bar.</summary>
    Public Overridable ReadOnly Property Subtitle As String
        Get
            Return ""
        End Get
    End Property

    ' ==================== Navigation ====================

    ''' <summary>
    ''' Raised when the page wants MainForm to open another screen (e.g. the dashboard's "Open" buttons).
    ''' businessId (optional) = the business the target screen should select first.
    ''' </summary>
    Public Event NavigateRequested(screen As AppScreen, businessId As Integer?)

    Protected Sub RequestNavigate(screen As AppScreen, Optional businessId As Integer? = Nothing)
        RaiseEvent NavigateRequested(screen, businessId)
    End Sub

    ''' <summary>Business the next opened screen should select first (set by MainForm when navigating).</summary>
    Private Shared pendingBusinessId As Integer?

    Public Shared Sub SetPendingBusiness(businessId As Integer?)
        pendingBusinessId = businessId
    End Sub

    ''' <summary>Returns the business requested by the last navigation (only once), or Nothing.</summary>
    Public Shared Function TakePendingBusiness() As Integer?
        Dim id = pendingBusinessId
        pendingBusinessId = Nothing
        Return id
    End Function

    ''' <summary>Reloads the page's data from the database (F5).</summary>
    Public Overridable Sub RefreshData()
    End Sub

    ''' <summary>
    ''' Shows a centered "coming soon" card. Used by placeholder pages until their phase is built.
    ''' </summary>
    Protected Sub ShowPlaceholder(glyph As String, heading As String, description As String, note As String)
        Dim layout As New TableLayoutPanel With {
            .Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 1, .BackColor = Color.Transparent
        }
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))

        Dim card As New RoundedPanel With {.Size = New Size(580, 320), .Anchor = AnchorStyles.None}

        Dim icon As New IconButton With {
            .Glyph = glyph, .IconSize = 22.0F, .IconColor = Theme.SidebarBlue,
            .CircleColor = Theme.AccentSoft, .Cursor = Cursors.Default,
            .Size = New Size(72, 72), .Location = New Point((580 - 72) \ 2, 36)
        }
        Dim lblHeading As New Label With {
            .Text = heading, .Font = Theme.TitleFont, .ForeColor = Theme.TextDark,
            .TextAlign = ContentAlignment.MiddleCenter, .AutoSize = False, .AutoEllipsis = True,
            .Location = New Point(20, 122), .Size = New Size(540, 36)
        }
        Dim lblDescription As New Label With {
            .Text = description, .Font = Theme.BodyFont, .ForeColor = Theme.TextMuted,
            .TextAlign = ContentAlignment.TopCenter, .AutoSize = False, .AutoEllipsis = True,
            .Location = New Point(40, 164), .Size = New Size(500, 66)
        }
        Dim chip As New StatusBadge With {.Text = note}
        chip.Location = New Point((580 - chip.Width) \ 2, 248)

        card.Controls.AddRange({icon, lblHeading, lblDescription, chip})
        layout.Controls.Add(card, 0, 0)
        Controls.Add(layout)
    End Sub

End Class
