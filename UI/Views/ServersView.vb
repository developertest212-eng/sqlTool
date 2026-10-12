Imports System.Drawing
Imports System.Windows.Forms
Imports SQLCommandCenter.Data
Imports SQLCommandCenter.Models
Imports SQLCommandCenter.Services
Imports SQLCommandCenter.UI.Controls
Imports SQLCommandCenter.UI.Forms
Imports SQLCommandCenter.UI.Theme

Namespace UI.Views
    Public Class ServersView
        Inherits UserControl

        Private ReadOnly _serverService As ServerService
        Private ReadOnly _monitoringService As MonitoringService

        ' UI Controls
        Private gridServers As ModernDataGrid
        Private txtSearch As TextBox
        Private cmbGroupFilter As ComboBox
        Private cmbEngineFilter As ComboBox
        Private btnAdd As Button
        Private btnEdit As Button
        Private btnDelete As Button
        Private btnTest As Button
        Private btnConnect As Button

        Public Sub New(serverService As ServerService, monitoringService As MonitoringService)
            _serverService = serverService
            _monitoringService = monitoringService

            InitializeComponent()

            AddHandler _serverService.ServerListChanged, AddressOf RefreshGrid
            AddHandler _monitoringService.ServerStatusChanged, AddressOf OnServerStatusChanged
            AddHandler ThemeManager.ThemeChanged, AddressOf OnThemeChanged

            RefreshFilters()
            RefreshGrid()
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

            ' Search
            Dim lblSearch As New Label() With {.Text = "Search:", .Location = New Point(16, 20), .AutoSize = True, .ForeColor = ThemeManager.TextSecondaryColor}
            txtSearch = New TextBox() With {.Location = New Point(70, 18), .Size = New Size(180, 24)}
            ThemeManager.StyleTextBox(txtSearch)
            AddHandler txtSearch.TextChanged, Sub(s, e) RefreshGrid()

            ' Group Filter
            Dim lblGroup As New Label() With {.Text = "Group:", .Location = New Point(265, 20), .AutoSize = True, .ForeColor = ThemeManager.TextSecondaryColor}
            cmbGroupFilter = New ComboBox() With {.Location = New Point(315, 18), .Size = New Size(130, 24), .DropDownStyle = ComboBoxStyle.DropDownList}
            ThemeManager.StyleComboBox(cmbGroupFilter)
            AddHandler cmbGroupFilter.SelectedIndexChanged, Sub(s, e) RefreshGrid()

            ' Action Buttons
            btnAdd = New Button() With {.Text = "+ Add Server", .Location = New Point(460, 16), .Size = New Size(110, 30)}
            ThemeManager.StyleButton(btnAdd, True)
            AddHandler btnAdd.Click, AddressOf OnAddServerClicked

            btnEdit = New Button() With {.Text = "✎ Edit", .Location = New Point(580, 16), .Size = New Size(85, 30)}
            ThemeManager.StyleButton(btnEdit, False)
            AddHandler btnEdit.Click, AddressOf OnEditServerClicked

            btnDelete = New Button() With {.Text = "🗑 Delete", .Location = New Point(675, 16), .Size = New Size(95, 30)}
            ThemeManager.StyleButton(btnDelete, False)
            AddHandler btnDelete.Click, AddressOf OnDeleteServerClicked

            btnTest = New Button() With {.Text = "⚡ Test", .Location = New Point(780, 16), .Size = New Size(85, 30)}
            ThemeManager.StyleButton(btnTest, False)
            AddHandler btnTest.Click, AddressOf OnTestServerClicked

            btnConnect = New Button() With {.Text = "★ Set Active", .Location = New Point(875, 16), .Size = New Size(110, 30)}
            ThemeManager.StyleButton(btnConnect, False)
            AddHandler btnConnect.Click, AddressOf OnSetActiveClicked

            pnlTop.Controls.AddRange({lblSearch, txtSearch, lblGroup, cmbGroupFilter, btnAdd, btnEdit, btnDelete, btnTest, btnConnect})

            ' Main Grid
            gridServers = New ModernDataGrid() With {
                .Dock = DockStyle.Fill
            }
            AddHandler gridServers.CellDoubleClick, AddressOf OnGridDoubleClick

            Controls.AddRange({gridServers, pnlTop})
        End Sub

        Private Sub RefreshFilters()
            cmbGroupFilter.Items.Clear()
            cmbGroupFilter.Items.Add("All Groups")
            For Each g In _serverService.GetGroups()
                cmbGroupFilter.Items.Add(g)
            Next
            cmbGroupFilter.SelectedIndex = 0
        End Sub

        Public Sub RefreshGrid()
            If InvokeRequired Then
                BeginInvoke(New Action(AddressOf RefreshGrid))
                Return
            End If

            Dim servers = _serverService.GetAllServers()
            Dim search = txtSearch.Text.Trim().ToLowerInvariant()
            Dim selectedGroup = If(cmbGroupFilter.SelectedItem?.ToString(), "All Groups")

            Dim filtered = servers.Where(Function(s)
                                             Dim matchesSearch = String.IsNullOrEmpty(search) OrElse
                                                                 s.Name.ToLowerInvariant().Contains(search) OrElse
                                                                 s.Host.ToLowerInvariant().Contains(search) OrElse
                                                                 s.Engine.ToString().ToLowerInvariant().Contains(search)
                                             Dim matchesGroup = (selectedGroup = "All Groups" OrElse s.GroupName = selectedGroup)
                                             Return matchesSearch AndAlso matchesGroup
                                         End Function).ToList()

            Dim dt As New DataTable()
            dt.Columns.Add("Active", GetType(String))
            dt.Columns.Add("Id", GetType(String))
            dt.Columns.Add("Server Name", GetType(String))
            dt.Columns.Add("Engine", GetType(String))
            dt.Columns.Add("Group", GetType(String))
            dt.Columns.Add("Host:Port", GetType(String))
            dt.Columns.Add("Auth Type", GetType(String))
            dt.Columns.Add("Status", GetType(String))
            dt.Columns.Add("Latency", GetType(String))
            dt.Columns.Add("Version", GetType(String))

            Dim sel = _serverService.SelectedServer

            For Each s In filtered
                Dim st = _monitoringService.GetStatus(s.Id)
                Dim isActive = If(sel IsNot Nothing AndAlso sel.Id = s.Id, "✔ ACTIVE", "")
                Dim statusStr = If(st.IsOnline, st.HealthStatus.ToString(), "Offline")
                Dim latencyStr = If(st.IsOnline, $"{st.LatencyMs} ms", "-")

                dt.Rows.Add(isActive, s.Id, s.Name, s.Engine.ToString(), s.GroupName, $"{s.Host}:{s.Port}", s.AuthType.ToString(), statusStr, latencyStr, st.Version)
            Next

            gridServers.DataSource = dt

            If gridServers.Columns.Contains("Id") Then
                gridServers.Columns("Id").Visible = False
            End If
            If gridServers.Columns.Contains("Active") Then
                gridServers.Columns("Active").Width = 85
            End If
            If gridServers.Columns.Contains("Server Name") Then
                gridServers.Columns("Server Name").Width = 160
            End If
            If gridServers.Columns.Contains("Engine") Then
                gridServers.Columns("Engine").Width = 110
            End If
            If gridServers.Columns.Contains("Status") Then
                gridServers.Columns("Status").Width = 100
            End If
            If gridServers.Columns.Contains("Latency") Then
                gridServers.Columns("Latency").Width = 90
            End If
        End Sub

        Private Function GetSelectedProfile() As ServerProfile
            If gridServers.SelectedRows.Count = 0 Then Return Nothing
            Dim row = gridServers.SelectedRows(0)
            Dim id = row.Cells("Id")?.Value?.ToString()
            If String.IsNullOrEmpty(id) Then Return Nothing
            Return _serverService.GetAllServers().FirstOrDefault(Function(s) s.Id = id)
        End Function

        Private Sub OnAddServerClicked(sender As Object, e As EventArgs)
            Using dlg As New ServerEditDialog()
                If dlg.ShowDialog(FindForm()) = DialogResult.OK Then
                    _serverService.AddOrUpdateServer(dlg.ResultProfile)
                    RefreshFilters()
                    RefreshGrid()
                End If
            End Using
        End Sub

        Private Sub OnEditServerClicked(sender As Object, e As EventArgs)
            Dim profile = GetSelectedProfile()
            If profile Is Nothing Then
                MessageBox.Show("Please select a server to edit.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Using dlg As New ServerEditDialog(profile)
                If dlg.ShowDialog(FindForm()) = DialogResult.OK Then
                    _serverService.AddOrUpdateServer(dlg.ResultProfile)
                    RefreshFilters()
                    RefreshGrid()
                End If
            End Using
        End Sub

        Private Sub OnDeleteServerClicked(sender As Object, e As EventArgs)
            Dim profile = GetSelectedProfile()
            If profile Is Nothing Then
                MessageBox.Show("Please select a server to delete.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim result = MessageBox.Show($"Are you sure you want to delete profile '{profile.Name}'?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
            If result = DialogResult.Yes Then
                _serverService.DeleteServer(profile.Id)
                RefreshFilters()
                RefreshGrid()
            End If
        End Sub

        Private Async Sub OnTestServerClicked(sender As Object, e As EventArgs)
            Dim profile = GetSelectedProfile()
            If profile Is Nothing Then
                MessageBox.Show("Please select a server to test.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            btnTest.Enabled = False
            btnTest.Text = "Testing..."

            Try
                Using provider = DatabaseProviderFactory.CreateProvider(profile)
                    Dim res = Await provider.TestConnectionAsync()
                    If res.Success Then
                        MessageBox.Show($"✓ Connected to {profile.Name} in {res.LatencyMs} ms!{Environment.NewLine}{res.Version}", "Connection Successful", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    Else
                        MessageBox.Show($"✕ Failed to connect to {profile.Name}:{Environment.NewLine}{res.ErrorMessage}", "Connection Failed", MessageBoxButtons.OK, MessageBoxIcon.Error)
                    End If
                End Using
            Finally
                btnTest.Enabled = True
                btnTest.Text = "⚡ Test"
            End Try
        End Sub

        Private Sub OnSetActiveClicked(sender As Object, e As EventArgs)
            Dim profile = GetSelectedProfile()
            If profile IsNot Nothing Then
                _serverService.SelectedServer = profile
                RefreshGrid()
            End If
        End Sub

        Private Sub OnGridDoubleClick(sender As Object, e As DataGridViewCellEventArgs)
            If e.RowIndex >= 0 Then
                OnSetActiveClicked(Nothing, EventArgs.Empty)
            End If
        End Sub

        Private Sub OnServerStatusChanged(sender As Object, status As ServerStatus)
            RefreshGrid()
        End Sub

        Private Sub OnThemeChanged(sender As Object, e As EventArgs)
            BackColor = ThemeManager.BackgroundColor
            ThemeManager.StyleButton(btnAdd, True)
            ThemeManager.StyleButton(btnEdit, False)
            ThemeManager.StyleButton(btnDelete, False)
            ThemeManager.StyleButton(btnTest, False)
            ThemeManager.StyleButton(btnConnect, False)
            ThemeManager.StyleTextBox(txtSearch)
            ThemeManager.StyleComboBox(cmbGroupFilter)
            RefreshGrid()
        End Sub
    End Class
End Namespace
