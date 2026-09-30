using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using TodangMotor.Common;
using TodangMotor.Services;

namespace TodangMotor.Forms
{
    /// <summary>
    /// Split-screen login: branded blue panel on the left, login card on the right.
    /// Borderless custom chrome with minimize + close only (no maximize).
    /// </summary>
    public class LoginForm : ShellForm
    {
        private const int LeftPanelPercent = 45;

        private readonly AuthService _authService = new();

        private RoundedTextBox _usernameBox;
        private RoundedTextBox _passwordBox;
        private Button _signInButton;
        private Label _errorLabel;
        private Panel _rightPanel;
        private Panel _leftPanel;
        private Button _closeBtn;
        private Button _minBtn;
        private bool _isBusy;

        public LoginForm()
        {
            ShowHeader = false;
            ShowMaximizeButton = false;
            ShowMinimizeButton = false;

            Text = "Todang Motor - Login";
            ClientSize = new Size(900, 560);
            MinimumSize = new Size(820, 520);
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            BackColor = Theme.Background;

            BuildLayout();

            KeyDown += LoginForm_KeyDown;
            Shown += LoginForm_Shown;
        }

        private void BuildLayout()
        {
            var split = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Theme.Background,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, LeftPanelPercent));
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100 - LeftPanelPercent));
            split.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _leftPanel = BuildLeftPanel();
            _rightPanel = BuildRightPanel();

            split.Controls.Add(_leftPanel, 0, 0);
            split.Controls.Add(_rightPanel, 1, 0);

            ContentPanel.Controls.Add(split);
        }

        private Panel BuildLeftPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Primary
            };

            var logo = new LogoPlaceholder
            {
                Width = 96,
                Height = 96,
                Radius = 20,
                TileColor = Color.White,
                LetterColor = Theme.Primary,
                Letter = "T"
            };

            var brand = new Label
            {
                Text = "TODANG MOTOR",
                Font = new Font(Theme.UiFontFamily, 22F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(340, 40)
            };

            var subtitle = new Label
            {
                Text = "Parts & Accessories",
                Font = new Font(Theme.UiFontFamily, 12F, FontStyle.Regular),
                ForeColor = Color.FromArgb(230, 235, 255),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(340, 26)
            };

            var tagline = new Label
            {
                Text = "Inventory & Sales Management",
                Font = Theme.FontSmall,
                ForeColor = Color.FromArgb(200, 210, 245),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(340, 22)
            };

            var footer = new Label
            {
                Text = "v1.0  ·  Offline Mode",
                Font = Theme.FontSmall,
                ForeColor = Color.FromArgb(190, 205, 245),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(340, 22)
            };

            panel.Resize += (s, e) =>
            {
                int cx = panel.ClientSize.Width / 2;
                int logoY = (int)(panel.ClientSize.Height * 0.20);
                int brandY = logoY + 96 + 20;
                int subY = brandY + 40 + 4;
                int tagY = subY + 26 + 18;

                logo.Location = new Point(cx - logo.Width / 2, logoY);
                brand.Location = new Point(cx - brand.Width / 2, brandY);
                subtitle.Location = new Point(cx - subtitle.Width / 2, subY);
                tagline.Location = new Point(cx - tagline.Width / 2, tagY);
                footer.Location = new Point(cx - footer.Width / 2, panel.ClientSize.Height - 40);
            };

            panel.Controls.Add(logo);
            panel.Controls.Add(brand);
            panel.Controls.Add(subtitle);
            panel.Controls.Add(tagline);
            panel.Controls.Add(footer);

            return panel;
        }

        private Panel BuildRightPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Background
            };

            _minBtn = CreateCornerChromeButton("\uE921", isClose: false);
            _minBtn.Click += (s, e) => WindowState = FormWindowState.Minimized;

            _closeBtn = CreateCornerChromeButton("\uE8BB", isClose: true);
            _closeBtn.Click += (s, e) => Close();

            panel.Controls.Add(_closeBtn);
            panel.Controls.Add(_minBtn);

            panel.Resize += (s, e) =>
            {
                _closeBtn.Location = new Point(panel.ClientSize.Width - _closeBtn.Width - 6, 6);
                _minBtn.Location = new Point(panel.ClientSize.Width - _closeBtn.Width - _minBtn.Width - 6, 6);
            };

            var card = BuildLoginCard();
            panel.Controls.Add(card);

            panel.Resize += (s, e) =>
            {
                card.Left = (panel.ClientSize.Width - card.Width) / 2;
                card.Top = (panel.ClientSize.Height - card.Height) / 2;
            };

            return panel;
        }

        private RoundedPanel BuildLoginCard()
        {
            var card = new RoundedPanel
            {
                Width = 360,
                Height = 410,
                BackColor = Theme.Surface,
                Radius = Theme.RadiusCard,
                BorderColor = Theme.Divider,
                BorderSize = 1
            };

            // ---- Header ----
            var title = new Label
            {
                Text = "Welcome Back",
                Font = new Font(Theme.UiFontFamily, 22F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(32, 26),
                Size = new Size(296, 42)
            };

            var subtitle = new Label
            {
                Text = "Sign in to continue",
                Font = Theme.FontBody,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(32, 72),
                Size = new Size(296, 22)
            };

            // ---- Username ----
            var userLabel = new Label
            {
                Text = "Username",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(32, 114),
                Size = new Size(296, 18)
            };

            _usernameBox = UiFactory.CreateTextBox(296);
            _usernameBox.Location = new Point(32, 136);
            _usernameBox.Placeholder = "Enter your username";
            _usernameBox.MaxLength = 50;

            // ---- Password ----
            var passLabel = new Label
            {
                Text = "Password",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(32, 188),
                Size = new Size(296, 18)
            };

            _passwordBox = UiFactory.CreateTextBox(296, isPassword: true);
            _passwordBox.Location = new Point(32, 210);
            _passwordBox.Placeholder = "Enter your password";
            _passwordBox.MaxLength = 100;
            _passwordBox.ShowPasswordToggle = true;

            // ---- Error ----
            _errorLabel = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.Danger,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(32, 260),
                Size = new Size(296, 22)
            };

            // ---- Sign In ----
            _signInButton = UiFactory.CreateButton("Sign In", UiFactory.ButtonStyle.Primary, 296, 44);
            _signInButton.Location = new Point(32, 296);
            _signInButton.Click += async (s, e) => await DoSignInAsync();

            _usernameBox.InputKeyDown += Input_KeyDown;
            _passwordBox.InputKeyDown += Input_KeyDown;

            card.Controls.Add(title);
            card.Controls.Add(subtitle);
            card.Controls.Add(userLabel);
            card.Controls.Add(_usernameBox);
            card.Controls.Add(passLabel);
            card.Controls.Add(_passwordBox);
            card.Controls.Add(_errorLabel);
            card.Controls.Add(_signInButton);

            return card;
        }

        private Button CreateCornerChromeButton(string glyph, bool isClose)
        {
            var b = new Button
            {
                Text = glyph,
                Font = Theme.FontIcon,
                ForeColor = Theme.TextSecondary,
                BackColor = Theme.Background,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(40, 40),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            b.FlatAppearance.BorderSize = 0;

            var normalBack = Theme.Background;
            var hoverBack = isClose ? Theme.Danger : Color.FromArgb(220, 228, 245);

            b.MouseEnter += (s, e) =>
            {
                b.BackColor = hoverBack;
                if (isClose) b.ForeColor = Color.White;
            };
            b.MouseLeave += (s, e) =>
            {
                b.BackColor = normalBack;
                b.ForeColor = Theme.TextSecondary;
            };

            return b;
        }

        // ============================================================
        // EVENTS
        // ============================================================

        private void LoginForm_Shown(object sender, EventArgs e)
        {
            Animator.FadeInForm(this, 180, 0.0);
            _usernameBox.FocusInput();
        }

        private void LoginForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                Close();
        }

        private async void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await DoSignInAsync();
            }
        }

        // ============================================================
        // SIGN-IN
        // ============================================================

        private async Task DoSignInAsync()
        {
            if (_isBusy) return;

            string username = (_usernameBox.Text ?? string.Empty).Trim();
            string password = _passwordBox.Text ?? string.Empty;

            if (username.Length == 0)
            {
                ShowError("Please enter your username.");
                _usernameBox.FocusInput();
                return;
            }
            if (password.Length == 0)
            {
                ShowError("Please enter your password.");
                _passwordBox.FocusInput();
                return;
            }

            SetBusy(true);
            try
            {
                var (result, user) = await _authService.LoginAsync(username, password);

                switch (result)
                {
                    case LoginResult.Success:
                        if (user == null)
                        {
                            ShowError("Login succeeded but no user was returned.");
                            return;
                        }
                        SessionManager.Login(user);
                        OpenDashboardForCurrentUser();
                        break;

                    case LoginResult.AccountDisabled:
                        ShowError("Your account has been disabled. Please contact the Owner.");
                        _passwordBox.Text = string.Empty;
                        _passwordBox.FocusInput();
                        break;

                    case LoginResult.InvalidCredentials:
                    default:
                        ShowError("Invalid username or password.");
                        _passwordBox.Text = string.Empty;
                        _passwordBox.FocusInput();
                        break;
                }
            }
            catch (Exception)
            {
                ShowError("Something went wrong. Please try again.");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void OpenDashboardForCurrentUser()
        {
            Form next = SessionManager.IsOwner
                ? new OwnerDashboardForm()
                : new CashierDashboardForm();

            Hide();
            next.FormClosed += (s, e) =>
            {
                // If the dashboard closed because of a logout
                // (SessionManager cleared the current user), show the
                // login form again instead of exiting the app.
                if (!SessionManager.IsLoggedIn)
                {
                    Show();
                    _usernameBox.Text = string.Empty;
                    _passwordBox.Text = string.Empty;
                    _errorLabel.Text = string.Empty;
                    Animator.FadeInForm(this, 180, 0.0);
                    _usernameBox.FocusInput();
                }
                else
                {
                    Close();
                }
            };
            next.Show();
        }

        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            _signInButton.Enabled = !busy;
            _signInButton.Text = busy ? "Signing in..." : "Sign In";
            _usernameBox.Enabled = !busy;
            _passwordBox.Enabled = !busy;
        }

        private void ShowError(string message)
        {
            _errorLabel.Text = message ?? string.Empty;
        }
    }
}