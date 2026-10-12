Imports System.Text.Json.Serialization
Imports Microsoft.Data.SqlClient
Imports MySqlConnector
Imports SQLCommandCenter.Security

Namespace Models
    Public Class ServerProfile
        Public Property Id As String = Guid.NewGuid().ToString()
        Public Property Name As String = "New Server"
        Public Property GroupName As String = "Default"
        Public Property Engine As DatabaseEngine = DatabaseEngine.SqlServer

        Public Property Host As String = "localhost"
        Public Property Port As Integer = 1433
        Public Property AuthType As AuthenticationType = AuthenticationType.SqlAuthentication
        Public Property Username As String = ""

        ''' <summary>
        ''' Encrypted password payload using Windows DPAPI. Never serialize plaintext password.
        ''' </summary>
        Public Property EncryptedPassword As String = ""

        Public Property DefaultDatabase As String = ""
        Public Property Encrypt As Boolean = False
        Public Property TrustServerCertificate As Boolean = True
        Public Property SslMode As String = "Preferred"

        Public Property ConnectionTimeoutSeconds As Integer = 10
        Public Property QueryTimeoutSeconds As Integer = 30

        Public Property IsFavorite As Boolean = False
        Public Property CreatedAt As DateTime = DateTime.Now
        Public Property LastConnected As Nullable(Of DateTime) = Nothing

        ''' <summary>
        ''' Gets or sets plaintext password via Windows DPAPI encryption in memory.
        ''' </summary>
        <JsonIgnore>
        Public Property PlainTextPassword As String
            Get
                Return CredentialManager.DecryptSecret(EncryptedPassword)
            End Get
            Set(value As String)
                EncryptedPassword = CredentialManager.EncryptSecret(value)
            End Set
        End Property

        Public Function BuildConnectionString(Optional overrideDatabase As String = "") As String
            Dim targetDb = If(Not String.IsNullOrWhiteSpace(overrideDatabase), overrideDatabase, DefaultDatabase)

            If Engine = DatabaseEngine.SqlServer Then
                Dim builder As New SqlConnectionStringBuilder()
                Dim serverHost = If(Port > 0 AndAlso Port <> 1433, $"{Host},{Port}", Host)
                builder.DataSource = serverHost
                builder.ConnectTimeout = Math.Max(3, ConnectionTimeoutSeconds)
                builder.TrustServerCertificate = TrustServerCertificate
                builder.Encrypt = Encrypt
                builder.ApplicationName = "SQL Command Center"

                If Not String.IsNullOrWhiteSpace(targetDb) Then
                    builder.InitialCatalog = targetDb
                End If

                If AuthType = AuthenticationType.WindowsAuthentication Then
                    builder.IntegratedSecurity = True
                Else
                    builder.IntegratedSecurity = False
                    builder.UserID = Username
                    builder.Password = PlainTextPassword
                End If

                Return builder.ConnectionString
            Else
                ' MySQL Connection String
                Dim builder As New MySqlConnectionStringBuilder()
                builder.Server = Host
                builder.Port = If(Port > 0, CUInt(Port), 3306UI)
                builder.ConnectionTimeout = CUInt(Math.Max(3, ConnectionTimeoutSeconds))
                builder.DefaultCommandTimeout = CUInt(Math.Max(5, QueryTimeoutSeconds))
                builder.UserID = Username
                builder.Password = PlainTextPassword
                builder.ApplicationName = "SQL Command Center"

                If Not String.IsNullOrWhiteSpace(targetDb) Then
                    builder.Database = targetDb
                End If

                ' SSL configuration
                Select Case SslMode?.ToLowerInvariant()
                    Case "none"
                        builder.SslMode = MySqlSslMode.None
                    Case "required"
                        builder.SslMode = MySqlSslMode.Required
                    Case "verifyca"
                        builder.SslMode = MySqlSslMode.VerifyCA
                    Case "verifyfull"
                        builder.SslMode = MySqlSslMode.VerifyFull
                    Case Else
                        builder.SslMode = MySqlSslMode.Preferred
                End Select

                Return builder.ConnectionString
            End If
        End Function

        Public Function Clone() As ServerProfile
            Dim json = System.Text.Json.JsonSerializer.Serialize(Me)
            Return System.Text.Json.JsonSerializer.Deserialize(Of ServerProfile)(json)
        End Function

        Public Overrides Function ToString() As String
            Return $"{Name} ({Engine})"
        End Function
    End Class
End Namespace
