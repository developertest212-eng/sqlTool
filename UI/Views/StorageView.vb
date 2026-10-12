Imports System.Data
Imports System.Drawing
Imports System.Windows.Forms
Imports SQLCommandCenter.Data
Imports SQLCommandCenter.Models
Imports SQLCommandCenter.Services
Imports SQLCommandCenter.UI.Controls
Imports SQLCommandCenter.UI.Theme

Namespace UI.Views
    Public Class StorageView
        Inherits UserControl

        Private ReadOnly _serverService As ServerService

        ' Controls
        Private gridStorage As ModernDataGrid
        Private cardTotalStorage As MetricCard
        Private cardDataStorage As MetricCard
        Private cardLogStorage As MetricCard
        Private btnRefresh As Button

        Public Sub New(serverService As ServerService)
            _serverService = serverService

            InitializeComponent()

            AddHandler _serverService.SelectedServerChanged, Sub(s, e) LoadStorageAsync()
            AddHandler ThemeManager.ThemeChanged, AddressOf OnThemeChanged

            LoadStorageAsync()
        End Sub

        Private Sub InitializeComponent()
            Dock = DockStyle.Fill
            BackColor = ThemeManager.BackgroundColor
            Font = ThemeManager.BodyFont

            ' Top Cards Panel
            Dim pnlTop As New Panel() With {
                .Dock = DockStyle.Top,
                .Height = 160,
                .Padding = New Padding(16),
                .BackColor = ThemeManager.TopBarColor
            }

            Dim pnlHeader As New Panel() With {.Dock = DockStyle.Top, .Height = 35}
            Dim lblTitle As New Label() With {
                .Text = "DATABASE FILE & STORAGE CONSUMPTION",
                .Font = ThemeManager.HeaderFont,
                .ForeColor = ThemeManager.AccentColor,
                .Location = New Point(0, 6),
                .AutoSize = True
            }
            btnRefresh = New Button() With {
                .Text = "↻ Refresh Storage",
                .Location = New Point(360, 2),
                .Size = New Size(140, 30)
            }
            ThemeManager.StyleButton(btnRefresh, False)
            AddHandler btnRefresh.Click, Sub(s, e) LoadStorageAsync()

            pnlHeader.Controls.AddRange({lblTitle, btnRefresh})
            pnlTop.Controls.Add(pnlHeader)

            Dim pnlCards As New FlowLayoutPanel() With {
                .Dock = DockStyle.Bottom,
                .Height = 105,
                .WrapContents = False,
                .BackColor = Color.Transparent
            }

            cardTotalStorage = New MetricCard() With {.Title = "TOTAL ALLOCATED", .Value = "0 MB", .SubText = "Across all files", .AccentColor = ThemeManager.AccentColor, .Margin = New Padding(0, 0, 16, 0)}
            cardDataStorage = New MetricCard() With {.Title = "DATA FILES (MDF/NDF)", .Value = "0 MB", .SubText = "Row storage", .AccentColor = ThemeManager.SuccessColor, .Margin = New Padding(0, 0, 16, 0)}
            cardLogStorage = New MetricCard() With {.Title = "LOG FILES (LDF)", .Value = "0 MB", .SubText = "Transaction logs", .AccentColor = ThemeManager.WarningColor, .Margin = New Padding(0, 0, 0, 0)}

            pnlCards.Controls.AddRange({cardTotalStorage, cardDataStorage, cardLogStorage})
            pnlTop.Controls.Add(pnlCards)

            ' Storage Grid
            gridStorage = New ModernDataGrid() With {.Dock = DockStyle.Fill}
            AddHandler gridStorage.CellFormatting, AddressOf OnCellFormatting

            Controls.AddRange({gridStorage, pnlTop})
        End Sub

        Public Async Sub LoadStorageAsync()
            Dim server = _serverService.SelectedServer
            If server Is Nothing Then
                gridStorage.DataSource = Nothing
                Return
            End If

            btnRefresh.Enabled = False
            btnRefresh.Text = "Calculating..."

            Try
                Using provider = DatabaseProviderFactory.CreateProvider(server)
                    Dim files = Await provider.GetStorageInfoAsync()

                    Dim totalMb = files.Sum(Function(f) f.TotalSizeMB)
                    Dim dataMb = files.Where(Function(f) f.FileType.Contains("DATA") OrElse f.FileType.Contains("TABLE") OrElse f.FileType.Contains("ROWS")).Sum(Function(f) f.TotalSizeMB)
                    Dim logMb = files.Where(Function(f) f.FileType.Contains("LOG")).Sum(Function(f) f.TotalSizeMB)

                    cardTotalStorage.Value = FormatMb(totalMb)
                    cardDataStorage.Value = FormatMb(dataMb)
                    cardLogStorage.Value = FormatMb(logMb)

                    Dim dt As New DataTable()
                    dt.Columns.Add("Database", GetType(String))
                    dt.Columns.Add("File Name", GetType(String))
                    dt.Columns.Add("Type", GetType(String))
                    dt.Columns.Add("Total Size (MB)", GetType(Double))
                    dt.Columns.Add("Used Size (MB)", GetType(Double))
                    dt.Columns.Add("Free Size (MB)", GetType(Double))
                    dt.Columns.Add("Used %", GetType(Double))
                    dt.Columns.Add("Auto Growth", GetType(String))
                    dt.Columns.Add("Physical Path", GetType(String))

                    For Each f In files
                        dt.Rows.Add(f.DatabaseName, f.FileName, f.FileType, f.TotalSizeMB, f.UsedSizeMB, f.FreeSizeMB, f.UsedPercent, f.AutoGrowth, f.PhysicalPath)
                    Next

                    gridStorage.DataSource = dt

                    If gridStorage.Columns.Contains("Database") Then gridStorage.Columns("Database").Width = 130
                    If gridStorage.Columns.Contains("File Name") Then gridStorage.Columns("File Name").Width = 140
                    If gridStorage.Columns.Contains("Type") Then gridStorage.Columns("Type").Width = 90
                    If gridStorage.Columns.Contains("Used %") Then gridStorage.Columns("Used %").Width = 80
                    If gridStorage.Columns.Contains("Auto Growth") Then gridStorage.Columns("Auto Growth").Width = 110
                End Using
            Catch ex As Exception
                MessageBox.Show($"Failed to load storage details: {ex.Message}", "Storage Query Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                btnRefresh.Enabled = True
                btnRefresh.Text = "↻ Refresh Storage"
            End Try
        End Sub

        Private Function FormatMb(mb As Double) As String
            If mb >= 1024 * 1024 Then
                Return $"{mb / (1024.0 * 1024.0):F2} TB"
            ElseIf mb >= 1024 Then
                Return $"{mb / 1024.0:F1} GB"
            Else
                Return $"{mb:F0} MB"
            End If
        End Function

        Private Sub OnCellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
            If gridStorage.Columns(e.ColumnIndex).Name = "Used %" AndAlso e.Value IsNot Nothing Then
                Dim val As Double = 0
                If Double.TryParse(e.Value.ToString(), val) Then
                    If val >= 90.0 Then
                        e.CellStyle.ForeColor = ThemeManager.DangerColor
                        e.CellStyle.Font = ThemeManager.BoldFont
                    ElseIf val >= 75.0 Then
                        e.CellStyle.ForeColor = ThemeManager.WarningColor
                        e.CellStyle.Font = ThemeManager.BoldFont
                    End If
                End If
            End If
        End Sub

        Private Sub OnThemeChanged(sender As Object, e As EventArgs)
            BackColor = ThemeManager.BackgroundColor
            ThemeManager.StyleButton(btnRefresh, False)
        End Sub
    End Class
End Namespace
