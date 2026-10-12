Imports System.Data
Imports System.Threading
Imports System.Threading.Tasks
Imports SQLCommandCenter.Models

Namespace Data
    Public Interface IDatabaseProvider
        Inherits IDisposable

        ReadOnly Property Profile As ServerProfile
        ReadOnly Property Engine As DatabaseEngine

        Function TestConnectionAsync(Optional cancellationToken As CancellationToken = Nothing) As Task(Of (Success As Boolean, LatencyMs As Long, Version As String, ErrorMessage As String))
        Function GetServerStatusAsync(Optional cancellationToken As CancellationToken = Nothing) As Task(Of ServerStatus)
        Function GetPerformanceMetricsAsync(Optional cancellationToken As CancellationToken = Nothing) As Task(Of PerformanceMetrics)
        Function GetDatabasesAsync(Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DatabaseInfo))
        Function GetDatabaseObjectsAsync(databaseName As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DatabaseObjectInfo))
        Function GetTableColumnsAsync(databaseName As String, schemaName As String, tableName As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of ColumnInfo))
        Function GetRunningQueriesAsync(Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of QueryInfo))
        Function GetSessionsAsync(Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of SessionInfo))
        Function GetBlockingInfoAsync(Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of BlockingInfo))
        Function GetStorageInfoAsync(Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of StorageInfo))
        Function GetBackupInfoAsync(Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of BackupInfo))
        Function ExecuteQueryAsync(sql As String, Optional databaseName As String = "", Optional maxRows As Integer = 1000, Optional timeoutSeconds As Integer = 30, Optional cancellationToken As CancellationToken = Nothing) As Task(Of (ResultTable As DataTable, ElapsedMs As Long, RowsAffected As Integer, Message As String))
        Function KillSessionAsync(sessionId As Integer, Optional cancellationToken As CancellationToken = Nothing) As Task(Of (Success As Boolean, Message As String))
    End Interface
End Namespace
