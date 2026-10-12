Imports System.Drawing
Imports System.Windows.Forms
Imports SQLCommandCenter.UI.Theme

Namespace UI.Controls
    Public Class ModernDataGrid
        Inherits DataGridView

        Public Sub New()
            SetStyle(ControlStyles.OptimizedDoubleBuffer Or ControlStyles.AllPaintingInWmPaint, True)
            DoubleBuffered = True
            BorderStyle = BorderStyle.None
            BackgroundColor = ThemeManager.CardColor
            GridColor = ThemeManager.CardBorderColor
            RowHeadersVisible = False
            AllowUserToAddRows = False
            AllowUserToDeleteRows = False
            [ReadOnly] = True
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
            MultiSelect = False
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None
            EnableHeadersVisualStyles = False
            AutoGenerateColumns = True

            ApplyThemeStyles()

            AddHandler ThemeManager.ThemeChanged, AddressOf OnThemeChanged
        End Sub

        Private Sub OnThemeChanged(sender As Object, e As EventArgs)
            ApplyThemeStyles()
            Invalidate()
        End Sub

        Public Sub ApplyThemeStyles()
            BackgroundColor = ThemeManager.CardColor
            GridColor = ThemeManager.CardBorderColor

            ' Column Header Style
            ColumnHeadersDefaultCellStyle.BackColor = ThemeManager.GridHeaderColor
            ColumnHeadersDefaultCellStyle.ForeColor = ThemeManager.AccentColor
            ColumnHeadersDefaultCellStyle.Font = ThemeManager.BoldFont
            ColumnHeadersDefaultCellStyle.Padding = New Padding(10, 8, 10, 8)
            ColumnHeadersHeight = 38
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing

            ' Rows Style
            DefaultCellStyle.BackColor = ThemeManager.CardColor
            DefaultCellStyle.ForeColor = ThemeManager.TextPrimaryColor
            DefaultCellStyle.SelectionBackColor = Color.FromArgb(45, ThemeManager.AccentColor)
            DefaultCellStyle.SelectionForeColor = If(ThemeManager.IsDark, Color.White, Color.Black)
            DefaultCellStyle.Font = ThemeManager.BodyFont
            DefaultCellStyle.Padding = New Padding(8, 4, 8, 4)

            ' Alternating Row Style
            AlternatingRowsDefaultCellStyle.BackColor = ThemeManager.GridAlternateRowColor
            AlternatingRowsDefaultCellStyle.ForeColor = ThemeManager.TextPrimaryColor
            AlternatingRowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(45, ThemeManager.AccentColor)
            AlternatingRowsDefaultCellStyle.SelectionForeColor = If(ThemeManager.IsDark, Color.White, Color.Black)

            RowTemplate.Height = 34
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                RemoveHandler ThemeManager.ThemeChanged, AddressOf OnThemeChanged
            End If
            MyBase.Dispose(disposing)
        End Sub
    End Class
End Namespace
