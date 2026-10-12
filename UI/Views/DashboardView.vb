Imports System.Drawing
Imports System.Windows.Forms
Imports SQLCommandCenter.Data
Imports SQLCommandCenter.Models
Imports SQLCommandCenter.Services
Imports SQLCommandCenter.UI.Controls
Imports SQLCommandCenter.UI.Theme

Namespace UI.Views
    Public Class DashboardView
        Inherits UserControl

        Private ReadOnly _serverService As ServerService
        Private ReadOnly _monitoringService As MonitoringService
        Private ReadOnly _alertService As AlertService

        ' Controls
        Private pnlServerFleet As FlowLayoutPanel
        Private cardCpu As MetricCard
        Private cardMemory As MetricCard
        Private cardConnections As MetricCard
        Private cardQueries As MetricCard
        Private cardDbSize As MetricCard
        Private cardQps As MetricCard
        Private chartCpu As ChartCanvas
        Private chartConn As ChartCanvas
        Private lblSelectedServerTitle As Label
        Private statusBadgeSelected As StatusBadge

        Public Sub New(serverService As ServerService, monitoringService As MonitoringService, alertService As AlertService)
            _serverService = serverService
            _monitoringService = monitoringService
            _alertService = alertService

            InitializeComponent()

            AddHandler _serverService.ServerListChanged, AddressOf OnServerListChanged
            AddHandler _serverService.SelectedServerChanged, AddressOf OnSelectedServerChanged
            AddHandler _monitoringService.ServerStatusChanged, AddressOf OnServerStatusChanged
            AddHandler _monitoringService.PerformanceUpdated, AddressOf OnPerformanceUpdated
            AddHandler ThemeManager.ThemeChanged, AddressOf OnThemeChanged

            RefreshFleetCards()
            UpdateSelectedServerKpis()
        End Sub

        Private Sub InitializeComponent()
            Dock = DockStyle.Fill
            AutoScroll = True
            BackColor = ThemeManager.BackgroundColor
            Font = ThemeManager.BodyFont

            Dim pnlContainer As New Panel() With {
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .Padding = New Padding(20)
            }

            Dim y = 10

            ' Section 1: Server Fleet Overview
            Dim lblFleetHeader As New Label() With {
                .Text = "SERVER FLEET OVERVIEW",
                .Font = ThemeManager.HeaderFont,
                .ForeColor = ThemeManager.AccentColor,
                .Location = New Point(20, y),
                .AutoSize = True
            }
            pnlContainer.Controls.Add(lblFleetHeader)
            y += 28

            pnlServerFleet = New FlowLayoutPanel() With {
                .Location = New Point(20, y),
                .Size = New Size(1160, 180),
                .AutoScroll = True,
                .WrapContents = False,
                .BackColor = Color.Transparent
            }
            pnlContainer.Controls.Add(pnlServerFleet)
            y += 190

            ' Section 2: Selected Server Header
            Dim pnlSelectedHeader As New Panel() With {
                .Location = New Point(20, y),
                .Size = New Size(1160, 40),
                .BackColor = Color.Transparent
            }

            lblSelectedServerTitle = New Label() With {
                .Text = "ACTIVE SERVER METRICS",
                .Font = ThemeManager.HeaderFont,
                .ForeColor = ThemeManager.AccentColor,
                .Location = New Point(0, 8),
                .AutoSize = True
            }

            statusBadgeSelected = New StatusBadge() With {
                .Location = New Point(260, 6),
                .Status = ServerHealthStatus.Healthy
            }

            pnlSelectedHeader.Controls.AddRange({lblSelectedServerTitle, statusBadgeSelected})
            pnlContainer.Controls.Add(pnlSelectedHeader)
            y += 45

            ' Section 3: KPI Metric Cards Grid
            Dim pnlCards As New FlowLayoutPanel() With {
                .Location = New Point(20, y),
                .Size = New Size(1160, 110),
                .BackColor = Color.Transparent,
                .WrapContents = False
            }

            cardCpu = New MetricCard() With {.Title = "CPU LOAD", .Value = "0%", .SubText = "Normal", .AccentColor = ThemeManager.AccentColor, .Margin = New Padding(0, 0, 15, 0)}
            cardMemory = New MetricCard() With {.Title = "MEMORY USAGE", .Value = "0 MB", .SubText = "0% utilized", .AccentColor = ThemeManager.WarningColor, .Margin = New Padding(0, 0, 15, 0)}
            cardConnections = New MetricCard() With {.Title = "ACTIVE CONNS", .Value = "0", .SubText = "0 max configured", .AccentColor = ThemeManager.SuccessColor, .Margin = New Padding(0, 0, 15, 0)}
            cardQueries = New MetricCard() With {.Title = "RUNNING QUERIES", .Value = "0", .SubText = "0 blocked", .AccentColor = Color.FromArgb(203, 166, 247), .Margin = New Padding(0, 0, 15, 0)}
            cardDbSize = New MetricCard() With {.Title = "TOTAL STORAGE", .Value = "0 MB", .SubText = "Databases & logs", .AccentColor = Color.FromArgb(148, 226, 213), .Margin = New Padding(0, 0, 0, 0)}

            pnlCards.Controls.AddRange({cardCpu, cardMemory, cardConnections, cardQueries, cardDbSize})
            pnlContainer.Controls.Add(pnlCards)
            y += 120

            ' Section 4: Real-Time Charts
            Dim pnlCharts As New Panel() With {
                .Location = New Point(20, y),
                .Size = New Size(1160, 240),
                .BackColor = Color.Transparent
            }

            chartCpu = New ChartCanvas() With {
                .Title = "Real-Time CPU Load (%)",
                .Unit = "%",
                .LineColor = ThemeManager.AccentColor,
                .FixedMax = 100.0,
                .Location = New Point(0, 0),
                .Size = New Size(570, 230),
                .Anchor = AnchorStyles.Top Or AnchorStyles.Left
            }

            chartConn = New ChartCanvas() With {
                .Title = "Active Connections & Concurrency",
                .Unit = " conn",
                .LineColor = ThemeManager.SuccessColor,
                .Location = New Point(590, 0),
                .Size = New Size(570, 230),
                .Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
            }

            pnlCharts.Controls.AddRange({chartCpu, chartConn})
            pnlContainer.Controls.Add(pnlCharts)

            Controls.Add(pnlContainer)
        End Sub

        Public Sub RefreshFleetCards()
            If InvokeRequired Then
                BeginInvoke(New Action(AddressOf RefreshFleetCards))
                Return
            End If

            pnlServerFleet.SuspendLayout()
            pnlServerFleet.Controls.Clear()

            Dim servers = _serverService.GetAllServers()
            Dim sel = _serverService.SelectedServer

            For Each s In servers
                Dim srv = s
                Dim card As New ServerCard() With {
                    .Profile = srv,
                    .Status = _monitoringService.GetStatus(srv.Id),
                    .IsSelected = (sel IsNot Nothing AndAlso sel.Id = srv.Id),
                    .Margin = New Padding(0, 0, 14, 0)
                }

                Dim history = _monitoringService.GetMetricsHistory(srv.Id, 1)
                If history.Count > 0 Then
                    card.Metrics = history.Last()
                End If

                AddHandler card.CardClicked, Sub(sender, profile)
                                                 _serverService.SelectedServer = profile
                                             End Sub

                AddHandler card.FavoriteClicked, Sub(sender, profile)
                                                     _serverService.ToggleFavorite(profile.Id)
                                                 End Sub

                pnlServerFleet.Controls.Add(card)
            Next

            pnlServerFleet.ResumeLayout()
        End Sub

        Private Sub UpdateSelectedServerKpis()
            If InvokeRequired Then
                BeginInvoke(New Action(AddressOf UpdateSelectedServerKpis))
                Return
            End If

            Dim sel = _serverService.SelectedServer
            If sel Is Nothing Then
                lblSelectedServerTitle.Text = "NO SERVER SELECTED"
                statusBadgeSelected.Status = ServerHealthStatus.Unknown
                Return
            End If

            Dim status = _monitoringService.GetStatus(sel.Id)
            lblSelectedServerTitle.Text = $"{sel.Name.ToUpperInvariant()} ({sel.Engine.ToString().ToUpperInvariant()}) — LIVE METRICS"
            statusBadgeSelected.Status = status.HealthStatus

            Dim history = _monitoringService.GetMetricsHistory(sel.Id, 60)
            If history.Count > 0 Then
                Dim latest = history.Last()
                cardCpu.Value = latest.CpuDisplay
                cardCpu.SubText = If(latest.CpuPercent > 80, "High CPU Load", "Healthy")
                cardCpu.AccentColor = If(latest.CpuPercent > 85, ThemeManager.DangerColor, If(latest.CpuPercent > 70, ThemeManager.WarningColor, ThemeManager.AccentColor))

                cardMemory.Value = latest.MemoryDisplay
                cardMemory.SubText = $"{latest.MemoryUsedMB:N0} MB In Use"

                cardConnections.Value = latest.ConnectionsDisplay
                cardConnections.SubText = $"{latest.RunningQueries} active queries"

                cardQueries.Value = latest.RunningQueries.ToString()
                cardQueries.SubText = $"{latest.BlockedSessionsCount} blocked sessions"

                cardDbSize.Value = latest.DatabaseSizeDisplay
                cardDbSize.SubText = $"{status.DatabaseCount} databases"

                ' Update Charts
                chartCpu.SetPoints(history.Select(Function(m) New ChartPoint(m.Timestamp, m.CpuPercent)))
                chartConn.SetPoints(history.Select(Function(m) New ChartPoint(m.Timestamp, m.ActiveConnections)))
            End If
        End Sub

        Private Sub OnServerListChanged(sender As Object, e As EventArgs)
            RefreshFleetCards()
            UpdateSelectedServerKpis()
        End Sub

        Private Sub OnSelectedServerChanged(sender As Object, e As ServerProfile)
            For Each ctrl In pnlServerFleet.Controls
                Dim sc = TryCast(ctrl, ServerCard)
                If sc IsNot Nothing Then
                    sc.IsSelected = (e IsNot Nothing AndAlso sc.Profile?.Id = e.Id)
                End If
            Next
            UpdateSelectedServerKpis()
        End Sub

        Private Sub OnServerStatusChanged(sender As Object, status As ServerStatus)
            If InvokeRequired Then
                BeginInvoke(Sub() OnServerStatusChanged(sender, status))
                Return
            End If

            For Each ctrl In pnlServerFleet.Controls
                Dim sc = TryCast(ctrl, ServerCard)
                If sc IsNot Nothing AndAlso sc.Profile?.Id = status.ServerId Then
                    sc.Status = status
                    Exit For
                End If
            Next

            Dim sel = _serverService.SelectedServer
            If sel IsNot Nothing AndAlso sel.Id = status.ServerId Then
                statusBadgeSelected.Status = status.HealthStatus
            End If
        End Sub

        Private Sub OnPerformanceUpdated(sender As Object, data As (ServerId As String, Metrics As PerformanceMetrics))
            If InvokeRequired Then
                BeginInvoke(Sub() OnPerformanceUpdated(sender, data))
                Return
            End If

            For Each ctrl In pnlServerFleet.Controls
                Dim sc = TryCast(ctrl, ServerCard)
                If sc IsNot Nothing AndAlso sc.Profile?.Id = data.ServerId Then
                    sc.Metrics = data.Metrics
                    Exit For
                End If
            Next

            Dim sel = _serverService.SelectedServer
            If sel IsNot Nothing AndAlso sel.Id = data.ServerId Then
                UpdateSelectedServerKpis()
            End If
        End Sub

        Private Sub OnThemeChanged(sender As Object, e As EventArgs)
            BackColor = ThemeManager.BackgroundColor
            chartCpu.LineColor = ThemeManager.AccentColor
            chartConn.LineColor = ThemeManager.SuccessColor
            RefreshFleetCards()
            UpdateSelectedServerKpis()
        End Sub
    End Class
End Namespace
