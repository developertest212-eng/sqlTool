Imports SQLCommandCenter.Models

Namespace Data
    Public Module DatabaseProviderFactory
        Public Function CreateProvider(profile As ServerProfile) As IDatabaseProvider
            If profile Is Nothing Then
                Throw New ArgumentNullException(NameOf(profile))
            End If

            Select Case profile.Engine
                Case DatabaseEngine.SqlServer
                    Return New SqlServerProvider(profile)
                Case DatabaseEngine.MySql
                    Return New MySqlProvider(profile)
                Case Else
                    Throw New NotSupportedException($"Database engine '{profile.Engine}' is not supported.")
            End Select
        End Function
    End Module
End Namespace
