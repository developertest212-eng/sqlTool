Namespace Configuration
    Public Class AppSettings
        Public Property Theme As String = "Dark" ' Dark or Light
        Public Property StartPage As String = "Dashboard"

        ' Polling
        Public Property PollingIntervalSeconds As Integer = 10
        Public Property AutoRefreshEnabled As Boolean = True
        Public Property MetricsHistoryRetentionMinutes As Integer = 60

        ' Safety
        Public Property ConfirmDestructiveQueries As Boolean = True
        Public Property MaxQueryResultsRows As Integer = 1000
        Public Property DefaultQueryTimeoutSeconds As Integer = 30

        ' Alert Thresholds
        Public Property AlertsEnabled As Boolean = True
        Public Property CpuWarningPercent As Double = 75.0
        Public Property CpuCriticalPercent As Double = 90.0
        Public Property MemoryWarningPercent As Double = 80.0
        Public Property MemoryCriticalPercent As Double = 92.0
        Public Property StorageWarningPercent As Double = 75.0
        Public Property StorageCriticalPercent As Double = 90.0
        Public Property LongRunningQuerySeconds As Integer = 30
        Public Property MaxConnectionsWarningPercent As Double = 85.0
        Public Property StaleBackupDaysThreshold As Integer = 7
    End Class
End Namespace
