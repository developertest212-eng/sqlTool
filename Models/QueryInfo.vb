Namespace Models
    Public Class QueryInfo
        Public Property SessionId As Integer = 0
        Public Property DatabaseName As String = ""
        Public Property UserName As String = ""
        Public Property HostName As String = ""
        Public Property ProgramName As String = ""
        Public Property Status As String = ""
        Public Property Command As String = ""
        Public Property QueryText As String = ""
        Public Property StartTime As DateTime = DateTime.Now
        Public Property DurationSeconds As Long = 0
        Public Property CpuTimeMs As Long = 0
        Public Property LogicalReads As Long = 0
        Public Property Writes As Long = 0
        Public Property WaitType As String = ""
        Public Property BlockingSessionId As Integer = 0

        Public ReadOnly Property IsBlocked As Boolean
            Get
                Return BlockingSessionId > 0
            End Get
        End Property

        Public ReadOnly Property DurationDisplay As String
            Get
                Dim ts = TimeSpan.FromSeconds(DurationSeconds)
                If ts.TotalHours >= 1 Then
                    Return $"{CInt(ts.TotalHours)}h {ts.Minutes}m {ts.Seconds}s"
                ElseIf ts.TotalMinutes >= 1 Then
                    Return $"{ts.Minutes}m {ts.Seconds}s"
                Else
                    Return $"{ts.Seconds}s"
                End If
            End Get
        End Property
    End Class

    Public Class SessionInfo
        Public Property SessionId As Integer = 0
        Public Property LoginName As String = ""
        Public Property HostName As String = ""
        Public Property ProgramName As String = ""
        Public Property Status As String = ""
        Public Property DatabaseName As String = ""
        Public Property LoginTime As DateTime = DateTime.MinValue
        Public Property LastRequestTime As DateTime = DateTime.MinValue
        Public Property Command As String = ""
        Public Property CpuTime As Long = 0
        Public Property MemoryUsageKB As Long = 0
    End Class
End Namespace
