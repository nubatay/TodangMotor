using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using TodangMotor.Common;

namespace TodangMotor.Forms
{
    /// <summary>
    /// Owner's main dashboard. Contains the side navigation bar for accessing
    /// all Owner-only features (Inventory, Sales, Reports, Users, etc.).
    /// More menu items will be added here as each module is built.
    /// </summary>
    public class OwnerDashboardForm : Form
    {
        // --- Win32 helper for dragging a borderless window ---
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        // --- Win32 constants for resize-by-edge-dragging ---
        private const int WM_NCHITTEST = 0x84;
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

        // --- Colors (MODERN LIGHT theme, matching LoginForm) ---
        private readonly Color _bgColor = Color.FromArgb(245, 246, 250);
        private readonly Color _headerBg = Color.White;
        private readonly Color _headerTitleColor = Color.FromArgb(45, 45, 45);
        private readonly Color _cardBorder = Color.FromArgb(232, 234, 238);
        private readonly Color _orange = Color.FromArgb(230, 126, 34);
        private readonly Color _orangeHover = Color.FromArgb(211, 84, 0);
        private readonly Color _textColor = Color.FromArgb(35, 35, 35);
        private readonly Color _sidebarBg = Color.White;
        private readonly Color _sidebarText = Color.FromArgb(80, 80, 80);
        private readonly Color _sidebarHoverBg = Color.FromArgb(245, 246, 250);
        private readonly Color _sidebarActiveBg = Color.FromArgb(255, 244, 230);
        private readonly Color _sidebarActiveText = Color.FromArgb(230, 126, 34);
        private readonly Color _chromeIcon = Color.FromArgb(120, 120, 120);
        private readonly Color _chromeHoverBg = Color.FromArgb(238, 238, 238);
        private readonly Color _chromeHoverFg = Color.FromArgb(45, 45, 45);
        private readonly Color _closeHoverBg = Color.FromArgb(232, 17, 35);
        private readonly Color _contentBg = Color.FromArgb(245, 246, 250);

        // --- Controls ---
        private Panel headerPanel;
        private Button btnMinimize;
        private Button btnMaximize;
        private Button btnClose;

        private Panel sidebarPanel;
        private Panel logoPlaceholder;
        private Label lblLogo;
        private Label lblBrandName;

        private Panel contentPanel;
        private Panel welcomeCard;
        private Label lblWelcome;
        private Button btnLogout;

        // Sidebar menu items (using Panels for hover effects)
        private Panel menuInventory;
        private Panel menuSuppliers;
        private Panel menuCategories;
        private Panel menuSales;
        private Panel menuReports;
        private Panel menuUsers;

        private const int SidebarWidth = 240;
        private const int HeaderHeight = 52;

        public OwnerDashboardForm()
        {
            // --- Form settings (borderless, will open maximized) ---
            this.Text = "Todang Motor Parts - Owner Dashboard";
            this.Size = new Size(1100, 720);           // fallback normal size
            this.MinimumSize = new Size(900, 600);     // prevent broken resize
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = _bgColor;
            this.KeyPreview = true;
            this.DoubleBuffered = true;

            // Keep maximize inside the working area (no taskbar overlap)
            this.MaximizedBounds = Screen.FromHandle(this.Handle).WorkingArea;

            this.Load += OwnerDashboardForm_Load;
            this.KeyDown += OwnerDashboardForm_KeyDown;
            this.Resize += OwnerDashboardForm_Resize;

            BuildHeader();
            BuildSidebar();
            BuildContent();

            // ---- Docking + Z-order (critical) ----
            // WinForms resolves Docking in REVERSE Z-order (last added docks first).
            // We want: header docks top, sidebar docks left (below header),
            // content fills the remainder.
            //
            // Render order (what paints on top): FIRST added = TOP.
            // Header must be visually on top so its chrome buttons can never
            // be covered by the sidebar or the content panel.
            this.Controls.Add(contentPanel);
            this.Controls.Add(sidebarPanel);
            this.Controls.Add(headerPanel);
            headerPanel.BringToFront();
        }

        // Rounds the corners of the whole window and the logo badge,
        // and starts the form maximized (per requirement #6).
        private void OwnerDashboardForm_Load(object? sender, EventArgs e)
        {
            this.Region = new Region(GetRoundedRectPath(this.ClientRectangle, 18));
            RoundControl(logoPlaceholder, 40);

            // Start maximized. Setting this here (after all controls are
            // built) ensures the Resize handler fires and re-lays-out
            // everything at the full desktop size.
            this.WindowState = FormWindowState.Maximized;

            ApplyRoundedRegionForCurrentState();
        }

