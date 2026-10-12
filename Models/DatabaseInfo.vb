Namespace Models
    Public Class DatabaseInfo
        Public Property Name As String = ""
        Public Property Status As String = "ONLINE"
        Public Property TotalSizeMB As Double = 0.0
        Public Property DataSizeMB As Double = 0.0
        Public Property LogSizeMB As Double = 0.0
        Public Property FreeSpaceMB As Double = 0.0
        Public Property RecoveryModel As String = "N/A"
        Public Property Collation As String = ""
        Public Property CompatibilityLevel As String = ""
        Public Property CreatedDate As DateTime = DateTime.MinValue
        Public Property LastBackupDate As Nullable(Of DateTime) = Nothing
        Public Property TableCount As Integer = 0

        Public ReadOnly Property TotalSizeDisplay As String
            Get
                If TotalSizeMB >= 1024 Then
                    Return $"{Math.Round(TotalSizeMB / 1024.0, 2)} GB"
                Else
                    Return $"{Math.Round(TotalSizeMB, 1)} MB"
                End If
            End Get
        End Property

        Public ReadOnly Property FreeSpacePercent As Double
            Get
                If TotalSizeMB <= 0 Then Return 0
                Return Math.Round((FreeSpaceMB / TotalSizeMB) * 100.0, 1)
            End Get
        End Property
    End Class

    Public Class DatabaseObjectInfo
        Public Property DatabaseName As String = ""
        Public Property SchemaName As String = "dbo"
        Public Property Name As String = ""
        Public Property ObjectType As DatabaseObjectType = DatabaseObjectType.Table
        Public Property RowCount As Long = 0
        Public Property TotalSizeKB As Long = 0
        Public Property CreateDate As DateTime = DateTime.MinValue
        Public Property ModifyDate As DateTime = DateTime.MinValue

        Public ReadOnly Property FullName As String
            Get
                If String.IsNullOrEmpty(SchemaName) Then Return Name
                Return $"{SchemaName}.{Name}"
            End Get
        End Property

        Public ReadOnly Property SizeDisplay As String
            Get
                If TotalSizeKB >= 1024 * 1024 Then
                    Return $"{Math.Round(TotalSizeKB / (1024.0 * 1024.0), 2)} GB"
                ElseIf TotalSizeKB >= 1024 Then
                    Return $"{Math.Round(TotalSizeKB / 1024.0, 1)} MB"
                Else
                    Return $"{TotalSizeKB} KB"
                End If
            End Get
        End Property
    End Class

    Public Class ColumnInfo
        Public Property ColumnName As String = ""
        Public Property DataType As String = ""
        Public Property MaxLength As Integer = 0
        Public Property IsNullable As Boolean = True
        Public Property IsPrimaryKey As Boolean = False
        Public Property IsIdentity As Boolean = False
        Public Property DefaultValue As String = ""

        Public ReadOnly Property TypeDefinition As String
            Get
                If MaxLength > 0 AndAlso (DataType.Contains("char") OrElse DataType.Contains("binary")) Then
                    Return If(MaxLength = -1, $"{DataType}(MAX)", $"{DataType}({MaxLength})")
                End If
                Return DataType
            End Get
        End Property
    End Class
End Namespace
