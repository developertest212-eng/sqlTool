Imports System.Drawing
Imports System.Windows.Forms
Imports SQLCommandCenter.UI.Theme

Namespace UI.Forms
    Public Class ConfirmDestructiveDialog
        Inherits Form

        Public Sub New(operationType As String, targetDescription As String, sqlPreview As String)
            Text = "Security Confirmation - Potentially Destructive Operation"
            Size = New Size(560, 380)
            StartPosition = FormStartPosition.CenterParent
            FormBorderStyle = FormBorderStyle.FixedDialog
            MaximizeBox = False
            MinimizeBox = False
            BackColor = ThemeManager.BackgroundColor
            ForeColor = ThemeManager.TextPrimaryColor
            Font = ThemeManager.BodyFont

            ' Banner Panel
            Dim pnlBanner As New Panel() With {
                .Dock = DockStyle.Top,
                .Height = 70,
                .BackColor = Color.FromArgb(40, 243, 139, 168)
            }
            Dim lblWarningTitle As New Label() With {
                .Text = $"⚠️ CAUTION: {operationType.ToUpperInvariant()} STATEMENT DETECTED",
                .Font = ThemeManager.HeaderFont,
                .ForeColor = ThemeManager.DangerColor,
                .Location = New Point(16, 14),
                .AutoSize = True
            }
            Dim lblWarningDesc As New Label() With {
                .Text = "This statement may permanently modify or remove database schema or data.",
                .Font = ThemeManager.BodyFont,
                .ForeColor = ThemeManager.TextPrimaryColor,
                .Location = New Point(16, 40),
                .AutoSize = True
            }
            pnlBanner.Controls.AddRange({lblWarningTitle, lblWarningDesc})

            ' Main Content
            Dim lblTarget As New Label() With {
                .Text = $"Target: {targetDescription}",
                .Font = ThemeManager.BoldFont,
                .Location = New Point(16, 82),
                .Size = New Size(510, 24)
            }

            Dim lblSql As New Label() With {
                .Text = "Command Preview:",
                .Font = ThemeManager.SmallFont,
                .ForeColor = ThemeManager.TextSecondaryColor,
                .Location = New Point(16, 112),
                .AutoSize = True
            }

            Dim txtSql As New TextBox() With {
                .Multiline = True,
                .ReadOnly = True,
                .ScrollBars = ScrollBars.Vertical,
                .Location = New Point(16, 134),
                .Size = New Size(512, 140),
                .Font = ThemeManager.CodeFont,
                .BackColor = ThemeManager.CardColor,
                .ForeColor = ThemeManager.TextPrimaryColor,
                .BorderStyle = BorderStyle.FixedSingle,
                .Text = sqlPreview
            }

            ' Button Panel
            Dim pnlBottom As New Panel() With {
                .Dock = DockStyle.Bottom,
                .Height = 55,
                .BackColor = ThemeManager.TopBarColor
            }

            Dim btnCancel As New Button() With {
                .Text = "Cancel",
                .DialogResult = DialogResult.Cancel,
                .Size = New Size(110, 36),
                .Location = New Point(280, 10)
            }
            ThemeManager.StyleButton(btnCancel, False)

            Dim btnConfirm As New Button() With {
                .Text = "Execute Anyway",
                .DialogResult = DialogResult.OK,
                .Size = New Size(130, 36),
                .Location = New Point(400, 10),
                .BackColor = ThemeManager.DangerColor,
                .ForeColor = Color.White
            }
            btnConfirm.FlatStyle = FlatStyle.Flat
            btnConfirm.FlatAppearance.BorderSize = 0
            btnConfirm.Font = ThemeManager.BoldFont
            btnConfirm.Cursor = Cursors.Hand

            pnlBottom.Controls.AddRange({btnCancel, btnConfirm})

            Controls.AddRange({pnlBanner, lblTarget, lblSql, txtSql, pnlBottom})
            AcceptButton = btnCancel ' Default safe choice is Cancel
            CancelButton = btnCancel
        End Sub
    End Class
End Namespace
