Imports System.Drawing
Imports System.Windows.Forms
Imports SQLCommandCenter.Configuration
Imports SQLCommandCenter.Models
Imports SQLCommandCenter.Services
Imports SQLCommandCenter.UI.Controls
Imports SQLCommandCenter.UI.Theme

Namespace UI.Views
    Public Class PerformanceView
        Inherits UserControl

        Private ReadOnly _serverService As ServerService
        Private ReadOnly _monitoringService As MonitoringService

        ' Charts
        Private chartCpu As ChartCanvas
        Private chartMemory As ChartCanvas
        Private chartConnections As ChartCanvas
        Private chartQps As ChartCanvas

        ' Interval buttons
        Private btn10s As Button
        Private btn30s As Button
        Private btn1m As Button
        Private btn5m As Button
        Private btn15m As Button
        Private btnManualRefresh As Button
        Private lblInterval As Label

        Private _selectedIntervalSeconds As Integer = 10

        Public Sub New(serverService As ServerService, monitoringService As MonitoringService)
            _serverService = serverService
            _monitoringService = monitoringService

            InitializeComponent()

            AddHandler _serverService.SelectedServerChanged, AddressOf OnSelectedServerChanged
            AddHandler _monitoringService.PerformanceUpdated, AddressOf OnPerformanceUpdated
            AddHandler ThemeManager.ThemeChanged, AddressOf OnThemeChanged

            UpdateCharts()
        End Sub

        Private Sub InitializeComponent()
            Dock = DockStyle.Fill
            AutoScroll = True
            BackColor = ThemeManager.BackgroundColor
            Font = ThemeManager.BodyFont

            ' Top Interval Toolbar
            Dim pnlTop As New Panel() With {
                .Dock = DockStyle.Top,
                .Height = 55,
                .Padding = New Padding(16, 12, 16, 12),
                .BackColor = ThemeManager.TopBarColor
            }

            lblInterval = New Label() With {
                .Text = "Refresh Interval:",
                .Location = New Point(16, 18),
                .AutoSize = True,
                .ForeColor = ThemeManager.TextSecondaryColor
            }

            btn10s = CreateIntervalButton("10s", 10, 130)
            btn30s = CreateIntervalButton("30s", 30, 185)
            btn1m = CreateIntervalButton("1m", 60, 240)
            btn5m = CreateIntervalButton("5m", 300, 295)
            btn15m = CreateIntervalButton("15m", 900, 350)

            btnManualRefresh = New Button() With {
                .Text = "↻ Refresh Now",
                .Location = New Point(430, 12),
                .Size = New Size(115, 30)
            }
            ThemeManager.StyleButton(btnManualRefresh, True)
            AddHandler btnManualRefresh.Click, AddressOf OnManualRefreshClicked

            pnlTop.Controls.AddRange({lblInterval, btn10s, btn30s, btn1m, btn5m, btn15m, btnManualRefresh})

            ' 2x2 Grid Panel for Charts
            Dim tblCharts As New TableLayoutPanel() With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 2,
                .RowCount = 2,
                .Padding = New Padding(16)
            }
            tblCharts.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
            tblCharts.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
            tblCharts.RowStyles.Add(New RowStyle(SizeType.Percent, 50.0F))
            tblCharts.RowStyles.Add(New RowStyle(SizeType.Percent, 50.0F))

            chartCpu = New ChartCanvas() With {
                .Dock = DockStyle.Fill,
                .Title = "CPU Utilization",
                .Unit = "%",
                .LineColor = ThemeManager.AccentColor,
                .FixedMax = 100.0,
                .Margin = New Padding(8)
            }

            chartMemory = New ChartCanvas() With {
                .Dock = DockStyle.Fill,
                .Title = "Memory Utilization",
                .Unit = "%",
                .LineColor = ThemeManager.WarningColor,
                .FixedMax = 100.0,
                .Margin = New Padding(8)
            }

            chartConnections = New ChartCanvas() With {
                .Dock = DockStyle.Fill,
                .Title = "Active Connections",
                .Unit = " conns",
                .LineColor = ThemeManager.SuccessColor,
                .Margin = New Padding(8)
            }

            chartQps = New ChartCanvas() With {
                .Dock = DockStyle.Fill,
                .Title = "Throughput (Queries / sec)",
                .Unit = " QPS",
                .LineColor = Color.FromArgb(203, 166, 247),
                .Margin = New Padding(8)
            }

            tblCharts.Controls.Add(chartCpu, 0, 0)
            tblCharts.Controls.Add(chartMemory, 1, 0)
            tblCharts.Controls.Add(chartConnections, 0, 1)
            tblCharts.Controls.Add(chartQps, 1, 1)

            Controls.AddRange({tblCharts, pnlTop})
            HighlightActiveIntervalButton()
        End Sub

        Private Function CreateIntervalButton(text As String, seconds As Integer, x As Integer) As Button
            Dim btn As New Button() With {
                .Text = text,
                .Location = New Point(x, 12),
                .Size = New Size(50, 30),
                .Tag = seconds
            }
            ThemeManager.StyleButton(btn, False)
            AddHandler btn.Click, Sub(s, e)
                                      _selectedIntervalSeconds = seconds
                                      ConfigurationManager.Settings.PollingIntervalSeconds = seconds
                                      ConfigurationManager.SaveSettings()
                                      HighlightActiveIntervalButton()
                                  End Sub
            Return btn
        End Function

        Private Sub HighlightActiveIntervalButton()
            For Each btn In {btn10s, btn30s, btn1m, btn5m, btn15m}
                Dim sec = CInt(btn.Tag)
                If sec = _selectedIntervalSeconds Then
                    btn.BackColor = ThemeManager.AccentColor
                    btn.ForeColor = If(ThemeManager.IsDark, Color.FromArgb(17, 17, 27), Color.White)
                Else
                    ThemeManager.StyleButton(btn, False)
                End If
            Next
        End Sub

        Private Async Sub OnManualRefreshClicked(sender As Object, e As EventArgs)
            Dim sel = _serverService.SelectedServer
            If sel IsNot Nothing Then
                btnManualRefresh.Enabled = False
                btnManualRefresh.Text = "Refreshing..."
                Await _monitoringService.RefreshServerAsync(sel)
                btnManualRefresh.Enabled = True
                btnManualRefresh.Text = "↻ Refresh Now"
                UpdateCharts()
            End If
        End Sub

        Public Sub UpdateCharts()
            If InvokeRequired Then
                BeginInvoke(New Action(AddressOf UpdateCharts))
                Return
            End If

            Dim sel = _serverService.SelectedServer
            If sel Is Nothing Then Return

            Dim history = _monitoringService.GetMetricsHistory(sel.Id, 100)
            If history.Count > 0 Then
                chartCpu.SetPoints(history.Select(Function(m) New ChartPoint(m.Timestamp, m.CpuPercent)))
                chartMemory.SetPoints(history.Select(Function(m) New ChartPoint(m.Timestamp, m.MemoryPercent)))
                chartConnections.SetPoints(history.Select(Function(m) New ChartPoint(m.Timestamp, m.ActiveConnections)))
                chartQps.SetPoints(history.Select(Function(m) New ChartPoint(m.Timestamp, m.QueriesPerSecond)))
            End If
        End Sub

        Private Sub OnSelectedServerChanged(sender As Object, e As ServerProfile)
            UpdateCharts()
        End Sub

        Private Sub OnPerformanceUpdated(sender As Object, data As (ServerId As String, Metrics As PerformanceMetrics))
            Dim sel = _serverService.SelectedServer
            If sel IsNot Nothing AndAlso sel.Id = data.ServerId Then
                UpdateCharts()
            End If
        End Sub

        Private Sub OnThemeChanged(sender As Object, e As EventArgs)
            BackColor = ThemeManager.BackgroundColor
            chartCpu.LineColor = ThemeManager.AccentColor
            chartMemory.LineColor = ThemeManager.WarningColor
            chartConnections.LineColor = ThemeManager.SuccessColor
            HighlightActiveIntervalButton()
            ThemeManager.StyleButton(btnManualRefresh, True)
        End Sub
    End Class
End Namespace
