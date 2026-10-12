Imports System.Drawing
Imports System.Windows.Forms
Imports SQLCommandCenter.Configuration
Imports SQLCommandCenter.Models
Imports SQLCommandCenter.Services
Imports SQLCommandCenter.UI.Controls
Imports SQLCommandCenter.UI.Theme
Imports SQLCommandCenter.UI.Views

Namespace UI.Forms
    Public Class MainForm
        Inherits Form

        ' Core Services
        Private ReadOnly _serverService As ServerService
        Private ReadOnly _alertService As AlertService
        Private ReadOnly _monitoringService As MonitoringService

        ' Top Bar Controls
        Private pnlTopBar As Panel
        Private lblAppTitle As Label
        Private lblEngineBadge As Label
        Private cmbActiveServer As ComboBox
        Private statusBadgeHeader As StatusBadge
        Private btnHeaderRefresh As Button
        Private btnAlertCounter As Button
        Private btnThemeToggle As Button

        ' Sidebar Controls
        Private pnlSidebar As Panel
        Private pnlNavButtons As Panel
        Private ReadOnly _navButtons As New List(Of SidebarButton)()

        ' Content Container
        Private pnlContent As Panel

        ' Views Cache
        Private ReadOnly _views As New Dictionary(Of String, UserControl)()
        Private _currentNavKey As String = ""

        Public Sub New()
            ' Initialize Theme & Config
            ConfigurationManager.LoadSettings()
            ThemeManager.Initialize()

            ' Initialize Services
            _serverService = New ServerService()
            _alertService = New AlertService()
            _monitoringService = New MonitoringService(_serverService, _alertService)

            InitializeComponent()
            InitializeViews()

            ' Event Handlers
            AddHandler _serverService.ServerListChanged, AddressOf OnServerListChanged
            AddHandler _serverService.SelectedServerChanged, AddressOf OnSelectedServerChanged
            AddHandler _monitoringService.ServerStatusChanged, AddressOf OnServerStatusChanged
            AddHandler _alertService.AlertsChanged, AddressOf UpdateAlertCounter
            AddHandler ThemeManager.ThemeChanged, AddressOf OnThemeChanged

            ' Start background monitor
            _monitoringService.Start()

            ' Populate servers
            RefreshServerDropdown()
            UpdateAlertCounter(Nothing, EventArgs.Empty)

            ' Navigate to initial view
            Dim startPage = ConfigurationManager.Settings.StartPage
            If String.IsNullOrEmpty(startPage) OrElse Not _views.ContainsKey(startPage) Then
                startPage = "Dashboard"
            End If
            NavigateTo(startPage)
        End Sub

        Private Sub InitializeComponent()
            Text = "SQL Command Center — SQL Server & MySQL Enterprise Dashboard"
            Size = New Size(1400, 880)
            MinimumSize = New Size(1100, 700)
            StartPosition = FormStartPosition.CenterScreen
            BackColor = ThemeManager.BackgroundColor
            ForeColor = ThemeManager.TextPrimaryColor
            Font = ThemeManager.BodyFont

            ' 1. Top Bar
            pnlTopBar = New Panel() With {
                .Dock = DockStyle.Top,
                .Height = 56,
                .BackColor = ThemeManager.TopBarColor,
                .Padding = New Padding(16, 10, 16, 10)
            }

            lblAppTitle = New Label() With {
                .Text = "⚡ SQL COMMAND CENTER",
                .Font = ThemeManager.TitleFont,
                .ForeColor = ThemeManager.AccentColor,
                .Location = New Point(16, 14),
                .AutoSize = True
            }

            lblEngineBadge = New Label() With {
                .Text = "MSSQL",
                .Font = ThemeManager.SmallFont,
                .ForeColor = Color.White,
                .BackColor = Color.FromArgb(220, 38, 38),
                .Location = New Point(265, 17),
                .Size = New Size(55, 20),
                .TextAlign = ContentAlignment.MiddleCenter
            }

            cmbActiveServer = New ComboBox() With {
                .Location = New Point(330, 15),
                .Size = New Size(220, 26),
                .DropDownStyle = ComboBoxStyle.DropDownList
            }
            ThemeManager.StyleComboBox(cmbActiveServer)
            AddHandler cmbActiveServer.SelectedIndexChanged, AddressOf OnActiveServerDropdownChanged

            statusBadgeHeader = New StatusBadge() With {
                .Location = New Point(560, 15),
                .Status = ServerHealthStatus.Unknown
            }

            btnHeaderRefresh = New Button() With {
                .Text = "↻ Refresh All",
                .Location = New Point(670, 13),
                .Size = New Size(110, 28)
            }
            ThemeManager.StyleButton(btnHeaderRefresh, False)
            AddHandler btnHeaderRefresh.Click, Async Sub() Await _monitoringService.RefreshAllAsync()

            btnAlertCounter = New Button() With {
                .Text = "🔔 Alerts (0)",
                .Anchor = AnchorStyles.Top Or AnchorStyles.Right,
                .Location = New Point(Width - 240, 13),
                .Size = New Size(110, 28),
                .BackColor = Color.FromArgb(30, 30, 46),
                .ForeColor = ThemeManager.SuccessColor,
                .FlatStyle = FlatStyle.Flat
            }
            btnAlertCounter.FlatAppearance.BorderSize = 0
            AddHandler btnAlertCounter.Click, Sub() NavigateTo("Alerts")

            btnThemeToggle = New Button() With {
                .Text = If(ThemeManager.IsDark, "☀️ Light", "🌙 Dark"),
                .Anchor = AnchorStyles.Top Or AnchorStyles.Right,
                .Location = New Point(Width - 115, 13),
                .Size = New Size(85, 28)
            }
            ThemeManager.StyleButton(btnThemeToggle, False)
            AddHandler btnThemeToggle.Click, Sub() ThemeManager.ToggleTheme()

            pnlTopBar.Controls.AddRange({lblAppTitle, lblEngineBadge, cmbActiveServer, statusBadgeHeader, btnHeaderRefresh, btnAlertCounter, btnThemeToggle})

            ' 2. Left Sidebar
            pnlSidebar = New Panel() With {
                .Dock = DockStyle.Left,
                .Width = 220,
                .BackColor = ThemeManager.SidebarColor
            }

            pnlNavButtons = New Panel() With {
                .Dock = DockStyle.Fill,
                .AutoScroll = True,
                .Padding = New Padding(0, 10, 0, 10)
            }

            CreateNavButton("Dashboard", "📊", 0)
            CreateNavButton("Servers", "🖥", 42)
            CreateNavButton("Databases", "🗄", 84)
            CreateNavButton("Performance", "📈", 126)
            CreateNavButton("Queries", "🔍", 168)
            CreateNavButton("Connections", "👥", 210)
            CreateNavButton("Locks & Blocking", "🔒", 252)
            CreateNavButton("Storage", "💾", 294)
            CreateNavButton("Backups", "📦", 336)
            CreateNavButton("Alerts", "🔔", 378)
            CreateNavButton("Reports", "📄", 420)
            CreateNavButton("SQL Console", "💻", 462)
            CreateNavButton("Settings", "⚙", 504)

            pnlSidebar.Controls.Add(pnlNavButtons)

            ' 3. Main Content Area
            pnlContent = New Panel() With {
                .Dock = DockStyle.Fill,
                .BackColor = ThemeManager.BackgroundColor
            }

            Controls.AddRange({pnlContent, pnlSidebar, pnlTopBar})
        End Sub

        Private Sub CreateNavButton(text As String, icon As String, y As Integer)
            Dim btn As New SidebarButton() With {
                .Text = text,
                .IconText = icon,
                .Location = New Point(0, y),
                .Width = 220,
                .Height = 42,
                .Tag = text
            }
            AddHandler btn.Click, Sub(s, e) NavigateTo(text)
            _navButtons.Add(btn)
            pnlNavButtons.Controls.Add(btn)
        End Sub

        Private Sub InitializeViews()
            _views("Dashboard") = New DashboardView(_serverService, _monitoringService, _alertService)
            _views("Servers") = New ServersView(_serverService, _monitoringService)
            _views("Databases") = New DatabasesView(_serverService)
            _views("Performance") = New PerformanceView(_serverService, _monitoringService)
            _views("Queries") = New QueriesView(_serverService)
            _views("Connections") = New ConnectionsView(_serverService)
            _views("Locks & Blocking") = New BlockingView(_serverService)
            _views("Storage") = New StorageView(_serverService)
            _views("Backups") = New BackupView(_serverService)
            _views("Alerts") = New AlertsView(_alertService)
            _views("Reports") = New ReportsView(_serverService, _alertService, _monitoringService)
            _views("SQL Console") = New SqlConsoleView(_serverService)
            _views("Settings") = New SettingsView()
        End Sub

        Public Sub NavigateTo(key As String)
            If Not _views.ContainsKey(key) Then Return
            _currentNavKey = key

            ' Update button visual active states
            For Each btn In _navButtons
                btn.IsActive = (btn.Tag?.ToString() = key)
            Next

            pnlContent.SuspendLayout()
            pnlContent.Controls.Clear()
            Dim view = _views(key)
            view.Dock = DockStyle.Fill
            pnlContent.Controls.Add(view)
            pnlContent.ResumeLayout()
        End Sub

        Private Sub RefreshServerDropdown()
            If InvokeRequired Then
                BeginInvoke(New Action(AddressOf RefreshServerDropdown))
                Return
            End If

            cmbActiveServer.Items.Clear()
            Dim servers = _serverService.GetAllServers()

            For Each s In servers
                cmbActiveServer.Items.Add(s)
            Next

            Dim sel = _serverService.SelectedServer
            If sel IsNot Nothing Then
                For i = 0 To cmbActiveServer.Items.Count - 1
                    Dim item = TryCast(cmbActiveServer.Items(i), ServerProfile)
                    If item IsNot Nothing AndAlso item.Id = sel.Id Then
                        cmbActiveServer.SelectedIndex = i
                        Exit For
                    End If
                Next
            ElseIf cmbActiveServer.Items.Count > 0 Then
                cmbActiveServer.SelectedIndex = 0
            End If

            UpdateHeaderBadge()
        End Sub

        Private Sub UpdateHeaderBadge()
            Dim sel = _serverService.SelectedServer
            If sel Is Nothing Then
                lblEngineBadge.Text = "NONE"
                lblEngineBadge.BackColor = ThemeManager.TextMutedColor
                statusBadgeHeader.Status = ServerHealthStatus.Unknown
                Return
            End If

            If sel.Engine = DatabaseEngine.SqlServer Then
                lblEngineBadge.Text = "MSSQL"
                lblEngineBadge.BackColor = Color.FromArgb(220, 38, 38)
            Else
                lblEngineBadge.Text = "MYSQL"
                lblEngineBadge.BackColor = Color.FromArgb(2, 132, 199)
            End If

            Dim st = _monitoringService.GetStatus(sel.Id)
            statusBadgeHeader.Status = st.HealthStatus
        End Sub

        Private Sub OnActiveServerDropdownChanged(sender As Object, e As EventArgs)
            Dim sel = TryCast(cmbActiveServer.SelectedItem, ServerProfile)
            If sel IsNot Nothing AndAlso (_serverService.SelectedServer Is Nothing OrElse _serverService.SelectedServer.Id <> sel.Id) Then
                _serverService.SelectedServer = sel
            End If
            UpdateHeaderBadge()
        End Sub

        Private Sub OnServerListChanged(sender As Object, e As EventArgs)
            RefreshServerDropdown()
        End Sub

        Private Sub OnSelectedServerChanged(sender As Object, e As ServerProfile)
            RefreshServerDropdown()
        End Sub

        Private Sub OnServerStatusChanged(sender As Object, status As ServerStatus)
            If InvokeRequired Then
                BeginInvoke(Sub() OnServerStatusChanged(sender, status))
                Return
            End If

            Dim sel = _serverService.SelectedServer
            If sel IsNot Nothing AndAlso sel.Id = status.ServerId Then
                statusBadgeHeader.Status = status.HealthStatus
            End If
        End Sub

        Private Sub UpdateAlertCounter(sender As Object, e As EventArgs)
            If InvokeRequired Then
                BeginInvoke(Sub() UpdateAlertCounter(sender, e))
                Return
            End If

            Dim activeAlerts = _alertService.GetActiveAlerts()
            btnAlertCounter.Text = $"🔔 Alerts ({activeAlerts.Count})"
            If activeAlerts.Any(Function(a) a.Severity = AlertSeverity.Critical) Then
                btnAlertCounter.ForeColor = ThemeManager.DangerColor
            ElseIf activeAlerts.Count > 0 Then
                btnAlertCounter.ForeColor = ThemeManager.WarningColor
            Else
                btnAlertCounter.ForeColor = ThemeManager.SuccessColor
            End If
        End Sub

        Private Sub OnThemeChanged(sender As Object, e As EventArgs)
            BackColor = ThemeManager.BackgroundColor
            pnlTopBar.BackColor = ThemeManager.TopBarColor
            pnlSidebar.BackColor = ThemeManager.SidebarColor
            pnlContent.BackColor = ThemeManager.BackgroundColor
            lblAppTitle.ForeColor = ThemeManager.AccentColor
            btnThemeToggle.Text = If(ThemeManager.IsDark, "☀️ Light", "🌙 Dark")
            ThemeManager.StyleButton(btnHeaderRefresh, False)
            ThemeManager.StyleButton(btnThemeToggle, False)
            ThemeManager.StyleComboBox(cmbActiveServer)
            UpdateHeaderBadge()
        End Sub

        Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
            _monitoringService.StopMonitoring()
            _monitoringService.Dispose()
            MyBase.OnFormClosing(e)
        End Sub
    End Class
End Namespace
