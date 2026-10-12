Imports SQLCommandCenter.Configuration
Imports SQLCommandCenter.Logging
Imports SQLCommandCenter.Models

Namespace Services
    Public Class AlertService
        Private ReadOnly _alerts As New List(Of AlertInfo)()
        Private ReadOnly _syncLock As New Object()

        Public Event AlertTriggered As EventHandler(Of AlertInfo)
        Public Event AlertsChanged As EventHandler

        Public Function GetActiveAlerts() As List(Of AlertInfo)
            SyncLock _syncLock
                Return _alerts.Where(Function(a) a.Status = AlertStatus.Active).OrderByDescending(Function(a) a.Timestamp).ToList()
            End SyncLock
        End Function

        Public Function GetAllAlerts() As List(Of AlertInfo)
            SyncLock _syncLock
                Return _alerts.OrderByDescending(Function(a) a.Timestamp).ToList()
            End SyncLock
        End Function

        Public Sub EvaluateMetrics(server As ServerProfile, metrics As PerformanceMetrics, status As ServerStatus)
            If Not ConfigurationManager.Settings.AlertsEnabled OrElse server Is Nothing OrElse metrics Is Nothing Then Return

            Dim settings = ConfigurationManager.Settings

            ' 1. CPU Alerts
            If metrics.CpuPercent >= settings.CpuCriticalPercent Then
                AddAlert(server.Id, server.Name, "", AlertSeverity.Critical, "High CPU Utilization", $"CPU load is at {metrics.CpuPercent:F1}%, exceeding critical threshold of {settings.CpuCriticalPercent}%.")
            ElseIf metrics.CpuPercent >= settings.CpuWarningPercent Then
                AddAlert(server.Id, server.Name, "", AlertSeverity.Warning, "Elevated CPU Utilization", $"CPU load is at {metrics.CpuPercent:F1}%, exceeding warning threshold of {settings.CpuWarningPercent}%.")
            End If

            ' 2. Memory Alerts
            If metrics.MemoryPercent >= settings.MemoryCriticalPercent Then
                AddAlert(server.Id, server.Name, "", AlertSeverity.Critical, "High Memory Usage", $"Memory utilization is at {metrics.MemoryPercent:F1}%, exceeding critical threshold of {settings.MemoryCriticalPercent}%.")
            ElseIf metrics.MemoryPercent >= settings.MemoryWarningPercent Then
                AddAlert(server.Id, server.Name, "", AlertSeverity.Warning, "Elevated Memory Usage", $"Memory utilization is at {metrics.MemoryPercent:F1}%, exceeding warning threshold of {settings.MemoryWarningPercent}%.")
            End If

            ' 3. Blocking / Locks
            If metrics.BlockedSessionsCount > 0 Then
                Dim severity = If(metrics.BlockedSessionsCount >= 3, AlertSeverity.Critical, AlertSeverity.Warning)
                AddAlert(server.Id, server.Name, "", severity, "Blocking Sessions Detected", $"{metrics.BlockedSessionsCount} blocked session(s) detected waiting on locks.")
            End If

            ' 4. High Latency
            If status IsNot Nothing AndAlso status.LatencyMs > 1000 Then
                AddAlert(server.Id, server.Name, "", AlertSeverity.Warning, "High Connection Latency", $"Server response time is {status.LatencyMs} ms.")
            End If
        End Sub

        Public Sub ReportServerOffline(server As ServerProfile, reason As String)
            If server Is Nothing Then Return
            Dim msg = If(String.IsNullOrWhiteSpace(reason), "Server failed to respond to ping.", reason)
            AddAlert(server.Id, server.Name, "", AlertSeverity.Critical, "Server Offline / Unreachable", $"Unable to connect to {server.Name}: {msg}")
        End Sub

        Public Sub AddAlert(serverId As String, serverName As String, databaseName As String, severity As AlertSeverity, title As String, message As String)
            SyncLock _syncLock
                ' Avoid duplicate active alert within 2 minutes
                Dim recentDuplicate = _alerts.FirstOrDefault(Function(a) a.ServerId = serverId AndAlso a.Title = title AndAlso a.Status = AlertStatus.Active AndAlso (DateTime.Now - a.Timestamp).TotalMinutes < 2)
                If recentDuplicate IsNot Nothing Then
                    Return
                End If

                Dim alert As New AlertInfo() With {
                    .ServerId = serverId,
                    .ServerName = serverName,
                    .DatabaseName = databaseName,
                    .Severity = severity,
                    .Title = title,
                    .Message = message,
                    .Timestamp = DateTime.Now,
                    .Status = AlertStatus.Active
                }

                _alerts.Insert(0, alert)
                If _alerts.Count > 1000 Then
                    _alerts.RemoveAt(_alerts.Count - 1)
                End If

                AppLogger.Warn("AlertService", $"[{severity.ToString().ToUpper()}] {serverName}: {title} - {message}")
                RaiseEvent AlertTriggered(Me, alert)
                RaiseEvent AlertsChanged(Me, EventArgs.Empty)
            End SyncLock
        End Sub

        Public Sub AcknowledgeAlert(alertId As String)
            SyncLock _syncLock
                Dim alert = _alerts.FirstOrDefault(Function(a) a.Id = alertId)
                If alert IsNot Nothing Then
                    alert.Status = AlertStatus.Acknowledged
                    RaiseEvent AlertsChanged(Me, EventArgs.Empty)
                End If
            End SyncLock
        End Sub

        Public Sub ResolveAlert(alertId As String)
            SyncLock _syncLock
                Dim alert = _alerts.FirstOrDefault(Function(a) a.Id = alertId)
                If alert IsNot Nothing Then
                    alert.Status = AlertStatus.Resolved
                    RaiseEvent AlertsChanged(Me, EventArgs.Empty)
                End If
            End SyncLock
        End Sub

        Public Sub ClearAll()
            SyncLock _syncLock
                _alerts.Clear()
                RaiseEvent AlertsChanged(Me, EventArgs.Empty)
            End SyncLock
        End Sub
    End Class
End Namespace
