Imports System.Drawing
Imports System.Windows.Forms
Imports SQLCommandCenter.UI.Theme

Namespace UI.Controls
    Public Class SidebarButton
        Inherits Control

        Private _iconText As String = "●"
        Private _isActive As Boolean = False
        Private _isHovered As Boolean = False

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
            Size = New Size(210, 42)
            Cursor = Cursors.Hand
            Font = ThemeManager.BodyFont
        End Sub

        Public Property IconText As String
            Get
                Return _iconText
            End Get
            Set(value As String)
                _iconText = value
                Invalidate()
            End Set
        End Property

        Public Property IsActive As Boolean
            Get
                Return _isActive
            End Get
            Set(value As Boolean)
                _isActive = value
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

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias
            g.TextRenderingHint = Drawing.Text.TextRenderingHint.ClearTypeGridFit

            ' Background
            Dim bg = ThemeManager.SidebarColor
            If _isActive Then
                bg = If(ThemeManager.IsDark, Color.FromArgb(30, 30, 46), Color.FromArgb(220, 226, 235))
            ElseIf _isHovered Then
                bg = If(ThemeManager.IsDark, Color.FromArgb(24, 24, 37), Color.FromArgb(228, 232, 240))
            End If

            Using bgBrush As New SolidBrush(bg)
                g.FillRectangle(bgBrush, ClientRectangle)
            End Using

            ' Active indicator bar on left
            If _isActive Then
                Using indBrush As New SolidBrush(ThemeManager.AccentColor)
                    g.FillRectangle(indBrush, 0, 4, 4, Height - 8)
                End Using
            End If

            ' Text color
            Dim textColor = If(_isActive, ThemeManager.AccentColor, If(_isHovered, ThemeManager.TextPrimaryColor, ThemeManager.TextSecondaryColor))
            Dim currentFont = If(_isActive, ThemeManager.BoldFont, ThemeManager.BodyFont)

            ' Draw Icon / Glyph
            Dim iconRect = New Rectangle(14, 0, 24, Height)
            Using iconFont As New Font("Segoe UI", 10.0F, FontStyle.Regular)
                TextRenderer.DrawText(g, _iconText, iconFont, iconRect, textColor, TextFormatFlags.VerticalCenter Or TextFormatFlags.HorizontalCenter)
            End Using

            ' Draw Label
            Dim textRect = New Rectangle(44, 0, Width - 50, Height)
            TextRenderer.DrawText(g, Text, currentFont, textRect, textColor, TextFormatFlags.VerticalCenter Or TextFormatFlags.Left Or TextFormatFlags.EndEllipsis)
        End Sub
    End Class
End Namespace
