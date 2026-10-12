Imports System.Data
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Threading.Tasks
Imports SQLCommandCenter.Configuration
Imports SQLCommandCenter.Data
Imports SQLCommandCenter.Logging
Imports SQLCommandCenter.Models

Namespace Services
    Public Class QueryService
        Private Shared ReadOnly DestructiveKeywordsRegex As New Regex("\b(DROP|DELETE|TRUNCATE|ALTER|UPDATE)\b", RegexOptions.IgnoreCase Or RegexOptions.Compiled)

        ''' <summary>
        ''' Scans SQL text to detect potentially destructive DDL or DML statements.
        ''' </summary>
        Public Function AnalyzeDestructiveOperations(sql As String) As (IsDestructive As Boolean, DetectedKeyword As String)
            If String.IsNullOrWhiteSpace(sql) Then Return (False, String.Empty)

            ' Strip comments (both single line -- and multi-line /* */)
            Dim cleaned = Regex.Replace(sql, "(/\*[\s\S]*?\*/|--[^\r\n]*)", " ")

            Dim match = DestructiveKeywordsRegex.Match(cleaned)
            If match.Success Then
                Return (True, match.Value.ToUpperInvariant())
            End If

            Return (False, String.Empty)
        End Function

        ''' <summary>
        ''' Executes a SQL query against the specified server profile and returns results.
        ''' </summary>
        Public Async Function ExecuteQueryAsync(profile As ServerProfile, sql As String, Optional databaseName As String = "", Optional timeoutSeconds As Integer = 30, Optional cancellationToken As CancellationToken = Nothing) As Task(Of (ResultTable As DataTable, ElapsedMs As Long, RowsAffected As Integer, Message As String))
            If profile Is Nothing Then
                Return (New DataTable(), 0, -1, "No active server selected.")
            End If

            If String.IsNullOrWhiteSpace(sql) Then
                Return (New DataTable(), 0, -1, "SQL command cannot be empty.")
            End If

            Try
                Using provider = DatabaseProviderFactory.CreateProvider(profile)
                    Dim maxRows = ConfigurationManager.Settings.MaxQueryResultsRows
                    Return Await provider.ExecuteQueryAsync(sql, databaseName, maxRows, timeoutSeconds, cancellationToken).ConfigureAwait(False)
                End Using
            Catch ex As Exception
                AppLogger.Error("QueryService", $"Execution error on {profile.Name}", ex)
                Return (New DataTable(), 0, -1, $"Execution failed: {ex.Message}")
            End Try
        End Function
    End Class
End Namespace
