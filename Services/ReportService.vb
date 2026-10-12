Imports System.Data
Imports System.IO
Imports System.Text
Imports SQLCommandCenter.Logging
Imports SQLCommandCenter.Models

Namespace Services
    Public Class ReportService
        Public Function ExportToCsv(table As DataTable, filePath As String) As Boolean
            Try
                Dim sb As New StringBuilder()

                ' Header
                Dim colNames = table.Columns.Cast(Of DataColumn)().Select(Function(c) EscapeCsv(c.ColumnName))
                sb.AppendLine(String.Join(",", colNames))

                ' Rows
                For Each row As DataRow In table.Rows
                    Dim fields = row.ItemArray.Select(Function(field) EscapeCsv(If(field?.ToString(), "")))
                    sb.AppendLine(String.Join(",", fields))
                Next

                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8)
                AppLogger.Info("ReportService", $"Exported {table.Rows.Count} rows to CSV: {filePath}")
                Return True
            Catch ex As Exception
                AppLogger.Error("ReportService", "Failed to export to CSV", ex)
                Return False
            End Try
        End Function

        Public Function ExportToHtmlReport(title As String, serverName As String, engine As String, table As DataTable, summaryCards As Dictionary(Of String, String), filePath As String) As Boolean
            Try
                Dim sb As New StringBuilder()
                sb.AppendLine("<!DOCTYPE html>")
                sb.AppendLine("<html lang='en'>")
                sb.AppendLine("<head>")
                sb.AppendLine("<meta charset='utf-8'>")
                sb.AppendLine($"<title>{title} - {serverName}</title>")
                sb.AppendLine("<style>")
                sb.AppendLine("body { font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, sans-serif; background-color: #0f111a; color: #e1e2e6; margin: 0; padding: 24px; }")
                sb.AppendLine(".header { display: flex; justify-content: space-between; align-items: center; border-bottom: 2px solid #2b2d42; padding-bottom: 16px; margin-bottom: 24px; }")
                sb.AppendLine("h1 { margin: 0; font-size: 24px; color: #89b4fa; }")
                sb.AppendLine(".meta { color: #a6adc8; font-size: 14px; }")
                sb.AppendLine(".cards-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 16px; margin-bottom: 28px; }")
                sb.AppendLine(".card { background: #181825; border: 1px solid #313244; border-radius: 8px; padding: 16px; }")
                sb.AppendLine(".card-title { font-size: 12px; text-transform: uppercase; color: #a6adc8; letter-spacing: 0.5px; margin-bottom: 8px; }")
                sb.AppendLine(".card-val { font-size: 22px; font-weight: bold; color: #cdd6f4; }")
                sb.AppendLine("table { width: 100%; border-collapse: collapse; background: #181825; border-radius: 8px; overflow: hidden; border: 1px solid #313244; }")
                sb.AppendLine("th { background-color: #1e1e2e; color: #89b4fa; text-align: left; padding: 12px 14px; font-size: 13px; text-transform: uppercase; border-bottom: 1px solid #313244; }")
                sb.AppendLine("td { padding: 10px 14px; border-bottom: 1px solid #2b2d42; font-size: 13px; color: #cdd6f4; }")
                sb.AppendLine("tr:hover td { background-color: #262738; }")
                sb.AppendLine(".footer { margin-top: 24px; font-size: 12px; color: #6c7086; text-align: right; }")
                sb.AppendLine("</style>")
                sb.AppendLine("</head>")
                sb.AppendLine("<body>")

                ' Header
                sb.AppendLine("<div class='header'>")
                sb.AppendLine($"  <div><h1>{title}</h1><div class='meta'>Server: <strong>{serverName}</strong> ({engine})</div></div>")
                sb.AppendLine($"  <div class='meta'>Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss} UTC</div>")
                sb.AppendLine("</div>")

                ' KPI Cards
                If summaryCards IsNot Nothing AndAlso summaryCards.Count > 0 Then
                    sb.AppendLine("<div class='cards-grid'>")
                    For Each kvp In summaryCards
                        sb.AppendLine($"<div class='card'><div class='card-title'>{kvp.Key}</div><div class='card-val'>{kvp.Value}</div></div>")
                    Next
                    sb.AppendLine("</div>")
                End If

                ' Table
                If table IsNot Nothing AndAlso table.Columns.Count > 0 Then
                    sb.AppendLine("<table>")
                    sb.AppendLine("<thead><tr>")
                    For Each col As DataColumn In table.Columns
                        sb.AppendLine($"<th>{col.ColumnName}</th>")
                    Next
                    sb.AppendLine("</tr></thead>")
                    sb.AppendLine("<tbody>")
                    For Each row As DataRow In table.Rows
                        sb.AppendLine("<tr>")
                        For Each item In row.ItemArray
                            Dim val = If(item?.ToString(), "")
                            sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(val)}</td>")
                        Next
                        sb.AppendLine("</tr>")
                    Next
                    sb.AppendLine("</tbody>")
                    sb.AppendLine("</table>")
                End If

                sb.AppendLine("<div class='footer'>SQL Command Center Enterprise Report</div>")
                sb.AppendLine("</body>")
                sb.AppendLine("</html>")

                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8)
                AppLogger.Info("ReportService", $"Exported HTML report: {filePath}")
                Return True
            Catch ex As Exception
                AppLogger.Error("ReportService", "Failed to export HTML report", ex)
                Return False
            End Try
        End Function

        Private Function EscapeCsv(field As String) As String
            If String.IsNullOrEmpty(field) Then Return ""
            If field.Contains(",") OrElse field.Contains("""") OrElse field.Contains(vbCr) OrElse field.Contains(vbLf) Then
                Return """" & field.Replace("""", """""") & """"
            End If
            Return field
        End Function
    End Class
End Namespace
