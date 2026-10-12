Imports System.Data
Imports System.IO
Imports System.Threading
Imports System.Windows.Forms
Imports SQLCommandCenter.Configuration
Imports SQLCommandCenter.Logging
Imports SQLCommandCenter.Models
Imports SQLCommandCenter.Security
Imports SQLCommandCenter.Services
Imports SQLCommandCenter.UI.Forms

Friend Module Program

    <STAThread()>
    Friend Sub Main(args As String())
        ' Check if automated smoke test is requested
        If args IsNot Nothing AndAlso args.Any(Function(a) a.Equals("--test", StringComparison.OrdinalIgnoreCase) OrElse a.Equals("/test", StringComparison.OrdinalIgnoreCase)) Then
            Dim exitCode = RunAutomatedSmokeTests()
            Environment.Exit(exitCode)
            Return
        End If

        Try
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2)
        Catch
            Application.SetHighDpiMode(HighDpiMode.SystemAware)
        End Try

        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)

        ' Global unhandled exception protection
        AddHandler Application.ThreadException, AddressOf OnThreadException
        AddHandler AppDomain.CurrentDomain.UnhandledException, AddressOf OnUnhandledException

        AppLogger.Info("Program", "SQL Command Center desktop application starting up.")

        Try
            Application.Run(New MainForm())
        Catch ex As Exception
            AppLogger.Error("Program", "Fatal error during Application.Run", ex)
            MessageBox.Show($"An unexpected error occurred: {ex.Message}", "SQL Command Center Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            AppLogger.Info("Program", "SQL Command Center desktop application exiting.")
        End Try
    End Sub

    Private Function RunAutomatedSmokeTests() As Integer
        Console.WriteLine("==================================================")
        Console.WriteLine("    SQL Command Center - Automated Smoke Tests    ")
        Console.WriteLine("==================================================")
        Dim passedCount = 0
        Dim failedCount = 0

        ' Test 1: DPAPI Credential Encryption
        Try
            Dim plain = "EnterpriseSecurePassword!2026"
            Dim encrypted = CredentialManager.EncryptSecret(plain)
            Dim decrypted = CredentialManager.DecryptSecret(encrypted)
            If decrypted = plain AndAlso Not String.IsNullOrEmpty(encrypted) AndAlso encrypted <> plain Then
                Console.WriteLine(" [PASS] 1. Windows DPAPI Credential Encryption / Decryption")
                passedCount += 1
            Else
                Console.WriteLine(" [FAIL] 1. DPAPI roundtrip failed.")
                failedCount += 1
            End If
        Catch ex As Exception
            Console.WriteLine($" [FAIL] 1. DPAPI Exception: {ex.Message}")
            failedCount += 1
        End Try

        ' Test 2: SQL Server Profile & Connection String
        Try
            Dim srv As New ServerProfile() With {
                .Name = "Prod-SQL-01",
                .Engine = DatabaseEngine.SqlServer,
                .Host = "sqlserver.corp.local",
                .Port = 1433,
                .AuthType = AuthenticationType.SqlAuthentication,
                .Username = "sa",
                .PlainTextPassword = "SecretPassword123!",
                .DefaultDatabase = "SalesDB",
                .Encrypt = True,
                .TrustServerCertificate = True
            }
            Dim connStr = srv.BuildConnectionString()
            If connStr.Contains("Data Source=sqlserver.corp.local") AndAlso connStr.Contains("Initial Catalog=SalesDB") AndAlso connStr.Contains("User ID=sa") Then
                Console.WriteLine(" [PASS] 2. SQL Server Connection String Builder")
                passedCount += 1
            Else
                Console.WriteLine($" [FAIL] 2. SQL Server Connection String invalid: {connStr}")
                failedCount += 1
            End If
        Catch ex As Exception
            Console.WriteLine($" [FAIL] 2. Exception: {ex.Message}")
            failedCount += 1
        End Try

        ' Test 3: MySQL Profile & Connection String
        Try
            Dim mysqlSrv As New ServerProfile() With {
                .Name = "Prod-MySQL-01",
                .Engine = DatabaseEngine.MySql,
                .Host = "mysql.corp.local",
                .Port = 3306,
                .Username = "root",
                .PlainTextPassword = "MySqlSecretPassword",
                .DefaultDatabase = "ecommerce",
                .SslMode = "Preferred"
            }
            Dim myConnStr = mysqlSrv.BuildConnectionString()
            If myConnStr.Contains("Server=mysql.corp.local") AndAlso myConnStr.Contains("Database=ecommerce") AndAlso myConnStr.Contains("User ID=root") Then
                Console.WriteLine(" [PASS] 3. MySQL Connection String Builder")
                passedCount += 1
            Else
                Console.WriteLine($" [FAIL] 3. MySQL Connection String invalid: {myConnStr}")
                failedCount += 1
            End If
        Catch ex As Exception
            Console.WriteLine($" [FAIL] 3. Exception: {ex.Message}")
            failedCount += 1
        End Try

        ' Test 4: Query Safety Analysis
        Try
            Dim qs As New QueryService()
            Dim testDrop = qs.AnalyzeDestructiveOperations("DROP TABLE Customers;")
            Dim testSelect = qs.AnalyzeDestructiveOperations("SELECT TOP 100 * FROM Orders WHERE Status = 'Active';")
            Dim testDelete = qs.AnalyzeDestructiveOperations("DELETE FROM AuditLogs WHERE CreatedDate < '2020-01-01';")
            Dim testTruncate = qs.AnalyzeDestructiveOperations("TRUNCATE TABLE TempStaging;")
            Dim testUpdate = qs.AnalyzeDestructiveOperations("UPDATE Products SET Price = Price * 1.05;")

            If testDrop.IsDestructive AndAlso Not testSelect.IsDestructive AndAlso testDelete.IsDestructive AndAlso testTruncate.IsDestructive AndAlso testUpdate.IsDestructive Then
                Console.WriteLine(" [PASS] 4. Destructive Statement Safety Detection (DROP, DELETE, TRUNCATE, ALTER, UPDATE)")
                passedCount += 1
            Else
                Console.WriteLine(" [FAIL] 4. Destructive statement detection failed.")
                failedCount += 1
            End If
        Catch ex As Exception
            Console.WriteLine($" [FAIL] 4. Exception: {ex.Message}")
            failedCount += 1
        End Try

        ' Test 5: Alert Engine Rule Evaluation
        Try
            Dim alertSvc As New AlertService()
            Dim srv As New ServerProfile() With {.Id = "srv-1", .Name = "Test-Server"}
            Dim status As New ServerStatus() With {.ServerId = "srv-1", .IsOnline = True, .LatencyMs = 25}

            ' Simulate high CPU
            Dim metricsCritical As New PerformanceMetrics() With {.ServerId = "srv-1", .CpuPercent = 95.5}
            alertSvc.EvaluateMetrics(srv, metricsCritical, status)

            Dim activeAlerts = alertSvc.GetActiveAlerts()
            If activeAlerts.Any(Function(a) a.Severity = AlertSeverity.Critical AndAlso a.Title.Contains("CPU")) Then
                Console.WriteLine(" [PASS] 5. Alert Engine Rule Evaluation & Threshold Detection")
                passedCount += 1
            Else
                Console.WriteLine(" [FAIL] 5. Alert engine did not trigger expected alert.")
                failedCount += 1
            End If
        Catch ex As Exception
            Console.WriteLine($" [FAIL] 5. Exception: {ex.Message}")
            failedCount += 1
        End Try

        ' Test 6: Report Generation & Export
        Try
            Dim reportSvc As New ReportService()
            Dim table As New DataTable()
            table.Columns.Add("Server", GetType(String))
            table.Columns.Add("CPU", GetType(String))
            table.Columns.Add("Status", GetType(String))
            table.Rows.Add("SQL01", "45%", "Healthy")
            table.Rows.Add("MYSQL01", "30%", "Healthy")

            Dim tempCsv = Path.Combine(Path.GetTempPath(), $"sqlcc_test_{Guid.NewGuid():N}.csv")
            Dim tempHtml = Path.Combine(Path.GetTempPath(), $"sqlcc_test_{Guid.NewGuid():N}.html")

            Dim csvSuccess = reportSvc.ExportToCsv(table, tempCsv)
            Dim htmlSuccess = reportSvc.ExportToHtmlReport("Fleet Health", "All Servers", "Multi-Engine", table, New Dictionary(Of String, String) From {{"Total", "2"}}, tempHtml)

            If csvSuccess AndAlso htmlSuccess AndAlso File.Exists(tempCsv) AndAlso File.Exists(tempHtml) Then
                File.Delete(tempCsv)
                File.Delete(tempHtml)
                Console.WriteLine(" [PASS] 6. Report Generation & Export (CSV & HTML)")
                passedCount += 1
            Else
                Console.WriteLine(" [FAIL] 6. Report generation failed.")
                failedCount += 1
            End If
        Catch ex As Exception
            Console.WriteLine($" [FAIL] 6. Exception: {ex.Message}")
            failedCount += 1
        End Try

        Console.WriteLine("--------------------------------------------------")
        Console.WriteLine($"Summary: {passedCount} Passed, {failedCount} Failed")
        Console.WriteLine("==================================================")

        Return If(failedCount = 0, 0, 1)
    End Function

    Private Sub OnThreadException(sender As Object, e As ThreadExceptionEventArgs)
        AppLogger.Error("Program", "Unhandled UI thread exception caught", e.Exception)
        MessageBox.Show($"An unexpected system event occurred: {e.Exception.Message}{Environment.NewLine}{Environment.NewLine}The event has been logged.", "SQL Command Center Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning)
    End Sub

    Private Sub OnUnhandledException(sender As Object, e As UnhandledExceptionEventArgs)
        Dim ex = TryCast(e.ExceptionObject, Exception)
        AppLogger.Error("Program", "Unhandled AppDomain exception caught", ex)
    End Sub

End Module
