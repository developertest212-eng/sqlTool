Imports System.Data
Imports System.Diagnostics
Imports System.Drawing
Imports System.Threading
Imports System.Windows.Forms
Imports SQLCommandCenter.Configuration
Imports SQLCommandCenter.Data
Imports SQLCommandCenter.Models
Imports SQLCommandCenter.Services
Imports SQLCommandCenter.UI.Controls
Imports SQLCommandCenter.UI.Forms
Imports SQLCommandCenter.UI.Theme

Namespace UI.Views
    Public Class SqlConsoleView
        Inherits UserControl

        Private ReadOnly _serverService As ServerService
        Private ReadOnly _queryService As QueryService

        Private _cts As CancellationTokenSource

        ' Controls
        Private txtSql As TextBox
        Private cmbDatabase As ComboBox
        Private btnExecute As Button
        Private btnCancel As Button
        Private btnClear As Button
        Private lblStats As Label
        Private tabResults As TabControl
        Private tabGrid As TabPage
        Private tabMessages As TabPage
        Private gridResults As ModernDataGrid
        Private txtMessages As TextBox
        Private splitConsole As SplitContainer

        Public Sub New(serverService As ServerService)
            _serverService = serverService
            _queryService = New QueryService()

            InitializeComponent()

            AddHandler _serverService.SelectedServerChanged, AddressOf OnSelectedServerChanged
            AddHandler ThemeManager.ThemeChanged, AddressOf OnThemeChanged

            LoadDatabasesDropdown()
        End Sub

        Private Sub InitializeComponent()
            Dock = DockStyle.Fill
            BackColor = ThemeManager.BackgroundColor
            Font = ThemeManager.BodyFont

            ' Top Command Bar
            Dim pnlTop As New Panel() With {
                .Dock = DockStyle.Top,
                .Height = 55,
                .Padding = New Padding(16, 12, 16, 12),
                .BackColor = ThemeManager.TopBarColor
            }

            Dim lblDb As New Label() With {.Text = "Database:", .Location = New Point(16, 18), .AutoSize = True, .ForeColor = ThemeManager.TextSecondaryColor}
            cmbDatabase = New ComboBox() With {.Location = New Point(80, 15), .Size = New Size(170, 24), .DropDownStyle = ComboBoxStyle.DropDownList}
            ThemeManager.StyleComboBox(cmbDatabase)

            btnExecute = New Button() With {
                .Text = "▶ Execute (F5)",
                .Location = New Point(265, 12),
                .Size = New Size(125, 30),
                .BackColor = ThemeManager.SuccessColor,
                .ForeColor = Color.FromArgb(17, 17, 27),
                .Font = ThemeManager.BoldFont,
                .FlatStyle = FlatStyle.Flat
            }
            btnExecute.FlatAppearance.BorderSize = 0
            AddHandler btnExecute.Click, AddressOf OnExecuteClicked

            btnCancel = New Button() With {
                .Text = "⬛ Cancel",
                .Location = New Point(400, 12),
                .Size = New Size(95, 30),
                .Enabled = False
            }
            ThemeManager.StyleButton(btnCancel, False)
            AddHandler btnCancel.Click, AddressOf OnCancelClicked

            btnClear = New Button() With {
                .Text = "Clear",
                .Location = New Point(505, 12),
                .Size = New Size(80, 30)
            }
            ThemeManager.StyleButton(btnClear, False)
            AddHandler btnClear.Click, Sub(s, e) txtSql.Clear()

            lblStats = New Label() With {
                .Text = "Ready",
                .Location = New Point(600, 18),
                .AutoSize = True,
                .Font = ThemeManager.SmallFont,
                .ForeColor = ThemeManager.TextSecondaryColor
            }

            pnlTop.Controls.AddRange({lblDb, cmbDatabase, btnExecute, btnCancel, btnClear, lblStats})

            ' Split Console: Top SQL Editor, Bottom Results
            splitConsole = New SplitContainer() With {
                .Dock = DockStyle.Fill,
                .Orientation = Orientation.Horizontal,
                .SplitterDistance = 220,
                .BackColor = ThemeManager.CardBorderColor
            }

            txtSql = New TextBox() With {
                .Dock = DockStyle.Fill,
                .Multiline = True,
                .AcceptsReturn = True,
                .AcceptsTab = True,
                .ScrollBars = ScrollBars.Both,
                .Font = ThemeManager.CodeFont,
                .BackColor = ThemeManager.InputBackgroundColor,
                .ForeColor = ThemeManager.TextPrimaryColor,
                .BorderStyle = BorderStyle.None,
                .Text = "-- Enter your SQL query here and press F5 or click Execute" & Environment.NewLine & "SELECT @@VERSION AS [ServerVersion], GETDATE() AS [CurrentTime];"
            }
            splitConsole.Panel1.Controls.Add(txtSql)

            ' Tabs for Results vs Messages
            tabResults = New TabControl() With {
                .Dock = DockStyle.Fill,
                .Font = ThemeManager.BodyFont
            }

            tabGrid = New TabPage("Results Grid")
            gridResults = New ModernDataGrid() With {.Dock = DockStyle.Fill}
            tabGrid.Controls.Add(gridResults)

            tabMessages = New TabPage("Messages & Output")
            txtMessages = New TextBox() With {
                .Dock = DockStyle.Fill,
                .Multiline = True,
                .ReadOnly = True,
                .ScrollBars = ScrollBars.Both,
                .Font = ThemeManager.CodeFont,
                .BackColor = ThemeManager.InputBackgroundColor,
                .ForeColor = ThemeManager.TextPrimaryColor,
                .BorderStyle = BorderStyle.None
            }
            tabMessages.Controls.Add(txtMessages)

            tabResults.TabPages.AddRange({tabGrid, tabMessages})
            splitConsole.Panel2.Controls.Add(tabResults)

            Controls.AddRange({splitConsole, pnlTop})
        End Sub

        Private Async Sub LoadDatabasesDropdown()
            Dim server = _serverService.SelectedServer
            cmbDatabase.Items.Clear()
            If server Is Nothing Then Return

            Try
                Using provider = DatabaseProviderFactory.CreateProvider(server)
                    Dim dbs = Await provider.GetDatabasesAsync()
                    For Each db In dbs
                        cmbDatabase.Items.Add(db.Name)
                    Next
                    If cmbDatabase.Items.Count > 0 Then
                        If Not String.IsNullOrEmpty(server.DefaultDatabase) AndAlso cmbDatabase.Items.Contains(server.DefaultDatabase) Then
                            cmbDatabase.SelectedItem = server.DefaultDatabase
                        Else
                            cmbDatabase.SelectedIndex = 0
                        End If
                    End If
                End Using
            Catch
                ' Fallback
            End Try
        End Sub

        Private Async Sub OnExecuteClicked(sender As Object, e As EventArgs)
            Dim server = _serverService.SelectedServer
            If server Is Nothing Then
                MessageBox.Show("Please select an active server before executing queries.", "No Server Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim sql = txtSql.Text.Trim()
            If String.IsNullOrWhiteSpace(sql) Then Return

            ' Check destructive safety guard
            If ConfigurationManager.Settings.ConfirmDestructiveQueries Then
                Dim analysis = _queryService.AnalyzeDestructiveOperations(sql)
                If analysis.IsDestructive Then
                    Using dlg As New ConfirmDestructiveDialog(analysis.DetectedKeyword, $"Database: {cmbDatabase.SelectedItem} on {server.Name}", sql)
                        If dlg.ShowDialog(FindForm()) <> DialogResult.OK Then
                            lblStats.Text = "Execution cancelled by user."
                            lblStats.ForeColor = ThemeManager.WarningColor
                            Return
                        End If
                    End Using
                End If
            End If

            _cts = New CancellationTokenSource()
            btnExecute.Enabled = False
            btnCancel.Enabled = True
            lblStats.Text = "Executing query..."
            lblStats.ForeColor = ThemeManager.AccentColor

            Dim dbName = cmbDatabase.SelectedItem?.ToString()
            Dim timeout = ConfigurationManager.Settings.DefaultQueryTimeoutSeconds

            Try
                Dim result = Await _queryService.ExecuteQueryAsync(server, sql, dbName, timeout, _cts.Token)

                gridResults.DataSource = result.ResultTable
                txtMessages.Text = result.Message & Environment.NewLine

                If result.RowsAffected >= 0 Then
                    txtMessages.AppendText($"({result.RowsAffected} row(s) affected)" & Environment.NewLine)
                End If

                lblStats.Text = $"✔ Success in {result.ElapsedMs} ms | {result.ResultTable.Rows.Count} row(s)"
                lblStats.ForeColor = ThemeManager.SuccessColor

                If result.ResultTable.Rows.Count > 0 Then
                    tabResults.SelectedTab = tabGrid
                Else
                    tabResults.SelectedTab = tabMessages
                End If
            Catch ex As OperationCanceledException
                lblStats.Text = "Query execution cancelled."
                lblStats.ForeColor = ThemeManager.WarningColor
                txtMessages.AppendText("Query was cancelled by user." & Environment.NewLine)
            Catch ex As Exception
                lblStats.Text = "Query failed."
                lblStats.ForeColor = ThemeManager.DangerColor
                txtMessages.AppendText($"Error: {ex.Message}" & Environment.NewLine)
                tabResults.SelectedTab = tabMessages
            Finally
                btnExecute.Enabled = True
                btnCancel.Enabled = False
                _cts = Nothing
            End Try
        End Sub

        Private Sub OnCancelClicked(sender As Object, e As EventArgs)
            If _cts IsNot Nothing Then
                _cts.Cancel()
                btnCancel.Enabled = False
            End If
        End Sub

        Protected Overrides Function ProcessCmdKey(ByRef msg As Message, keyData As Keys) As Boolean
            If keyData = Keys.F5 Then
                If btnExecute.Enabled Then
                    OnExecuteClicked(Nothing, EventArgs.Empty)
                    Return True
                End If
            End If
            Return MyBase.ProcessCmdKey(msg, keyData)
        End Function

        Private Sub OnSelectedServerChanged(sender As Object, e As ServerProfile)
            LoadDatabasesDropdown()
            If e IsNot Nothing Then
                If e.Engine = DatabaseEngine.SqlServer Then
                    txtSql.Text = "-- SQL Server Query" & Environment.NewLine & "SELECT @@VERSION AS [Version], DB_NAME() AS [CurrentDatabase];"
                Else
                    txtSql.Text = "-- MySQL Query" & Environment.NewLine & "SELECT VERSION() AS Version, DATABASE() AS CurrentDatabase;"
                End If
            End If
        End Sub

        Private Sub OnThemeChanged(sender As Object, e As EventArgs)
            BackColor = ThemeManager.BackgroundColor
            splitConsole.BackColor = ThemeManager.CardBorderColor
            txtSql.BackColor = ThemeManager.InputBackgroundColor
            txtSql.ForeColor = ThemeManager.TextPrimaryColor
            txtMessages.BackColor = ThemeManager.InputBackgroundColor
            txtMessages.ForeColor = ThemeManager.TextPrimaryColor
            ThemeManager.StyleComboBox(cmbDatabase)
            ThemeManager.StyleButton(btnCancel, False)
            ThemeManager.StyleButton(btnClear, False)
        End Sub
    End Class
End Namespace
