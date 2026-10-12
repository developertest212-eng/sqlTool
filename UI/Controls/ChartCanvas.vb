Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports SQLCommandCenter.UI.Theme

Namespace UI.Controls
    Public Class ChartPoint
        Public Property Timestamp As DateTime
        Public Property Value As Double

        Public Sub New(time As DateTime, val As Double)
            Timestamp = time
            Value = val
        End Sub
    End Class

    Public Class ChartCanvas
        Inherits Control

        Private ReadOnly _points As New List(Of ChartPoint)()
        Private ReadOnly _pointsLock As New Object()
        Private _title As String = "CHART"
        Private _unit As String = ""
        Private _lineColor As Color = Color.FromArgb(137, 180, 250)
        Private _fixedMax As Double = -1 ' -1 means auto-scale
        Private _currentValue As Double = 0.0

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
            Size = New Size(400, 200)
            Font = ThemeManager.SmallFont
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

        Public Property Unit As String
            Get
                Return _unit
            End Get
            Set(value As String)
                _unit = value
                Invalidate()
            End Set
        End Property

        Public Property LineColor As Color
            Get
                Return _lineColor
            End Get
            Set(value As Color)
                _lineColor = value
                Invalidate()
            End Set
        End Property

        Public Property FixedMax As Double
            Get
                Return _fixedMax
            End Get
            Set(value As Double)
                _fixedMax = value
                Invalidate()
            End Set
        End Property

        Public Sub AddPoint(val As Double, Optional time As Nullable(Of DateTime) = Nothing)
            Dim t = If(time.HasValue, time.Value, DateTime.Now)
            SyncLock _pointsLock
                _points.Add(New ChartPoint(t, val))
                _currentValue = val
                If _points.Count > 100 Then
                    _points.RemoveAt(0)
                End If
            End SyncLock
            Invalidate()
        End Sub

        Public Sub SetPoints(points As IEnumerable(Of ChartPoint))
            SyncLock _pointsLock
                _points.Clear()
                If points IsNot Nothing Then
                    _points.AddRange(points)
                    If _points.Count > 0 Then
                        _currentValue = _points.Last().Value
                    End If
                End If
            End SyncLock
            Invalidate()
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim g = e.Graphics
            g.SmoothingMode = SmoothingMode.AntiAlias
            g.TextRenderingHint = Drawing.Text.TextRenderingHint.ClearTypeGridFit

            Dim rect = New Rectangle(0, 0, Width - 1, Height - 1)

            ' Background & Border
            Using bgBrush As New SolidBrush(ThemeManager.CardColor)
                g.FillRectangle(bgBrush, rect)
            End Using
            Using borderPen As New Pen(ThemeManager.CardBorderColor)
                g.DrawRectangle(borderPen, rect)
            End Using

            ' Draw Title and Current Value
            Dim headerRect = New Rectangle(14, 10, Width - 28, 24)
            Dim titleText = $"{_title.ToUpperInvariant()}   —   {_currentValue:F1}{_unit}"
            TextRenderer.DrawText(g, titleText, ThemeManager.BoldFont, headerRect, ThemeManager.TextPrimaryColor, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter)

            ' Chart Area bounds
            Dim plotLeft = 45
            Dim plotTop = 40
            Dim plotRight = Width - 15
            Dim plotBottom = Height - 25
            Dim plotWidth = plotRight - plotLeft
            Dim plotHeight = plotBottom - plotTop

            If plotWidth <= 10 OrElse plotHeight <= 10 Then Return

            ' Grid lines (4 horizontal divisions)
            Dim maxVal As Double = If(_fixedMax > 0, _fixedMax, 10.0)
            SyncLock _pointsLock
                If _points.Count > 0 AndAlso _fixedMax <= 0 Then
                    Dim maxData = _points.Max(Function(p) p.Value)
                    maxVal = Math.Max(10.0, maxData * 1.15)
                End If
            End SyncLock

            Using gridPen As New Pen(Color.FromArgb(40, ThemeManager.CardBorderColor))
                gridPen.DashStyle = DashStyle.Dash
                For i = 0 To 4
                    Dim y = plotBottom - CInt((plotHeight / 4.0) * i)
                    g.DrawLine(gridPen, plotLeft, y, plotRight, y)

                    ' Y-axis labels
                    Dim yVal = (maxVal / 4.0) * i
                    Dim lbl = If(yVal >= 1000, $"{yVal / 1000:F0}k", $"{yVal:F0}")
                    Dim lblRect = New Rectangle(2, y - 8, plotLeft - 6, 16)
                    TextRenderer.DrawText(g, lbl, ThemeManager.SmallFont, lblRect, ThemeManager.TextMutedColor, TextFormatFlags.Right Or TextFormatFlags.VerticalCenter)
                Next
            End Using

            ' Plot Line and Gradient Area
            Dim localPoints As List(Of ChartPoint)
            SyncLock _pointsLock
                localPoints = New List(Of ChartPoint)(_points)
            End SyncLock

            If localPoints.Count >= 2 Then
                Dim screenPoints As New List(Of PointF)()
                For i = 0 To localPoints.Count - 1
                    Dim x = plotLeft + CSng((plotWidth / Math.Max(1, localPoints.Count - 1)) * i)
                    Dim normVal = Math.Min(maxVal, Math.Max(0.0, localPoints(i).Value))
                    Dim y = plotBottom - CSng((normVal / maxVal) * plotHeight)
                    screenPoints.Add(New PointF(x, y))
                Next

                ' Area Gradient Fill
                Using fillPath As New GraphicsPath()
                    fillPath.AddLine(screenPoints.First().X, plotBottom, screenPoints.First().X, screenPoints.First().Y)
                    fillPath.AddLines(screenPoints.ToArray())
                    fillPath.AddLine(screenPoints.Last().X, screenPoints.Last().Y, screenPoints.Last().X, plotBottom)
                    fillPath.CloseFigure()

                    Using brush As New LinearGradientBrush(New Point(0, plotTop), New Point(0, plotBottom), Color.FromArgb(50, _lineColor), Color.FromArgb(5, _lineColor))
                        g.FillPath(brush, fillPath)
                    End Using
                End Using

                ' Smooth line
                Using pen As New Pen(_lineColor, 2.0F)
                    pen.LineJoin = LineJoin.Round
                    g.DrawLines(pen, screenPoints.ToArray())
                End Using

                ' Draw end dot
                Dim lastPt = screenPoints.Last()
                Using dotBrush As New SolidBrush(_lineColor)
                    g.FillEllipse(dotBrush, lastPt.X - 3, lastPt.Y - 3, 6, 6)
                End Using
            Else
                ' Empty State
                Dim emptyRect = New Rectangle(plotLeft, plotTop, plotWidth, plotHeight)
                TextRenderer.DrawText(g, "Collecting metrics...", ThemeManager.SmallFont, emptyRect, ThemeManager.TextMutedColor, TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
            End If
        End Sub
    End Class
End Namespace
