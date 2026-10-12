Imports System.Data
Imports System.Drawing
Imports System.Windows.Forms
Imports SQLCommandCenter.Data
Imports SQLCommandCenter.Models
Imports SQLCommandCenter.Services
Imports SQLCommandCenter.UI.Controls
Imports SQLCommandCenter.UI.Theme

Namespace UI.Views
    Public Class ReportsView
        Inherits UserControl

        Private ReadOnly _serverService As ServerService
        Private ReadOnly _reportService As ReportService
        Private ReadOnly _alertService As AlertService
        Private ReadOnly _monitoringService As MonitoringService

        Private _currentReportTable As DataTable

        ' Controls
        Private cmbReportType As ComboBox
        Private btnGenerate As Button
        Private btnExportCsv As Button
        Private btnExportHtml As Button
        Private gridReport As ModernDataGrid
        Private lblReportTitle As Label

        Public Sub New(serverService As ServerService, alertService As AlertService, monitoringService As MonitoringService)
            _serverService = serverService
            _alertService = alertService
            _monitoringService = monitoringService
            _reportService = New ReportService()

            InitializeComponent()

            AddHandler ThemeManager.ThemeChanged, AddressOf OnThemeChanged
        End Sub

        Private Sub InitializeComponent()
            Dock = DockStyle.Fill
            BackColor = ThemeManager.BackgroundColor
            Font = ThemeManager.BodyFont

            Dim pnlTop As New Panel() With {
                .Dock = DockStyle.Top,
                .Height = 65,
                .Padding = New Padding(16, 12, 16, 12),
                .BackColor = ThemeManager.TopBarColor
            }

            Dim lblType As New Label() With {.Text = "Report Template:", .Location = New Point(16, 18), .AutoSize = True, .ForeColor = ThemeManager.TextSecondaryColor}
            cmbReportType = New ComboBox() With {.Location = New Point(135, 15), .Size = New Size(240, 24), .DropDownStyle = ComboBoxStyle.DropDownList}
            cmbReportType.Items.AddRange({
                "Server Health & Inventory Summary",
                "Database Space & File Allocation",
                "Active Query & Workload Audit",
                "Security & System Alerts Log"
            })
            cmbReportType.SelectedIndex = 0
            ThemeManager.StyleComboBox(cmbReportType)

            btnGenerate = New Button() With {.Text = "⚡ Generate", .Location = New Point(385, 12), .Size = New Size(100, 30)}
            ThemeManager.StyleButton(btnGenerate, True)
            AddHandler btnGenerate.Click, AddressOf OnGenerateClicked

            btnExportCsv = New Button() With {.Text = "📥 Export CSV", .Location = New Point(495, 12), .Size = New Size(110, 30), .Enabled = False}
            ThemeManager.StyleButton(btnExportCsv, False)
            AddHandler btnExportCsv.Click, AddressOf OnExportCsvClicked

            btnExportHtml = New Button() With {.Text = "🌐 Export HTML", .Location = New Point(615, 12), .Size = New Size(115, 30), .Enabled = False}
            ThemeManager.StyleButton(btnExportHtml, False)
            AddHandler btnExportHtml.Click, AddressOf OnExportHtmlClicked

            pnlTop.Controls.AddRange({lblType, cmbReportType, btnGenerate, btnExportCsv, btnExportHtml})

            Dim pnlGridHost As New Panel() With {.Dock = DockStyle.Fill, .BackColor = ThemeManager.CardColor}
            lblReportTitle = New Label() With {
                .Dock = DockStyle.Top,
                .Height = 32,
                .Text = "Select a report template and click 'Generate'.",
                .Font = ThemeManager.BoldFont,
                .ForeColor = ThemeManager.AccentColor,
                .Padding = New Padding(12, 8, 0, 0)
            }
            gridReport = New ModernDataGrid() With {.Dock = DockStyle.Fill}
            pnlGridHost.Controls.AddRange({gridReport, lblReportTitle})

            Controls.AddRange({pnlGridHost, pnlTop})
        End Sub

        Private Async Sub OnGenerateClicked(sender As Object, e As EventArgs)
            Dim server = _serverService.SelectedServer
            If server Is Nothing Then
                MessageBox.Show("Please select an active server first.", "No Server", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            btnGenerate.Enabled = False
            btnGenerate.Text = "Generating..."

            Try
                Dim reportIndex = cmbReportType.SelectedIndex
                Dim dt As New DataTable()

                Select Case reportIndex
                    Case 0 ' Server Health & Inventory
                        lblReportTitle.Text = $"Server Health & Inventory Summary — {server.Name} ({server.Engine})"
                        dt.Columns.Add("Parameter", GetType(String))
                        dt.Columns.Add("Current Value", GetType(String))

                        Dim st = _monitoringService.GetStatus(server.Id)
                        dt.Rows.Add("Server Name", server.Name)
                        dt.Rows.Add("Database Engine", server.Engine.ToString())
                        dt.Rows.Add("Host & Port", $"{server.Host}:{server.Port}")
                        dt.Rows.Add("Engine Version", st.Version)
                        dt.Rows.Add("Edition", st.Edition)
                        dt.Rows.Add("Uptime", st.UptimeDisplay)
                        dt.Rows.Add("Online Status", If(st.IsOnline, "Online", "Offline"))
                        dt.Rows.Add("Response Latency", $"{st.LatencyMs} ms")
                        dt.Rows.Add("Databases Count", st.DatabaseCount.ToString())
                        dt.Rows.Add("Active Connections", st.ActiveConnections.ToString())

                    Case 1 ' Storage & Space
                        lblReportTitle.Text = $"Database Space & File Allocation Report — {server.Name}"
                        Using provider = DatabaseProviderFactory.CreateProvider(server)
                            Dim files = Await provider.GetStorageInfoAsync()
                            dt.Columns.Add("Database", GetType(String))
                            dt.Columns.Add("File Name", GetType(String))
                            dt.Columns.Add("Type", GetType(String))
                            dt.Columns.Add("Total Size (MB)", GetType(Double))
                            dt.Columns.Add("Used %", GetType(Double))
                            dt.Columns.Add("Physical Path", GetType(String))
                            For Each f In files
                                dt.Rows.Add(f.DatabaseName, f.FileName, f.FileType, f.TotalSizeMB, f.UsedPercent, f.PhysicalPath)
                            Next
                        End Using

                    Case 2 ' Active Queries
                        lblReportTitle.Text = $"Active Query & Workload Audit Report — {server.Name}"
                        Using provider = DatabaseProviderFactory.CreateProvider(server)
                            Dim queries = Await provider.GetRunningQueriesAsync()
                            dt.Columns.Add("Session ID", GetType(Integer))
                            dt.Columns.Add("User", GetType(String))
                            dt.Columns.Add("Database", GetType(String))
                            dt.Columns.Add("Duration (s)", GetType(Long))
                            dt.Columns.Add("Wait Type", GetType(String))
                            dt.Columns.Add("Query Text", GetType(String))
                            For Each q In queries
                                dt.Rows.Add(q.SessionId, q.UserName, q.DatabaseName, q.DurationSeconds, q.WaitType, q.QueryText)
                            Next
                        End Using

                    Case 3 ' Alerts Log
                        lblReportTitle.Text = $"Security & System Alerts Log — {server.Name}"
                        Dim alerts = _alertService.GetAllAlerts().Where(Function(a) a.ServerId = server.Id OrElse String.IsNullOrEmpty(a.ServerId)).ToList()
                        dt.Columns.Add("Severity", GetType(String))
                        dt.Columns.Add("Timestamp", GetType(DateTime))
                        dt.Columns.Add("Title", GetType(String))
                        dt.Columns.Add("Message", GetType(String))
                        dt.Columns.Add("Status", GetType(String))
                        For Each a In alerts
                            dt.Rows.Add(a.SeverityDisplay, a.Timestamp, a.Title, a.Message, a.Status.ToString())
                        Next
                End Select

                _currentReportTable = dt
                gridReport.DataSource = dt
                btnExportCsv.Enabled = (dt.Rows.Count > 0)
                btnExportHtml.Enabled = (dt.Rows.Count > 0)
            Catch ex As Exception
                MessageBox.Show($"Report generation failed: {ex.Message}", "Report Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                btnGenerate.Enabled = True
                btnGenerate.Text = "⚡ Generate"
            End Try
        End Sub

        Private Sub OnExportCsvClicked(sender As Object, e As EventArgs)
            If _currentReportTable Is Nothing OrElse _currentReportTable.Rows.Count = 0 Then Return

            Using sfd As New SaveFileDialog()
                sfd.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*"
                sfd.FileName = $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                If sfd.ShowDialog(FindForm()) = DialogResult.OK Then
                    If _reportService.ExportToCsv(_currentReportTable, sfd.FileName) Then
                        MessageBox.Show("CSV report exported successfully!", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    End If
                End If
            End Using
        End Sub

        Private Sub OnExportHtmlClicked(sender As Object, e As EventArgs)
            If _currentReportTable Is Nothing OrElse _currentReportTable.Rows.Count = 0 Then Return

            Dim server = _serverService.SelectedServer
            Dim serverName = If(server IsNot Nothing, server.Name, "Server")
            Dim engine = If(server IsNot Nothing, server.Engine.ToString(), "RDBMS")

            Using sfd As New SaveFileDialog()
                sfd.Filter = "HTML Files (*.html)|*.html|All Files (*.*)|*.*"
                sfd.FileName = $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.html"
                If sfd.ShowDialog(FindForm()) = DialogResult.OK Then
                    Dim summary As New Dictionary(Of String, String) From {
                        {"Total Records", _currentReportTable.Rows.Count.ToString()},
                        {"Server", serverName},
                        {"Engine", engine}
                    }
                    If _reportService.ExportToHtmlReport(lblReportTitle.Text, serverName, engine, _currentReportTable, summary, sfd.FileName) Then
                        MessageBox.Show("HTML report exported successfully! You can open it in your browser or print to PDF.", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    End If
                End If
            End Using
        End Sub

        Private Sub OnThemeChanged(sender As Object, e As EventArgs)
            BackColor = ThemeManager.BackgroundColor
            ThemeManager.StyleComboBox(cmbReportType)
            ThemeManager.StyleButton(btnGenerate, True)
            ThemeManager.StyleButton(btnExportCsv, False)
            ThemeManager.StyleButton(btnExportHtml, False)
        End Sub
    End Class
End Namespace
