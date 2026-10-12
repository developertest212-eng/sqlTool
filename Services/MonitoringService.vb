Imports System.Collections.Concurrent
Imports System.Threading
Imports System.Threading.Tasks
Imports SQLCommandCenter.Configuration
Imports SQLCommandCenter.Data
Imports SQLCommandCenter.Logging
Imports SQLCommandCenter.Models

Namespace Services
    Public Class MonitoringService
        Implements IDisposable

        Private ReadOnly _serverService As ServerService
        Private ReadOnly _alertService As AlertService
        Private ReadOnly _statuses As New ConcurrentDictionary(Of String, ServerStatus)()
        Private ReadOnly _metricsHistory As New ConcurrentDictionary(Of String, List(Of PerformanceMetrics))()
        Private ReadOnly _historyLock As New Object()

        Private _cts As CancellationTokenSource
        Private _monitoringTask As Task
        Private _isDisposed As Boolean = False
        Private _isPaused As Boolean = False

        Public Event ServerStatusChanged As EventHandler(Of ServerStatus)
        Public Event PerformanceUpdated As EventHandler(Of (ServerId As String, Metrics As PerformanceMetrics))
        Public Event RefreshCompleted As EventHandler

        Public Sub New(serverService As ServerService, alertService As AlertService)
            _serverService = serverService
            _alertService = alertService
        End Sub

        Public Sub Start()
            If _monitoringTask IsNot Nothing Then Return

            _cts = New CancellationTokenSource()
            _monitoringTask = Task.Run(AddressOf MonitoringLoopAsync)
            AppLogger.Info("MonitoringService", "Background monitoring service started.")
        End Sub

        Public Sub StopMonitoring()
            Try
                If _cts IsNot Nothing Then
                    _cts.Cancel()
                End If
            Catch ex As Exception
                AppLogger.Warn("MonitoringService", "Error stopping monitor task", ex)
            End Try
        End Sub

        Public Sub Pause()
            _isPaused = True
        End Sub

        Public Sub [Resume]()
            _isPaused = False
        End Sub

        Public Function GetStatus(serverId As String) As ServerStatus
            Dim status As ServerStatus = Nothing
            If _statuses.TryGetValue(serverId, status) Then
                Return status
            End If
            Return New ServerStatus() With {.ServerId = serverId, .HealthStatus = ServerHealthStatus.Unknown}
        End Function

        Public Function GetAllStatuses() As List(Of ServerStatus)
            Return _statuses.Values.ToList()
        End Function

        Public Function GetMetricsHistory(serverId As String, Optional maxPoints As Integer = 60) As List(Of PerformanceMetrics)
            SyncLock _historyLock
                Dim list As List(Of PerformanceMetrics) = Nothing
                If _metricsHistory.TryGetValue(serverId, list) Then
                    Return list.TakeLast(maxPoints).ToList()
                End If
                Return New List(Of PerformanceMetrics)()
            End SyncLock
        End Function

        Public Async Function RefreshServerAsync(profile As ServerProfile) As Task
            If profile Is Nothing Then Return

            Try
                Using provider = DatabaseProviderFactory.CreateProvider(profile)
                    Dim status = Await provider.GetServerStatusAsync().ConfigureAwait(False)
                    _statuses(profile.Id) = status
                    RaiseEvent ServerStatusChanged(Me, status)

                    If status.IsOnline Then
                        Dim metrics = Await provider.GetPerformanceMetricsAsync().ConfigureAwait(False)
                        RecordMetrics(profile.Id, metrics)
                        RaiseEvent PerformanceUpdated(Me, (profile.Id, metrics))

                        ' Evaluate alerts
                        _alertService.EvaluateMetrics(profile, metrics, status)
                    Else
                        _alertService.ReportServerOffline(profile, status.ErrorMessage)
                    End If
                End Using
            Catch ex As Exception
                AppLogger.Error("MonitoringService", $"Error during refresh for {profile.Name}", ex)
                Dim offStatus As New ServerStatus() With {
                    .ServerId = profile.Id,
                    .ServerName = profile.Name,
                    .Engine = profile.Engine,
                    .IsOnline = False,
                    .HealthStatus = ServerHealthStatus.Offline,
                    .ErrorMessage = ex.Message,
                    .LastCheckTime = DateTime.Now
                }
                _statuses(profile.Id) = offStatus
                RaiseEvent ServerStatusChanged(Me, offStatus)
                _alertService.ReportServerOffline(profile, ex.Message)
            End Try
        End Function

        Public Async Function RefreshAllAsync() As Task
            Dim servers = _serverService.GetAllServers()
            Dim tasks = servers.Select(Function(s) RefreshServerAsync(s)).ToList()
            Await Task.WhenAll(tasks).ConfigureAwait(False)
            RaiseEvent RefreshCompleted(Me, EventArgs.Empty)
        End Function

        Private Sub RecordMetrics(serverId As String, metrics As PerformanceMetrics)
            SyncLock _historyLock
                Dim list As List(Of PerformanceMetrics) = Nothing
                If Not _metricsHistory.TryGetValue(serverId, list) Then
                    list = New List(Of PerformanceMetrics)()
                    _metricsHistory(serverId) = list
                End If

                list.Add(metrics)
                Dim retentionMinutes = ConfigurationManager.Settings.MetricsHistoryRetentionMinutes
                Dim cutoff = DateTime.Now.AddMinutes(-retentionMinutes)
                list.RemoveAll(Function(m) m.Timestamp < cutoff)
            End SyncLock
        End Sub

        Private Async Function MonitoringLoopAsync() As Task
            While Not _cts.Token.IsCancellationRequested
                Dim encounteredError As Boolean = False
                Try
                    If Not _isPaused AndAlso ConfigurationManager.Settings.AutoRefreshEnabled Then
                        Dim servers = _serverService.GetAllServers()
                        For Each server In servers
                            If _cts.Token.IsCancellationRequested Then Exit For
                            Await RefreshServerAsync(server).ConfigureAwait(False)
                        Next
                        RaiseEvent RefreshCompleted(Me, EventArgs.Empty)
                    End If

                    Dim delaySec = Math.Max(5, ConfigurationManager.Settings.PollingIntervalSeconds)
                    Await Task.Delay(TimeSpan.FromSeconds(delaySec), _cts.Token).ConfigureAwait(False)
                Catch ex As OperationCanceledException
                    Exit While
                Catch ex As Exception
                    AppLogger.Error("MonitoringService", "Unexpected error in monitoring loop", ex)
                    encounteredError = True
                End Try

                If encounteredError AndAlso Not _cts.Token.IsCancellationRequested Then
                    Try
                        Await Task.Delay(5000, _cts.Token).ConfigureAwait(False)
                    Catch
                        Exit While
                    End Try
                End If
            End While
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            If Not _isDisposed Then
                _isDisposed = True
                StopMonitoring()
            End If
        End Sub
    End Class
End Namespace
