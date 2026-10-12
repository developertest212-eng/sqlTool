Imports System.Data
Imports System.Diagnostics
Imports System.Threading
Imports System.Threading.Tasks
Imports MySqlConnector
Imports SQLCommandCenter.Logging
Imports SQLCommandCenter.Models

Namespace Data
    Public Class MySqlProvider
        Implements IDatabaseProvider

        Private ReadOnly _profile As ServerProfile
        Private _disposed As Boolean = False
        Private _lastQuestions As Long = -1
        Private _lastQuestionsTime As DateTime = DateTime.MinValue

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
                Return DatabaseEngine.MySql
            End Get
        End Property

        Private Function CreateConnection(Optional databaseName As String = "") As MySqlConnection
            Dim connStr = _profile.BuildConnectionString(databaseName)
            Return New MySqlConnection(connStr)
        End Function

        Public Async Function TestConnectionAsync(Optional cancellationToken As CancellationToken = Nothing) As Task(Of (Success As Boolean, LatencyMs As Long, Version As String, ErrorMessage As String)) Implements IDatabaseProvider.TestConnectionAsync
            Dim sw = Stopwatch.StartNew()
            Try
                Using conn = CreateConnection()
                    Await conn.OpenAsync(cancellationToken).ConfigureAwait(False)
                    sw.Stop()

                    Dim version = ""
                    Using cmd = conn.CreateCommand()
                        cmd.CommandText = "SELECT VERSION() AS Ver, @@version_comment AS Comment, @@hostname AS Host;"
                        cmd.CommandTimeout = _profile.QueryTimeoutSeconds
                        Using reader = Await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(False)
                            If Await reader.ReadAsync(cancellationToken).ConfigureAwait(False) Then
                                Dim ver = reader("Ver")?.ToString()
                                Dim comment = reader("Comment")?.ToString()
                                version = $"MySQL {ver} ({comment})"
                            End If
                        End Using
                    End Using

                    Return (True, sw.ElapsedMilliseconds, version, String.Empty)
                End Using
            Catch ex As Exception
                sw.Stop()
                AppLogger.Warn("MySqlProvider", $"Connection test failed for {_profile.Name}: {ex.Message}")
                Return (False, sw.ElapsedMilliseconds, String.Empty, ex.Message)
            End Try
        End Function

        Public Async Function GetServerStatusAsync(Optional cancellationToken As CancellationToken = Nothing) As Task(Of ServerStatus) Implements IDatabaseProvider.GetServerStatusAsync
            Dim status As New ServerStatus() With {
                .ServerId = _profile.Id,
                .ServerName = _profile.Name,
                .Engine = DatabaseEngine.MySql,
                .LastCheckTime = DateTime.Now
            }

            Dim sw = Stopwatch.StartNew()
            Try
                Using conn = CreateConnection()
                    Await conn.OpenAsync(cancellationToken).ConfigureAwait(False)
                    sw.Stop()
                    status.LatencyMs = sw.ElapsedMilliseconds
                    status.IsOnline = True

                    ' Server info
                    Using cmd = conn.CreateCommand()
                        cmd.CommandText = "SELECT VERSION() AS Ver, @@version_comment AS Comment, @@hostname AS Host;"
                        Using reader = Await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(False)
                            If Await reader.ReadAsync(cancellationToken).ConfigureAwait(False) Then
                                status.Version = $"MySQL {reader("Ver")}"
                                status.Edition = reader("Comment")?.ToString()
                                status.HostPlatform = reader("Host")?.ToString()
                            End If
                        End Using
                    End Using

                    ' Uptime, Threads, Queries
                    Using cmd = conn.CreateCommand()
                        cmd.CommandText = "SHOW GLOBAL STATUS WHERE Variable_name IN ('Uptime', 'Threads_connected', 'Threads_running');"
                        Using reader = Await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(False)
                            While Await reader.ReadAsync(cancellationToken).ConfigureAwait(False)
                                Dim name = reader.GetString(0)
                                Dim val = reader.GetString(1)
                                Select Case name
                                    Case "Uptime"
                                        Dim sec As Long = 0
                                        If Long.TryParse(val, sec) Then
                                            status.Uptime = TimeSpan.FromSeconds(sec)
                                        End If
                                    Case "Threads_connected"
                                        Dim connCount As Integer = 0
                                        If Integer.TryParse(val, connCount) Then
                                            status.ActiveConnections = connCount
                                        End If
                                    Case "Threads_running"
                                        Dim qCount As Integer = 0
                                        If Integer.TryParse(val, qCount) Then
                                            status.RunningQueries = qCount
                                        End If
                                End Select
                            End While
                        End Using
                    End Using

                    ' Database count
                    Using cmd = conn.CreateCommand()
                        cmd.CommandText = "SELECT COUNT(*) FROM information_schema.SCHEMATA;"
                        Dim dbCount = Await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(False)
                        If dbCount IsNot Nothing AndAlso Not IsDBNull(dbCount) Then
                            status.DatabaseCount = Convert.ToInt32(dbCount)
                        End If
                    End Using

                    status.HealthStatus = If(status.LatencyMs > 500, ServerHealthStatus.Warning, ServerHealthStatus.Healthy)
                End Using
            Catch ex As Exception
                sw.Stop()
                status.IsOnline = False
                status.HealthStatus = ServerHealthStatus.Offline
                status.ErrorMessage = ex.Message
                status.LatencyMs = sw.ElapsedMilliseconds
                AppLogger.Error("MySqlProvider", $"Failed to get status for {_profile.Name}: {ex.Message}")
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

                    ' Read status variables
                    Dim statusQuery = "SHOW GLOBAL STATUS WHERE Variable_name IN (
                        'Threads_connected', 'Threads_running', 'Questions', 'Queries', 
                        'Innodb_buffer_pool_reads', 'Innodb_buffer_pool_read_requests',
                        'Bytes_received', 'Bytes_sent');"

                    Dim currentQuestions As Long = -1
                    Dim bufferPoolReads As Long = 0
                    Dim bufferPoolRequests As Long = 0

                    Using cmd = conn.CreateCommand()
                        cmd.CommandText = statusQuery
                        cmd.CommandTimeout = 5
                        Using reader = Await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(False)
                            While Await reader.ReadAsync(cancellationToken).ConfigureAwait(False)
                                Dim name = reader.GetString(0)
                                Dim val = reader.GetString(1)

                                Select Case name
                                    Case "Threads_connected"
                                        Integer.TryParse(val, metrics.ActiveConnections)
                                    Case "Threads_running"
                                        Integer.TryParse(val, metrics.RunningQueries)
                                    Case "Questions"
                                        Long.TryParse(val, currentQuestions)
                                    Case "Innodb_buffer_pool_reads"
                                        Long.TryParse(val, bufferPoolReads)
                                    Case "Innodb_buffer_pool_read_requests"
                                        Long.TryParse(val, bufferPoolRequests)
                                End Select
                            End While
                        End Using
                    End Using

                    ' Calculate QPS
                    Dim now = DateTime.Now
                    If _lastQuestions >= 0 AndAlso currentQuestions >= _lastQuestions AndAlso _lastQuestionsTime > DateTime.MinValue Then
                        Dim elapsedSec = (now - _lastQuestionsTime).TotalSeconds
                        If elapsedSec > 0 Then
                            metrics.QueriesPerSecond = Math.Round((currentQuestions - _lastQuestions) / elapsedSec, 1)
                        End If
                    End If
                    _lastQuestions = currentQuestions
                    _lastQuestionsTime = now

                    ' Buffer cache hit ratio
                    If bufferPoolRequests > 0 Then
                        Dim ratio = (1.0 - (CDbl(bufferPoolReads) / CDbl(bufferPoolRequests))) * 100.0
                        metrics.BufferCacheHitRatio = Math.Round(Math.Max(0.0, Math.Min(100.0, ratio)), 2)
                    Else
                        metrics.BufferCacheHitRatio = 100.0
                    End If

                    ' Read max_connections variable
                    Using cmd = conn.CreateCommand()
                        cmd.CommandText = "SHOW VARIABLES LIKE 'max_connections';"
                        cmd.CommandTimeout = 5
                        Using reader = Await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(False)
                            If Await reader.ReadAsync(cancellationToken).ConfigureAwait(False) Then
                                Dim maxVal As Integer = 0
                                If Integer.TryParse(reader.GetString(1), maxVal) Then
                                    metrics.MaxConnections = maxVal
                                End If
                            End If
                        End Using
                    End Using

                    ' Calculate Total Database Size in MB
                    Dim sizeQuery = "SELECT IFNULL(SUM(DATA_LENGTH + INDEX_LENGTH) / 1048576, 0) FROM information_schema.TABLES;"
                    Using cmd = conn.CreateCommand()
                        cmd.CommandText = sizeQuery
                        cmd.CommandTimeout = 10
                        Dim totalMb = Await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(False)
                        If totalMb IsNot Nothing AndAlso Not IsDBNull(totalMb) Then
                            metrics.DatabaseSizeMB = Convert.ToDouble(totalMb)
                        End If
                    End Using

                    ' Approximation of CPU & Memory load for MySQL from process list
                    If metrics.MaxConnections > 0 AndAlso metrics.ActiveConnections > 0 Then
                        metrics.MemoryPercent = Math.Round((CDbl(metrics.ActiveConnections) / CDbl(metrics.MaxConnections)) * 100.0, 1)
                    End If
                    metrics.CpuPercent = Math.Round(Math.Min(100.0, metrics.RunningQueries * 15.0), 1)

                End Using
            Catch ex As Exception
                AppLogger.Warn("MySqlProvider", $"Failed to retrieve performance metrics for {_profile.Name}: {ex.Message}")
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
                            s.SCHEMA_NAME AS DatabaseName,
                            s.DEFAULT_COLLATION_NAME AS CollationName,
                            IFNULL(SUM(t.DATA_LENGTH) / 1048576, 0) AS DataSizeMB,
                            IFNULL(SUM(t.INDEX_LENGTH) / 1048576, 0) AS LogSizeMB,
                            IFNULL(SUM(t.DATA_LENGTH + t.INDEX_LENGTH) / 1048576, 0) AS TotalSizeMB,
                            COUNT(t.TABLE_NAME) AS TableCount
                        FROM information_schema.SCHEMATA s
                        LEFT JOIN information_schema.TABLES t ON s.SCHEMA_NAME = t.TABLE_SCHEMA
                        GROUP BY s.SCHEMA_NAME, s.DEFAULT_COLLATION_NAME
                        ORDER BY s.SCHEMA_NAME;"

                    Using cmd = conn.CreateCommand()
                        cmd.CommandText = query
                        cmd.CommandTimeout = _profile.QueryTimeoutSeconds
                        Using reader = Await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(False)
                            While Await reader.ReadAsync(cancellationToken).ConfigureAwait(False)
                                Dim info As New DatabaseInfo() With {
                                    .Name = reader("DatabaseName")?.ToString(),
                                    .Status = "ONLINE",
                                    .RecoveryModel = "InnoDB",
                                    .Collation = reader("CollationName")?.ToString(),
                                    .CompatibilityLevel = "MySQL",
                                    .DataSizeMB = Convert.ToDouble(reader("DataSizeMB")),
                                    .LogSizeMB = Convert.ToDouble(reader("LogSizeMB")),
                                    .TotalSizeMB = Convert.ToDouble(reader("TotalSizeMB")),
                                    .TableCount = Convert.ToInt32(reader("TableCount"))
                                }
                                list.Add(info)
                            End While
                        End Using
                    End Using
                End Using
            Catch ex As Exception
                AppLogger.Error("MySqlProvider", $"Failed to fetch databases for {_profile.Name}", ex)
            End Try

            Return list
        End Function

        Public Async Function GetDatabaseObjectsAsync(databaseName As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DatabaseObjectInfo)) Implements IDatabaseProvider.GetDatabaseObjectsAsync
            Dim list As New List(Of DatabaseObjectInfo)()
            Try
                Using conn = CreateConnection()
                    Await conn.OpenAsync(cancellationToken).ConfigureAwait(False)

                    Dim query = "
                        SELECT 
                            TABLE_SCHEMA,
                            TABLE_NAME,
                            TABLE_TYPE,
                            IFNULL(TABLE_ROWS, 0) AS Row_Count,
                            IFNULL((DATA_LENGTH + INDEX_LENGTH) / 1024, 0) AS TotalSizeKB,
                            CREATE_TIME,
                            UPDATE_TIME
                        FROM information_schema.TABLES
                        WHERE TABLE_SCHEMA = @db
                        ORDER BY TABLE_NAME;"

                    Using cmd = conn.CreateCommand()
                        cmd.CommandText = query
                        cmd.Parameters.AddWithValue("@db", databaseName)
                        cmd.CommandTimeout = _profile.QueryTimeoutSeconds

                        Using reader = Await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(False)
                            While Await reader.ReadAsync(cancellationToken).ConfigureAwait(False)
                                Dim tableType = reader("TABLE_TYPE")?.ToString()
                                Dim isView = tableType.Equals("VIEW", StringComparison.OrdinalIgnoreCase)

                                Dim createTime = DateTime.MinValue
                                If Not IsDBNull(reader("CREATE_TIME")) Then
                                    DateTime.TryParse(reader("CREATE_TIME").ToString(), createTime)
                                End If

                                Dim updateTime = DateTime.MinValue
                                If Not IsDBNull(reader("UPDATE_TIME")) Then
                                    DateTime.TryParse(reader("UPDATE_TIME").ToString(), updateTime)
                                End If

                                Dim obj As New DatabaseObjectInfo() With {
                                    .DatabaseName = databaseName,
                                    .SchemaName = reader("TABLE_SCHEMA")?.ToString(),
                                    .Name = reader("TABLE_NAME")?.ToString(),
                                    .ObjectType = If(isView, DatabaseObjectType.View, DatabaseObjectType.Table),
                                    .RowCount = Convert.ToInt64(reader("Row_Count")),
                                    .TotalSizeKB = Convert.ToInt64(reader("TotalSizeKB")),
                                    .CreateDate = createTime,
                                    .ModifyDate = updateTime
                                }
                                list.Add(obj)
                            End While
                        End Using
                    End Using
                End Using
            Catch ex As Exception
                AppLogger.Error("MySqlProvider", $"Failed to get objects for database {databaseName}", ex)
            End Try

            Return list
        End Function

        Public Async Function GetTableColumnsAsync(databaseName As String, schemaName As String, tableName As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of ColumnInfo)) Implements IDatabaseProvider.GetTableColumnsAsync
            Dim columns As New List(Of ColumnInfo)()
            Try
                Using conn = CreateConnection()
                    Await conn.OpenAsync(cancellationToken).ConfigureAwait(False)

                    Dim query = "
                        SELECT 
                            COLUMN_NAME,
                            DATA_TYPE,
                            IFNULL(CHARACTER_MAXIMUM_LENGTH, 0) AS CHARACTER_MAXIMUM_LENGTH,
                            IS_NULLABLE,
                            COLUMN_KEY,
                            COLUMN_DEFAULT,
                            EXTRA
                        FROM information_schema.COLUMNS
                        WHERE TABLE_SCHEMA = @db AND TABLE_NAME = @table
                        ORDER BY ORDINAL_POSITION;"

                    Using cmd = conn.CreateCommand()
                        cmd.CommandText = query
                        cmd.Parameters.AddWithValue("@db", databaseName)
                        cmd.Parameters.AddWithValue("@table", tableName)
                        cmd.CommandTimeout = _profile.QueryTimeoutSeconds

                        Using reader = Await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(False)
                            While Await reader.ReadAsync(cancellationToken).ConfigureAwait(False)
                                Dim colKey = reader("COLUMN_KEY")?.ToString()
                                Dim extra = reader("EXTRA")?.ToString()
                                Dim col As New ColumnInfo() With {
                                    .ColumnName = reader("COLUMN_NAME")?.ToString(),
                                    .DataType = reader("DATA_TYPE")?.ToString(),
                                    .MaxLength = Convert.ToInt32(reader("CHARACTER_MAXIMUM_LENGTH")),
                                    .IsNullable = reader("IS_NULLABLE")?.ToString().Equals("YES", StringComparison.OrdinalIgnoreCase),
                                    .IsPrimaryKey = colKey.Equals("PRI", StringComparison.OrdinalIgnoreCase),
                                    .IsIdentity = extra.Contains("auto_increment", StringComparison.OrdinalIgnoreCase),
                                    .DefaultValue = reader("COLUMN_DEFAULT")?.ToString()
                                }
                                columns.Add(col)
                            End While
                        End Using
                    End Using
                End Using
            Catch ex As Exception
                AppLogger.Error("MySqlProvider", $"Failed to get columns for {databaseName}.{tableName}", ex)
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
                            ID,
                            IFNULL(USER, '') AS UserName,
                            IFNULL(HOST, '') AS HostName,
                            IFNULL(DB, '') AS DatabaseName,
                            IFNULL(COMMAND, '') AS Command,
                            IFNULL(TIME, 0) AS DurationSec,
                            IFNULL(STATE, '') AS Status,
                            IFNULL(INFO, '') AS QueryText
                        FROM information_schema.PROCESSLIST
                        WHERE COMMAND <> 'Sleep' AND ID <> CONNECTION_ID()
                        ORDER BY TIME DESC;"

                    Using cmd = conn.CreateCommand()
                        cmd.CommandText = query
                        cmd.CommandTimeout = _profile.QueryTimeoutSeconds
                        Using reader = Await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(False)
                            While Await reader.ReadAsync(cancellationToken).ConfigureAwait(False)
                                Dim q As New QueryInfo() With {
                                    .SessionId = Convert.ToInt32(reader("ID")),
                                    .UserName = reader("UserName")?.ToString(),
                                    .HostName = reader("HostName")?.ToString(),
                                    .DatabaseName = reader("DatabaseName")?.ToString(),
                                    .Command = reader("Command")?.ToString(),
                                    .DurationSeconds = Convert.ToInt64(reader("DurationSec")),
                                    .Status = reader("Status")?.ToString(),
                                    .QueryText = reader("QueryText")?.ToString(),
                                    .StartTime = DateTime.Now.AddSeconds(-Convert.ToInt64(reader("DurationSec")))
                                }
                                queries.Add(q)
                            End While
                        End Using
                    End Using
                End Using
            Catch ex As Exception
                AppLogger.Error("MySqlProvider", $"Failed to fetch running queries for {_profile.Name}", ex)
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
                            ID,
                            IFNULL(USER, '') AS UserName,
                            IFNULL(HOST, '') AS HostName,
                            IFNULL(DB, '') AS DatabaseName,
                            IFNULL(COMMAND, '') AS Command,
                            IFNULL(TIME, 0) AS DurationSec,
                            IFNULL(STATE, '') AS Status
                        FROM information_schema.PROCESSLIST
                        ORDER BY ID;"

                    Using cmd = conn.CreateCommand()
                        cmd.CommandText = query
                        cmd.CommandTimeout = _profile.QueryTimeoutSeconds
                        Using reader = Await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(False)
                            While Await reader.ReadAsync(cancellationToken).ConfigureAwait(False)
                                Dim duration = Convert.ToInt64(reader("DurationSec"))
                                Dim s As New SessionInfo() With {
                                    .SessionId = Convert.ToInt32(reader("ID")),
                                    .LoginName = reader("UserName")?.ToString(),
                                    .HostName = reader("HostName")?.ToString(),
                                    .DatabaseName = reader("DatabaseName")?.ToString(),
                                    .Command = reader("Command")?.ToString(),
                                    .Status = reader("Status")?.ToString(),
                                    .LoginTime = DateTime.Now.AddSeconds(-duration),
                                    .LastRequestTime = DateTime.Now.AddSeconds(-duration)
                                }
                                list.Add(s)
                            End While
                        End Using
                    End Using
                End Using
            Catch ex As Exception
                AppLogger.Error("MySqlProvider", $"Failed to fetch sessions for {_profile.Name}", ex)
            End Try

            Return list
        End Function

        Public Async Function GetBlockingInfoAsync(Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of BlockingInfo)) Implements IDatabaseProvider.GetBlockingInfoAsync
            Dim list As New List(Of BlockingInfo)()
            Try
                Using conn = CreateConnection()
                    Await conn.OpenAsync(cancellationToken).ConfigureAwait(False)

                    ' Attempt to query sys.innodb_lock_waits if available
                    Dim query = "
                        SELECT 
                            waiting_trx_id,
                            waiting_pid AS BlockedSessionId,
                            waiting_query AS BlockedQuery,
                            blocking_trx_id,
                            blocking_pid AS BlockerSessionId,
                            blocking_query AS BlockerQuery,
                            wait_age AS WaitAge,
                            wait_started AS WaitStarted,
                            locked_table AS LockedTable,
                            locked_type AS LockType
                        FROM sys.innodb_lock_waits;"

                    Using cmd = conn.CreateCommand()
                        cmd.CommandText = query
                        cmd.CommandTimeout = 5
                        Using reader = Await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(False)
                            While Await reader.ReadAsync(cancellationToken).ConfigureAwait(False)
                                Dim b As New BlockingInfo() With {
                                    .BlockedSessionId = Convert.ToInt32(reader("BlockedSessionId")),
                                    .BlockerSessionId = Convert.ToInt32(reader("BlockerSessionId")),
                                    .BlockedQuery = reader("BlockedQuery")?.ToString(),
                                    .BlockerQuery = reader("BlockerQuery")?.ToString(),
                                    .ResourceDescription = reader("LockedTable")?.ToString(),
                                    .LockType = reader("LockType")?.ToString(),
                                    .WaitType = "InnoDB Lock"
                                }
                                list.Add(b)
                            End While
                        End Using
                    End Using
                End Using
            Catch
                ' sys schema or lock waits view may not exist or require elevated permissions in MySQL
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
                            TABLE_SCHEMA AS DatabaseName,
                            TABLE_NAME AS FileName,
                            'TABLE/INDEX' AS FileType,
                            ROUND(DATA_LENGTH / 1048576, 2) AS DataSizeMB,
                            ROUND(INDEX_LENGTH / 1048576, 2) AS IndexSizeMB,
                            ROUND((DATA_LENGTH + INDEX_LENGTH) / 1048576, 2) AS TotalSizeMB
                        FROM information_schema.TABLES
                        WHERE TABLE_SCHEMA NOT IN ('information_schema', 'performance_schema', 'mysql', 'sys')
                        ORDER BY TotalSizeMB DESC
                        LIMIT 100;"

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
                                    .PhysicalPath = "InnoDB Tablespace",
                                    .TotalSizeMB = totalSize,
                                    .UsedSizeMB = totalSize * 0.8,
                                    .FreeSizeMB = totalSize * 0.2,
                                    .AutoGrowth = "Dynamic"
                                }
                                list.Add(s)
                            End While
                        End Using
                    End Using
                End Using
            Catch ex As Exception
                AppLogger.Error("MySqlProvider", $"Failed to fetch storage info for {_profile.Name}", ex)
            End Try

            Return list
        End Function

        Public Async Function GetBackupInfoAsync(Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of BackupInfo)) Implements IDatabaseProvider.GetBackupInfoAsync
            ' MySQL does not provide native backup tracking in core schemas
            Dim list As New List(Of BackupInfo)()
            Try
                Using conn = CreateConnection()
                    Await conn.OpenAsync(cancellationToken).ConfigureAwait(False)

                    Dim cmd = conn.CreateCommand()
                    cmd.CommandText = "SHOW BINARY LOGS;"
                    Using reader = Await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(False)
                        While Await reader.ReadAsync(cancellationToken).ConfigureAwait(False)
                            Dim logName = reader.GetString(0)
                            Dim sizeBytes = reader.GetInt64(1)
                            Dim b As New BackupInfo() With {
                                .DatabaseName = "Binlog: " & logName,
                                .BackupType = "Binary Log",
                                .BackupSizeMB = Math.Round(sizeBytes / 1048576.0, 2),
                                .Status = "ACTIVE",
                                .StartDate = DateTime.Now
                            }
                            list.Add(b)
                        End While
                    End Using
                End Using
            Catch
                ' Binlogs may not be enabled, return empty list gracefully
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
                AppLogger.Warn("MySqlProvider", $"Query execution failed: {ex.Message}")
                Return (table, sw.ElapsedMilliseconds, -1, $"Error: {ex.Message}")
            End Try
        End Function

        Public Async Function KillSessionAsync(sessionId As Integer, Optional cancellationToken As CancellationToken = Nothing) As Task(Of (Success As Boolean, Message As String)) Implements IDatabaseProvider.KillSessionAsync
            If sessionId <= 0 Then
                Return (False, "Invalid thread/connection ID.")
            End If

            Try
                Using conn = CreateConnection()
                    Await conn.OpenAsync(cancellationToken).ConfigureAwait(False)

                    Using cmd = conn.CreateCommand()
                        cmd.CommandText = $"KILL CONNECTION {sessionId};"
                        cmd.CommandTimeout = 10
                        Await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(False)
                    End Using
                End Using

                AppLogger.Info("MySqlProvider", $"Killed MySQL connection ID {sessionId} on {_profile.Name}.")
                Return (True, $"Connection {sessionId} has been terminated.")
            Catch ex As Exception
                AppLogger.Error("MySqlProvider", $"Failed to kill MySQL connection ID {sessionId}", ex)
                Return (False, $"Failed to kill connection {sessionId}: {ex.Message}")
            End Try
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            _disposed = True
        End Sub
    End Class
End Namespace
