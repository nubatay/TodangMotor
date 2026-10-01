using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using TodangMotor.Common;
using TodangMotor.Services;

namespace TodangMotor.Forms
{
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

        private System.Windows.Forms.Timer _lockoutTimer;
        private int _lockoutSecondsRemaining;
        private bool _lockoutActive;

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

            _lockoutTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _lockoutTimer.Tick += LockoutTimer_Tick;

            BuildLayout();

            KeyDown += LoginForm_KeyDown;
            Shown += LoginForm_Shown;
            FormClosed += LoginForm_FormClosed;
        }

        // ============================================================
        // LAYOUT
        // ============================================================

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

        // ---------------- LEFT PANEL — just a big logo ----------------

        private Panel BuildLeftPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Primary
            };

            // Large white card with the logo inside — aspect preserved.
            // Large white card with the logo inside — aspect preserved.
            var logo = new LogoPlaceholder
            {
                Width = 500,
                Height = 170,
                Radius = 24,
                TileColor = Color.White,
                LetterColor = Theme.Primary,
                Letter = "T"
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

                logo.Left = cx - logo.Width / 2;
                logo.Top = (panel.ClientSize.Height - logo.Height) / 2 - 20;

                footer.Left = cx - footer.Width / 2;
                footer.Top = panel.ClientSize.Height - 40;
            };

            panel.Controls.Add(logo);
            panel.Controls.Add(footer);

            return panel;
        }

        // ---------------- RIGHT PANEL — login card ----------------

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

        private void LoginForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (_lockoutTimer != null)
            {
                _lockoutTimer.Stop();
                _lockoutTimer.Dispose();
            }
        }

        private async void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !_lockoutActive)
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
            if (_isBusy || _lockoutActive) return;

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
                var (result, user, lockoutSeconds) =
                    await _authService.LoginAsync(username, password);

                switch (result)
                {
                    case LoginResult.Success:
                        if (user == null)
                        {
                            ShowError("Login succeeded but no user was returned.");
                            return;
                        }
                        StopLockoutCountdown();
                        SessionManager.Login(user);
                        OpenDashboardForCurrentUser();
                        break;

                    case LoginResult.LockedOut:
                        StartLockoutCountdown(lockoutSeconds);
                        _passwordBox.Text = string.Empty;
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

        // ============================================================
        // LOCKOUT COUNTDOWN
        // ============================================================

        private void StartLockoutCountdown(int seconds)
        {
            if (seconds <= 0) seconds = 1;

            _lockoutActive = true;
            _lockoutSecondsRemaining = seconds;

            UpdateLockoutMessage();
            SetBusy(false);

            _lockoutTimer.Stop();
            _lockoutTimer.Start();
        }

        private void StopLockoutCountdown()
        {
            _lockoutActive = false;
            _lockoutSecondsRemaining = 0;
            _lockoutTimer.Stop();
        }

        private void LockoutTimer_Tick(object? sender, EventArgs e)
        {
            _lockoutSecondsRemaining--;

            if (_lockoutSecondsRemaining <= 0)
            {
                StopLockoutCountdown();
                _errorLabel.Text = string.Empty;
                SetBusy(false);
                _usernameBox.FocusInput();
            }
            else
            {
                UpdateLockoutMessage();
            }
        }

        private void UpdateLockoutMessage()
        {
            int min = _lockoutSecondsRemaining / 60;
            int sec = _lockoutSecondsRemaining % 60;
            _errorLabel.ForeColor = Theme.Danger;
            _errorLabel.Text = $"Account locked. Try again in {min}:{sec:00}.";
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            bool enabled = !busy && !_lockoutActive;

            _signInButton.Enabled = enabled;
            _signInButton.Text = busy ? "Signing in..." : "Sign In";
            _usernameBox.Enabled = enabled;
            _passwordBox.Enabled = enabled;
        }

        private void ShowError(string message)
        {
            _errorLabel.ForeColor = Theme.Danger;
            _errorLabel.Text = message ?? string.Empty;
        }
    }
}