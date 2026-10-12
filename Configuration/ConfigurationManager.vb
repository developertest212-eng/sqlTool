Imports System.IO
Imports System.Text.Json
Imports SQLCommandCenter.Logging
Imports SQLCommandCenter.Models

Namespace Configuration
    ''' <summary>
    ''' Manages persistence and retrieval of application configuration and server connection profiles.
    ''' </summary>
    Public Module ConfigurationManager
        Private ReadOnly SyncLockObject As New Object()
        Private ReadOnly ConfigDirectory As String
        Private ReadOnly SettingsFilePath As String
        Private ReadOnly ServersFilePath As String

        Private _CurrentSettings As AppSettings

        Private ReadOnly JsonOptions As New JsonSerializerOptions() With {
            .WriteIndented = True,
            .PropertyNameCaseInsensitive = True
        }

        Sub New()
            Try
                Dim localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                ConfigDirectory = Path.Combine(localAppData, "SQLCommandCenter", "config")
                If Not Directory.Exists(ConfigDirectory) Then
                    Directory.CreateDirectory(ConfigDirectory)
                End If
                SettingsFilePath = Path.Combine(ConfigDirectory, "appsettings.json")
                ServersFilePath = Path.Combine(ConfigDirectory, "servers.json")
            Catch ex As Exception
                ConfigDirectory = AppDomain.CurrentDomain.BaseDirectory
                SettingsFilePath = Path.Combine(ConfigDirectory, "appsettings.json")
                ServersFilePath = Path.Combine(ConfigDirectory, "servers.json")
                AppLogger.Error("ConfigManager", "Failed to initialize configuration paths", ex)
            End Try
        End Sub

        Public ReadOnly Property Settings As AppSettings
            Get
                If _CurrentSettings Is Nothing Then
                    LoadSettings()
                End If
                Return _CurrentSettings
            End Get
        End Property

        Public Sub LoadSettings()
            SyncLock SyncLockObject
                Try
                    If File.Exists(SettingsFilePath) Then
                        Dim json = File.ReadAllText(SettingsFilePath)
                        _CurrentSettings = JsonSerializer.Deserialize(Of AppSettings)(json, JsonOptions)
                    End If
                Catch ex As Exception
                    AppLogger.Warn("ConfigManager", "Failed to load appsettings.json, using defaults.", ex)
                End Try

                If _CurrentSettings Is Nothing Then
                    _CurrentSettings = New AppSettings()
                    SaveSettings()
                End If
            End SyncLock
        End Sub

        Public Sub SaveSettings()
            SyncLock SyncLockObject
                Try
                    If _CurrentSettings Is Nothing Then _CurrentSettings = New AppSettings()
                    Dim json = JsonSerializer.Serialize(_CurrentSettings, JsonOptions)
                    File.WriteAllText(SettingsFilePath, json)
                    AppLogger.Info("ConfigManager", "Configuration saved successfully.")
                Catch ex As Exception
                    AppLogger.Error("ConfigManager", "Failed to save appsettings.json", ex)
                End Try
            End SyncLock
        End Sub

        Public Function LoadServers() As List(Of ServerProfile)
            SyncLock SyncLockObject
                Try
                    If File.Exists(ServersFilePath) Then
                        Dim json = File.ReadAllText(ServersFilePath)
                        Dim list = JsonSerializer.Deserialize(Of List(Of ServerProfile))(json, JsonOptions)
                        If list IsNot Nothing Then
                            Return list
                        End If
                    End If
                Catch ex As Exception
                    AppLogger.Error("ConfigManager", "Failed to load servers.json", ex)
                End Try
                Return New List(Of ServerProfile)()
            End SyncLock
        End Function

        Public Sub SaveServers(servers As IEnumerable(Of ServerProfile))
            SyncLock SyncLockObject
                Try
                    Dim serverList = servers.ToList()
                    Dim json = JsonSerializer.Serialize(serverList, JsonOptions)
                    File.WriteAllText(ServersFilePath, json)
                    AppLogger.Info("ConfigManager", $"Saved {serverList.Count} server profiles.")
                Catch ex As Exception
                    AppLogger.Error("ConfigManager", "Failed to save servers.json", ex)
                End Try
            End SyncLock
        End Sub
    End Module
End Namespace
