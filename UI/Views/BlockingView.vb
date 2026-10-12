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
    Public Class BlockingView
        Inherits UserControl

        Private ReadOnly _serverService As ServerService
        Private _blockingList As New List(Of BlockingInfo)()

        ' Controls
        Private gridBlocking As ModernDataGrid
        Private treeBlocking As TreeView
        Private splitBlocking As SplitContainer
        Private btnRefresh As Button
        Private btnKillBlocker As Button
        Private lblStatus As Label

        Public Sub New(serverService As ServerService)
            _serverService = serverService

            InitializeComponent()

            AddHandler _serverService.SelectedServerChanged, Sub(s, e) LoadBlockingAsync()
            AddHandler ThemeManager.ThemeChanged, AddressOf OnThemeChanged

            LoadBlockingAsync()
        End Sub

        Private Sub InitializeComponent()
            Dock = DockStyle.Fill
            BackColor = ThemeManager.BackgroundColor
            Font = ThemeManager.BodyFont

            ' Top Bar
            Dim pnlTop As New Panel() With {
                .Dock = DockStyle.Top,
                .Height = 55,
                .Padding = New Padding(16, 12, 16, 12),
                .BackColor = ThemeManager.TopBarColor
            }

            Dim lblTitle As New Label() With {
                .Text = "LOCK CONTENTION & BLOCKING CHAINS",
                .Font = ThemeManager.HeaderFont,
                .ForeColor = ThemeManager.AccentColor,
                .Location = New Point(16, 18),
                .AutoSize = True
            }

            btnRefresh = New Button() With {.Text = "↻ Refresh", .Location = New Point(380, 12), .Size = New Size(95, 30)}
            ThemeManager.StyleButton(btnRefresh, False)
            AddHandler btnRefresh.Click, Sub(s, e) LoadBlockingAsync()

            btnKillBlocker = New Button() With {
                .Text = "🛑 Kill Lead Blocker",
                .Location = New Point(485, 12),
                .Size = New Size(140, 30),
                .BackColor = ThemeManager.DangerColor,
                .ForeColor = Color.White,
                .FlatStyle = FlatStyle.Flat,
                .Font = ThemeManager.BoldFont
            }
            btnKillBlocker.FlatAppearance.BorderSize = 0
            AddHandler btnKillBlocker.Click, AddressOf OnKillBlockerClicked

            lblStatus = New Label() With {
                .Text = "No Blocking Detected",
                .Location = New Point(640, 18),
                .AutoSize = True,
                .Font = ThemeManager.BoldFont,
                .ForeColor = ThemeManager.SuccessColor
            }

            pnlTop.Controls.AddRange({lblTitle, btnRefresh, btnKillBlocker, lblStatus})

            ' Split Container: Tree on Left, Grid on Right
            splitBlocking = New SplitContainer() With {
                .Dock = DockStyle.Fill,
                .Orientation = Orientation.Vertical,
                .SplitterDistance = 360,
                .BackColor = ThemeManager.CardBorderColor
            }

            Dim pnlTreeHost As New Panel() With {.Dock = DockStyle.Fill, .BackColor = ThemeManager.CardColor}
            Dim lblTreeHeader As New Label() With {
                .Dock = DockStyle.Top,
                .Height = 30,
                .Text = "Hierarchical Blocking Tree",
                .Font = ThemeManager.BoldFont,
                .ForeColor = ThemeManager.TextSecondaryColor,
                .Padding = New Padding(10, 8, 0, 0)
            }
            treeBlocking = New TreeView() With {
                .Dock = DockStyle.Fill,
                .BorderStyle = BorderStyle.None,
                .BackColor = ThemeManager.CardColor,
                .ForeColor = ThemeManager.TextPrimaryColor,
                .Font = ThemeManager.BodyFont
            }
            pnlTreeHost.Controls.AddRange({treeBlocking, lblTreeHeader})
            splitBlocking.Panel1.Controls.Add(pnlTreeHost)

            gridBlocking = New ModernDataGrid() With {.Dock = DockStyle.Fill}
            splitBlocking.Panel2.Controls.Add(gridBlocking)

            Controls.AddRange({splitBlocking, pnlTop})
        End Sub

        Public Async Sub LoadBlockingAsync()
            Dim server = _serverService.SelectedServer
            If server Is Nothing Then
                gridBlocking.DataSource = Nothing
                treeBlocking.Nodes.Clear()
                lblStatus.Text = "No Server Selected"
                lblStatus.ForeColor = ThemeManager.TextMutedColor
                Return
            End If

            btnRefresh.Enabled = False
            btnRefresh.Text = "Scanning..."

            Try
                Using provider = DatabaseProviderFactory.CreateProvider(server)
                    _blockingList = Await provider.GetBlockingInfoAsync()

                    If _blockingList.Count = 0 Then
                        lblStatus.Text = "✔ No Blocking Detected (Healthy)"
                        lblStatus.ForeColor = ThemeManager.SuccessColor
                    Else
                        lblStatus.Text = $"⚠️ {_blockingList.Count} Blocking Relationship(s) Active"
                        lblStatus.ForeColor = ThemeManager.DangerColor
                    End If

                    ' Populate Grid
                    Dim dt As New DataTable()
                    dt.Columns.Add("Blocker Session", GetType(Integer))
                    dt.Columns.Add("Blocked Session", GetType(Integer))
                    dt.Columns.Add("Wait Sec", GetType(Double))
                    dt.Columns.Add("Database", GetType(String))
                    dt.Columns.Add("Blocker User", GetType(String))
                    dt.Columns.Add("Blocked User", GetType(String))
                    dt.Columns.Add("Wait Type", GetType(String))
                    dt.Columns.Add("Resource / Lock", GetType(String))
                    dt.Columns.Add("Blocker SQL", GetType(String))
                    dt.Columns.Add("Blocked SQL", GetType(String))

                    For Each b In _blockingList
                        dt.Rows.Add(b.BlockerSessionId, b.BlockedSessionId, b.WaitDurationSeconds, b.DatabaseName, b.BlockerUser, b.BlockedUser, b.WaitType, b.ResourceDescription, b.BlockerQuery, b.BlockedQuery)
                    Next

                    gridBlocking.DataSource = dt

                    ' Populate Tree
                    treeBlocking.Nodes.Clear()
                    If _blockingList.Count > 0 Then
                        Dim blockers = _blockingList.Select(Function(b) b.BlockerSessionId).Distinct()
                        For Each blkId In blockers
                            Dim id = blkId
                            Dim blkNode As New TreeNode($"Lead Blocker: SPID {id}") With {.Tag = id, .ForeColor = ThemeManager.DangerColor}

                            Dim blockedSessions = _blockingList.Where(Function(b) b.BlockerSessionId = id)
                            For Each b In blockedSessions
                                Dim item = b
                                Dim childNode As New TreeNode($"➔ Blocked: SPID {item.BlockedSessionId} (Waited {item.WaitDurationSeconds}s on {item.WaitType})") With {.Tag = item.BlockedSessionId}
                                blkNode.Nodes.Add(childNode)
                            Next

                            treeBlocking.Nodes.Add(blkNode)
                            blkNode.ExpandAll()
                        Next
                    Else
                        treeBlocking.Nodes.Add(New TreeNode("No active locks or blocking chains."))
                    End If
                End Using
            Catch ex As Exception
                lblStatus.Text = "Error scanning locks"
                lblStatus.ForeColor = ThemeManager.DangerColor
            Finally
                btnRefresh.Enabled = True
                btnRefresh.Text = "↻ Refresh"
            End Try
        End Sub

        Private Async Sub OnKillBlockerClicked(sender As Object, e As EventArgs)
            If _blockingList.Count = 0 Then
                MessageBox.Show("There are no active blocking sessions to terminate.", "No Blocker", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim server = _serverService.SelectedServer
            If server Is Nothing Then Return

            ' Find lead blocker
            Dim leadBlockerId = _blockingList.First().BlockerSessionId
            If leadBlockerId <= 0 Then Return

            Dim blockerItem = _blockingList.First()
            Dim queryPreview = If(Not String.IsNullOrEmpty(blockerItem.BlockerQuery), blockerItem.BlockerQuery, "KILL " & leadBlockerId)

            Using dlg As New ConfirmDestructiveDialog("KILL BLOCKER", $"Blocker Session {leadBlockerId} on {server.Name}", queryPreview)
                If dlg.ShowDialog(FindForm()) = DialogResult.OK Then
                    Try
                        Using provider = DatabaseProviderFactory.CreateProvider(server)
                            Dim res = Await provider.KillSessionAsync(leadBlockerId)
                            If res.Success Then
                                MessageBox.Show(res.Message, "Blocker Terminated", MessageBoxButtons.OK, MessageBoxIcon.Information)
                                LoadBlockingAsync()
                            Else
                                MessageBox.Show(res.Message, "Kill Failed", MessageBoxButtons.OK, MessageBoxIcon.Error)
                            End If
                        End Using
                    Catch ex As Exception
                        MessageBox.Show($"Error terminating blocker session: {ex.Message}", "Session Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                    End Try
                End If
            End Using
        End Sub

        Private Sub OnThemeChanged(sender As Object, e As EventArgs)
            BackColor = ThemeManager.BackgroundColor
            splitBlocking.BackColor = ThemeManager.CardBorderColor
            treeBlocking.BackColor = ThemeManager.CardColor
            treeBlocking.ForeColor = ThemeManager.TextPrimaryColor
            ThemeManager.StyleButton(btnRefresh, False)
        End Sub
    End Class
End Namespace
