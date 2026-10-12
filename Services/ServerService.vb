Imports SQLCommandCenter.Configuration
Imports SQLCommandCenter.Data
Imports SQLCommandCenter.Logging
Imports SQLCommandCenter.Models

Namespace Services
    Public Class ServerService
        Private ReadOnly _servers As New List(Of ServerProfile)()
        Private ReadOnly _syncLock As New Object()
        Private _selectedServer As ServerProfile = Nothing

        Public Event ServerListChanged As EventHandler
        Public Event SelectedServerChanged As EventHandler(Of ServerProfile)

        Public Sub New()
            LoadServers()
        End Sub

        Public Sub LoadServers()
            SyncLock _syncLock
                _servers.Clear()
                Dim loaded = ConfigurationManager.LoadServers()
                _servers.AddRange(loaded)

                If _servers.Count > 0 AndAlso _selectedServer Is Nothing Then
                    _selectedServer = _servers.First()
                End If
            End SyncLock
            RaiseEvent ServerListChanged(Me, EventArgs.Empty)
        End Sub

        Public Function GetAllServers() As List(Of ServerProfile)
            SyncLock _syncLock
                Return _servers.Select(Function(s) s.Clone()).ToList()
            End SyncLock
        End Function

        Public Function GetGroups() As List(Of String)
            SyncLock _syncLock
                Return _servers.Select(Function(s) If(String.IsNullOrWhiteSpace(s.GroupName), "Default", s.GroupName)).Distinct().OrderBy(Function(g) g).ToList()
            End SyncLock
        End Function

        Public Property SelectedServer As ServerProfile
            Get
                SyncLock _syncLock
                    Return _selectedServer
                End SyncLock
            End Get
            Set(value As ServerProfile)
                SyncLock _syncLock
                    If value Is Nothing Then
                        _selectedServer = Nothing
                    Else
                        _selectedServer = _servers.FirstOrDefault(Function(s) s.Id = value.Id)
                    End If
                End SyncLock
                RaiseEvent SelectedServerChanged(Me, _selectedServer)
            End Set
        End Property

        Public Sub AddOrUpdateServer(profile As ServerProfile)
            If profile Is Nothing Then Return

            SyncLock _syncLock
                Dim existing = _servers.FirstOrDefault(Function(s) s.Id = profile.Id)
                If existing IsNot Nothing Then
                    Dim idx = _servers.IndexOf(existing)
                    _servers(idx) = profile
                Else
                    _servers.Add(profile)
                End If

                ConfigurationManager.SaveServers(_servers)

                If _selectedServer Is Nothing OrElse _selectedServer.Id = profile.Id Then
                    _selectedServer = profile
                    RaiseEvent SelectedServerChanged(Me, _selectedServer)
                End If
            End SyncLock

            AppLogger.Info("ServerService", $"Saved server profile: {profile.Name} ({profile.Engine})")
            RaiseEvent ServerListChanged(Me, EventArgs.Empty)
        End Sub

        Public Sub DeleteServer(serverId As String)
            SyncLock _syncLock
                Dim target = _servers.FirstOrDefault(Function(s) s.Id = serverId)
                If target IsNot Nothing Then
                    _servers.Remove(target)
                    ConfigurationManager.SaveServers(_servers)

                    If _selectedServer IsNot Nothing AndAlso _selectedServer.Id = serverId Then
                        _selectedServer = _servers.FirstOrDefault()
                        RaiseEvent SelectedServerChanged(Me, _selectedServer)
                    End If
                End If
            End SyncLock

            AppLogger.Info("ServerService", $"Deleted server profile ID: {serverId}")
            RaiseEvent ServerListChanged(Me, EventArgs.Empty)
        End Sub

        Public Sub ToggleFavorite(serverId As String)
            SyncLock _syncLock
                Dim target = _servers.FirstOrDefault(Function(s) s.Id = serverId)
                If target IsNot Nothing Then
                    target.IsFavorite = Not target.IsFavorite
                    ConfigurationManager.SaveServers(_servers)
                End If
            End SyncLock
            RaiseEvent ServerListChanged(Me, EventArgs.Empty)
        End Sub
    End Class
End Namespace
