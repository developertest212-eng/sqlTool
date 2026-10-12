Imports System.Drawing
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports SQLCommandCenter.Data
Imports SQLCommandCenter.Models
Imports SQLCommandCenter.UI.Theme

Namespace UI.Forms
    Public Class ServerEditDialog
        Inherits Form

        Private ReadOnly _profile As ServerProfile
        Private _isEditMode As Boolean = False

        ' Controls
        Private txtName As TextBox
        Private txtGroup As TextBox
        Private cmbEngine As ComboBox
        Private txtHost As TextBox
        Private numPort As NumericUpDown
        Private cmbAuthType As ComboBox
        Private txtUser As TextBox
        Private txtPassword As TextBox
        Private txtDefaultDb As TextBox
        Private chkEncrypt As CheckBox
        Private chkTrustCert As CheckBox
        Private cmbSslMode As ComboBox
        Private lblTestResult As Label
        Private btnTest As Button
        Private btnSave As Button
        Private btnCancel As Button
        Private pnlAuthFields As Panel
        Private pnlMySqlSsl As Panel

        Public Property ResultProfile As ServerProfile

        Public Sub New(Optional existingProfile As ServerProfile = Nothing)
            _isEditMode = (existingProfile IsNot Nothing)
            _profile = If(existingProfile IsNot Nothing, existingProfile.Clone(), New ServerProfile())

            InitializeComponent()
            LoadProfileData()
        End Sub

        Private Sub InitializeComponent()
            Text = If(_isEditMode, "Edit Server Connection", "Add New Server Connection")
            Size = New Size(540, 680)
            StartPosition = FormStartPosition.CenterParent
            FormBorderStyle = FormBorderStyle.FixedDialog
            MaximizeBox = False
            MinimizeBox = False
            BackColor = ThemeManager.BackgroundColor
            ForeColor = ThemeManager.TextPrimaryColor
            Font = ThemeManager.BodyFont

            Dim pnlContent As New Panel() With {
                .Dock = DockStyle.Fill,
                .Padding = New Padding(20),
                .AutoScroll = True
            }

            Dim y = 15

            ' Header
            Dim lblTitle As New Label() With {
                .Text = If(_isEditMode, "Server Connection Properties", "Configure New Server Profile"),
                .Font = ThemeManager.TitleFont,
                .ForeColor = ThemeManager.AccentColor,
                .Location = New Point(20, y),
                .AutoSize = True
            }
            pnlContent.Controls.Add(lblTitle)
            y += 35

            ' Profile Name
            pnlContent.Controls.Add(CreateLabel("Profile Name:", 20, y))
            txtName = New TextBox() With {.Location = New Point(150, y), .Size = New Size(340, 24)}
            ThemeManager.StyleTextBox(txtName)
            pnlContent.Controls.Add(txtName)
            y += 35

            ' Group Name
            pnlContent.Controls.Add(CreateLabel("Group:", 20, y))
            txtGroup = New TextBox() With {.Location = New Point(150, y), .Size = New Size(340, 24), .Text = "Default"}
            ThemeManager.StyleTextBox(txtGroup)
            pnlContent.Controls.Add(txtGroup)
            y += 35

            ' Database Engine
            pnlContent.Controls.Add(CreateLabel("Database Engine:", 20, y))
            cmbEngine = New ComboBox() With {.Location = New Point(150, y), .Size = New Size(340, 24), .DropDownStyle = ComboBoxStyle.DropDownList}
            cmbEngine.Items.AddRange({"Microsoft SQL Server", "MySQL"})
            cmbEngine.SelectedIndex = 0
            ThemeManager.StyleComboBox(cmbEngine)
            AddHandler cmbEngine.SelectedIndexChanged, AddressOf OnEngineChanged
            pnlContent.Controls.Add(cmbEngine)
            y += 35

            ' Host / IP
            pnlContent.Controls.Add(CreateLabel("Server Host / IP:", 20, y))
            txtHost = New TextBox() With {.Location = New Point(150, y), .Size = New Size(220, 24), .Text = "localhost"}
            ThemeManager.StyleTextBox(txtHost)
            pnlContent.Controls.Add(txtHost)

            ' Port
            pnlContent.Controls.Add(CreateLabel("Port:", 380, y))
            numPort = New NumericUpDown() With {.Location = New Point(420, y), .Size = New Size(70, 24), .Minimum = 1, .Maximum = 65535, .Value = 1433}
            numPort.BackColor = ThemeManager.InputBackgroundColor
            numPort.ForeColor = ThemeManager.TextPrimaryColor
            pnlContent.Controls.Add(numPort)
            y += 35

            ' Authentication Type
            pnlContent.Controls.Add(CreateLabel("Authentication:", 20, y))
            cmbAuthType = New ComboBox() With {.Location = New Point(150, y), .Size = New Size(340, 24), .DropDownStyle = ComboBoxStyle.DropDownList}
            cmbAuthType.Items.AddRange({"SQL Server Authentication", "Windows Authentication"})
            cmbAuthType.SelectedIndex = 0
            ThemeManager.StyleComboBox(cmbAuthType)
            AddHandler cmbAuthType.SelectedIndexChanged, AddressOf OnAuthTypeChanged
            pnlContent.Controls.Add(cmbAuthType)
            y += 35

            ' Panel for Username & Password
            pnlAuthFields = New Panel() With {.Location = New Point(20, y), .Size = New Size(480, 70)}
            pnlAuthFields.Controls.Add(CreateLabel("Username:", 0, 0))
            txtUser = New TextBox() With {.Location = New Point(130, 0), .Size = New Size(340, 24)}
            ThemeManager.StyleTextBox(txtUser)
            pnlAuthFields.Controls.Add(txtUser)

            pnlAuthFields.Controls.Add(CreateLabel("Password:", 0, 35))
            txtPassword = New TextBox() With {.Location = New Point(130, 35), .Size = New Size(340, 24), .UseSystemPasswordChar = True}
            ThemeManager.StyleTextBox(txtPassword)
            pnlAuthFields.Controls.Add(txtPassword)
            pnlContent.Controls.Add(pnlAuthFields)
            y += 75

            ' Default Database
            pnlContent.Controls.Add(CreateLabel("Default Database:", 20, y))
            txtDefaultDb = New TextBox() With {.Location = New Point(150, y), .Size = New Size(340, 24)}
            ThemeManager.StyleTextBox(txtDefaultDb)
            pnlContent.Controls.Add(txtDefaultDb)
            y += 35

            ' SSL / Encryption Options
            chkEncrypt = New CheckBox() With {.Text = "Encrypt Connection", .Location = New Point(150, y), .AutoSize = True, .ForeColor = ThemeManager.TextPrimaryColor}
            chkTrustCert = New CheckBox() With {.Text = "Trust Server Certificate", .Location = New Point(290, y), .AutoSize = True, .Checked = True, .ForeColor = ThemeManager.TextPrimaryColor}
            pnlContent.Controls.AddRange({chkEncrypt, chkTrustCert})
            y += 30

            ' MySQL SSL Mode
            pnlMySqlSsl = New Panel() With {.Location = New Point(20, y), .Size = New Size(480, 35), .Visible = False}
            pnlMySqlSsl.Controls.Add(CreateLabel("SSL Mode:", 0, 4))
            cmbSslMode = New ComboBox() With {.Location = New Point(130, 0), .Size = New Size(340, 24), .DropDownStyle = ComboBoxStyle.DropDownList}
            cmbSslMode.Items.AddRange({"Preferred", "None", "Required", "VerifyCA", "VerifyFull"})
            cmbSslMode.SelectedIndex = 0
            ThemeManager.StyleComboBox(cmbSslMode)
            pnlMySqlSsl.Controls.Add(cmbSslMode)
            pnlContent.Controls.Add(pnlMySqlSsl)
            y += 40

            ' Test Connection Button & Result Label
            btnTest = New Button() With {
                .Text = "⚡ Test Connection",
                .Location = New Point(150, y),
                .Size = New Size(150, 32)
            }
            ThemeManager.StyleButton(btnTest, False)
            AddHandler btnTest.Click, AddressOf OnTestConnectionClicked
            pnlContent.Controls.Add(btnTest)
            y += 38

            lblTestResult = New Label() With {
                .Text = "",
                .Location = New Point(20, y),
                .Size = New Size(470, 45),
                .Font = ThemeManager.SmallFont,
                .ForeColor = ThemeManager.TextSecondaryColor
            }
            pnlContent.Controls.Add(lblTestResult)

            ' Bottom Action Panel
            Dim pnlBottom As New Panel() With {
                .Dock = DockStyle.Bottom,
                .Height = 55,
                .BackColor = ThemeManager.TopBarColor
            }

            btnCancel = New Button() With {
                .Text = "Cancel",
                .DialogResult = DialogResult.Cancel,
                .Size = New Size(100, 34),
                .Location = New Point(290, 10)
            }
            ThemeManager.StyleButton(btnCancel, False)

            btnSave = New Button() With {
                .Text = "Save Server",
                .Size = New Size(110, 34),
                .Location = New Point(400, 10)
            }
            ThemeManager.StyleButton(btnSave, True)
            AddHandler btnSave.Click, AddressOf OnSaveClicked

            pnlBottom.Controls.AddRange({btnCancel, btnSave})

            Controls.AddRange({pnlContent, pnlBottom})
            AcceptButton = btnSave
            CancelButton = btnCancel
        End Sub

        Private Function CreateLabel(text As String, x As Integer, y As Integer) As Label
            Return New Label() With {
                .Text = text,
                .Location = New Point(x, y + 3),
                .AutoSize = True,
                .ForeColor = ThemeManager.TextSecondaryColor,
                .Font = ThemeManager.BodyFont
            }
        End Function

        Private Sub LoadProfileData()
            txtName.Text = _profile.Name
            txtGroup.Text = If(String.IsNullOrWhiteSpace(_profile.GroupName), "Default", _profile.GroupName)
            cmbEngine.SelectedIndex = If(_profile.Engine = DatabaseEngine.SqlServer, 0, 1)
            txtHost.Text = _profile.Host
            numPort.Value = If(_profile.Port > 0, _profile.Port, If(_profile.Engine = DatabaseEngine.SqlServer, 1433, 3306))
            cmbAuthType.SelectedIndex = If(_profile.AuthType = AuthenticationType.WindowsAuthentication, 1, 0)
            txtUser.Text = _profile.Username
            txtPassword.Text = _profile.PlainTextPassword
            txtDefaultDb.Text = _profile.DefaultDatabase
            chkEncrypt.Checked = _profile.Encrypt
            chkTrustCert.Checked = _profile.TrustServerCertificate

            Dim sslIdx = cmbSslMode.FindStringExact(_profile.SslMode)
            If sslIdx >= 0 Then cmbSslMode.SelectedIndex = sslIdx

            UpdateControlVisibility()
        End Sub

        Private Sub OnEngineChanged(sender As Object, e As EventArgs)
            Dim isSql = (cmbEngine.SelectedIndex = 0)
            If isSql Then
                If numPort.Value = 3306 Then numPort.Value = 1433
            Else
                If numPort.Value = 1433 Then numPort.Value = 3306
            End If
            UpdateControlVisibility()
        End Sub

        Private Sub OnAuthTypeChanged(sender As Object, e As EventArgs)
            UpdateControlVisibility()
        End Sub

        Private Sub UpdateControlVisibility()
            Dim isSql = (cmbEngine.SelectedIndex = 0)
            cmbAuthType.Visible = isSql
            pnlMySqlSsl.Visible = Not isSql

            If isSql Then
                Dim isWinAuth = (cmbAuthType.SelectedIndex = 1)
                pnlAuthFields.Visible = Not isWinAuth
            Else
                pnlAuthFields.Visible = True
            End If
        End Sub

        Private Sub SyncProfileFromControls()
            _profile.Name = txtName.Text.Trim()
            _profile.GroupName = If(String.IsNullOrWhiteSpace(txtGroup.Text), "Default", txtGroup.Text.Trim())
            _profile.Engine = If(cmbEngine.SelectedIndex = 0, DatabaseEngine.SqlServer, DatabaseEngine.MySql)
            _profile.Host = txtHost.Text.Trim()
            _profile.Port = CInt(numPort.Value)
            _profile.AuthType = If(_profile.Engine = DatabaseEngine.SqlServer AndAlso cmbAuthType.SelectedIndex = 1, AuthenticationType.WindowsAuthentication, AuthenticationType.SqlAuthentication)
            _profile.Username = txtUser.Text.Trim()
            _profile.PlainTextPassword = txtPassword.Text
            _profile.DefaultDatabase = txtDefaultDb.Text.Trim()
            _profile.Encrypt = chkEncrypt.Checked
            _profile.TrustServerCertificate = chkTrustCert.Checked
            _profile.SslMode = cmbSslMode.SelectedItem?.ToString()
        End Sub

        Private Async Sub OnTestConnectionClicked(sender As Object, e As EventArgs)
            SyncProfileFromControls()

            btnTest.Enabled = False
            btnTest.Text = "Testing..."
            lblTestResult.ForeColor = ThemeManager.TextSecondaryColor
            lblTestResult.Text = "Attempting connection to server..."

            Try
                Using provider = DatabaseProviderFactory.CreateProvider(_profile)
                    Dim res = Await provider.TestConnectionAsync().ConfigureAwait(True)
                    If res.Success Then
                        lblTestResult.ForeColor = ThemeManager.SuccessColor
                        lblTestResult.Text = $"✓ Connected successfully!{Environment.NewLine}{res.Version}{Environment.NewLine}Latency: {res.LatencyMs} ms"
                    Else
                        lblTestResult.ForeColor = ThemeManager.DangerColor
                        lblTestResult.Text = $"✕ Connection Failed:{Environment.NewLine}{res.ErrorMessage}"
                    End If
                End Using
            Catch ex As Exception
                lblTestResult.ForeColor = ThemeManager.DangerColor
                lblTestResult.Text = $"✕ Connection Error: {ex.Message}"
            Finally
                btnTest.Enabled = True
                btnTest.Text = "⚡ Test Connection"
            End Try
        End Sub

        Private Sub OnSaveClicked(sender As Object, e As EventArgs)
            If String.IsNullOrWhiteSpace(txtName.Text) Then
                MessageBox.Show("Please enter a profile name.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                txtName.Focus()
                Return
            End If

            If String.IsNullOrWhiteSpace(txtHost.Text) Then
                MessageBox.Show("Please enter the server host address.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                txtHost.Focus()
                Return
            End If

            SyncProfileFromControls()
            ResultProfile = _profile
            DialogResult = DialogResult.OK
            Close()
        End Sub
    End Class
End Namespace
