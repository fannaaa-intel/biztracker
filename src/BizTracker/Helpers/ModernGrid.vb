''' <summary>
''' DataGridView used by every module list. Same as a normal grid, but it never draws the
''' dotted keyboard-focus box or cell borders (rows are separated by a thin line painted
''' in UiHelper.StyleGrid), and it is double-buffered for smooth scrolling.
''' Call UiHelper.StyleGrid(grid, ...) after creating it.
''' </summary>
Public Class ModernGrid
    Inherits DataGridView

    Public Sub New()
        DoubleBuffered = True
        CellBorderStyle = DataGridViewCellBorderStyle.None
    End Sub

    Protected Overrides ReadOnly Property ShowFocusCues As Boolean
        Get
            Return False
        End Get
    End Property

    ''' <summary>True while the grid is being closed (e.g. on logout) - it must not be touched any more.</summary>
    Private ReadOnly Property IsClosing As Boolean
        Get
            Return IsDisposed OrElse Disposing
        End Get
    End Property

    ' While the main window closes, the layout can still resize the grid or change its selection
    ' (more often on laptops with display scaling). The page handlers (FitGridColumns,
    ' ShowSelected, ...) would then work on a disposed grid, so skip those events.
    Protected Overrides Sub OnResize(e As EventArgs)
        If IsClosing Then Return
        MyBase.OnResize(e)
    End Sub

    Protected Overrides Sub OnSelectionChanged(e As EventArgs)
        If IsClosing Then Return
        MyBase.OnSelectionChanged(e)
    End Sub

    ''' <summary>Late window messages (tooltip, focus, accessibility) to a closed grid are ignored.</summary>
    Protected Overrides Sub WndProc(ByRef m As Message)
        Try
            MyBase.WndProc(m)
        Catch ex As ObjectDisposedException When IsClosing
            ' The grid is gone - nothing to do.
        End Try
    End Sub

End Class
