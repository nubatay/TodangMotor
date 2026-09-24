using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using TodangMotor.Common;
using TodangMotor.Services;

namespace TodangMotor.Forms
{
    /// <summary>
    /// This is the very first screen anyone sees when the app opens.
    /// It asks for a username and password, checks them using AuthService,
    /// and then opens either the Owner or Cashier dashboard.
    /// </summary>
    public class LoginForm : Form
    {
        // --- Win32 helper for dragging a borderless window ---
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        // --- Win32 constants for edge-resize hit testing (WM_NCHITTEST) ---
        private const int WM_NCHITTEST = 0x0084;
        private const int HTCLIENT = 1;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;
        private const int ResizeBorderThickness = 8;

        // --- Colors used throughout the form (MODERN LIGHT theme) ---
        private readonly Color _bgColor = Color.FromArgb(245, 246, 250);
        private readonly Color _headerBg = Color.White;
        private readonly Color _headerTitleColor = Color.FromArgb(45, 45, 45);
        private readonly Color _cardBorder = Color.FromArgb(232, 234, 238);
        private readonly Color _orange = Color.FromArgb(230, 126, 34);
        private readonly Color _orangeHover = Color.FromArgb(211, 84, 0);
        private readonly Color _placeholderColor = Color.FromArgb(160, 160, 160);
        private readonly Color _textColor = Color.FromArgb(35, 35, 35);
        private readonly Color _inputBg = Color.FromArgb(248, 249, 251);
        private readonly Color _inputBorder = Color.FromArgb(220, 223, 228);
        private readonly Color _inputBorderFocused = Color.FromArgb(230, 126, 34);
        private readonly Color _chromeIcon = Color.FromArgb(120, 120, 120);
        private readonly Color _chromeHoverBg = Color.FromArgb(238, 238, 238);
        private readonly Color _chromeHoverFg = Color.FromArgb(45, 45, 45);
        private readonly Color _closeHoverBg = Color.FromArgb(232, 17, 35);

        private const string UsernamePlaceholder = "Username";
        private const string PasswordPlaceholder = "Password";

        // --- Controls ---
        private Panel headerPanel;
        private Button btnMinimize;
        private Button btnMaximize;
        private Button btnClose;

        private Panel cardPanel;
        private Panel logoCircle;
        private Label lblLogo;
        private Label lblWelcomeTitle;
        private Label lblSubtitle;
        private Panel usernameContainer;
        private Panel passwordContainer;
        private TextBox txtUsername;
        private TextBox txtPassword;
        private CheckBox chkShowPassword;
        private Label lblError;
        private Button btnLogin;
        private Label lblFooter;

        private readonly AuthService _authService;

        // Fixed card size so all child positions below stay aligned
        private const int CardWidth = 460;
        private const int CardHeight = 580;

        public LoginForm()
        {
            _authService = new AuthService();

            // --- Form settings (borderless, will open maximized) ---
            this.Text = "Todang Motor Parts - Login";
            this.Size = new Size(1100, 720);           // fallback normal size
            this.MinimumSize = new Size(720, 640);     // prevent broken resize
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = _bgColor;
            this.KeyPreview = true;
            this.DoubleBuffered = true;

            // Keep maximize inside the working area (no taskbar overlap)
            this.MaximizedBounds = Screen.FromHandle(this.Handle).WorkingArea;

            this.Load += LoginForm_Load;
            this.KeyDown += LoginForm_KeyDown;

            BuildHeader();
            BuildCard();

            // Z-order: header added last -> drawn FIRST -> sits on top
            // (so its chrome buttons are always visible and clickable).
            this.Controls.Add(cardPanel);
            this.Controls.Add(headerPanel);
            headerPanel.BringToFront();

            this.AcceptButton = btnLogin; // Enter key = click Login
            this.ActiveControl = cardPanel; // prevents auto-focus from clearing the placeholder text
        }

        // Rounds the corners of the whole window; skipped while maximized.
        private void LoginForm_Load(object? sender, EventArgs e)
        {
            ApplyRoundedRegion();
            RoundControl(logoCircle, 40);
            RoundControl(usernameContainer, 10);
            RoundControl(passwordContainer, 10);
            RoundControl(btnLogin, 10);

            // Start maximized (per requirement #6)
            this.WindowState = FormWindowState.Maximized;
        }

