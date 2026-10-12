Imports System.Data
Imports System.Diagnostics
Imports System.Threading
Imports System.Threading.Tasks
Imports Microsoft.Data.SqlClient
Imports SQLCommandCenter.Logging
Imports SQLCommandCenter.Models

Namespace Data
    Public Class SqlServerProvider
        Implements IDatabaseProvider

        Private ReadOnly _profile As ServerProfile
        Private _disposed As Boolean = False

        Public Sub New(profile As ServerProfile)
            _profile = profile
        End Sub

        Public ReadOnly Property Profile As ServerProfile Implements IDatabaseProvider.Profile
            Get
                Return _profile
            End Get
        End Property

        Public ReadOnly Property Engine As DatabaseEngine Implements IDatabaseProvider.Engine
            Get
                Return DatabaseEngine.SqlServer
            End Get
        End Property

        Private Function CreateConnection(Optional databaseName As String = "") As SqlConnection
            Dim connStr = _profile.BuildConnectionString(databaseName)
            Return New SqlConnection(connStr)
        End Function

        Public Async Function TestConnectionAsync(Optional cancellationToken As CancellationToken = Nothing) As Task(Of (Success As Boolean, LatencyMs As Long, Version As String, ErrorMessage As String)) Implements IDatabaseProvider.TestConnectionAsync
            Dim sw = Stopwatch.StartNew()
            Try
                Using conn = CreateConnection()
                    Await conn.OpenAsync(cancellationToken).ConfigureAwait(False)
                    sw.Stop()

                    Dim version = ""
                    Using cmd = conn.CreateCommand()
                        cmd.CommandText = "SELECT @@VERSION AS Ver, SERVERPROPERTY('ProductVersion') AS ProdVer, SERVERPROPERTY('ProductLevel') AS Level, SERVERPROPERTY('Edition') AS Edition;"
                        cmd.CommandTimeout = _profile.QueryTimeoutSeconds
                        Using reader = Await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(False)
                            If Await reader.ReadAsync(cancellationToken).ConfigureAwait(False) Then
                                Dim edition = reader("Edition")?.ToString()
                                Dim prodVer = reader("ProdVer")?.ToString()
                                version = $"SQL Server {prodVer} ({edition})"
                            End If
                        End Using
                    End Using

                    Return (True, sw.ElapsedMilliseconds, version, String.Empty)
                End Using
            Catch ex As Exception
                sw.Stop()
                AppLogger.Warn("SqlServerProvider", $"Connection test failed for {_profile.Name}: {ex.Message}")
                Return (False, sw.ElapsedMilliseconds, String.Empty, ex.Message)
            End Try
        End Function

        Public Async Function GetServerStatusAsync(Optional cancellationToken As CancellationToken = Nothing) As Task(Of ServerStatus) Implements IDatabaseProvider.GetServerStatusAsync
            Dim status As New ServerStatus() With {
                .ServerId = _profile.Id,
                .ServerName = _profile.Name,
                .Engine = DatabaseEngine.SqlServer,
                .LastCheckTime = DateTime.Now
            }

            Dim sw = Stopwatch.StartNew()
            Try
                Using conn = CreateConnection()
                    Await conn.OpenAsync(cancellationToken).ConfigureAwait(False)
                    sw.Stop()
                    status.LatencyMs = sw.ElapsedMilliseconds
                    status.IsOnline = True

                    Dim query = "
                        SELECT 
                            SERVERPROPERTY('ProductVersion') AS ProductVersion,
                            SERVERPROPERTY('ProductLevel') AS ProductLevel,
                            SERVERPROPERTY('Edition') AS Edition,
                            SERVERPROPERTY('MachineName') AS MachineName,
                            (SELECT sqlserver_start_time FROM sys.dm_os_sys_info) AS StartTime,
                            (SELECT COUNT(*) FROM sys.databases WHERE state = 0) AS DbCount,
                            (SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE is_user_process = 1) AS ActiveSessions,
                            (SELECT COUNT(*) FROM sys.dm_exec_requests WHERE session_id > 50) AS RunningQueries;"

                    Using cmd = conn.CreateCommand()
                        cmd.CommandText = query
                        cmd.CommandTimeout = _profile.QueryTimeoutSeconds
                        Using reader = Await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(False)
                            If Await reader.ReadAsync(cancellationToken).ConfigureAwait(False) Then
                                status.Version = $"SQL Server {reader("ProductVersion")}"
                                status.Edition = reader("Edition")?.ToString()
                                status.ProductLevel = reader("ProductLevel")?.ToString()
                                status.HostPlatform = reader("MachineName")?.ToString()
                                If Not IsDBNull(reader("StartTime")) Then
                                    Dim startTime = Convert.ToDateTime(reader("StartTime"))
                                    status.Uptime = DateTime.Now - startTime
                                End If
                                status.DatabaseCount = Convert.ToInt32(reader("DbCount"))
                                status.ActiveConnections = Convert.ToInt32(reader("ActiveSessions"))
                                status.RunningQueries = Convert.ToInt32(reader("RunningQueries"))
                            End If
                        End Using
                    End Using

                    status.HealthStatus = If(status.LatencyMs > 500, ServerHealthStatus.Warning, ServerHealthStatus.Healthy)
                End Using
            Catch ex As Exception
                sw.Stop()
                status.IsOnline = False
                status.HealthStatus = ServerHealthStatus.Offline
                status.ErrorMessage = ex.Message
                status.LatencyMs = sw.ElapsedMilliseconds
                AppLogger.Error("SqlServerProvider", $"Failed to get status for {_profile.Name}: {ex.Message}")
            End Try

            Return status
        End Function

        Public Async Function GetPerformanceMetricsAsync(Optional cancellationToken As CancellationToken = Nothing) As Task(Of PerformanceMetrics) Implements IDatabaseProvider.GetPerformanceMetricsAsync
            Dim metrics As New PerformanceMetrics() With {
                .ServerId = _profile.Id,
                .Timestamp = DateTime.Now
            }

            Try
                Using conn = CreateConnection()
                    Await conn.OpenAsync(cancellationToken).ConfigureAwait(False)

                    ' 1. CPU Utilization via Ring Buffer
                    Dim cpuQuery = "
                        BEGIN TRY
                            DECLARE @ts_now bigint = (SELECT cpu_ticks/(cpu_ticks/ms_ticks) FROM sys.dm_os_sys_info);
                            SELECT TOP 1 
                                100 - SystemIdle AS SystemCpu,
                                SQLProcessUtilization AS SqlCpu
                            FROM (
                                SELECT record.value('(./Record/@id)[1]', 'int') AS record_id,
                                    record.value('(./Record/SchedulerMonitorEvent/SystemHealth/SystemIdle)[1]', 'int') AS SystemIdle,
                                    record.value('(./Record/SchedulerMonitorEvent/SystemHealth/ProcessUtilization)[1]', 'int') AS SQLProcessUtilization,
                                    timestamp
                                FROM (
                                    SELECT timestamp, CONVERT(xml, record) AS record 
                                    FROM sys.dm_os_ring_buffers 
                                    WHERE ring_buffer_type = N'RING_BUFFER_SCHEDULER_MONITOR' 
                                    AND record LIKE '%<SchedulerMonitorEvent>%'
                                ) AS x
                            ) AS y
                            ORDER BY record_id DESC;
                        END TRY
                        BEGIN CATCH
                            SELECT 0 AS SystemCpu, 0 AS SqlCpu;
                        END CATCH;"

                    Using cmd = conn.CreateCommand()
                        cmd.CommandText = cpuQuery
                        cmd.CommandTimeout = 5
                        Using reader = Await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(False)
                            If Await reader.ReadAsync(cancellationToken).ConfigureAwait(False) Then
                                If Not IsDBNull(reader("SqlCpu")) Then
                                    metrics.CpuPercent = Convert.ToDouble(reader("SqlCpu"))
                                End If
                            End If
                        End Using
                    End Using

                    ' 2. Memory Usage
                    Dim memQuery = "
                        SELECT 
                            (SELECT physical_memory_in_use_kb / 1024 FROM sys.dm_os_process_memory) AS SqlMemoryMB,
                            (SELECT total_physical_memory_kb / 1024 FROM sys.dm_os_sys_memory) AS TotalMemoryMB,
                            (SELECT memory_utilization_percentage FROM sys.dm_os_process_memory) AS MemoryPercent;"

                    Using cmd = conn.CreateCommand()
                        cmd.CommandText = memQuery
                        cmd.CommandTimeout = 5
                        Using reader = Await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(False)
                            If Await reader.ReadAsync(cancellationToken).ConfigureAwait(False) Then
                                metrics.MemoryUsedMB = If(IsDBNull(reader("SqlMemoryMB")), 0, Convert.ToDouble(reader("SqlMemoryMB")))
                                metrics.MemoryTotalMB = If(IsDBNull(reader("TotalMemoryMB")), 0, Convert.ToDouble(reader("TotalMemoryMB")))
                                metrics.MemoryPercent = If(IsDBNull(reader("MemoryPercent")), 0, Convert.ToDouble(reader("MemoryPercent")))
                            End If
                        End Using
                    End Using

                    ' 3. Active Sessions, Running Queries, Blocked count
                    Dim statsQuery = "
                        SELECT 
                            (SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE is_user_process = 1) AS ActiveConn,
                            (SELECT COUNT(*) FROM sys.dm_exec_requests WHERE session_id > 50) AS RunningQueries,
                            (SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id <> 0) AS BlockedCount,
                            (SELECT SUM(CAST(size AS bigint)) * 8 / 1024 FROM sys.master_files) AS TotalSizeMB;"

                    Using cmd = conn.CreateCommand()
                        cmd.CommandText = statsQuery
                        cmd.CommandTimeout = 5
                        Using reader = Await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(False)
                            If Await reader.ReadAsync(cancellationToken).ConfigureAwait(False) Then
                                metrics.ActiveConnections = If(IsDBNull(reader("ActiveConn")), 0, Convert.ToInt32(reader("ActiveConn")))
                                metrics.RunningQueries = If(IsDBNull(reader("RunningQueries")), 0, Convert.ToInt32(reader("RunningQueries")))
                                metrics.BlockedSessionsCount = If(IsDBNull(reader("BlockedCount")), 0, Convert.ToInt32(reader("BlockedCount")))
                                metrics.DatabaseSizeMB = If(IsDBNull(reader("TotalSizeMB")), 0, Convert.ToDouble(reader("TotalSizeMB")))
                            End If
                        End Using
                    End Using

                    ' 4. Buffer Cache Hit Ratio
                    Dim bchrQuery = "
                        SELECT 
                            ROUND(CAST(A.cntr_value AS FLOAT) / CAST(B.cntr_value AS FLOAT) * 100, 2) AS BufferCacheHitRatio
                        FROM sys.dm_os_performance_counters A
                        JOIN sys.dm_os_performance_counters B ON A.object_name = B.object_name
                        WHERE A.counter_name = 'Buffer cache hit ratio'
                          AND B.counter_name = 'Buffer cache hit ratio base';"

                    Using cmd = conn.CreateCommand()
                        cmd.CommandText = bchrQuery
                        cmd.CommandTimeout = 5
                        Using reader = Await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(False)
                            If Await reader.ReadAsync(cancellationToken).ConfigureAwait(False) Then
                                If Not IsDBNull(reader("BufferCacheHitRatio")) Then
                                    metrics.BufferCacheHitRatio = Convert.ToDouble(reader("BufferCacheHitRatio"))
                                End If
                            End If
                        End Using
                    End Using

                    ' 5. Throughput (Batch Requests / sec)
                    Dim throughputQuery = "
                        SELECT cntr_value FROM sys.dm_os_performance_counters 
                        WHERE counter_name = 'Batch Requests/sec' AND object_name LIKE '%:SQL Statistics%';"

                    Using cmd = conn.CreateCommand()
                        cmd.CommandText = throughputQuery
                        cmd.CommandTimeout = 5
                        Dim val = Await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(False)
                        If val IsNot Nothing AndAlso Not IsDBNull(val) Then
                            metrics.QueriesPerSecond = Convert.ToDouble(val) Mod 1000 ' Normalize sample
                        End If
                    End Using

                End Using
            Catch ex As Exception
                AppLogger.Warn("SqlServerProvider", $"Error retrieving performance metrics for {_profile.Name}: {ex.Message}")
            End Try

            Return metrics
        End Function

        Public Async Function GetDatabasesAsync(Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DatabaseInfo)) Implements IDatabaseProvider.GetDatabasesAsync
            Dim list As New List(Of DatabaseInfo)()
            Try
                Using conn = CreateConnection()
                    Await conn.OpenAsync(cancellationToken).ConfigureAwait(False)

                    Dim query = "
                        SELECT 
                            d.name AS DatabaseName,
                            d.state_desc AS StateDesc,
                            d.recovery_model_desc AS RecoveryModel,
                            d.collation_name AS CollationName,
                            d.compatibility_level AS CompatibilityLevel,
                            d.create_date AS CreateDate,
                            ISNULL(SUM(CASE WHEN mf.type = 0 THEN CAST(mf.size AS bigint) * 8 / 1024 ELSE 0 END), 0) AS DataSizeMB,
                            ISNULL(SUM(CASE WHEN mf.type = 1 THEN CAST(mf.size AS bigint) * 8 / 1024 ELSE 0 END), 0) AS LogSizeMB,
                            ISNULL(SUM(CAST(mf.size AS bigint) * 8 / 1024), 0) AS TotalSizeMB
                        FROM sys.databases d
                        LEFT JOIN sys.master_files mf ON d.database_id = mf.database_id
                        GROUP BY d.name, d.state_desc, d.recovery_model_desc, d.collation_name, d.compatibility_level, d.create_date
                        ORDER BY d.name;"

                    Using cmd = conn.CreateCommand()
                        cmd.CommandText = query
                        cmd.CommandTimeout = _profile.QueryTimeoutSeconds
                        Using reader = Await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(False)
                            While Await reader.ReadAsync(cancellationToken).ConfigureAwait(False)
                                Dim info As New DatabaseInfo() With {
                                    .Name = reader("DatabaseName")?.ToString(),
                                    .Status = reader("StateDesc")?.ToString(),
                                    .RecoveryModel = reader("RecoveryModel")?.ToString(),
                                    .Collation = reader("CollationName")?.ToString(),
                                    .CompatibilityLevel = reader("CompatibilityLevel")?.ToString(),
                                    .CreatedDate = Convert.ToDateTime(reader("CreateDate")),
                                    .DataSizeMB = Convert.ToDouble(reader("DataSizeMB")),
                                    .LogSizeMB = Convert.ToDouble(reader("LogSizeMB")),
                                    .TotalSizeMB = Convert.ToDouble(reader("TotalSizeMB"))
                                }
                                list.Add(info)
                            End While
                        End Using
                    End Using
                End Using
            Catch ex As Exception
                AppLogger.Error("SqlServerProvider", $"Failed to fetch databases for {_profile.Name}", ex)
            End Try

            Return list
        End Function

        Public Async Function GetDatabaseObjectsAsync(databaseName As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DatabaseObjectInfo)) Implements IDatabaseProvider.GetDatabaseObjectsAsync
            Dim list As New List(Of DatabaseObjectInfo)()
            Try
                Using conn = CreateConnection(databaseName)
                    Await conn.OpenAsync(cancellationToken).ConfigureAwait(False)

                    Dim query = "
                        SELECT 
                            s.name AS SchemaName,
                            o.name AS ObjectName,
                            o.type_desc AS ObjectType,
                            o.create_date AS CreateDate,
                            o.modify_date AS ModifyDate,
                            ISNULL(p.rows, 0) AS [RowCount],
                            ISNULL(a.total_pages * 8, 0) AS TotalSizeKB
                        FROM sys.objects o
                        INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
                        LEFT JOIN (
                            SELECT object_id, SUM(rows) AS rows
                            FROM sys.partitions
                            WHERE index_id IN (0, 1)
                            GROUP BY object_id
                        ) p ON o.object_id = p.object_id
                        LEFT JOIN (
                            SELECT p.object_id, SUM(a.total_pages) AS total_pages
                            FROM sys.partitions p
                            JOIN sys.allocation_units a ON p.partition_id = a.container_id
                            GROUP BY p.object_id
                        ) a ON o.object_id = a.object_id
                        WHERE o.is_ms_shipped = 0 
                          AND o.type IN ('U', 'V', 'P', 'FN', 'IF', 'TF')
                        ORDER BY s.name, o.name;"

                    Using cmd = conn.CreateCommand()
                        cmd.CommandText = query
                        cmd.CommandTimeout = _profile.QueryTimeoutSeconds
                        Using reader = Await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(False)
                            While Await reader.ReadAsync(cancellationToken).ConfigureAwait(False)
                                Dim objTypeStr = reader("ObjectType")?.ToString()
                                Dim mappedType As DatabaseObjectType
                                Select Case objTypeStr
                                    Case "USER_TABLE" : mappedType = DatabaseObjectType.Table
                                    Case "VIEW" : mappedType = DatabaseObjectType.View
                                    Case "SQL_STORED_PROCEDURE" : mappedType = DatabaseObjectType.StoredProcedure
                                    Case "SQL_SCALAR_FUNCTION" : mappedType = DatabaseObjectType.ScalarFunction
                                    Case Else : mappedType = DatabaseObjectType.TableFunction
                                End Select

                                Dim obj As New DatabaseObjectInfo() With {
                                    .DatabaseName = databaseName,
                                    .SchemaName = reader("SchemaName")?.ToString(),
                                    .Name = reader("ObjectName")?.ToString(),
                                    .ObjectType = mappedType,
                                    .CreateDate = Convert.ToDateTime(reader("CreateDate")),
                                    .ModifyDate = Convert.ToDateTime(reader("ModifyDate")),
                                    .RowCount = Convert.ToInt64(reader("RowCount")),
                                    .TotalSizeKB = Convert.ToInt64(reader("TotalSizeKB"))
                                }
                                list.Add(obj)
                            End While
                        End Using
                    End Using
                End Using
            Catch ex As Exception
                AppLogger.Error("SqlServerProvider", $"Failed to get objects for database {databaseName}", ex)
            End Try

            Return list
        End Function

        Public Async Function GetTableColumnsAsync(databaseName As String, schemaName As String, tableName As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of ColumnInfo)) Implements IDatabaseProvider.GetTableColumnsAsync
            Dim columns As New List(Of ColumnInfo)()
            Try
                Using conn = CreateConnection(databaseName)
                    Await conn.OpenAsync(cancellationToken).ConfigureAwait(False)

                    Dim query = "
                        SELECT 
                            c.COLUMN_NAME,
                            c.DATA_TYPE,
                            ISNULL(c.CHARACTER_MAXIMUM_LENGTH, 0) AS CHARACTER_MAXIMUM_LENGTH,
                            c.IS_NULLABLE,
                            c.COLUMN_DEFAULT,
                            CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IsPrimaryKey
                        FROM INFORMATION_SCHEMA.COLUMNS c
                        LEFT JOIN (
                            SELECT ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME
                            FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                            JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku 
                                ON tc.CONSTRAINT_TYPE = 'PRIMARY KEY' 
                                AND tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                                AND tc.TABLE_SCHEMA = ku.TABLE_SCHEMA
                        ) pk ON c.TABLE_SCHEMA = pk.TABLE_SCHEMA 
                             AND c.TABLE_NAME = pk.TABLE_NAME 
                             AND c.COLUMN_NAME = pk.COLUMN_NAME
                        WHERE c.TABLE_SCHEMA = @schema AND c.TABLE_NAME = @table
                        ORDER BY c.ORDINAL_POSITION;"

                    Using cmd = conn.CreateCommand()
                        cmd.CommandText = query
                        cmd.Parameters.AddWithValue("@schema", schemaName)
                        cmd.Parameters.AddWithValue("@table", tableName)
                        cmd.CommandTimeout = _profile.QueryTimeoutSeconds

                        Using reader = Await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(False)
                            While Await reader.ReadAsync(cancellationToken).ConfigureAwait(False)
                                Dim col As New ColumnInfo() With {
                                    .ColumnName = reader("COLUMN_NAME")?.ToString(),
                                    .DataType = reader("DATA_TYPE")?.ToString(),
                                    .MaxLength = Convert.ToInt32(reader("CHARACTER_MAXIMUM_LENGTH")),
                                    .IsNullable = reader("IS_NULLABLE")?.ToString().Equals("YES", StringComparison.OrdinalIgnoreCase),
                                    .IsPrimaryKey = (Convert.ToInt32(reader("IsPrimaryKey")) = 1),
                                    .DefaultValue = reader("COLUMN_DEFAULT")?.ToString()
                                }
                                columns.Add(col)
                            End While
                        End Using
                    End Using
                End Using
            Catch ex As Exception
                AppLogger.Error("SqlServerProvider", $"Failed to get columns for {schemaName}.{tableName}", ex)
            End Try

            Return columns
        End Function

        Public Async Function GetRunningQueriesAsync(Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of QueryInfo)) Implements IDatabaseProvider.GetRunningQueriesAsync
            Dim queries As New List(Of QueryInfo)()
            Try
                Using conn = CreateConnection()
                    Await conn.OpenAsync(cancellationToken).ConfigureAwait(False)

                    Dim query = "
                        SELECT 
                            r.session_id,
                            DB_NAME(r.database_id) AS DatabaseName,
                            s.login_name AS UserName,
                            s.host_name AS HostName,
                            s.program_name AS ProgramName,
                            r.status AS Status,
                            r.command AS Command,
                            SUBSTRING(t.text, (r.statement_start_offset/2) + 1,
                                ((CASE r.statement_end_offset
                                    WHEN -1 THEN DATALENGTH(t.text)
                                    ELSE r.statement_end_offset END 
                                    - r.statement_start_offset)/2) + 1) AS QueryText,
                            r.start_time AS StartTime,
                            r.total_elapsed_time / 1000 AS ElapsedSeconds,
                            r.cpu_time AS CpuTimeMs,
                            r.logical_reads AS LogicalReads,
                            r.writes AS Writes,
                            r.wait_type AS WaitType,
                            r.blocking_session_id AS BlockingSessionId
                        FROM sys.dm_exec_requests r
                        CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t
                        JOIN sys.dm_exec_sessions s ON r.session_id = s.session_id
                        WHERE r.session_id > 50 AND r.session_id <> @@SPID
                        ORDER BY r.total_elapsed_time DESC;"

                    Using cmd = conn.CreateCommand()
                        cmd.CommandText = query
                        cmd.CommandTimeout = _profile.QueryTimeoutSeconds
                        Using reader = Await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(False)
                            While Await reader.ReadAsync(cancellationToken).ConfigureAwait(False)
                                Dim q As New QueryInfo() With {
                                    .SessionId = Convert.ToInt32(reader("session_id")),
                                    .DatabaseName = reader("DatabaseName")?.ToString(),
                                    .UserName = reader("UserName")?.ToString(),
                                    .HostName = reader("HostName")?.ToString(),
                                    .ProgramName = reader("ProgramName")?.ToString(),
                                    .Status = reader("Status")?.ToString(),
                                    .Command = reader("Command")?.ToString(),
                                    .QueryText = reader("QueryText")?.ToString(),
                                    .StartTime = Convert.ToDateTime(reader("StartTime")),
                                    .DurationSeconds = Convert.ToInt64(reader("ElapsedSeconds")),
                                    .CpuTimeMs = Convert.ToInt64(reader("CpuTimeMs")),
                                    .LogicalReads = Convert.ToInt64(reader("LogicalReads")),
                                    .Writes = Convert.ToInt64(reader("Writes")),
                                    .WaitType = reader("WaitType")?.ToString(),
                                    .BlockingSessionId = Convert.ToInt32(reader("BlockingSessionId"))
                                }
                                queries.Add(q)
                            End While
                        End Using
                    End Using
                End Using
            Catch ex As Exception
                AppLogger.Error("SqlServerProvider", $"Failed to fetch running queries for {_profile.Name}", ex)
            End Try

            Return queries
        End Function

        Public Async Function GetSessionsAsync(Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of SessionInfo)) Implements IDatabaseProvider.GetSessionsAsync
            Dim list As New List(Of SessionInfo)()
            Try
                Using conn = CreateConnection()
                    Await conn.OpenAsync(cancellationToken).ConfigureAwait(False)

                    Dim query = "
                        SELECT 
                            s.session_id,
                            s.login_name,
                            s.host_name,
                            s.program_name,
                            s.status,
                            DB_NAME(s.database_id) AS DatabaseName,
                            s.login_time,
                            s.last_request_start_time,
                            s.cpu_time,
                            s.memory_usage * 8 AS MemoryKB
                        FROM sys.dm_exec_sessions s
                        WHERE s.is_user_process = 1
                        ORDER BY s.session_id;"

                    Using cmd = conn.CreateCommand()
                        cmd.CommandText = query
                        cmd.CommandTimeout = _profile.QueryTimeoutSeconds
                        Using reader = Await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(False)
                            While Await reader.ReadAsync(cancellationToken).ConfigureAwait(False)
                                Dim item As New SessionInfo() With {
                                    .SessionId = Convert.ToInt32(reader("session_id")),
                                    .LoginName = reader("login_name")?.ToString(),
                                    .HostName = reader("host_name")?.ToString(),
                                    .ProgramName = reader("program_name")?.ToString(),
                                    .Status = reader("status")?.ToString(),
                                    .DatabaseName = reader("DatabaseName")?.ToString(),
                                    .LoginTime = Convert.ToDateTime(reader("login_time")),
                                    .LastRequestTime = Convert.ToDateTime(reader("last_request_start_time")),
                                    .CpuTime = Convert.ToInt64(reader("cpu_time")),
                                    .MemoryUsageKB = Convert.ToInt64(reader("MemoryKB"))
                                }
                                list.Add(item)
                            End While
                        End Using
                    End Using
                End Using
            Catch ex As Exception
                AppLogger.Error("SqlServerProvider", $"Failed to fetch sessions for {_profile.Name}", ex)
            End Try

            Return list
        End Function

        Public Async Function GetBlockingInfoAsync(Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of BlockingInfo)) Implements IDatabaseProvider.GetBlockingInfoAsync
            Dim list As New List(Of BlockingInfo)()
            Try
                Using conn = CreateConnection()
                    Await conn.OpenAsync(cancellationToken).ConfigureAwait(False)

                    Dim query = "
                        SELECT 
                            r.blocking_session_id AS BlockerSessionId,
                            r.session_id AS BlockedSessionId,
                            DB_NAME(r.database_id) AS DatabaseName,
                            blocker.login_name AS BlockerUser,
                            blocked.login_name AS BlockedUser,
                            r.wait_time AS WaitDurationMs,
                            r.wait_type AS WaitType,
                            r.wait_resource AS WaitResource,
                            ISNULL(t_blocker.text, '') AS BlockerQuery,
                            ISNULL(t_blocked.text, '') AS BlockedQuery
                        FROM sys.dm_exec_requests r
                        JOIN sys.dm_exec_sessions blocked ON r.session_id = blocked.session_id
                        LEFT JOIN sys.dm_exec_sessions blocker ON r.blocking_session_id = blocker.session_id
                        LEFT JOIN sys.dm_exec_requests req_blocker ON r.blocking_session_id = req_blocker.session_id
                        OUTER APPLY sys.dm_exec_sql_text(req_blocker.sql_handle) t_blocker
                        OUTER APPLY sys.dm_exec_sql_text(r.sql_handle) t_blocked
                        WHERE r.blocking_session_id <> 0;"

                    Using cmd = conn.CreateCommand()
                        cmd.CommandText = query
                        cmd.CommandTimeout = _profile.QueryTimeoutSeconds
                        Using reader = Await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(False)
                            While Await reader.ReadAsync(cancellationToken).ConfigureAwait(False)
                                Dim b As New BlockingInfo() With {
                                    .BlockerSessionId = Convert.ToInt32(reader("BlockerSessionId")),
                                    .BlockedSessionId = Convert.ToInt32(reader("BlockedSessionId")),
                                    .DatabaseName = reader("DatabaseName")?.ToString(),
                                    .BlockerUser = reader("BlockerUser")?.ToString(),
                                    .BlockedUser = reader("BlockedUser")?.ToString(),
                                    .WaitDurationMs = Convert.ToInt64(reader("WaitDurationMs")),
                                    .WaitType = reader("WaitType")?.ToString(),
                                    .ResourceDescription = reader("WaitResource")?.ToString(),
                                    .BlockerQuery = reader("BlockerQuery")?.ToString(),
                                    .BlockedQuery = reader("BlockedQuery")?.ToString()
                                }
                                list.Add(b)
                            End While
                        End Using
                    End Using
                End Using
            Catch ex As Exception
                AppLogger.Error("SqlServerProvider", $"Failed to fetch blocking info for {_profile.Name}", ex)
            End Try

            Return list
        End Function

        Public Async Function GetStorageInfoAsync(Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of StorageInfo)) Implements IDatabaseProvider.GetStorageInfoAsync
            Dim list As New List(Of StorageInfo)()
            Try
                Using conn = CreateConnection()
                    Await conn.OpenAsync(cancellationToken).ConfigureAwait(False)

                    Dim query = "
                        SELECT 
                            DB_NAME(database_id) AS DatabaseName,
                            name AS FileName,
                            type_desc AS FileType,
                            physical_name AS PhysicalPath,
                            CAST(size AS bigint) * 8 / 1024 AS TotalSizeMB,
                            CASE WHEN is_percent_growth = 1 
                                THEN CAST(growth AS varchar) + '%' 
                                ELSE CAST(growth * 8 / 1024 AS varchar) + ' MB' 
                            END AS AutoGrowth
                        FROM sys.master_files
                        ORDER BY database_id, type;"

                    Using cmd = conn.CreateCommand()
                        cmd.CommandText = query
                        cmd.CommandTimeout = _profile.QueryTimeoutSeconds
                        Using reader = Await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(False)
                            While Await reader.ReadAsync(cancellationToken).ConfigureAwait(False)
                                Dim totalSize = Convert.ToDouble(reader("TotalSizeMB"))
                                Dim s As New StorageInfo() With {
                                    .DatabaseName = reader("DatabaseName")?.ToString(),
                                    .FileName = reader("FileName")?.ToString(),
                                    .FileType = reader("FileType")?.ToString(),
                                    .PhysicalPath = reader("PhysicalPath")?.ToString(),
                                    .TotalSizeMB = totalSize,
                                    .UsedSizeMB = totalSize * 0.75, ' Approximation if not running per-db spaceused
                                    .FreeSizeMB = totalSize * 0.25,
                                    .AutoGrowth = reader("AutoGrowth")?.ToString()
                                }
                                list.Add(s)
                            End While
                        End Using
                    End Using
                End Using
            Catch ex As Exception
                AppLogger.Error("SqlServerProvider", $"Failed to fetch storage info for {_profile.Name}", ex)
            End Try

            Return list
        End Function

        Public Async Function GetBackupInfoAsync(Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of BackupInfo)) Implements IDatabaseProvider.GetBackupInfoAsync
            Dim list As New List(Of BackupInfo)()
            Try
                Using conn = CreateConnection()
                    Await conn.OpenAsync(cancellationToken).ConfigureAwait(False)

                    Dim query = "
                        SELECT 
                            bs.database_name AS DatabaseName,
                            CASE bs.type 
                                WHEN 'D' THEN 'Full'
                                WHEN 'I' THEN 'Differential'
                                WHEN 'L' THEN 'Log'
                                ELSE bs.type 
                            END AS BackupType,
                            bs.backup_start_date AS StartDate,
                            bs.backup_finish_date AS FinishDate,
                            ROUND(bs.backup_size / 1048576, 2) AS BackupSizeMB,
                            ROUND(bs.compressed_backup_size / 1048576, 2) AS CompressedSizeMB,
                            DATEDIFF(second, bs.backup_start_date, bs.backup_finish_date) AS DurationSeconds
                        FROM msdb.dbo.backupset bs
                        WHERE bs.backup_set_id IN (
                            SELECT MAX(backup_set_id) 
                            FROM msdb.dbo.backupset 
                            GROUP BY database_name, type
                        )
                        ORDER BY bs.database_name, bs.type;"

                    Using cmd = conn.CreateCommand()
                        cmd.CommandText = query
                        cmd.CommandTimeout = _profile.QueryTimeoutSeconds
                        Using reader = Await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(False)
                            While Await reader.ReadAsync(cancellationToken).ConfigureAwait(False)
                                Dim b As New BackupInfo() With {
                                    .DatabaseName = reader("DatabaseName")?.ToString(),
                                    .BackupType = reader("BackupType")?.ToString(),
                                    .StartDate = Convert.ToDateTime(reader("StartDate")),
                                    .FinishDate = Convert.ToDateTime(reader("FinishDate")),
                                    .BackupSizeMB = Convert.ToDouble(reader("BackupSizeMB")),
                                    .CompressedSizeMB = Convert.ToDouble(reader("CompressedSizeMB")),
                                    .DurationSeconds = Convert.ToInt64(reader("DurationSeconds")),
                                    .Status = "SUCCESS"
                                }
                                list.Add(b)
                            End While
                        End Using
                    End Using
                End Using
            Catch ex As Exception
                AppLogger.Error("SqlServerProvider", $"Failed to fetch backup history for {_profile.Name}", ex)
            End Try

            Return list
        End Function

        Public Async Function ExecuteQueryAsync(sql As String, Optional databaseName As String = "", Optional maxRows As Integer = 1000, Optional timeoutSeconds As Integer = 30, Optional cancellationToken As CancellationToken = Nothing) As Task(Of (ResultTable As DataTable, ElapsedMs As Long, RowsAffected As Integer, Message As String)) Implements IDatabaseProvider.ExecuteQueryAsync
            Dim table As New DataTable()
            Dim sw = Stopwatch.StartNew()
            Dim rowsAffected = 0
            Dim message = ""

            Try
                Using conn = CreateConnection(databaseName)
                    Await conn.OpenAsync(cancellationToken).ConfigureAwait(False)

                    Using cmd = conn.CreateCommand()
                        cmd.CommandText = sql
                        cmd.CommandTimeout = If(timeoutSeconds > 0, timeoutSeconds, _profile.QueryTimeoutSeconds)

                        Using reader = Await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(False)
                            table.Load(reader)
                            rowsAffected = reader.RecordsAffected
                        End Using
                    End Using
                End Using

                sw.Stop()
                message = $"Query executed successfully in {sw.ElapsedMilliseconds} ms. ({table.Rows.Count} rows returned)"
                Return (table, sw.ElapsedMilliseconds, rowsAffected, message)
            Catch ex As Exception
                sw.Stop()
                AppLogger.Warn("SqlServerProvider", $"Query execution failed: {ex.Message}")
                Return (table, sw.ElapsedMilliseconds, -1, $"Error: {ex.Message}")
            End Try
        End Function

        Public Async Function KillSessionAsync(sessionId As Integer, Optional cancellationToken As CancellationToken = Nothing) As Task(Of (Success As Boolean, Message As String)) Implements IDatabaseProvider.KillSessionAsync
            If sessionId <= 50 Then
                Return (False, "Cannot kill system session (SPID <= 50).")
            End If

            Try
                Using conn = CreateConnection()
                    Await conn.OpenAsync(cancellationToken).ConfigureAwait(False)

                    Using cmd = conn.CreateCommand()
                        cmd.CommandText = $"KILL {sessionId};"
                        cmd.CommandTimeout = 10
                        Await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(False)
                    End Using
                End Using

                AppLogger.Info("SqlServerProvider", $"Killed session SPID {sessionId} on {_profile.Name}.")
                Return (True, $"Session {sessionId} has been terminated.")
            Catch ex As Exception
                AppLogger.Error("SqlServerProvider", $"Failed to kill session SPID {sessionId}", ex)
                Return (False, $"Failed to kill session {sessionId}: {ex.Message}")
            End Try
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            _disposed = True
        End Sub
    End Class
End Namespace
