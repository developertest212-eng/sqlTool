Imports System.Drawing
Imports System.Windows.Forms
Imports SQLCommandCenter.Configuration
Imports SQLCommandCenter.UI.Theme

Namespace UI.Views
    Public Class SettingsView
        Inherits UserControl

        ' Controls
        Private cmbTheme As ComboBox
        Private numPollingInterval As NumericUpDown
        Private chkAutoRefresh As CheckBox
        Private numRetentionMinutes As NumericUpDown
        Private chkConfirmDestructive As CheckBox
        Private numQueryTimeout As NumericUpDown
        Private numMaxRows As NumericUpDown
        Private chkAlertsEnabled As CheckBox
        Private numCpuWarning As NumericUpDown
        Private numCpuCritical As NumericUpDown
        Private numMemWarning As NumericUpDown
        Private numMemCritical As NumericUpDown
        Private numStorageWarning As NumericUpDown
        Private numStorageCritical As NumericUpDown
        Private numLongQuery As NumericUpDown
        Private btnSaveSettings As Button
        Private lblSaveMessage As Label

        Public Sub New()
            InitializeComponent()
            LoadCurrentSettings()

            AddHandler ThemeManager.ThemeChanged, AddressOf OnThemeChanged
        End Sub

        Private Sub InitializeComponent()
            Dock = DockStyle.Fill
            AutoScroll = True
            BackColor = ThemeManager.BackgroundColor
            Font = ThemeManager.BodyFont

            Dim pnlContent As New Panel() With {
                .Dock = DockStyle.Top,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .Padding = New Padding(24)
            }

            Dim y = 10

            ' Section 1: Appearance & UI
            pnlContent.Controls.Add(CreateSectionHeader("APPEARANCE & GENERAL", y))
            y += 35

            pnlContent.Controls.Add(CreateLabel("UI Color Theme:", 24, y))
            cmbTheme = New ComboBox() With {.Location = New Point(220, y), .Size = New Size(180, 24), .DropDownStyle = ComboBoxStyle.DropDownList}
            cmbTheme.Items.AddRange({"Dark", "Light"})
            ThemeManager.StyleComboBox(cmbTheme)
            pnlContent.Controls.Add(cmbTheme)
            y += 35

            ' Section 2: Monitoring & Polling
            pnlContent.Controls.Add(CreateSectionHeader("MONITORING & BACKGROUND POLLING", y))
            y += 35

            chkAutoRefresh = New CheckBox() With {.Text = "Enable Automatic Background Polling", .Location = New Point(24, y), .AutoSize = True, .ForeColor = ThemeManager.TextPrimaryColor}
            pnlContent.Controls.Add(chkAutoRefresh)
            y += 30

            pnlContent.Controls.Add(CreateLabel("Polling Interval (seconds):", 24, y))
            numPollingInterval = CreateNumeric(220, y, 5, 300, 10)
            pnlContent.Controls.Add(numPollingInterval)
            y += 32

            pnlContent.Controls.Add(CreateLabel("Metrics History Retention (minutes):", 24, y))
            numRetentionMinutes = CreateNumeric(220, y, 10, 1440, 60)
            pnlContent.Controls.Add(numRetentionMinutes)
            y += 38

            ' Section 3: Query & Safety
            pnlContent.Controls.Add(CreateSectionHeader("QUERY SAFETY & EXECUTION", y))
            y += 35

            chkConfirmDestructive = New CheckBox() With {.Text = "Require Confirmation for Destructive SQL (DROP, DELETE, TRUNCATE, ALTER, UPDATE, KILL)", .Location = New Point(24, y), .AutoSize = True, .ForeColor = ThemeManager.TextPrimaryColor}
            pnlContent.Controls.Add(chkConfirmDestructive)
            y += 30

            pnlContent.Controls.Add(CreateLabel("Query Timeout (seconds):", 24, y))
            numQueryTimeout = CreateNumeric(220, y, 5, 3600, 30)
            pnlContent.Controls.Add(numQueryTimeout)
            y += 32

            pnlContent.Controls.Add(CreateLabel("Max Query Results Rows:", 24, y))
            numMaxRows = CreateNumeric(220, y, 50, 50000, 1000)
            pnlContent.Controls.Add(numMaxRows)
            y += 38

            ' Section 4: Alert Thresholds
            pnlContent.Controls.Add(CreateSectionHeader("ALERT THRESHOLDS", y))
            y += 35

            chkAlertsEnabled = New CheckBox() With {.Text = "Enable Health Rule & Threshold Alerts", .Location = New Point(24, y), .AutoSize = True, .ForeColor = ThemeManager.TextPrimaryColor}
            pnlContent.Controls.Add(chkAlertsEnabled)
            y += 30

            pnlContent.Controls.Add(CreateLabel("CPU Warning Threshold (%):", 24, y))
            numCpuWarning = CreateNumeric(220, y, 10, 100, 75)
            pnlContent.Controls.Add(CreateLabel("CPU Critical (%):", 340, y))
            numCpuCritical = CreateNumeric(450, y, 10, 100, 90)
            pnlContent.Controls.AddRange({numCpuWarning, numCpuCritical})
            y += 32

            pnlContent.Controls.Add(CreateLabel("Memory Warning (%):", 24, y))
            numMemWarning = CreateNumeric(220, y, 10, 100, 80)
            pnlContent.Controls.Add(CreateLabel("Memory Critical (%):", 340, y))
            numMemCritical = CreateNumeric(450, y, 10, 100, 92)
            pnlContent.Controls.AddRange({numMemWarning, numMemCritical})
            y += 32

            pnlContent.Controls.Add(CreateLabel("Storage Warning (%):", 24, y))
            numStorageWarning = CreateNumeric(220, y, 10, 100, 75)
            pnlContent.Controls.Add(CreateLabel("Storage Critical (%):", 340, y))
            numStorageCritical = CreateNumeric(450, y, 10, 100, 90)
            pnlContent.Controls.AddRange({numStorageWarning, numStorageCritical})
            y += 32

            pnlContent.Controls.Add(CreateLabel("Long Running Query (sec):", 24, y))
            numLongQuery = CreateNumeric(220, y, 5, 3600, 30)
            pnlContent.Controls.Add(numLongQuery)
            y += 42

            ' Section 5: Security Note
            pnlContent.Controls.Add(CreateSectionHeader("SECURITY & CREDENTIAL ENCRYPTION", y))
            y += 30
            Dim lblSecurityNote As New Label() With {
                .Text = "Credentials and database passwords are encrypted using Windows DPAPI (CurrentUser). Secrets are decrypted strictly in-memory during connection setup.",
                .Location = New Point(24, y),
                .Size = New Size(700, 34),
                .ForeColor = ThemeManager.TextSecondaryColor
            }
            pnlContent.Controls.Add(lblSecurityNote)
            y += 45

            ' Save Button & Message
            btnSaveSettings = New Button() With {
                .Text = "💾 Save & Apply Settings",
                .Location = New Point(24, y),
                .Size = New Size(180, 36)
            }
            ThemeManager.StyleButton(btnSaveSettings, True)
            AddHandler btnSaveSettings.Click, AddressOf OnSaveSettingsClicked

            lblSaveMessage = New Label() With {
                .Text = "",
                .Location = New Point(220, y + 8),
                .AutoSize = True,
                .Font = ThemeManager.BoldFont,
                .ForeColor = ThemeManager.SuccessColor
            }

            pnlContent.Controls.AddRange({btnSaveSettings, lblSaveMessage})
            y += 60

            Controls.Add(pnlContent)
        End Sub

        Private Function CreateSectionHeader(text As String, y As Integer) As Label
            Return New Label() With {
                .Text = text,
                .Location = New Point(24, y),
                .AutoSize = True,
                .Font = ThemeManager.HeaderFont,
                .ForeColor = ThemeManager.AccentColor
            }
        End Function

        Private Function CreateLabel(text As String, x As Integer, y As Integer) As Label
            Return New Label() With {
                .Text = text,
                .Location = New Point(x, y + 4),
                .AutoSize = True,
                .ForeColor = ThemeManager.TextSecondaryColor
            }
        End Function

        Private Function CreateNumeric(x As Integer, y As Integer, min As Integer, max As Integer, defaultVal As Integer) As NumericUpDown
            Dim num As New NumericUpDown() With {
                .Location = New Point(x, y),
                .Size = New Size(90, 24),
                .Minimum = min,
                .Maximum = max,
                .Value = defaultVal,
                .BackColor = ThemeManager.InputBackgroundColor,
                .ForeColor = ThemeManager.TextPrimaryColor
            }
            Return num
        End Function

        Private Sub LoadCurrentSettings()
            Dim s = ConfigurationManager.Settings
            cmbTheme.SelectedItem = s.Theme
            chkAutoRefresh.Checked = s.AutoRefreshEnabled
            numPollingInterval.Value = Math.Max(5, Math.Min(300, s.PollingIntervalSeconds))
            numRetentionMinutes.Value = Math.Max(10, Math.Min(1440, s.MetricsHistoryRetentionMinutes))
            chkConfirmDestructive.Checked = s.ConfirmDestructiveQueries
            numQueryTimeout.Value = Math.Max(5, Math.Min(3600, s.DefaultQueryTimeoutSeconds))
            numMaxRows.Value = Math.Max(50, Math.Min(50000, s.MaxQueryResultsRows))
            chkAlertsEnabled.Checked = s.AlertsEnabled
            numCpuWarning.Value = CDec(s.CpuWarningPercent)
            numCpuCritical.Value = CDec(s.CpuCriticalPercent)
            numMemWarning.Value = CDec(s.MemoryWarningPercent)
            numMemCritical.Value = CDec(s.MemoryCriticalPercent)
            numStorageWarning.Value = CDec(s.StorageWarningPercent)
            numStorageCritical.Value = CDec(s.StorageCriticalPercent)
            numLongQuery.Value = s.LongRunningQuerySeconds
        End Sub

        Private Sub OnSaveSettingsClicked(sender As Object, e As EventArgs)
            Dim s = ConfigurationManager.Settings
            Dim themeChanged = (s.Theme <> cmbTheme.SelectedItem?.ToString())

            s.Theme = cmbTheme.SelectedItem?.ToString()
            s.AutoRefreshEnabled = chkAutoRefresh.Checked
            s.PollingIntervalSeconds = CInt(numPollingInterval.Value)
            s.MetricsHistoryRetentionMinutes = CInt(numRetentionMinutes.Value)
            s.ConfirmDestructiveQueries = chkConfirmDestructive.Checked
            s.DefaultQueryTimeoutSeconds = CInt(numQueryTimeout.Value)
            s.MaxQueryResultsRows = CInt(numMaxRows.Value)
            s.AlertsEnabled = chkAlertsEnabled.Checked
            s.CpuWarningPercent = CDbl(numCpuWarning.Value)
            s.CpuCriticalPercent = CDbl(numCpuCritical.Value)
            s.MemoryWarningPercent = CDbl(numMemWarning.Value)
            s.MemoryCriticalPercent = CDbl(numMemCritical.Value)
            s.StorageWarningPercent = CDbl(numStorageWarning.Value)
            s.StorageCriticalPercent = CDbl(numStorageCritical.Value)
            s.LongRunningQuerySeconds = CInt(numLongQuery.Value)

            ConfigurationManager.SaveSettings()

            If themeChanged Then
                ThemeManager.Initialize()
                ThemeManager.ToggleTheme()
            End If

            lblSaveMessage.Text = "✔ Settings successfully saved and applied!"
            lblSaveMessage.ForeColor = ThemeManager.SuccessColor
        End Sub

        Private Sub OnThemeChanged(sender As Object, e As EventArgs)
            BackColor = ThemeManager.BackgroundColor
            ThemeManager.StyleComboBox(cmbTheme)
            ThemeManager.StyleButton(btnSaveSettings, True)
        End Sub
    End Class
End Namespace