        // Keeps the login card visually centered on the form, regardless
        // of window size / maximize state.
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            CenterCard();
            ApplyRoundedRegion();
        }

        private void CenterCard()
        {
            if (cardPanel == null) return;
            int x = Math.Max(0, (this.ClientSize.Width - cardPanel.Width) / 2);
            int y = Math.Max(headerPanel.Height + 20,
                             headerPanel.Height + (this.ClientSize.Height - headerPanel.Height - cardPanel.Height) / 2);
            cardPanel.Location = new Point(x, y);
        }

        // Apply or clear the rounded window region depending on state.
        private void ApplyRoundedRegion()
        {
            if (this.WindowState == FormWindowState.Maximized)
            {
                this.Region = null; // square corners while maximized
            }
            else if (this.ClientRectangle.Width > 0 && this.ClientRectangle.Height > 0)
            {
                this.Region = new Region(GetRoundedRectPath(this.ClientRectangle, 18));
            }
        }

        private GraphicsPath GetRoundedRectPath(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0) radius = 1;
            int d = radius * 2;
            if (d > bounds.Width) d = bounds.Width;
            if (d > bounds.Height) d = bounds.Height;
            if (d <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void RoundControl(Control c, int radius)
        {
            if (c.Width <= 0 || c.Height <= 0) return;
            if (radius <= 0) return;
            c.Region = new Region(GetRoundedRectPath(new Rectangle(0, 0, c.Width, c.Height), radius));
        }

        // Esc key exits the app, same as clicking the ✕ button.
        private void LoginForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Application.Exit();
            }
        }

        // ==================== EDGE-RESIZE SUPPORT ====================
        // Borderless windows lose the native resize grip; WM_NCHITTEST
        // lets Windows think the cursor is on a native border so it
        // performs the resize for us. Skipped while maximized.
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST && this.WindowState != FormWindowState.Maximized)
            {
                base.WndProc(ref m);

                // Only override when Windows thinks we're on the client area.
                if ((int)m.Result == HTCLIENT)
                {
                    // Convert screen coords (lParam) to client coords.
                    int x = unchecked((short)(long)m.LParam);
                    int y = unchecked((short)((long)m.LParam >> 16));
                    Point p = this.PointToClient(new Point(x, y));

                    bool left = p.X <= ResizeBorderThickness;
                    bool right = p.X >= this.ClientSize.Width - ResizeBorderThickness;
                    bool top = p.Y <= ResizeBorderThickness;
                    bool bottom = p.Y >= this.ClientSize.Height - ResizeBorderThickness;

                    if (top && left) m.Result = (IntPtr)HTTOPLEFT;
                    else if (top && right) m.Result = (IntPtr)HTTOPRIGHT;
                    else if (bottom && left) m.Result = (IntPtr)HTBOTTOMLEFT;
                    else if (bottom && right) m.Result = (IntPtr)HTBOTTOMRIGHT;
                    else if (left) m.Result = (IntPtr)HTLEFT;
                    else if (right) m.Result = (IntPtr)HTRIGHT;
                    else if (top) m.Result = (IntPtr)HTTOP;
                    else if (bottom) m.Result = (IntPtr)HTBOTTOM;
                }
                return;
            }

            base.WndProc(ref m);
        }

        // ==================== HEADER ====================
        private void BuildHeader()
        {
            headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = _headerBg
            };
            headerPanel.MouseDown += HeaderPanel_MouseDown;

            // ---- Minimize button (proper Button so the glyph always renders) ----
            btnMinimize = new Button
            {
                Text = "—",
                ForeColor = _chromeIcon,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                Size = new Size(46, 52),
                Location = new Point(this.Width - 138, 0),
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = _headerBg,
                TabStop = false
            };
            btnMinimize.FlatAppearance.BorderSize = 0;
            btnMinimize.FlatAppearance.MouseOverBackColor = _chromeHoverBg;
            btnMinimize.FlatAppearance.MouseDownBackColor = _chromeHoverBg;
            btnMinimize.Click += (s, e) => this.WindowState = FormWindowState.Minimized;
            btnMinimize.MouseEnter += (s, e) => btnMinimize.ForeColor = _chromeHoverFg;
            btnMinimize.MouseLeave += (s, e) => btnMinimize.ForeColor = _chromeIcon;

            // ---- Maximize / Restore button ----
            btnMaximize = new Button
            {
                Text = "□",
                ForeColor = _chromeIcon,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Size = new Size(46, 52),
                Location = new Point(this.Width - 92, 0),
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = _headerBg,
                TabStop = false
            };
            btnMaximize.FlatAppearance.BorderSize = 0;
            btnMaximize.FlatAppearance.MouseOverBackColor = _chromeHoverBg;
            btnMaximize.FlatAppearance.MouseDownBackColor = _chromeHoverBg;
            btnMaximize.Click += (s, e) => ToggleMaximize();
            btnMaximize.MouseEnter += (s, e) => btnMaximize.ForeColor = _chromeHoverFg;
            btnMaximize.MouseLeave += (s, e) => btnMaximize.ForeColor = _chromeIcon;

            // ---- Close button (red on hover) ----
            btnClose = new Button
            {
                Text = "✕",
                ForeColor = _chromeIcon,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                Size = new Size(46, 52),
                Location = new Point(this.Width - 46, 0),
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = _headerBg,
                TabStop = false
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = _closeHoverBg;
            btnClose.FlatAppearance.MouseDownBackColor = _closeHoverBg;
            btnClose.Click += (s, e) => Application.Exit();
            btnClose.MouseEnter += (s, e) => btnClose.ForeColor = Color.White;
            btnClose.MouseLeave += (s, e) => btnClose.ForeColor = _chromeIcon;

            headerPanel.Controls.Add(btnMinimize);
            headerPanel.Controls.Add(btnMaximize);
            headerPanel.Controls.Add(btnClose);
        }

        // Toggles between Maximized and Normal, swaps the glyph, and
        // re-applies/clears the rounded region accordingly.
        private void ToggleMaximize()
        {
            if (this.WindowState == FormWindowState.Maximized)
            {
                this.WindowState = FormWindowState.Normal;
                btnMaximize.Text = "□";
            }
            else
            {
                this.WindowState = FormWindowState.Maximized;
                btnMaximize.Text = "❐";
            }
            ApplyRoundedRegion();
            CenterCard();
        }

        private void HeaderPanel_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // If currently maximized, Windows requires us to restore
                // to Normal first, then start the drag — standard behavior.
                if (this.WindowState == FormWindowState.Maximized)
                {
                    // Capture the cursor position ratio to keep the window
                    // under the mouse after restoring (nice-to-have).
                    this.WindowState = FormWindowState.Normal;
                    btnMaximize.Text = "□";
                    ApplyRoundedRegion();
                    CenterCard();
                }

                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        // ==================== CARD ====================
        private void BuildCard()
        {
            // ---- Card is now INVISIBLE: same color as the page background ----
            // It still exists as a container (so absolute child positions and
            // CenterCard() keep working), but it no longer draws a white
            // rectangle or a border behind the inputs.
            cardPanel = new Panel
            {
                Size = new Size(CardWidth, CardHeight),
                Location = new Point((this.Width - CardWidth) / 2, 88),
                BackColor = _bgColor,       // was Color.White
                TabStop = true
            };
            // NOTE: cardPanel.Paint is intentionally NOT wired up anymore —
            // that's what used to draw the white rounded border around the card.

            // ---- Circular logo badge (image will be dropped in later) ----
            logoCircle = new Panel
            {
                Size = new Size(84, 84),
                Location = new Point((CardWidth - 84) / 2, 34),
                BackColor = Color.FromArgb(255, 244, 230)
            };

            lblLogo = new Label
            {
                Text = "",                          // blanked — your image goes here
                Font = new Font("Segoe UI Emoji", 32),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };
            logoCircle.Controls.Add(lblLogo);

            lblWelcomeTitle = new Label
            {
                Text = "Welcome Back",
                Font = new Font("Segoe UI", 21, FontStyle.Bold),
                ForeColor = Color.FromArgb(35, 35, 35),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(CardWidth - 80, 42),
                Location = new Point(40, 134),
                BackColor = Color.Transparent
            };

            lblSubtitle = new Label
            {
                Text = "Sign in to Inventory & Sales Management",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(140, 140, 140),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(CardWidth - 80, 22),
                Location = new Point(40, 176),
                BackColor = Color.Transparent
            };

            // ---- Username input (rounded container) ----
            int inputLeft = 60;
            int inputWidth = CardWidth - 120;

            usernameContainer = new Panel
            {
                Location = new Point(inputLeft, 226),
                Size = new Size(inputWidth, 46),
                BackColor = _inputBg
            };
            usernameContainer.Paint += InputContainer_Paint;

            var lblUserIcon = new Label
            {
                Text = "👤",
                Font = new Font("Segoe UI Emoji", 11),
                ForeColor = Color.Gray,
                Size = new Size(38, 46),
                Location = new Point(6, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            txtUsername = new TextBox
            {
                Location = new Point(46, 13),
                Size = new Size(inputWidth - 58, 24),
                Font = new Font("Segoe UI", 11),
                BorderStyle = BorderStyle.None,
                BackColor = _inputBg
            };

            usernameContainer.Controls.Add(lblUserIcon);
            usernameContainer.Controls.Add(txtUsername);

            // ---- Password input (rounded container) ----
            passwordContainer = new Panel
            {
                Location = new Point(inputLeft, 286),
                Size = new Size(inputWidth, 46),
                BackColor = _inputBg
            };
            passwordContainer.Paint += InputContainer_Paint;

            var lblPassIcon = new Label
            {
                Text = "🔒",
                Font = new Font("Segoe UI Emoji", 11),
                ForeColor = Color.Gray,
                Size = new Size(38, 46),
                Location = new Point(6, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            txtPassword = new TextBox
            {
                Location = new Point(46, 13),
                Size = new Size(inputWidth - 58, 24),
                Font = new Font("Segoe UI", 11),
                BorderStyle = BorderStyle.None,
                BackColor = _inputBg
            };

            passwordContainer.Controls.Add(lblPassIcon);
            passwordContainer.Controls.Add(txtPassword);

            // Focus highlight on input containers
            txtUsername.Enter += (s, e) => usernameContainer.Invalidate();
            txtUsername.Leave += (s, e) => usernameContainer.Invalidate();
            txtPassword.Enter += (s, e) => passwordContainer.Invalidate();
            txtPassword.Leave += (s, e) => passwordContainer.Invalidate();

            chkShowPassword = new CheckBox
            {
                Text = "Show Password",
                ForeColor = Color.FromArgb(110, 110, 110),
                Font = new Font("Segoe UI", 9f),
                Location = new Point(inputLeft, 344),
                AutoSize = true,
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent
            };
            chkShowPassword.CheckedChanged += ChkShowPassword_CheckedChanged;

            lblError = new Label
            {
                Text = "",
                ForeColor = Color.Firebrick,
                Font = new Font("Segoe UI", 9.5f),
                Size = new Size(inputWidth, 40),
                Location = new Point(inputLeft, 372),
                BackColor = Color.Transparent
            };

            btnLogin = new Button
            {
                Text = "LOG IN",
                Size = new Size(inputWidth, 52),
                Location = new Point(inputLeft, 426),
                BackColor = _orange,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnLogin.FlatAppearance.BorderSize = 0;
            btnLogin.FlatAppearance.MouseOverBackColor = _orangeHover;
            btnLogin.FlatAppearance.MouseDownBackColor = _orangeHover;
            btnLogin.MouseEnter += (s, e) => btnLogin.BackColor = _orangeHover;
            btnLogin.MouseLeave += (s, e) => btnLogin.BackColor = _orange;
            btnLogin.Click += BtnLogin_Click;

            lblFooter = new Label
            {
                Text = "© 2025 Todang Motor Parts",
                Font = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(170, 170, 170),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(CardWidth - 80, 20),
                Location = new Point(40, 522),
                BackColor = Color.Transparent
            };

            SetupPlaceholder(txtUsername, UsernamePlaceholder, isPassword: false);
            SetupPlaceholder(txtPassword, PasswordPlaceholder, isPassword: true);

            cardPanel.Controls.Add(logoCircle);
            cardPanel.Controls.Add(lblWelcomeTitle);
            cardPanel.Controls.Add(lblSubtitle);
            cardPanel.Controls.Add(usernameContainer);
            cardPanel.Controls.Add(passwordContainer);
            cardPanel.Controls.Add(chkShowPassword);
            cardPanel.Controls.Add(lblError);
            cardPanel.Controls.Add(btnLogin);
            cardPanel.Controls.Add(lblFooter);
        }

        // Focus highlight border for input containers
        private void InputContainer_Paint(object? sender, PaintEventArgs e)
        {
            var container = (Panel)sender!;
            bool focused = (container == usernameContainer && txtUsername.Focused) ||
                           (container == passwordContainer && txtPassword.Focused);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, container.Width - 1, container.Height - 1);
            using var path = GetRoundedRectPath(rect, 10);
            using var pen = new Pen(focused ? _inputBorderFocused : _inputBorder, focused ? 1.6f : 1f);
            e.Graphics.DrawPath(pen, path);
        }

        // Makes a textbox show gray placeholder text when empty, and turn
        // into a real black-text input as soon as the user clicks into it.
        private void SetupPlaceholder(TextBox box, string placeholder, bool isPassword)
        {
            box.Text = placeholder;
            box.ForeColor = _placeholderColor;
            if (isPassword) box.UseSystemPasswordChar = false;

            box.Enter += (s, e) =>
            {
                if (box.Text == placeholder && box.ForeColor == _placeholderColor)
                {
                    box.Text = "";
                    box.ForeColor = _textColor;
                    if (isPassword) box.UseSystemPasswordChar = !chkShowPassword.Checked;
                }
            };

            box.Leave += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(box.Text))
                {
                    box.Text = placeholder;
                    box.ForeColor = _placeholderColor;
                    if (isPassword) box.UseSystemPasswordChar = false;
                }
            };
        }

        private void ChkShowPassword_CheckedChanged(object? sender, EventArgs e)
        {
            // Only toggle masking if the user has actually typed a real password
            // (not just looking at the gray placeholder text).
            if (txtPassword.ForeColor != _placeholderColor)
            {
                txtPassword.UseSystemPasswordChar = !chkShowPassword.Checked;
            }
        }

        // Reads the REAL username, ignoring the gray placeholder text.
        private string GetUsername()
        {
            return txtUsername.ForeColor == _placeholderColor ? "" : txtUsername.Text.Trim();
        }

        // Reads the REAL password, ignoring the gray placeholder text.
        private string GetPassword()
        {
            return txtPassword.ForeColor == _placeholderColor ? "" : txtPassword.Text;
        }

        private async void BtnLogin_Click(object? sender, EventArgs e)
        {
            lblError.Text = "";

            string username = GetUsername();
            string password = GetPassword();

            if (string.IsNullOrWhiteSpace(username))
            {
                lblError.Text = "Please enter your username.";
                txtUsername.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                lblError.Text = "Please enter your password.";
                txtPassword.Focus();
                return;
            }

            btnLogin.Enabled = false; // prevent double-click while checking

            try
            {
                var (result, user) = await _authService.LoginAsync(username, password);

                switch (result)
                {
                    case LoginResult.Success:
                        SessionManager.Login(user!);
                        OpenDashboard(user!.Role);
                        break;

                    case LoginResult.InvalidCredentials:
                        lblError.Text = "Invalid username or password.";
                        break;

                    case LoginResult.AccountDisabled:
                        lblError.Text = "This account is disabled. Please contact the owner.";
                        break;
                }
            }
            catch (Exception)
            {
                lblError.Text = "Cannot connect to the database. Please contact support.";
            }
            finally
            {
                btnLogin.Enabled = true;
            }
        }

        private void OpenDashboard(string role)
        {
            ResetFields();

            if (role == "Owner")
            {
                using var dashboard = new OwnerDashboardForm();
                dashboard.ShowDialog();
            }
            else
            {
                using var dashboard = new CashierDashboardForm();
                dashboard.ShowDialog();
            }
        }

        private void ResetFields()
        {
            txtUsername.Text = UsernamePlaceholder;
            txtUsername.ForeColor = _placeholderColor;

            txtPassword.Text = PasswordPlaceholder;
            txtPassword.ForeColor = _placeholderColor;
            txtPassword.UseSystemPasswordChar = false;

            chkShowPassword.Checked = false;
            lblError.Text = "";
        }
    }
}