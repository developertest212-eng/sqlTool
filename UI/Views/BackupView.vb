Imports System.Data
Imports System.Drawing
Imports System.Windows.Forms
Imports SQLCommandCenter.Data
Imports SQLCommandCenter.Models
Imports SQLCommandCenter.Services
Imports SQLCommandCenter.UI.Controls
Imports SQLCommandCenter.UI.Theme

Namespace UI.Views
    Public Class BackupView
        Inherits UserControl

        Private ReadOnly _serverService As ServerService

        ' Controls
        Private gridBackups As ModernDataGrid
        Private btnRefresh As Button
        Private lblEngineNote As Label

        Public Sub New(serverService As ServerService)
            _serverService = serverService

            InitializeComponent()

            AddHandler _serverService.SelectedServerChanged, Sub(s, e) LoadBackupsAsync()
            AddHandler ThemeManager.ThemeChanged, AddressOf OnThemeChanged

            LoadBackupsAsync()
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

            Dim lblTitle As New Label() With {
                .Text = "BACKUP STATUS & RECOVERY VALIDATION",
                .Font = ThemeManager.HeaderFont,
                .ForeColor = ThemeManager.AccentColor,
                .Location = New Point(16, 10),
                .AutoSize = True
            }

            lblEngineNote = New Label() With {
                .Text = "Tracks MSDB backup history for SQL Server and active Binary Logs for MySQL.",
                .Font = ThemeManager.SmallFont,
                .ForeColor = ThemeManager.TextSecondaryColor,
                .Location = New Point(16, 34),
                .AutoSize = True
            }

            btnRefresh = New Button() With {
                .Text = "↻ Refresh Backups",
                .Location = New Point(500, 16),
                .Size = New Size(140, 30)
            }
            ThemeManager.StyleButton(btnRefresh, False)
            AddHandler btnRefresh.Click, Sub(s, e) LoadBackupsAsync()

            pnlTop.Controls.AddRange({lblTitle, lblEngineNote, btnRefresh})

            gridBackups = New ModernDataGrid() With {.Dock = DockStyle.Fill}
            AddHandler gridBackups.CellFormatting, AddressOf OnCellFormatting

            Controls.AddRange({gridBackups, pnlTop})
        End Sub

        Public Async Sub LoadBackupsAsync()
            Dim server = _serverService.SelectedServer
            If server Is Nothing Then
                gridBackups.DataSource = Nothing
                Return
            End If

            btnRefresh.Enabled = False
            btnRefresh.Text = "Checking..."

            Try
                Using provider = DatabaseProviderFactory.CreateProvider(server)
                    Dim backups = Await provider.GetBackupInfoAsync()

                    Dim dt As New DataTable()
                    dt.Columns.Add("Database", GetType(String))
                    dt.Columns.Add("Backup Type", GetType(String))
                    dt.Columns.Add("Last Backup Finish", GetType(DateTime))
                    dt.Columns.Add("Age (Days)", GetType(Double))
                    dt.Columns.Add("Size (MB)", GetType(Double))
                    dt.Columns.Add("Compressed (MB)", GetType(Double))
                    dt.Columns.Add("Duration (Sec)", GetType(Long))
                    dt.Columns.Add("Health Warning", GetType(String))

                    For Each b In backups
                        Dim warning = If(b.IsStale, "⚠️ STALE BACKUP", "✔ FRESH")
                        dt.Rows.Add(b.DatabaseName, b.BackupType, b.FinishDate, b.AgeDays, b.BackupSizeMB, b.CompressedSizeMB, b.DurationSeconds, warning)
                    Next

                    gridBackups.DataSource = dt

                    If gridBackups.Columns.Contains("Database") Then gridBackups.Columns("Database").Width = 160
                    If gridBackups.Columns.Contains("Backup Type") Then gridBackups.Columns("Backup Type").Width = 110
                    If gridBackups.Columns.Contains("Age (Days)") Then gridBackups.Columns("Age (Days)").Width = 95
                    If gridBackups.Columns.Contains("Health Warning") Then gridBackups.Columns("Health Warning").Width = 140
                End Using
            Catch ex As Exception
                MessageBox.Show($"Failed to query backup history: {ex.Message}", "Backup Status Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                btnRefresh.Enabled = True
                btnRefresh.Text = "↻ Refresh Backups"
            End Try
        End Sub

        Private Sub OnCellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
            If gridBackups.Columns(e.ColumnIndex).Name = "Health Warning" AndAlso e.Value IsNot Nothing Then
                If e.Value.ToString().Contains("STALE") Then
                    e.CellStyle.ForeColor = ThemeManager.DangerColor
                    e.CellStyle.Font = ThemeManager.BoldFont
                Else
                    e.CellStyle.ForeColor = ThemeManager.SuccessColor
                End If
            End If
        End Sub

        Private Sub OnThemeChanged(sender As Object, e As EventArgs)
            BackColor = ThemeManager.BackgroundColor
            ThemeManager.StyleButton(btnRefresh, False)
        End Sub
    End Class
End Namespace
