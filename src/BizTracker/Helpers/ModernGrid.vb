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

End Class
