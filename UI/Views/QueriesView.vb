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
    Public Class QueriesView
        Inherits UserControl

        Private ReadOnly _serverService As ServerService
        Private _runningQueries As New List(Of QueryInfo)()

        ' Controls
        Private gridQueries As ModernDataGrid
        Private txtQueryDetail As TextBox
        Private txtSearch As TextBox
        Private btnRefresh As Button
        Private btnCopyQuery As Button
        Private btnKillSession As Button
        Private splitQueries As SplitContainer

        Public Sub New(serverService As ServerService)
            _serverService = serverService

            InitializeComponent()

            AddHandler _serverService.SelectedServerChanged, Sub(s, e) LoadQueriesAsync()
            AddHandler ThemeManager.ThemeChanged, AddressOf OnThemeChanged

            LoadQueriesAsync()
        End Sub

        Private Sub InitializeComponent()
            Dock = DockStyle.Fill
            BackColor = ThemeManager.BackgroundColor
            Font = ThemeManager.BodyFont

            ' Top bar
            Dim pnlTop As New Panel() With {
                .Dock = DockStyle.Top,
                .Height = 55,
                .Padding = New Padding(16, 12, 16, 12),
                .BackColor = ThemeManager.TopBarColor
            }

            Dim lblSearch As New Label() With {.Text = "Filter:", .Location = New Point(16, 18), .AutoSize = True, .ForeColor = ThemeManager.TextSecondaryColor}
            txtSearch = New TextBox() With {.Location = New Point(65, 15), .Size = New Size(220, 24)}
            ThemeManager.StyleTextBox(txtSearch)
            AddHandler txtSearch.TextChanged, Sub(s, e) ApplyFilter()

            btnRefresh = New Button() With {.Text = "↻ Refresh", .Location = Point.Add(txtSearch.Location, New Size(230, -2)), .Size = New Size(95, 28)}
            ThemeManager.StyleButton(btnRefresh, False)
            AddHandler btnRefresh.Click, Sub(s, e) LoadQueriesAsync()

            btnCopyQuery = New Button() With {.Text = "📋 Copy Query", .Location = Point.Add(btnRefresh.Location, New Size(105, 0)), .Size = New Size(115, 28)}
            ThemeManager.StyleButton(btnCopyQuery, False)
            AddHandler btnCopyQuery.Click, AddressOf OnCopyQueryClicked

            btnKillSession = New Button() With {
                .Text = "🛑 Kill Session",
                .Location = Point.Add(btnCopyQuery.Location, New Size(125, 0)),
                .Size = New Size(115, 28),
                .BackColor = ThemeManager.DangerColor,
                .ForeColor = Color.White,
                .FlatStyle = FlatStyle.Flat,
                .Font = ThemeManager.BoldFont
            }
            btnKillSession.FlatAppearance.BorderSize = 0
            AddHandler btnKillSession.Click, AddressOf OnKillSessionClicked

            pnlTop.Controls.AddRange({lblSearch, txtSearch, btnRefresh, btnCopyQuery, btnKillSession})

            ' Split Container for Grid and Query Text
            splitQueries = New SplitContainer() With {
                .Dock = DockStyle.Fill,
                .Orientation = Orientation.Horizontal,
                .SplitterDistance = 340,
                .BackColor = ThemeManager.CardBorderColor
            }

            gridQueries = New ModernDataGrid() With {.Dock = DockStyle.Fill}
            AddHandler gridQueries.SelectionChanged, AddressOf OnGridSelectionChanged
            splitQueries.Panel1.Controls.Add(gridQueries)

            Dim pnlDetail As New Panel() With {.Dock = DockStyle.Fill, .BackColor = ThemeManager.CardColor}
            Dim lblDetailHeader As New Label() With {
                .Dock = DockStyle.Top,
                .Height = 28,
                .Text = "Complete Statement Text",
                .Font = ThemeManager.BoldFont,
                .ForeColor = ThemeManager.TextSecondaryColor,
                .Padding = New Padding(10, 6, 0, 0)
            }
            txtQueryDetail = New TextBox() With {
                .Dock = DockStyle.Fill,
                .Multiline = True,
                .ReadOnly = True,
                .ScrollBars = ScrollBars.Both,
                .Font = ThemeManager.CodeFont,
                .BackColor = ThemeManager.InputBackgroundColor,
                .ForeColor = ThemeManager.TextPrimaryColor,
                .BorderStyle = BorderStyle.None
            }
            pnlDetail.Controls.AddRange({txtQueryDetail, lblDetailHeader})
            splitQueries.Panel2.Controls.Add(pnlDetail)

            Controls.AddRange({splitQueries, pnlTop})
        End Sub

        Public Async Sub LoadQueriesAsync()
            Dim server = _serverService.SelectedServer
            If server Is Nothing Then
                gridQueries.DataSource = Nothing
                txtQueryDetail.Text = ""
                Return
            End If

            btnRefresh.Enabled = False
            btnRefresh.Text = "Loading..."

            Try
                Using provider = DatabaseProviderFactory.CreateProvider(server)
                    _runningQueries = Await provider.GetRunningQueriesAsync()
                    ApplyFilter()
                End Using
            Catch ex As Exception
                MessageBox.Show($"Failed to fetch running queries: {ex.Message}", "Query Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                btnRefresh.Enabled = True
                btnRefresh.Text = "↻ Refresh"
            End Try
        End Sub

        Private Sub ApplyFilter()
            Dim search = txtSearch.Text.Trim().ToLowerInvariant()
            Dim filtered = _runningQueries.Where(Function(q)
                                                    If String.IsNullOrEmpty(search) Then Return True
                                                    Return q.SessionId.ToString().Contains(search) OrElse
                                                           q.UserName.ToLowerInvariant().Contains(search) OrElse
                                                           q.DatabaseName.ToLowerInvariant().Contains(search) OrElse
                                                           q.Command.ToLowerInvariant().Contains(search) OrElse
                                                           q.QueryText.ToLowerInvariant().Contains(search)
                                                End Function).ToList()

            Dim dt As New DataTable()
            dt.Columns.Add("Session ID", GetType(Integer))
            dt.Columns.Add("User", GetType(String))
            dt.Columns.Add("Database", GetType(String))
            dt.Columns.Add("Duration", GetType(String))
            dt.Columns.Add("Status", GetType(String))
            dt.Columns.Add("Command", GetType(String))
            dt.Columns.Add("Wait Type", GetType(String))
            dt.Columns.Add("Blocked By", GetType(String))
            dt.Columns.Add("CPU (ms)", GetType(Long))
            dt.Columns.Add("Reads", GetType(Long))
            dt.Columns.Add("Writes", GetType(Long))
            dt.Columns.Add("Query Preview", GetType(String))

            For Each q In filtered
                Dim blockedStr = If(q.BlockingSessionId > 0, q.BlockingSessionId.ToString(), "-")
                Dim preview = If(q.QueryText?.Length > 120, q.QueryText.Substring(0, 120).Replace(vbCr, " ").Replace(vbLf, " ") & "...", q.QueryText)
                dt.Rows.Add(q.SessionId, q.UserName, q.DatabaseName, q.DurationDisplay, q.Status, q.Command, q.WaitType, blockedStr, q.CpuTimeMs, q.LogicalReads, q.Writes, preview)
            Next

            gridQueries.DataSource = dt

            If gridQueries.Columns.Contains("Session ID") Then gridQueries.Columns("Session ID").Width = 85
            If gridQueries.Columns.Contains("Duration") Then gridQueries.Columns("Duration").Width = 90
            If gridQueries.Columns.Contains("User") Then gridQueries.Columns("User").Width = 110
            If gridQueries.Columns.Contains("Database") Then gridQueries.Columns("Database").Width = 120
            If gridQueries.Columns.Contains("Status") Then gridQueries.Columns("Status").Width = 90
            If gridQueries.Columns.Contains("Wait Type") Then gridQueries.Columns("Wait Type").Width = 110
        End Sub

        Private Sub OnGridSelectionChanged(sender As Object, e As EventArgs)
            If gridQueries.SelectedRows.Count = 0 Then
                txtQueryDetail.Text = ""
                Return
            End If

            Dim sessionId = Convert.ToInt32(gridQueries.SelectedRows(0).Cells("Session ID")?.Value)
            Dim queryObj = _runningQueries.FirstOrDefault(Function(q) q.SessionId = sessionId)
            If queryObj IsNot Nothing Then
                txtQueryDetail.Text = queryObj.QueryText
            End If
        End Sub

        Private Sub OnCopyQueryClicked(sender As Object, e As EventArgs)
            If Not String.IsNullOrEmpty(txtQueryDetail.Text) Then
                Clipboard.SetText(txtQueryDetail.Text)
                MessageBox.Show("Query statement copied to clipboard.", "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End If
        End Sub

        Private Async Sub OnKillSessionClicked(sender As Object, e As EventArgs)
            If gridQueries.SelectedRows.Count = 0 Then
                MessageBox.Show("Please select an active query/session to terminate.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim sessionId = Convert.ToInt32(gridQueries.SelectedRows(0).Cells("Session ID")?.Value)
            Dim server = _serverService.SelectedServer
            If server Is Nothing Then Return

            Dim queryObj = _runningQueries.FirstOrDefault(Function(q) q.SessionId = sessionId)
            Dim queryPreview = If(queryObj IsNot Nothing, queryObj.QueryText, "Unknown")

            Using dlg As New ConfirmDestructiveDialog("KILL SESSION", $"Session ID {sessionId} on {server.Name}", queryPreview)
                If dlg.ShowDialog(FindForm()) = DialogResult.OK Then
                    Try
                        Using provider = DatabaseProviderFactory.CreateProvider(server)
                            Dim res = Await provider.KillSessionAsync(sessionId)
                            If res.Success Then
                                MessageBox.Show(res.Message, "Session Terminated", MessageBoxButtons.OK, MessageBoxIcon.Information)
                                LoadQueriesAsync()
                            Else
                                MessageBox.Show(res.Message, "Kill Session Failed", MessageBoxButtons.OK, MessageBoxIcon.Error)
                            End If
                        End Using
                    Catch ex As Exception
                        MessageBox.Show($"Error terminating session: {ex.Message}", "Session Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                    End Try
                End If
            End Using
        End Sub

        Private Sub OnThemeChanged(sender As Object, e As EventArgs)
            BackColor = ThemeManager.BackgroundColor
            splitQueries.BackColor = ThemeManager.CardBorderColor
            txtQueryDetail.BackColor = ThemeManager.InputBackgroundColor
            txtQueryDetail.ForeColor = ThemeManager.TextPrimaryColor
            ThemeManager.StyleButton(btnRefresh, False)
            ThemeManager.StyleButton(btnCopyQuery, False)
            ThemeManager.StyleTextBox(txtSearch)
        End Sub
    End Class
End Namespace
