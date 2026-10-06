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

End Class
