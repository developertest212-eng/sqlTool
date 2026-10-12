Namespace Models
    Public Class PerformanceMetrics
        Public Property ServerId As String = ""
        Public Property Timestamp As DateTime = DateTime.Now

        ' CPU
        Public Property CpuPercent As Double = 0.0

        ' Memory
        Public Property MemoryUsedMB As Double = 0.0
        Public Property MemoryTotalMB As Double = 0.0
        Public Property MemoryPercent As Double = 0.0

        ' Connections & Sessions
        Public Property ActiveConnections As Integer = 0
        Public Property MaxConnections As Integer = 0
        Public Property RunningQueries As Integer = 0
        Public Property BlockedSessionsCount As Integer = 0

        ' Throughput
        Public Property QueriesPerSecond As Double = 0.0
        Public Property TransactionsPerSecond As Double = 0.0

        ' Storage & Buffer
        Public Property DatabaseSizeMB As Double = 0.0
        Public Property DiskUsagePercent As Double = 0.0
        Public Property BufferCacheHitRatio As Double = 0.0

        ' Network & I/O
        Public Property BytesReceivedPerSec As Double = 0.0
        Public Property BytesSentPerSec As Double = 0.0

        Public ReadOnly Property CpuDisplay As String
            Get
                Return $"{Math.Round(CpuPercent, 1)}%"
            End Get
        End Property

        Public ReadOnly Property MemoryDisplay As String
            Get
                If MemoryTotalMB > 0 Then
                    Return $"{Math.Round(MemoryUsedMB, 0):N0} MB / {Math.Round(MemoryTotalMB, 0):N0} MB ({Math.Round(MemoryPercent, 1)}%)"
                ElseIf MemoryUsedMB > 0 Then
                    Return $"{Math.Round(MemoryUsedMB, 0):N0} MB"
                Else
                    Return $"{Math.Round(MemoryPercent, 1)}%"
                End If
            End Get
        End Property

        Public ReadOnly Property ConnectionsDisplay As String
            Get
                If MaxConnections > 0 Then
                    Return $"{ActiveConnections} / {MaxConnections}"
                Else
                    Return $"{ActiveConnections}"
                End If
            End Get
        End Property

        Public ReadOnly Property DatabaseSizeDisplay As String
            Get
                If DatabaseSizeMB >= 1024 * 1024 Then
                    Return $"{Math.Round(DatabaseSizeMB / (1024.0 * 1024.0), 2)} TB"
                ElseIf DatabaseSizeMB >= 1024 Then
                    Return $"{Math.Round(DatabaseSizeMB / 1024.0, 1)} GB"
                Else
                    Return $"{Math.Round(DatabaseSizeMB, 0)} MB"
                End If
            End Get
        End Property
    End Class
End Namespace
