Namespace Models
    Public Enum DatabaseEngine
        SqlServer = 1
        MySql = 2
    End Enum

    Public Enum ServerHealthStatus
        Unknown = 0
        Healthy = 1
        Warning = 2
        Critical = 3
        Offline = 4
    End Enum

    Public Enum AlertSeverity
        Info = 1
        Warning = 2
        Critical = 3
    End Enum

    Public Enum AlertStatus
        Active = 1
        Acknowledged = 2
        Resolved = 3
    End Enum

    Public Enum AuthenticationType
        WindowsAuthentication = 1
        SqlAuthentication = 2
    End Enum

    Public Enum DatabaseObjectType
        Table = 1
        View = 2
        StoredProcedure = 3
        ScalarFunction = 4
        TableFunction = 5
        Trigger = 6
        Index = 7
    End Enum
End Namespace
