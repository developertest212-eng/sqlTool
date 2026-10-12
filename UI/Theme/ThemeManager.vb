Imports System.Drawing
Imports System.Windows.Forms
Imports SQLCommandCenter.Configuration

Namespace UI.Theme
    Public Module ThemeManager
        Public Event ThemeChanged As EventHandler

        Public Property IsDark As Boolean = True

        ' Theme Colors
        Public ReadOnly Property BackgroundColor As Color
            Get
                Return If(IsDark, Color.FromArgb(24, 24, 37), Color.FromArgb(245, 247, 250))
            End Get
        End Property

        Public ReadOnly Property SidebarColor As Color
            Get
                Return If(IsDark, Color.FromArgb(17, 17, 27), Color.FromArgb(232, 236, 242))
            End Get
        End Property

        Public ReadOnly Property TopBarColor As Color
            Get
                Return If(IsDark, Color.FromArgb(20, 20, 32), Color.FromArgb(240, 243, 248))
            End Get
        End Property

        Public ReadOnly Property CardColor As Color
            Get
                Return If(IsDark, Color.FromArgb(30, 30, 46), Color.White)
            End Get
        End Property

        Public ReadOnly Property CardBorderColor As Color
            Get
                Return If(IsDark, Color.FromArgb(49, 50, 68), Color.FromArgb(226, 232, 240))
            End Get
        End Property

        Public ReadOnly Property TextPrimaryColor As Color
            Get
                Return If(IsDark, Color.FromArgb(205, 214, 244), Color.FromArgb(30, 41, 59))
            End Get
        End Property

        Public ReadOnly Property TextSecondaryColor As Color
            Get
                Return If(IsDark, Color.FromArgb(166, 173, 200), Color.FromArgb(100, 116, 139))
            End Get
        End Property

        Public ReadOnly Property TextMutedColor As Color
            Get
                Return If(IsDark, Color.FromArgb(108, 112, 134), Color.FromArgb(148, 163, 184))
            End Get
        End Property

        Public ReadOnly Property AccentColor As Color
            Get
                Return If(IsDark, Color.FromArgb(137, 180, 250), Color.FromArgb(37, 99, 235))
            End Get
        End Property

        Public ReadOnly Property AccentHoverColor As Color
            Get
                Return If(IsDark, Color.FromArgb(180, 190, 254), Color.FromArgb(29, 78, 216))
            End Get
        End Property

        Public ReadOnly Property SuccessColor As Color
            Get
                Return Color.FromArgb(166, 227, 161)
            End Get
        End Property

        Public ReadOnly Property WarningColor As Color
            Get
                Return Color.FromArgb(249, 226, 175)
            End Get
        End Property

        Public ReadOnly Property DangerColor As Color
            Get
                Return Color.FromArgb(243, 139, 168)
            End Get
        End Property

        Public ReadOnly Property InputBackgroundColor As Color
            Get
                Return If(IsDark, Color.FromArgb(24, 24, 37), Color.White)
            End Get
        End Property

        Public ReadOnly Property InputBorderColor As Color
            Get
                Return If(IsDark, Color.FromArgb(69, 71, 90), Color.FromArgb(203, 213, 225))
            End Get
        End Property

        Public ReadOnly Property GridHeaderColor As Color
            Get
                Return If(IsDark, Color.FromArgb(20, 20, 32), Color.FromArgb(241, 245, 249))
            End Get
        End Property

        Public ReadOnly Property GridAlternateRowColor As Color
            Get
                Return If(IsDark, Color.FromArgb(34, 34, 52), Color.FromArgb(248, 250, 252))
            End Get
        End Property

        ' Standard Fonts
        Public ReadOnly Property TitleFont As New Font("Segoe UI", 14.0F, FontStyle.Bold)
        Public ReadOnly Property HeaderFont As New Font("Segoe UI", 11.0F, FontStyle.Bold)
        Public ReadOnly Property BodyFont As New Font("Segoe UI", 9.0F, FontStyle.Regular)
        Public ReadOnly Property BoldFont As New Font("Segoe UI", 9.0F, FontStyle.Bold)
        Public ReadOnly Property SmallFont As New Font("Segoe UI", 8.25F, FontStyle.Regular)
        Public ReadOnly Property CodeFont As New Font("Consolas", 9.75F, FontStyle.Regular)

        Public Sub Initialize()
            IsDark = (ConfigurationManager.Settings.Theme = "Dark")
        End Sub

        Public Sub ToggleTheme()
            IsDark = Not IsDark
            ConfigurationManager.Settings.Theme = If(IsDark, "Dark", "Light")
            ConfigurationManager.SaveSettings()
            RaiseEvent ThemeChanged(Nothing, EventArgs.Empty)
        End Sub

        Public Sub StyleButton(btn As Button, isPrimary As Boolean)
            btn.FlatStyle = FlatStyle.Flat
            btn.FlatAppearance.BorderSize = 0
            btn.Font = BoldFont
            btn.Cursor = Cursors.Hand

            If isPrimary Then
                btn.BackColor = AccentColor
                btn.ForeColor = If(IsDark, Color.FromArgb(17, 17, 27), Color.White)
            Else
                btn.BackColor = If(IsDark, Color.FromArgb(49, 50, 68), Color.FromArgb(226, 232, 240))
                btn.ForeColor = TextPrimaryColor
            End If
        End Sub

        Public Sub StyleTextBox(txt As TextBox)
            txt.BorderStyle = BorderStyle.FixedSingle
            txt.BackColor = InputBackgroundColor
            txt.ForeColor = TextPrimaryColor
            txt.Font = BodyFont
        End Sub

        Public Sub StyleComboBox(cmb As ComboBox)
            cmb.FlatStyle = FlatStyle.Flat
            cmb.BackColor = InputBackgroundColor
            cmb.ForeColor = TextPrimaryColor
            cmb.Font = BodyFont
        End Sub
    End Module
End Namespace
