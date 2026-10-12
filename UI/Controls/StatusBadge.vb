Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports SQLCommandCenter.Models
Imports SQLCommandCenter.UI.Theme

Namespace UI.Controls
    Public Class StatusBadge
        Inherits Control

        Private _status As ServerHealthStatus = ServerHealthStatus.Healthy
        Private _customText As String = ""

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
            Size = New Size(95, 26)
            Font = ThemeManager.SmallFont
        End Sub

        Public Property Status As ServerHealthStatus
            Get
                Return _status
            End Get
            Set(value As ServerHealthStatus)
                _status = value
                Invalidate()
            End Set
        End Property

        Public Property CustomText As String
            Get
                Return _customText
            End Get
            Set(value As String)
                _customText = value
                Invalidate()
            End Set
        End Property

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            g.SmoothingMode = SmoothingMode.AntiAlias
            g.TextRenderingHint = Drawing.Text.TextRenderingHint.ClearTypeGridFit

            Dim dotColor As Color
            Dim bgTint As Color
            Dim displayText As String = If(Not String.IsNullOrEmpty(_customText), _customText, _status.ToString())

            Select Case _status
                Case ServerHealthStatus.Healthy
                    dotColor = ThemeManager.SuccessColor
                    bgTint = Color.FromArgb(35, 166, 227, 161)
                Case ServerHealthStatus.Warning
                    dotColor = ThemeManager.WarningColor
                    bgTint = Color.FromArgb(35, 249, 226, 175)
                Case ServerHealthStatus.Critical, ServerHealthStatus.Offline
                    dotColor = ThemeManager.DangerColor
                    bgTint = Color.FromArgb(35, 243, 139, 168)
                Case Else
                    dotColor = ThemeManager.TextMutedColor
                    bgTint = Color.FromArgb(35, 108, 112, 134)
            End Select

            Dim rect = New Rectangle(0, 0, Width - 1, Height - 1)
            Using path = GetRoundedPath(rect, 12)
                Using bgBrush As New SolidBrush(bgTint)
                    g.FillPath(bgBrush, path)
                End Using
                Using borderPen As New Pen(Color.FromArgb(80, dotColor))
                    g.DrawPath(borderPen, path)
                End Using
            End Using

            ' Draw Status Dot
            Dim dotSize = 8
            Dim dotY = (Height - dotSize) \ 2
            Using dotBrush As New SolidBrush(dotColor)
                g.FillEllipse(dotBrush, 10, dotY, dotSize, dotSize)
            End Using

            ' Draw Text
            Dim textRect = New Rectangle(22, 0, Width - 24, Height)
            Dim flags = TextFormatFlags.VerticalCenter Or TextFormatFlags.Left Or TextFormatFlags.EndEllipsis
            TextRenderer.DrawText(g, displayText, ThemeManager.BoldFont, textRect, dotColor, flags)
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