        private void OwnerDashboardForm_Resize(object? sender, EventArgs e)
        {
            ApplyRoundedRegionForCurrentState();
        }

        private void ApplyRoundedRegionForCurrentState()
        {
            if (this.WindowState == FormWindowState.Maximized)
            {
                this.Region = null; // square corners when full-screen
                if (btnMaximize != null) btnMaximize.Text = "❐";
            }
            else
            {
                if (this.ClientRectangle.Width > 0 && this.ClientRectangle.Height > 0)
                {
                    this.Region = new Region(GetRoundedRectPath(this.ClientRectangle, 18));
                }
                if (btnMaximize != null) btnMaximize.Text = "□";
            }
        }

        // Lets Windows detect when the mouse is near an edge/corner so the
        // user can drag to resize, even though this window has no visible border.
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg == WM_NCHITTEST && (int)m.Result == HTCLIENT
                && this.WindowState != FormWindowState.Maximized)
            {
                Point screenPoint = new Point(m.LParam.ToInt32());
                Point clientPoint = this.PointToClient(screenPoint);

                int x = clientPoint.X;
                int y = clientPoint.Y;
                int width = this.ClientSize.Width;
                int height = this.ClientSize.Height;

                if (x <= ResizeBorderThickness && y <= ResizeBorderThickness)
                    m.Result = (IntPtr)HTTOPLEFT;
                else if (x >= width - ResizeBorderThickness && y <= ResizeBorderThickness)
                    m.Result = (IntPtr)HTTOPRIGHT;
                else if (x <= ResizeBorderThickness && y >= height - ResizeBorderThickness)
                    m.Result = (IntPtr)HTBOTTOMLEFT;
                else if (x >= width - ResizeBorderThickness && y >= height - ResizeBorderThickness)
                    m.Result = (IntPtr)HTBOTTOMRIGHT;
                else if (x <= ResizeBorderThickness)
                    m.Result = (IntPtr)HTLEFT;
                else if (x >= width - ResizeBorderThickness)
                    m.Result = (IntPtr)HTRIGHT;
                else if (y <= ResizeBorderThickness)
                    m.Result = (IntPtr)HTTOP;
                else if (y >= height - ResizeBorderThickness)
                    m.Result = (IntPtr)HTBOTTOM;
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

        private void OwnerDashboardForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Application.Exit();
            }
        }

        // ==================== HEADER ====================
        private void BuildHeader()
        {
            headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = HeaderHeight,
                BackColor = _headerBg
            };
            headerPanel.MouseDown += HeaderPanel_MouseDown;

            // ---- Minimize button ----
            btnMinimize = new Button
            {
                Text = "—",
                ForeColor = _chromeIcon,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                Size = new Size(46, HeaderHeight),
                Location = new Point(headerPanel.Width - 138, 0),
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
                Size = new Size(46, HeaderHeight),
                Location = new Point(headerPanel.Width - 92, 0),
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
            btnMaximize.Click += (s, e) =>
            {
                this.WindowState = this.WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal
                    : FormWindowState.Maximized;
                ApplyRoundedRegionForCurrentState();
            };
            btnMaximize.MouseEnter += (s, e) => btnMaximize.ForeColor = _chromeHoverFg;
            btnMaximize.MouseLeave += (s, e) => btnMaximize.ForeColor = _chromeIcon;

            // ---- Close button ----
            btnClose = new Button
            {
                Text = "✕",
                ForeColor = _chromeIcon,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                Size = new Size(46, HeaderHeight),
                Location = new Point(headerPanel.Width - 46, 0),
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

        private void HeaderPanel_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // Snap-back: if maximized, restore to Normal first so the
                // drag moves the window instead of doing nothing.
                if (this.WindowState == FormWindowState.Maximized)
                {
                    this.WindowState = FormWindowState.Normal;
                    ApplyRoundedRegionForCurrentState();
                }

                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        // ==================== SIDEBAR ====================
        private void BuildSidebar()
        {
            // NOTE: no top padding — the sidebar's own white background will
            // sit behind the header, and both are white so they visually merge.
            // The sidebar's CONTENT (logo, menu) is offset via absolute
            // Location values below so it starts below the header strip.
            sidebarPanel = new Panel
            {
                Width = SidebarWidth,
                Dock = DockStyle.Left,
                BackColor = _sidebarBg,
                Padding = new Padding(0)
            };
            sidebarPanel.Paint += SidebarPanel_Paint;

            // ---- Logo placeholder (circular) ----
            // Y is offset by HeaderHeight so it appears BELOW the header.
            logoPlaceholder = new Panel
            {
                Size = new Size(80, 80),
                Location = new Point((SidebarWidth - 80) / 2, HeaderHeight + 30),
                BackColor = Color.FromArgb(255, 244, 230)
            };

            lblLogo = new Label
            {
                Text = "🏍️",
                Font = new Font("Segoe UI Emoji", 30),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };
            logoPlaceholder.Controls.Add(lblLogo);

            // ---- Brand name under logo ----
            lblBrandName = new Label
            {
                Text = "TODANG MOTOR",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = _headerTitleColor,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(SidebarWidth, 24),
                Location = new Point(0, HeaderHeight + 120),
                BackColor = Color.Transparent
            };

            // ---- Menu items ----
            // Order (per client decision, option B):
            // Inventory -> Suppliers -> Categories -> Sales -> Reports -> Users
            int menuStartY = HeaderHeight + 170;
            int menuItemHeight = 48;
            int menuSpacing = 4;
            int step = menuItemHeight + menuSpacing;

            menuInventory = CreateMenuItem("📦  Inventory", menuStartY);
            menuSuppliers = CreateMenuItem("🚚  Suppliers", menuStartY + step);
            menuCategories = CreateMenuItem("📋  Categories", menuStartY + 2 * step);
            menuSales = CreateMenuItem("💰  Sales", menuStartY + 3 * step);
            menuReports = CreateMenuItem("📊  Reports", menuStartY + 4 * step);
            menuUsers = CreateMenuItem("👥  Users", menuStartY + 5 * step);

            // ---- Logout button at bottom ----
            btnLogout = new Button
            {
                Text = "🚪  Logout",
                Size = new Size(SidebarWidth - 40, 42),
                Location = new Point(20, sidebarPanel.Height - 70),
                BackColor = Color.Transparent,
                ForeColor = _sidebarText,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                Padding = new Padding(12, 0, 0, 0)
            };
            btnLogout.FlatAppearance.BorderSize = 0;
            btnLogout.FlatAppearance.MouseOverBackColor = _sidebarHoverBg;
            btnLogout.FlatAppearance.MouseDownBackColor = _sidebarHoverBg;
            btnLogout.Click += BtnLogout_Click;
            btnLogout.MouseEnter += (s, e) => btnLogout.ForeColor = _orange;
            btnLogout.MouseLeave += (s, e) => btnLogout.ForeColor = _sidebarText;

            sidebarPanel.Controls.Add(logoPlaceholder);
            sidebarPanel.Controls.Add(lblBrandName);
            sidebarPanel.Controls.Add(menuInventory);
            sidebarPanel.Controls.Add(menuSuppliers);
            sidebarPanel.Controls.Add(menuCategories);
            sidebarPanel.Controls.Add(menuSales);
            sidebarPanel.Controls.Add(menuReports);
            sidebarPanel.Controls.Add(menuUsers);
            sidebarPanel.Controls.Add(btnLogout);
        }

        private Panel CreateMenuItem(string text, int yPosition)
        {
            var panel = new Panel
            {
                Size = new Size(SidebarWidth - 24, 48),
                Location = new Point(12, yPosition),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Tag = text
            };

            var lbl = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 10f, FontStyle.Regular),
                ForeColor = _sidebarText,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            panel.Controls.Add(lbl);

            panel.MouseEnter += (s, e) =>
            {
                panel.BackColor = _sidebarHoverBg;
                lbl.ForeColor = _orange;
            };
            panel.MouseLeave += (s, e) =>
            {
                panel.BackColor = Color.Transparent;
                lbl.ForeColor = _sidebarText;
            };
            lbl.MouseEnter += (s, e) =>
            {
                panel.BackColor = _sidebarHoverBg;
                lbl.ForeColor = _orange;
            };
            lbl.MouseLeave += (s, e) =>
            {
                panel.BackColor = Color.Transparent;
                lbl.ForeColor = _sidebarText;
            };

            panel.Click += (s, e) => MenuItem_Click(text);
            lbl.Click += (s, e) => MenuItem_Click(text);

            return panel;
        }

        private void MenuItem_Click(string menuName)
        {
            foreach (var item in new[] { menuInventory, menuSuppliers, menuCategories, menuSales, menuReports, menuUsers })
            {
                item.BackColor = Color.Transparent;
                if (item.Controls[0] is Label lbl)
                    lbl.ForeColor = _sidebarText;
            }

            var clicked = menuName switch
            {
                var t when t.Contains("Inventory") => menuInventory,
                var t when t.Contains("Suppliers") => menuSuppliers,
                var t when t.Contains("Categories") => menuCategories,
                var t when t.Contains("Sales") => menuSales,
                var t when t.Contains("Reports") => menuReports,
                var t when t.Contains("Users") => menuUsers,
                _ => null
            };

            if (clicked != null)
            {
                clicked.BackColor = _sidebarActiveBg;
                if (clicked.Controls[0] is Label lbl)
                    lbl.ForeColor = _sidebarActiveText;
            }

            if (menuName.Contains("Suppliers"))
            {
                SuppliersMenuItem_Click(this, EventArgs.Empty);
            }
            else if (menuName.Contains("Categories"))
            {
                CategoriesMenuItem_Click(this, EventArgs.Empty);
            }
            else if (menuName.Contains("Inventory"))
            {
                InventoryMenuItem_Click(this, EventArgs.Empty);
            }
        }

        private void SidebarPanel_Paint(object? sender, PaintEventArgs e)
        {
            // Thin border on the right edge of the sidebar.
            // Only draw it BELOW the header strip so the border doesn't
            // visually cut through the header bar.
            using var pen = new Pen(_cardBorder, 1);
            e.Graphics.DrawLine(pen, sidebarPanel.Width - 1, HeaderHeight,
                                sidebarPanel.Width - 1, sidebarPanel.Height);
        }

        // ==================== CONTENT ====================
        private void BuildContent()
        {
            // The content panel is docked Fill, so it takes the remaining
            // space to the right of the sidebar and below the header.
            // Its Padding creates a uniform 40px inset for its children,
            // so the welcome card is automatically 40px from the content
            // panel's edges — no manual anchoring or SizeChanged hacks.
            contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = _contentBg,
                Padding = new Padding(40, 40, 40, 40)
            };

            // ---- Welcome card ----
            // Docked Top so it fills the padded content-panel width
            // automatically on every resize / maximize.
            welcomeCard = new Panel
            {
                Dock = DockStyle.Top,
                Height = 140,
                BackColor = Color.White
            };
            welcomeCard.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, welcomeCard.Width - 1, welcomeCard.Height - 1);
                using var path = GetRoundedRectPath(rect, 18);
                using var pen = new Pen(_cardBorder, 1);
                e.Graphics.DrawPath(pen, path);
            };

            lblWelcome = new Label
            {
                Text = $"Welcome back, {SessionManager.CurrentUser?.FullName ?? "Owner"}!",
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = _textColor,
                AutoSize = true,
                Location = new Point(30, 30),
                BackColor = Color.Transparent
            };

            var lblRole = new Label
            {
                Text = "Owner Dashboard — Manage your inventory, sales, and reports from here.",
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.FromArgb(140, 140, 140),
                AutoSize = true,
                Location = new Point(30, 70),
                BackColor = Color.Transparent
            };

            var accentLine = new Panel
            {
                Size = new Size(60, 4),
                Location = new Point(30, 105),
                BackColor = _orange
            };
            accentLine.Region = new Region(GetRoundedRectPath(new Rectangle(0, 0, 60, 4), 2));

            welcomeCard.Controls.Add(lblWelcome);
            welcomeCard.Controls.Add(lblRole);
            welcomeCard.Controls.Add(accentLine);

            contentPanel.Controls.Add(welcomeCard);
        }

        // ==================== EVENT HANDLERS (unchanged logic) ====================
        private void CategoriesMenuItem_Click(object? sender, EventArgs e)
        {
            using var categoryForm = new CategoryForm();
            categoryForm.ShowDialog(this);
        }

        private void SuppliersMenuItem_Click(object? sender, EventArgs e)
        {
            using var supplierForm = new SupplierForm();
            supplierForm.ShowDialog(this);
        }

        private void BtnLogout_Click(object? sender, EventArgs e)
        {
            SessionManager.Logout();
            this.Close();
        }
        private void InventoryMenuItem_Click(object? sender, EventArgs e)
        {
            using var productListForm = new ProductListForm();
            productListForm.ShowDialog(this);
        }
    }
}