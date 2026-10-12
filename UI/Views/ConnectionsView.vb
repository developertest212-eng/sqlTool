Imports System.Data
Imports System.Drawing
Imports System.Windows.Forms
Imports SQLCommandCenter.Data
Imports SQLCommandCenter.Models
Imports SQLCommandCenter.Services
Imports SQLCommandCenter.UI.Controls
Imports SQLCommandCenter.UI.Forms
Imports SQLCommandCenter.UI.Theme

Namespace UI.Views
    Public Class ConnectionsView
        Inherits UserControl

        Private ReadOnly _serverService As ServerService
        Private _sessions As New List(Of SessionInfo)()

        ' Controls
        Private gridConnections As ModernDataGrid
        Private txtSearch As TextBox
        Private btnRefresh As Button
        Private btnKill As Button
        Private lblSessionCount As Label

        Public Sub New(serverService As ServerService)
            _serverService = serverService

            InitializeComponent()

            AddHandler _serverService.SelectedServerChanged, Sub(s, e) LoadConnectionsAsync()
            AddHandler ThemeManager.ThemeChanged, AddressOf OnThemeChanged

            LoadConnectionsAsync()
        End Sub

        Private Sub InitializeComponent()
            Dock = DockStyle.Fill
            BackColor = ThemeManager.BackgroundColor
            Font = ThemeManager.BodyFont

            ' Top Toolbar
            Dim pnlTop As New Panel() With {
                .Dock = DockStyle.Top,
                .Height = 55,
                .Padding = New Padding(16, 12, 16, 12),
                .BackColor = ThemeManager.TopBarColor
            }

            Dim lblSearch As New Label() With {.Text = "Search:", .Location = New Point(16, 18), .AutoSize = True, .ForeColor = ThemeManager.TextSecondaryColor}
            txtSearch = New TextBox() With {.Location = New Point(70, 15), .Size = New Size(220, 24)}
            ThemeManager.StyleTextBox(txtSearch)
            AddHandler txtSearch.TextChanged, Sub(s, e) ApplyFilter()

            btnRefresh = New Button() With {.Text = "↻ Refresh", .Location = Point.Add(txtSearch.Location, New Size(230, -2)), .Size = New Size(95, 28)}
            ThemeManager.StyleButton(btnRefresh, False)
            AddHandler btnRefresh.Click, Sub(s, e) LoadConnectionsAsync()

            btnKill = New Button() With {
                .Text = "🛑 Terminate Connection",
                .Location = Point.Add(btnRefresh.Location, New Size(105, 0)),
                .Size = New Size(165, 28),
                .BackColor = ThemeManager.DangerColor,
                .ForeColor = Color.White,
                .FlatStyle = FlatStyle.Flat,
                .Font = ThemeManager.BoldFont
            }
            btnKill.FlatAppearance.BorderSize = 0
            AddHandler btnKill.Click, AddressOf OnKillConnectionClicked

            lblSessionCount = New Label() With {
                .Text = "0 Active Sessions",
                .Location = Point.Add(btnKill.Location, New Size(180, 5)),
                .AutoSize = True,
                .Font = ThemeManager.BoldFont,
                .ForeColor = ThemeManager.AccentColor
            }

            pnlTop.Controls.AddRange({lblSearch, txtSearch, btnRefresh, btnKill, lblSessionCount})

            ' Main Grid
            gridConnections = New ModernDataGrid() With {.Dock = DockStyle.Fill}

            Controls.AddRange({gridConnections, pnlTop})
        End Sub

        Public Async Sub LoadConnectionsAsync()
            Dim server = _serverService.SelectedServer
            If server Is Nothing Then
                gridConnections.DataSource = Nothing
                lblSessionCount.Text = "No Server Selected"
                Return
            End If

            btnRefresh.Enabled = False
            btnRefresh.Text = "Loading..."

            Try
                Using provider = DatabaseProviderFactory.CreateProvider(server)
                    _sessions = Await provider.GetSessionsAsync()
                    ApplyFilter()
                End Using
            Catch ex As Exception
                MessageBox.Show($"Failed to load connections: {ex.Message}", "Connection Query Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                btnRefresh.Enabled = True
                btnRefresh.Text = "↻ Refresh"
            End Try
        End Sub

        Private Sub ApplyFilter()
            Dim search = txtSearch.Text.Trim().ToLowerInvariant()
            Dim filtered = _sessions.Where(Function(s)
                                              If String.IsNullOrEmpty(search) Then Return True
                                              Return s.SessionId.ToString().Contains(search) OrElse
                                                     s.LoginName.ToLowerInvariant().Contains(search) OrElse
                                                     s.HostName.ToLowerInvariant().Contains(search) OrElse
                                                     s.ProgramName.ToLowerInvariant().Contains(search) OrElse
                                                     s.DatabaseName.ToLowerInvariant().Contains(search) OrElse
                                                     s.Status.ToLowerInvariant().Contains(search)
                                          End Function).ToList()

            lblSessionCount.Text = $"{filtered.Count} Active Sessions"

            Dim dt As New DataTable()
            dt.Columns.Add("Session ID", GetType(Integer))
            dt.Columns.Add("Login Name", GetType(String))
            dt.Columns.Add("Host Name", GetType(String))
            dt.Columns.Add("Application / Program", GetType(String))
            dt.Columns.Add("Database", GetType(String))
            dt.Columns.Add("Status", GetType(String))
            dt.Columns.Add("Login Time", GetType(DateTime))
            dt.Columns.Add("Last Request", GetType(DateTime))
            dt.Columns.Add("CPU (ms)", GetType(Long))
            dt.Columns.Add("Memory (KB)", GetType(Long))

            For Each s In filtered
                dt.Rows.Add(s.SessionId, s.LoginName, s.HostName, s.ProgramName, s.DatabaseName, s.Status, s.LoginTime, s.LastRequestTime, s.CpuTime, s.MemoryUsageKB)
            Next

            gridConnections.DataSource = dt

            If gridConnections.Columns.Contains("Session ID") Then gridConnections.Columns("Session ID").Width = 85
            If gridConnections.Columns.Contains("Login Name") Then gridConnections.Columns("Login Name").Width = 120
            If gridConnections.Columns.Contains("Host Name") Then gridConnections.Columns("Host Name").Width = 120
            If gridConnections.Columns.Contains("Database") Then gridConnections.Columns("Database").Width = 110
            If gridConnections.Columns.Contains("Status") Then gridConnections.Columns("Status").Width = 90
        End Sub

        Private Async Sub OnKillConnectionClicked(sender As Object, e As EventArgs)
            If gridConnections.SelectedRows.Count = 0 Then
                MessageBox.Show("Please select a connection/session to terminate.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim sessionId = Convert.ToInt32(gridConnections.SelectedRows(0).Cells("Session ID")?.Value)
            Dim login = gridConnections.SelectedRows(0).Cells("Login Name")?.Value?.ToString()
            Dim server = _serverService.SelectedServer
            If server Is Nothing Then Return

            Using dlg As New ConfirmDestructiveDialog("KILL CONNECTION", $"Session {sessionId} ({login}) on {server.Name}", $"KILL {sessionId};")
                If dlg.ShowDialog(FindForm()) = DialogResult.OK Then
                    Try
                        Using provider = DatabaseProviderFactory.CreateProvider(server)
                            Dim res = Await provider.KillSessionAsync(sessionId)
                            If res.Success Then
                                MessageBox.Show(res.Message, "Connection Terminated", MessageBoxButtons.OK, MessageBoxIcon.Information)
                                LoadConnectionsAsync()
                            Else
                                MessageBox.Show(res.Message, "Kill Connection Failed", MessageBoxButtons.OK, MessageBoxIcon.Error)
                            End If
                        End Using
                    Catch ex As Exception
                        MessageBox.Show($"Error terminating connection: {ex.Message}", "Session Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                    End Try
                End If
            End Using
        End Sub

        Private Sub OnThemeChanged(sender As Object, e As EventArgs)
            BackColor = ThemeManager.BackgroundColor
            ThemeManager.StyleButton(btnRefresh, False)
            ThemeManager.StyleTextBox(txtSearch)
        End Sub
    End Class
End Namespace
