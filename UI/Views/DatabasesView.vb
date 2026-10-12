Imports System.Data
Imports System.Drawing
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports SQLCommandCenter.Data
Imports SQLCommandCenter.Models
Imports SQLCommandCenter.Services
Imports SQLCommandCenter.UI.Controls
Imports SQLCommandCenter.UI.Theme

Namespace UI.Views
    Public Class DatabasesView
        Inherits UserControl

        Private ReadOnly _serverService As ServerService
        Private _currentProvider As IDatabaseProvider

        ' Controls
        Private gridDatabases As ModernDataGrid
        Private gridColumns As ModernDataGrid
        Private treeExplorer As TreeView
        Private splitMain As SplitContainer
        Private splitDetails As SplitContainer
        Private btnRefresh As Button
        Private btnGenerateSelect As Button
        Private lblDetailsTitle As Label

        Public Sub New(serverService As ServerService)
            _serverService = serverService

            InitializeComponent()

            AddHandler _serverService.SelectedServerChanged, AddressOf OnSelectedServerChanged
            AddHandler ThemeManager.ThemeChanged, AddressOf OnThemeChanged

            LoadDatabasesAsync()
        End Sub

        Private Sub InitializeComponent()
            Dock = DockStyle.Fill
            BackColor = ThemeManager.BackgroundColor
            Font = ThemeManager.BodyFont

            Dim pnlTop As New Panel() With {
                .Dock = DockStyle.Top,
                .Height = 50,
                .Padding = New Padding(16, 10, 16, 10),
                .BackColor = ThemeManager.TopBarColor
            }

            Dim lblTitle As New Label() With {
                .Text = "DATABASE INVENTORY & SCHEMA EXPLORER",
                .Font = ThemeManager.HeaderFont,
                .ForeColor = ThemeManager.AccentColor,
                .Location = New Point(16, 14),
                .AutoSize = True
            }

            btnRefresh = New Button() With {
                .Text = "↻ Refresh",
                .Location = New Point(420, 10),
                .Size = New Size(95, 30)
            }
            ThemeManager.StyleButton(btnRefresh, False)
            AddHandler btnRefresh.Click, Sub(s, e) LoadDatabasesAsync()

            btnGenerateSelect = New Button() With {
                .Text = "📋 Generate SELECT",
                .Location = New Point(525, 10),
                .Size = New Size(140, 30)
            }
            ThemeManager.StyleButton(btnGenerateSelect, False)
            AddHandler btnGenerateSelect.Click, AddressOf OnGenerateSelectClicked

            pnlTop.Controls.AddRange({lblTitle, btnRefresh, btnGenerateSelect})

            ' Split Containers
            splitMain = New SplitContainer() With {
                .Dock = DockStyle.Fill,
                .Orientation = Orientation.Horizontal,
                .SplitterDistance = 240,
                .BackColor = ThemeManager.CardBorderColor
            }

            ' Top half: Databases Overview Grid
            gridDatabases = New ModernDataGrid() With {.Dock = DockStyle.Fill}
            AddHandler gridDatabases.SelectionChanged, AddressOf OnDatabaseSelected
            splitMain.Panel1.Controls.Add(gridDatabases)

            ' Bottom half: Schema Tree + Columns Grid
            splitDetails = New SplitContainer() With {
                .Dock = DockStyle.Fill,
                .Orientation = Orientation.Vertical,
                .SplitterDistance = 320,
                .BackColor = ThemeManager.CardBorderColor
            }

            treeExplorer = New TreeView() With {
                .Dock = DockStyle.Fill,
                .BorderStyle = BorderStyle.None,
                .BackColor = ThemeManager.CardColor,
                .ForeColor = ThemeManager.TextPrimaryColor,
                .Font = ThemeManager.BodyFont
            }
            AddHandler treeExplorer.AfterSelect, AddressOf OnTreeNodeSelected
            splitDetails.Panel1.Controls.Add(treeExplorer)

            Dim pnlRightDetails As New Panel() With {.Dock = DockStyle.Fill, .BackColor = ThemeManager.CardColor}
            lblDetailsTitle = New Label() With {
                .Dock = DockStyle.Top,
                .Height = 32,
                .Text = "Table Columns & Schema Definition",
                .Font = ThemeManager.BoldFont,
                .ForeColor = ThemeManager.TextSecondaryColor,
                .Padding = New Padding(10, 8, 0, 0)
            }
            gridColumns = New ModernDataGrid() With {.Dock = DockStyle.Fill}
            pnlRightDetails.Controls.AddRange({gridColumns, lblDetailsTitle})
            splitDetails.Panel2.Controls.Add(pnlRightDetails)

            splitMain.Panel2.Controls.Add(splitDetails)

            Controls.AddRange({splitMain, pnlTop})
        End Sub

        Public Async Sub LoadDatabasesAsync()
            Dim server = _serverService.SelectedServer
            If server Is Nothing Then
                gridDatabases.DataSource = Nothing
                treeExplorer.Nodes.Clear()
                Return
            End If

            btnRefresh.Enabled = False
            btnRefresh.Text = "Loading..."

            Try
                Using provider = DatabaseProviderFactory.CreateProvider(server)
                    Dim dbs = Await provider.GetDatabasesAsync()

                    Dim dt As New DataTable()
                    dt.Columns.Add("Database Name", GetType(String))
                    dt.Columns.Add("Status", GetType(String))
                    dt.Columns.Add("Total Size (MB)", GetType(Double))
                    dt.Columns.Add("Data Size (MB)", GetType(Double))
                    dt.Columns.Add("Log Size (MB)", GetType(Double))
                    dt.Columns.Add("Recovery Model", GetType(String))
                    dt.Columns.Add("Collation", GetType(String))
                    dt.Columns.Add("Compatibility", GetType(String))
                    dt.Columns.Add("Created Date", GetType(DateTime))

                    For Each db In dbs
                        dt.Rows.Add(db.Name, db.Status, db.TotalSizeMB, db.DataSizeMB, db.LogSizeMB, db.RecoveryModel, db.Collation, db.CompatibilityLevel, db.CreatedDate)
                    Next

                    gridDatabases.DataSource = dt

                    ' Populate Tree root
                    treeExplorer.Nodes.Clear()
                    Dim rootNode As New TreeNode($"{server.Name} ({server.Engine})") With {.Tag = "SERVER"}
                    For Each db In dbs
                        Dim dbNode As New TreeNode(db.Name) With {.Tag = $"DB:{db.Name}"}
                        dbNode.Nodes.Add(New TreeNode("Loading schema..."))
                        rootNode.Nodes.Add(dbNode)
                    Next
                    treeExplorer.Nodes.Add(rootNode)
                    rootNode.Expand()
                End Using
            Catch ex As Exception
                MessageBox.Show($"Failed to load databases: {ex.Message}", "Database Query Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                btnRefresh.Enabled = True
                btnRefresh.Text = "↻ Refresh"
            End Try
        End Sub

        Private Async Sub OnDatabaseSelected(sender As Object, e As EventArgs)
            If gridDatabases.SelectedRows.Count = 0 Then Return
            Dim dbName = gridDatabases.SelectedRows(0).Cells("Database Name")?.Value?.ToString()
            If String.IsNullOrEmpty(dbName) Then Return

            ' Expand node in tree
            If treeExplorer.Nodes.Count > 0 Then
                For Each node As TreeNode In treeExplorer.Nodes(0).Nodes
                    If node.Text = dbName Then
                        treeExplorer.SelectedNode = node
                        Await ExpandDatabaseNodeAsync(node, dbName)
                        Exit For
                    End If
                Next
            End If
        End Sub

        Private Async Function ExpandDatabaseNodeAsync(dbNode As TreeNode, dbName As String) As Task
            Dim server = _serverService.SelectedServer
            If server Is Nothing Then Return

            dbNode.Nodes.Clear()
            Dim tablesNode As New TreeNode("Tables") With {.Tag = $"FOLDER_TABLES:{dbName}"}
            Dim viewsNode As New TreeNode("Views") With {.Tag = $"FOLDER_VIEWS:{dbName}"}
            Dim procsNode As New TreeNode("Stored Procedures") With {.Tag = $"FOLDER_PROCS:{dbName}"}
            dbNode.Nodes.AddRange({tablesNode, viewsNode, procsNode})

            Try
                Using provider = DatabaseProviderFactory.CreateProvider(server)
                    Dim objects = Await provider.GetDatabaseObjectsAsync(dbName)

                    For Each obj In objects
                        Dim objNode As New TreeNode(obj.FullName) With {
                            .Tag = $"OBJ:{dbName}:{obj.SchemaName}:{obj.Name}:{obj.ObjectType}"
                        }

                        Select Case obj.ObjectType
                            Case DatabaseObjectType.Table
                                tablesNode.Nodes.Add(objNode)
                            Case DatabaseObjectType.View
                                viewsNode.Nodes.Add(objNode)
                            Case Else
                                procsNode.Nodes.Add(objNode)
                        End Select
                    Next
                End Using
                dbNode.Expand()
                tablesNode.Expand()
            Catch ex As Exception
                tablesNode.Nodes.Add(New TreeNode($"Error: {ex.Message}"))
            End Try
        End Function

        Private Async Sub OnTreeNodeSelected(sender As Object, e As TreeViewEventArgs)
            Dim tag = e.Node?.Tag?.ToString()
            If String.IsNullOrEmpty(tag) Then Return

            If tag.StartsWith("DB:") Then
                Dim dbName = tag.Substring(3)
                Await ExpandDatabaseNodeAsync(e.Node, dbName)
            ElseIf tag.StartsWith("OBJ:") Then
                Dim parts = tag.Split(":"c)
                If parts.Length >= 5 Then
                    Dim dbName = parts(1)
                    Dim schema = parts(2)
                    Dim table = parts(3)
                    Await LoadTableColumnsAsync(dbName, schema, table)
                End If
            End If
        End Sub

        Private Async Function LoadTableColumnsAsync(databaseName As String, schemaName As String, tableName As String) As Task
            Dim server = _serverService.SelectedServer
            If server Is Nothing Then Return

            lblDetailsTitle.Text = $"Columns for [{schemaName}].[{tableName}] in database '{databaseName}'"

            Try
                Using provider = DatabaseProviderFactory.CreateProvider(server)
                    Dim columns = Await provider.GetTableColumnsAsync(databaseName, schemaName, tableName)

                    Dim dt As New DataTable()
                    dt.Columns.Add("Primary Key", GetType(String))
                    dt.Columns.Add("Column Name", GetType(String))
                    dt.Columns.Add("Data Type", GetType(String))
                    dt.Columns.Add("Max Length", GetType(String))
                    dt.Columns.Add("Nullable", GetType(String))
                    dt.Columns.Add("Default Value", GetType(String))

                    For Each c In columns
                        Dim pkStr = If(c.IsPrimaryKey, "🔑 PK", "")
                        Dim lenStr = If(c.MaxLength = -1, "MAX", If(c.MaxLength > 0, c.MaxLength.ToString(), "-"))
                        Dim nullStr = If(c.IsNullable, "YES", "NO")
                        dt.Rows.Add(pkStr, c.ColumnName, c.DataType, lenStr, nullStr, c.DefaultValue)
                    Next

                    gridColumns.DataSource = dt
                End Using
            Catch ex As Exception
                lblDetailsTitle.Text = $"Error loading columns: {ex.Message}"
            End Try
        End Function

        Private Sub OnGenerateSelectClicked(sender As Object, e As EventArgs)
            Dim selNode = treeExplorer.SelectedNode
            If selNode Is Nothing OrElse selNode.Tag Is Nothing Then
                MessageBox.Show("Please select a table or view in the database tree.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim tag = selNode.Tag.ToString()
            If Not tag.StartsWith("OBJ:") Then
                MessageBox.Show("Please select a table or view.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim parts = tag.Split(":"c)
            Dim db = parts(1)
            Dim schema = parts(2)
            Dim tbl = parts(3)

            Dim sql = $"SELECT TOP 100 * FROM [{db}].[{schema}].[{tbl}];"
            If _serverService.SelectedServer?.Engine = DatabaseEngine.MySql Then
                sql = $"SELECT * FROM `{db}`.`{tbl}` LIMIT 100;"
            End If

            Clipboard.SetText(sql)
            MessageBox.Show($"Generated SELECT statement copied to clipboard:{Environment.NewLine}{Environment.NewLine}{sql}", "SQL Copied", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End Sub

        Private Sub OnSelectedServerChanged(sender As Object, e As ServerProfile)
            LoadDatabasesAsync()
        End Sub

        Private Sub OnThemeChanged(sender As Object, e As EventArgs)
            BackColor = ThemeManager.BackgroundColor
            treeExplorer.BackColor = ThemeManager.CardColor
            treeExplorer.ForeColor = ThemeManager.TextPrimaryColor
            splitMain.BackColor = ThemeManager.CardBorderColor
            splitDetails.BackColor = ThemeManager.CardBorderColor
            ThemeManager.StyleButton(btnRefresh, False)
            ThemeManager.StyleButton(btnGenerateSelect, False)
        End Sub
    End Class
End Namespace
