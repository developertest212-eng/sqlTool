Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports SQLCommandCenter.UI.Theme

Namespace UI.Controls
    Public Class MetricCard
        Inherits Control

        Private _title As String = "METRIC"
        Private _value As String = "0"
        Private _subText As String = ""
        Private _accentColor As Color = Color.FromArgb(137, 180, 250)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
            Size = New Size(220, 100)
            Font = ThemeManager.BodyFont
        End Sub

        Public Property Title As String
            Get
                Return _title
            End Get
            Set(value As String)
                _title = value
                Invalidate()
            End Set
        End Property

        Public Property Value As String
            Get
                Return _value
            End Get
            Set(value As String)
                _value = value
                Invalidate()
            End Set
        End Property

        Public Property SubText As String
            Get
                Return _subText
            End Get
            Set(value As String)
                _subText = value
                Invalidate()
            End Set
        End Property

        Public Property AccentColor As Color
            Get
                Return _accentColor
            End Get
            Set(value As Color)
                _accentColor = value
                Invalidate()
            End Set
        End Property

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            g.SmoothingMode = SmoothingMode.AntiAlias
            g.TextRenderingHint = Drawing.Text.TextRenderingHint.ClearTypeGridFit

            Dim rect = New Rectangle(0, 0, Width - 1, Height - 1)

            ' Card Background
            Using path = GetRoundedPath(rect, 8)
                Using bgBrush As New SolidBrush(ThemeManager.CardColor)
                    g.FillPath(bgBrush, path)
                End Using
                Using borderPen As New Pen(ThemeManager.CardBorderColor)
                    g.DrawPath(borderPen, path)
                End Using
            End Using

            ' Left Accent Indicator Bar
            Using accentPath As New GraphicsPath()
                accentPath.AddLine(0, 8, 0, Height - 8)
                Using accentPen As New Pen(_accentColor, 4.0F)
                    g.DrawLine(accentPen, 2, 8, 2, Height - 8)
                End Using
            End Using

            ' Draw Title
            Dim titleRect = New Rectangle(16, 12, Width - 24, 18)
            TextRenderer.DrawText(g, _title.ToUpperInvariant(), ThemeManager.SmallFont, titleRect, ThemeManager.TextSecondaryColor, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter)

            ' Draw Large Value
            Using valFont As New Font("Segoe UI", 20.0F, FontStyle.Bold)
                Dim valRect = New Rectangle(16, 32, Width - 24, 38)
                TextRenderer.DrawText(g, _value, valFont, valRect, ThemeManager.TextPrimaryColor, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter)
            End Using

            ' Draw SubText
            If Not String.IsNullOrEmpty(_subText) Then
                Dim subRect = New Rectangle(16, 72, Width - 24, 18)
                TextRenderer.DrawText(g, _subText, ThemeManager.SmallFont, subRect, ThemeManager.TextMutedColor, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter)
            End If
        End Sub

        Private Function GetRoundedPath(rect As Rectangle, radius As Integer) As GraphicsPath
            Dim path As New GraphicsPath()
            Dim d = radius * 2
            path.AddArc(rect.X, rect.Y, d, d, 180, 90)
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90)
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90)
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90)
            path.CloseFigure()
            Return path
        End Function
    End Class
End Namespace
