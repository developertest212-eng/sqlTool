Namespace Models
    Public Class ServerStatus
        Public Property ServerId As String = ""
        Public Property ServerName As String = ""
        Public Property Engine As DatabaseEngine = DatabaseEngine.SqlServer
        Public Property HealthStatus As ServerHealthStatus = ServerHealthStatus.Unknown
        Public Property IsOnline As Boolean = False
        Public Property Version As String = "Unknown"
        Public Property Edition As String = ""
        Public Property ProductLevel As String = ""
        Public Property HostPlatform As String = ""
        Public Property Uptime As TimeSpan = TimeSpan.Zero
        Public Property LatencyMs As Long = 0
        Public Property LastCheckTime As DateTime = DateTime.MinValue
        Public Property ErrorMessage As String = ""
        Public Property DatabaseCount As Integer = 0
        Public Property ActiveConnections As Integer = 0
        Public Property RunningQueries As Integer = 0

        Public ReadOnly Property UptimeDisplay As String
            Get
                If Uptime.TotalSeconds <= 0 Then Return "0m"
                If Uptime.TotalDays >= 1 Then
                    Return $"{CInt(Uptime.TotalDays)}d {Uptime.Hours}h {Uptime.Minutes}m"
                ElseIf Uptime.TotalHours >= 1 Then
                    Return $"{Uptime.Hours}h {Uptime.Minutes}m"
                Else
                    Return $"{Uptime.Minutes}m {Uptime.Seconds}s"
                End If
            End Get
        End Property

        Public ReadOnly Property LatencyDisplay As String
            Get
                If Not IsOnline Then Return "Offline"
                Return $"{LatencyMs} ms"
            End Get
        End Property
    End Class
End Namespace
