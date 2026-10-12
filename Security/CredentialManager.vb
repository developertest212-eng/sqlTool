Imports System.Security.Cryptography
Imports System.Text

Namespace Security
    ''' <summary>
    ''' Provides secure encryption and decryption for sensitive credentials using Windows DPAPI.
    ''' Protects secrets using the current Windows user's encryption key.
    ''' </summary>
    Public Module CredentialManager
        Private ReadOnly AdditionalEntropy As Byte() = Encoding.UTF8.GetBytes("SQLCommandCenter_DPAPI_Entropy_2026")

        ''' <summary>
        ''' Encrypts a plaintext string using Windows DPAPI (CurrentUser scope).
        ''' Returns a Base64-encoded encrypted string.
        ''' </summary>
        Public Function EncryptSecret(plainText As String) As String
            If String.IsNullOrEmpty(plainText) Then Return String.Empty

            Try
                Dim plainBytes = Encoding.UTF8.GetBytes(plainText)
                Dim cipherBytes = ProtectedData.Protect(plainBytes, AdditionalEntropy, DataProtectionScope.CurrentUser)
                Return Convert.ToBase64String(cipherBytes)
            Catch ex As Exception
                ' DPAPI failure fallback
                Return String.Empty
            End Try
        End Function

        ''' <summary>
        ''' Decrypts a Base64-encoded cipher string using Windows DPAPI (CurrentUser scope).
        ''' Returns the decrypted plaintext, or empty string on failure.
        ''' </summary>
        Public Function DecryptSecret(cipherText As String) As String
            If String.IsNullOrEmpty(cipherText) Then Return String.Empty

            Try
                Dim cipherBytes = Convert.FromBase64String(cipherText)
                Dim plainBytes = ProtectedData.Unprotect(cipherBytes, AdditionalEntropy, DataProtectionScope.CurrentUser)
                Return Encoding.UTF8.GetString(plainBytes)
            Catch ex As Exception
                ' If data was encrypted by another user or corrupted, return empty
                Return String.Empty
            End Try
        End Function
    End Module
End Namespace
