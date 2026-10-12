Imports System.Data
Imports System.Drawing
Imports System.Windows.Forms
Imports SQLCommandCenter.Models
Imports SQLCommandCenter.Services
Imports SQLCommandCenter.UI.Controls
Imports SQLCommandCenter.UI.Theme

Namespace UI.Views
    Public Class AlertsView
        Inherits UserControl

        Private ReadOnly _alertService As AlertService

        ' Controls
        Private gridAlerts As ModernDataGrid
        Private cmbSeverityFilter As ComboBox
        Private chkActiveOnly As CheckBox
        Private btnAcknowledge As Button
        Private btnResolve As Button
        Private btnClearAll As Button
        Private lblActiveCount As Label

        Public Sub New(alertService As AlertService)
            _alertService = alertService

            InitializeComponent()

            AddHandler _alertService.AlertsChanged, AddressOf RefreshGrid
            AddHandler ThemeManager.ThemeChanged, AddressOf OnThemeChanged

            RefreshGrid()
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

            Dim lblSeverity As New Label() With {.Text = "Severity:", .Location = New Point(16, 18), .AutoSize = True, .ForeColor = ThemeManager.TextSecondaryColor}
            cmbSeverityFilter = New ComboBox() With {.Location = New Point(75, 15), .Size = New Size(110, 24), .DropDownStyle = ComboBoxStyle.DropDownList}
            cmbSeverityFilter.Items.AddRange({"All", "Critical", "Warning", "Info"})
            cmbSeverityFilter.SelectedIndex = 0
            ThemeManager.StyleComboBox(cmbSeverityFilter)
            AddHandler cmbSeverityFilter.SelectedIndexChanged, Sub(s, e) RefreshGrid()

            chkActiveOnly = New CheckBox() With {
                .Text = "Active Only",
                .Location = New Point(200, 17),
                .AutoSize = True,
                .Checked = True,
                .ForeColor = ThemeManager.TextPrimaryColor
            }
            AddHandler chkActiveOnly.CheckedChanged, Sub(s, e) RefreshGrid()

            btnAcknowledge = New Button() With {.Text = "✔ Acknowledge", .Location = New Point(310, 13), .Size = New Size(120, 28)}
            ThemeManager.StyleButton(btnAcknowledge, False)
            AddHandler btnAcknowledge.Click, AddressOf OnAcknowledgeClicked

            btnResolve = New Button() With {.Text = "★ Resolve", .Location = New Point(440, 13), .Size = New Size(95, 28)}
            ThemeManager.StyleButton(btnResolve, False)
            AddHandler btnResolve.Click, AddressOf OnResolveClicked

            btnClearAll = New Button() With {.Text = "🗑 Clear All", .Location = New Point(545, 13), .Size = New Size(95, 28)}
            ThemeManager.StyleButton(btnClearAll, False)
            AddHandler btnClearAll.Click, AddressOf OnClearAllClicked

            lblActiveCount = New Label() With {
                .Text = "0 Active Alerts",
                .Location = New Point(660, 18),
                .AutoSize = True,
                .Font = ThemeManager.BoldFont,
                .ForeColor = ThemeManager.DangerColor
            }

            pnlTop.Controls.AddRange({lblSeverity, cmbSeverityFilter, chkActiveOnly, btnAcknowledge, btnResolve, btnClearAll, lblActiveCount})

            ' Main Grid
            gridAlerts = New ModernDataGrid() With {.Dock = DockStyle.Fill}
            AddHandler gridAlerts.CellFormatting, AddressOf OnCellFormatting

            Controls.AddRange({gridAlerts, pnlTop})
        End Sub

        Public Sub RefreshGrid()
            If InvokeRequired Then
                BeginInvoke(New Action(AddressOf RefreshGrid))
                Return
            End If

            Dim allAlerts = If(chkActiveOnly.Checked, _alertService.GetActiveAlerts(), _alertService.GetAllAlerts())
            Dim selectedSeverity = cmbSeverityFilter.SelectedItem?.ToString()

            Dim filtered = allAlerts.Where(Function(a)
                                              If selectedSeverity = "All" OrElse String.IsNullOrEmpty(selectedSeverity) Then Return True
                                              Return a.Severity.ToString().Equals(selectedSeverity, StringComparison.OrdinalIgnoreCase)
                                          End Function).ToList()

            Dim activeCount = _alertService.GetActiveAlerts().Count
            lblActiveCount.Text = $"{activeCount} Active Alert(s)"
            lblActiveCount.ForeColor = If(activeCount > 0, ThemeManager.DangerColor, ThemeManager.SuccessColor)

            Dim dt As New DataTable()
            dt.Columns.Add("Id", GetType(String))
            dt.Columns.Add("Severity", GetType(String))
            dt.Columns.Add("Timestamp", GetType(DateTime))
            dt.Columns.Add("Server Name", GetType(String))
            dt.Columns.Add("Database", GetType(String))
            dt.Columns.Add("Title", GetType(String))
            dt.Columns.Add("Message", GetType(String))
            dt.Columns.Add("Status", GetType(String))

            For Each a In filtered
                dt.Rows.Add(a.Id, a.SeverityDisplay, a.Timestamp, a.ServerName, a.DatabaseName, a.Title, a.Message, a.Status.ToString())
            Next

            gridAlerts.DataSource = dt

            If gridAlerts.Columns.Contains("Id") Then gridAlerts.Columns("Id").Visible = False
            If gridAlerts.Columns.Contains("Severity") Then gridAlerts.Columns("Severity").Width = 90
            If gridAlerts.Columns.Contains("Timestamp") Then gridAlerts.Columns("Timestamp").Width = 145
            If gridAlerts.Columns.Contains("Server Name") Then gridAlerts.Columns("Server Name").Width = 140
            If gridAlerts.Columns.Contains("Status") Then gridAlerts.Columns("Status").Width = 100
        End Sub

        Private Function GetSelectedAlertId() As String
            If gridAlerts.SelectedRows.Count = 0 Then Return Nothing
            Return gridAlerts.SelectedRows(0).Cells("Id")?.Value?.ToString()
        End Function

        Private Sub OnAcknowledgeClicked(sender As Object, e As EventArgs)
            Dim id = GetSelectedAlertId()
            If Not String.IsNullOrEmpty(id) Then
                _alertService.AcknowledgeAlert(id)
                RefreshGrid()
            End If
        End Sub

        Private Sub OnResolveClicked(sender As Object, e As EventArgs)
            Dim id = GetSelectedAlertId()
            If Not String.IsNullOrEmpty(id) Then
                _alertService.ResolveAlert(id)
                RefreshGrid()
            End If
        End Sub

        Private Sub OnClearAllClicked(sender As Object, e As EventArgs)
            Dim res = MessageBox.Show("Are you sure you want to clear all recorded alerts?", "Clear Alerts", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
            If res = DialogResult.Yes Then
                _alertService.ClearAll()
                RefreshGrid()
            End If
        End Sub

        Private Sub OnCellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
            If gridAlerts.Columns(e.ColumnIndex).Name = "Severity" AndAlso e.Value IsNot Nothing Then
                Dim sev = e.Value.ToString().ToUpperInvariant()
                Select Case sev
                    Case "CRITICAL"
                        e.CellStyle.ForeColor = ThemeManager.DangerColor
                        e.CellStyle.Font = ThemeManager.BoldFont
                    Case "WARNING"
                        e.CellStyle.ForeColor = ThemeManager.WarningColor
                        e.CellStyle.Font = ThemeManager.BoldFont
                    Case "INFO"
                        e.CellStyle.ForeColor = ThemeManager.AccentColor
                End Select
            End If
        End Sub

        Private Sub OnThemeChanged(sender As Object, e As EventArgs)
            BackColor = ThemeManager.BackgroundColor
            ThemeManager.StyleComboBox(cmbSeverityFilter)
            ThemeManager.StyleButton(btnAcknowledge, False)
            ThemeManager.StyleButton(btnResolve, False)
            ThemeManager.StyleButton(btnClearAll, False)
        End Sub
    End Class
End Namespace
