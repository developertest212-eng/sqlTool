Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports SQLCommandCenter.Models
Imports SQLCommandCenter.UI.Theme

Namespace UI.Controls
    Public Class ServerCard
        Inherits Control

        Private _profile As ServerProfile
        Private _status As ServerStatus
        Private _metrics As PerformanceMetrics
        Private _isSelected As Boolean = False
        Private _isHovered As Boolean = False

        Public Event CardClicked As EventHandler(Of ServerProfile)
        Public Event FavoriteClicked As EventHandler(Of ServerProfile)

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
            Size = New Size(280, 160)
            Cursor = Cursors.Hand
            Font = ThemeManager.BodyFont
        End Sub

        Public Property Profile As ServerProfile
            Get
                Return _profile
            End Get
            Set(value As ServerProfile)
                _profile = value
                Invalidate()
            End Set
        End Property

        Public Property Status As ServerStatus
            Get
                Return _status
            End Get
            Set(value As ServerStatus)
                _status = value
                Invalidate()
            End Set
        End Property

        Public Property Metrics As PerformanceMetrics
            Get
                Return _metrics
            End Get
            Set(value As PerformanceMetrics)
                _metrics = value
                Invalidate()
            End Set
        End Property

        Public Property IsSelected As Boolean
            Get
                Return _isSelected
            End Get
            Set(value As Boolean)
                _isSelected = value
                Invalidate()
            End Set
        End Property

        Protected Overrides Sub OnMouseEnter(e As EventArgs)
            MyBase.OnMouseEnter(e)
            _isHovered = True
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            _isHovered = False
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
            MyBase.OnMouseClick(e)
            ' Check if clicked on star (top right 24x24)
            If e.X >= Width - 36 AndAlso e.Y <= 36 Then
                If _profile IsNot Nothing Then
                    RaiseEvent FavoriteClicked(Me, _profile)
                End If
            Else
                If _profile IsNot Nothing Then
                    RaiseEvent CardClicked(Me, _profile)
                End If
            End If
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            g.SmoothingMode = SmoothingMode.AntiAlias
            g.TextRenderingHint = Drawing.Text.TextRenderingHint.ClearTypeGridFit

            Dim rect = New Rectangle(0, 0, Width - 1, Height - 1)

            ' Card background
            Dim bg = ThemeManager.CardColor
            If _isSelected Then
                bg = If(ThemeManager.IsDark, Color.FromArgb(38, 38, 58), Color.FromArgb(235, 242, 255))
            ElseIf _isHovered Then
                bg = If(ThemeManager.IsDark, Color.FromArgb(34, 34, 52), Color.FromArgb(245, 248, 255))
            End If

            Using path = GetRoundedPath(rect, 8)
                Using bgBrush As New SolidBrush(bg)
                    g.FillPath(bgBrush, path)
                End Using
                Dim borderCol = If(_isSelected, ThemeManager.AccentColor, If(_isHovered, Color.FromArgb(100, ThemeManager.AccentColor), ThemeManager.CardBorderColor))
                Using borderPen As New Pen(borderCol, If(_isSelected, 2.0F, 1.0F))
                    g.DrawPath(borderPen, path)
                End Using
            End Using

            If _profile Is Nothing Then
                TextRenderer.DrawText(g, "No Server", ThemeManager.BoldFont, rect, ThemeManager.TextMutedColor, TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
                Return
            End If

            ' 1. Engine Badge (MSSQL vs MYSQL)
            Dim engineBg = If(_profile.Engine = DatabaseEngine.SqlServer, Color.FromArgb(220, 38, 38), Color.FromArgb(2, 132, 199))
            Dim engineText = If(_profile.Engine = DatabaseEngine.SqlServer, "MSSQL", "MYSQL")
            Dim badgeRect = New Rectangle(14, 14, 55, 20)
            Using bPath = GetRoundedPath(badgeRect, 4)
                Using bBrush As New SolidBrush(Color.FromArgb(40, engineBg))
                    g.FillPath(bBrush, bPath)
                End Using
            End Using
            TextRenderer.DrawText(g, engineText, ThemeManager.SmallFont, badgeRect, engineBg, TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)

            ' 2. Server Name & Group
            Dim nameRect = New Rectangle(75, 12, Width - 120, 22)
            TextRenderer.DrawText(g, _profile.Name, ThemeManager.BoldFont, nameRect, ThemeManager.TextPrimaryColor, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis)

            Dim groupRect = New Rectangle(75, 32, Width - 120, 16)
            TextRenderer.DrawText(g, _profile.GroupName, ThemeManager.SmallFont, groupRect, ThemeManager.TextMutedColor, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter)

            ' 3. Favorite Star
            Dim starChar = If(_profile.IsFavorite, "★", "☆")
            Dim starCol = If(_profile.IsFavorite, Color.FromArgb(249, 226, 175), ThemeManager.TextMutedColor)
            Dim starRect = New Rectangle(Width - 32, 12, 22, 22)
            Using starFont As New Font("Segoe UI", 12.0F, FontStyle.Bold)
                TextRenderer.DrawText(g, starChar, starFont, starRect, starCol, TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
            End Using

            ' Divider
            Using divPen As New Pen(ThemeManager.CardBorderColor)
                g.DrawLine(divPen, 14, 56, Width - 14, 56)
            End Using

            ' 4. Status Indicator Dot & Label
            Dim isOnline = (_status IsNot Nothing AndAlso _status.IsOnline)
            Dim health = If(_status IsNot Nothing, _status.HealthStatus, ServerHealthStatus.Unknown)
            Dim statusDotColor = ThemeManager.TextMutedColor
            If isOnline Then
                Select Case health
                    Case ServerHealthStatus.Healthy : statusDotColor = ThemeManager.SuccessColor
                    Case ServerHealthStatus.Warning : statusDotColor = ThemeManager.WarningColor
                    Case ServerHealthStatus.Critical : statusDotColor = ThemeManager.DangerColor
                End Select
            Else
                statusDotColor = ThemeManager.DangerColor
            End If

            Using dotBrush As New SolidBrush(statusDotColor)
                g.FillEllipse(dotBrush, 16, 72, 8, 8)
            End Using

            Dim statusText = If(isOnline, $"{health} ({_status.LatencyMs}ms)", "Offline")
            Dim statusRect = New Rectangle(28, 66, 140, 20)
            TextRenderer.DrawText(g, statusText, ThemeManager.SmallFont, statusRect, statusDotColor, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter)

            ' 5. Metric summaries
            Dim cpuVal = If(_metrics IsNot Nothing, $"{_metrics.CpuPercent:F0}%", "-")
            Dim connVal = If(_metrics IsNot Nothing, $"{_metrics.ActiveConnections}", "-")
            Dim dbVal = If(_status IsNot Nothing AndAlso _status.DatabaseCount > 0, $"{_status.DatabaseCount}", "-")

            ' Draw 3 mini columns
            DrawMetricBox(g, "CPU", cpuVal, 14, 96, 75)
            DrawMetricBox(g, "CONNS", connVal, 99, 96, 75)
            DrawMetricBox(g, "DATABASES", dbVal, 184, 96, 80)
        End Sub

        Private Sub DrawMetricBox(g As Graphics, label As String, val As String, x As Integer, y As Integer, w As Integer)
            Dim lblRect = New Rectangle(x, y, w, 16)
            TextRenderer.DrawText(g, label, ThemeManager.SmallFont, lblRect, ThemeManager.TextSecondaryColor, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter)

            Dim valRect = New Rectangle(x, y + 16, w, 22)
            TextRenderer.DrawText(g, val, ThemeManager.BoldFont, valRect, ThemeManager.TextPrimaryColor, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter)
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
