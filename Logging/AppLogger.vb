Imports System.IO
Imports System.Text
Imports System.Text.RegularExpressions

Namespace Logging
    Public Enum LogLevel
        Debug = 1
        Info = 2
        Warning = 3
        [Error] = 4
    End Enum

    Public Class LogEntry
        Public Property Timestamp As DateTime = DateTime.Now
        Public Property Level As LogLevel
        Public Property Component As String
        Public Property Message As String
        Public Property ExceptionDetail As String

        Public Overrides Function ToString() As String
            Dim exText = If(String.IsNullOrEmpty(ExceptionDetail), "", $" | Exception: {ExceptionDetail}")
            Return $"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level.ToString().ToUpper()}] [{Component}] {Message}{exText}"
        End Function
    End Class

    ''' <summary>
    ''' Thread-safe structured file and memory logger with automatic credential masking.
    ''' </summary>
    Public Module AppLogger
        Private ReadOnly SyncRoot As New Object()
        Private ReadOnly LogDirectory As String
        Private ReadOnly LogFilePath As String
        Private ReadOnly RecentEntries As New List(Of LogEntry)()
        Private Const MaxRecentEntries As Integer = 500

        Sub New()
            Try
                Dim appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                LogDirectory = Path.Combine(appData, "SQLCommandCenter", "logs")
                If Not Directory.Exists(LogDirectory) Then
                    Directory.CreateDirectory(LogDirectory)
                End If
                LogFilePath = Path.Combine(LogDirectory, "sqlcommandcenter.log")
            Catch
                LogDirectory = AppDomain.CurrentDomain.BaseDirectory
                LogFilePath = Path.Combine(LogDirectory, "sqlcommandcenter.log")
            End Try
        End Sub

        Public Sub Log(level As LogLevel, component As String, message As String, Optional ex As Exception = Nothing)
            Try
                Dim sanitizedMessage = MaskCredentials(message)
                Dim exDetails = If(ex IsNot Nothing, MaskCredentials(ex.ToString()), String.Empty)

                Dim entry As New LogEntry() With {
                    .Timestamp = DateTime.Now,
                    .Level = level,
                    .Component = component,
                    .Message = sanitizedMessage,
                    .ExceptionDetail = exDetails
                }

                SyncLock SyncRoot
                    RecentEntries.Add(entry)
                    If RecentEntries.Count > MaxRecentEntries Then
                        RecentEntries.RemoveAt(0)
                    End If

                    Try
                        File.AppendAllText(LogFilePath, entry.ToString() & Environment.NewLine, Encoding.UTF8)
                    Catch
                        ' Ignore write collisions if file is locked
                    End Try
                End SyncLock
            Catch
                ' Logger should never crash the application
            End Try
        End Sub

        Public Sub Info(component As String, message As String)
            Log(LogLevel.Info, component, message)
        End Sub

        Public Sub Warn(component As String, message As String, Optional ex As Exception = Nothing)
            Log(LogLevel.Warning, component, message, ex)
        End Sub

        Public Sub [Error](component As String, message As String, Optional ex As Exception = Nothing)
            Log(LogLevel.Error, component, message, ex)
        End Sub

        Public Sub Debug(component As String, message As String)
            Log(LogLevel.Debug, component, message)
        End Sub

        Public Function GetRecentLogs() As List(Of LogEntry)
            SyncLock SyncRoot
                Return New List(Of LogEntry)(RecentEntries)
            End SyncLock
        End Function

        ''' <summary>
        ''' Sanitizes connection strings and text to mask passwords and sensitive tokens.
        ''' </summary>
        Public Function MaskCredentials(input As String) As String
            If String.IsNullOrEmpty(input) Then Return String.Empty

            ' Mask Password / Pwd values in connection strings
            Dim pattern = "(?i)(password|pwd)\s*=\s*([^;]+)"
            Dim masked = Regex.Replace(input, pattern, "$1=********")

            Return masked
        End Function
    End Module
End Namespace
