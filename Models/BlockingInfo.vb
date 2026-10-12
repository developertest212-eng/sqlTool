Namespace Models
    Public Class BlockingInfo
        Public Property BlockerSessionId As Integer = 0
        Public Property BlockedSessionId As Integer = 0
        Public Property DatabaseName As String = ""
        Public Property BlockerUser As String = ""
        Public Property BlockedUser As String = ""
        Public Property WaitDurationMs As Long = 0
        Public Property WaitType As String = ""
        Public Property LockType As String = ""
        Public Property ResourceDescription As String = ""
        Public Property BlockerQuery As String = ""
        Public Property BlockedQuery As String = ""

        Public ReadOnly Property WaitDurationSeconds As Double
            Get
                Return Math.Round(WaitDurationMs / 1000.0, 1)
            End Get
        End Property

        Public Overrides Function ToString() As String
            Return $"Session {BlockerSessionId} ➔ Blocking Session {BlockedSessionId} ({WaitDurationSeconds}s)"
        End Function
    End Class

    Public Class StorageInfo
        Public Property DatabaseName As String = ""
        Public Property FileName As String = ""
        Public Property FileType As String = "DATA" ' DATA or LOG
        Public Property PhysicalPath As String = ""
        Public Property TotalSizeMB As Double = 0.0
        Public Property UsedSizeMB As Double = 0.0
        Public Property FreeSizeMB As Double = 0.0
        Public Property AutoGrowth As String = ""

        Public ReadOnly Property UsedPercent As Double
            Get
                If TotalSizeMB <= 0 Then Return 0
                Return Math.Round((UsedSizeMB / TotalSizeMB) * 100.0, 1)
            End Get
        End Property

        Public ReadOnly Property FreePercent As Double
            Get
                If TotalSizeMB <= 0 Then Return 0
                Return Math.Round((FreeSizeMB / TotalSizeMB) * 100.0, 1)
            End Get
        End Property

        Public ReadOnly Property StatusLevel As ServerHealthStatus
            Get
                If UsedPercent >= 90.0 Then
                    Return ServerHealthStatus.Critical
                ElseIf UsedPercent >= 75.0 Then
                    Return ServerHealthStatus.Warning
                Else
                    Return ServerHealthStatus.Healthy
                End If
            End Get
        End Property
    End Class

    Public Class BackupInfo
        Public Property DatabaseName As String = ""
        Public Property BackupType As String = "Full" ' Full, Differential, Log
        Public Property StartDate As DateTime = DateTime.MinValue
        Public Property FinishDate As DateTime = DateTime.MinValue
        Public Property BackupSizeMB As Double = 0.0
        Public Property CompressedSizeMB As Double = 0.0
        Public Property DurationSeconds As Long = 0
        Public Property Status As String = "SUCCESS"

        Public ReadOnly Property AgeDays As Double
            Get
                If FinishDate = DateTime.MinValue Then Return 9999
                Return Math.Round((DateTime.Now - FinishDate).TotalDays, 1)
            End Get
        End Property

        Public ReadOnly Property IsStale As Boolean
            Get
                ' Warn if full backup is older than 7 days, or log backup older than 1 day
                If BackupType.Equals("Full", StringComparison.OrdinalIgnoreCase) Then
                    Return AgeDays > 7
                ElseIf BackupType.Equals("Log", StringComparison.OrdinalIgnoreCase) Then
                    Return AgeDays > 1
                End If
                Return AgeDays > 3
            End Get
        End Property
    End Class

    Public Class AlertInfo
        Public Property Id As String = Guid.NewGuid().ToString()
        Public Property ServerId As String = ""
        Public Property ServerName As String = ""
        Public Property DatabaseName As String = ""
        Public Property Timestamp As DateTime = DateTime.Now
        Public Property Severity As AlertSeverity = AlertSeverity.Warning
        Public Property Title As String = ""
        Public Property Message As String = ""
        Public Property Status As AlertStatus = AlertStatus.Active

        Public ReadOnly Property SeverityDisplay As String
            Get
                Return Severity.ToString().ToUpperInvariant()
            End Get
        End Property
    End Class
End Namespace
