using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using TodangMotor.Common;
using TodangMotor.Controls;

namespace TodangMotor.Forms
{
    /// <summary>
    /// Owner dashboard shell.
    /// Light sidebar with section headers, right border, logo at top.
    /// Header bar shows the page title + user chip (with Change Password menu).
    /// Logout sits at the bottom of the sidebar with no divider above it.
    /// </summary>
    public class OwnerDashboardForm : ShellForm
    {
        // ============================================================
        // NAV DEFINITION
        // ============================================================

        private const string KeyDashboard = "dashboard";
        private const string KeyPos = "pos";
        private const string KeySales = "sales";
        private const string KeyItems = "items";
        private const string KeyInventory = "inventory";
        private const string KeyStockIn = "stockin";
        private const string KeyPurchaseOrders = "purchaseorders";
        private const string KeySuppliers = "suppliers";
        private const string KeyCategories = "categories";
        private const string KeyUsers = "users";
        private const string KeyReports = "reports";
        private const string KeySettings = "settings";

        private const string IconDashboard = "\uE80F";
        private const string IconPos = "\uE719";
        private const string IconSales = "\uE719";
        private const string IconItems = "\uE8F1";
        private const string IconInventory = "\uE7B8";
        private const string IconStockIn = "\uE710";
        private const string IconPurchaseOrders = "\uE7BF";
        private const string IconSuppliers = "\uE716";
        private const string IconCategories = "\uE8EC";
        private const string IconUsers = "\uE77B";
        private const string IconReports = "\uE9D9";
        private const string IconSettings = "\uE713";
        private const string IconLogout = "\uE7E8";
        private const string IconPerson = "\uE77B";
        private const string IconChevronDown = "\uE70D";

        // ============================================================
        // FIELDS
        // ============================================================

        private TableLayoutPanel _rootLayout;
        private Panel _mainArea;
        private Panel _sidebar;
        private Panel _contentHost;
        private FlowLayoutPanel _navList;
        private Dictionary<string, NavItemControl> _navItems = new();

        private ContextMenuStrip _chipMenu;

        private Control _currentContent;
        private string _currentKey = KeyDashboard;

        // ============================================================
        // CONSTRUCTION
        // ============================================================

        public OwnerDashboardForm()
        {
            HeaderTitle = "Dashboard";
            ShowMaximizeButton = true;
            ShowMinimizeButton = true;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1400, 900);       // restore size when un-maximized
            MinimumSize = new Size(1100, 700);       // never let it crush the layout
            WindowState = FormWindowState.Maximized;
            BackColor = Theme.Background;

            HeaderTitleLabel.Padding = new Padding(20, 0, 0, 0);

            // ---- Add user chip to the header bar (between title and chrome buttons) ----
            var chipPanel = BuildUserChipPanel();

            HeaderPanel.Controls.Remove(ChromeContainer);
            HeaderPanel.Controls.Remove(HeaderTitleLabel);

            HeaderPanel.Controls.Add(HeaderTitleLabel);   // docks Left
            HeaderPanel.Controls.Add(chipPanel);           // docks Right
            HeaderPanel.Controls.Add(ChromeContainer);     // docks Right (rightmost)

            RestructureLayout();

            BuildSidebar();
            BuildContentHost();
            UpdateActiveNav(KeyDashboard);
            

            EnsureChromeButtons();

            Shown += OwnerDashboardForm_Shown;
        }

        // ============================================================
        // USER CHIP (header)
        // ============================================================

        private Panel BuildUserChipPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 300,
                BackColor = Color.Transparent
            };

            // Context menu
            _chipMenu = new ContextMenuStrip
            {
                Font = Theme.FontBody,
                ShowImageMargin = false
            };

            var changePasswordItem = new ToolStripMenuItem("Change Password");
            changePasswordItem.Click += (s, e) => OpenChangePassword();
            _chipMenu.Items.Add(changePasswordItem);

            // Chip
            var chip = new Panel
            {
                Width = 280,
                Height = 40,
                Location = new Point(10, 8),
                BackColor = Theme.Surface,
                Cursor = Cursors.Hand
            };
            Theme.ApplyRoundedRegion(chip, 8);

            chip.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = Theme.RoundedRect(
                    new Rectangle(0, 0, chip.Width - 1, chip.Height - 1), 8);
                using var pen = new Pen(Theme.Divider, 1f);
                e.Graphics.DrawPath(pen, path);
            };

            string fullName = SessionManager.CurrentUser?.FullName ?? "User";
            string firstName = fullName.Split(' ').FirstOrDefault() ?? "User";
            string greeting = GetGreeting() + ", " + firstName;

            var iconLabel = new Label
            {
                Text = IconPerson,
                Font = new Font(Theme.IconFontFamily, 12F),
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(28, 40),
                Location = new Point(10, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            var textLabel = new Label
            {
                Text = greeting,
                Font = Theme.FontBodyBold,
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Size = new Size(210, 40),
                Location = new Point(40, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                AutoEllipsis = true
            };

            var chevronLabel = new Label
            {
                Text = IconChevronDown,
                Font = new Font(Theme.IconFontFamily, 8F),
                ForeColor = Theme.TextMuted,
                AutoSize = false,
                Size = new Size(14, 40),
                Location = new Point(256, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            chip.Controls.Add(iconLabel);
            chip.Controls.Add(textLabel);
            chip.Controls.Add(chevronLabel);

            // Click opens the menu
            EventHandler click = (s, e) =>
                _chipMenu.Show(chip, new Point(0, chip.Height + 4));
            chip.Click += click;
            iconLabel.Click += click;
            textLabel.Click += click;
            chevronLabel.Click += click;

            // Hover state — soft blue tint
            EventHandler enter = (s, e) => chip.BackColor = Color.FromArgb(232, 238, 255);
            EventHandler leave = (s, e) => chip.BackColor = Theme.Surface;

            chip.MouseEnter += enter;
            chip.MouseLeave += leave;
            iconLabel.MouseEnter += enter;
            iconLabel.MouseLeave += leave;
            textLabel.MouseEnter += enter;
            textLabel.MouseLeave += leave;
            chevronLabel.MouseEnter += enter;
            chevronLabel.MouseLeave += leave;

            panel.Controls.Add(chip);
            return panel;
        }

        private void OpenChangePassword()
        {
            using var form = new ChangePasswordForm();
            form.ShowDialog(this);
        }

        private static string GetGreeting()
        {
            int hour = DateTime.Now.Hour;
            if (hour < 12) return "Good morning";
            if (hour < 18) return "Good afternoon";
            return "Good evening";
        }

        // ============================================================
        // CHROME BUTTONS
        // ============================================================

        private void EnsureChromeButtons()
        {
            if (ChromeContainer == null) return;

            MinimizeButton.Visible = true;
            MaximizeButton.Visible = true;
            CloseButton.Visible = true;

            const int size = 40;
            const int y = 8;
            ChromeContainer.Width = size * 3;

            MinimizeButton.Size = new Size(size, size);
            MaximizeButton.Size = new Size(size, size);
            CloseButton.Size = new Size(size, size);

            MinimizeButton.Location = new Point(0, y);
            MaximizeButton.Location = new Point(size, y);
            CloseButton.Location = new Point(size * 2, y);

            MinimizeButton.BringToFront();
            MaximizeButton.BringToFront();
            CloseButton.BringToFront();
        }

        private void OwnerDashboardForm_Shown(object sender, EventArgs e)
        {
            Animator.FadeInForm(this, 150, 0.85);
            NavigateTo(KeyDashboard);
        }

        // ============================================================
        // LAYOUT RESTRUCTURE
        // ============================================================

        private void RestructureLayout()
        {
            Controls.Remove(HeaderPanel);
            Controls.Remove(ContentPanel);

            _rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Theme.Background,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.SidebarWidth));
            _rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _mainArea = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Background
            };

            HeaderPanel.Dock = DockStyle.Top;
            ContentPanel.Dock = DockStyle.Fill;

            _mainArea.Controls.Add(ContentPanel);
            _mainArea.Controls.Add(HeaderPanel);

            _rootLayout.Controls.Add(_mainArea, 1, 0);

            Controls.Add(_rootLayout);
        }

        // ============================================================
        // SIDEBAR
        // ============================================================

        private void BuildSidebar()
        {
            _sidebar = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.SidebarBg
            };

            // Right border (1px) — added last so it docks first (rightmost).
            var rightBorder = new Panel
            {
                Dock = DockStyle.Right,
                Width = 1,
                BackColor = Theme.SidebarBorder
            };

            // Nav list (Fill)
            _navList = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Theme.SidebarBg,
                Padding = new Padding(0, 4, 0, 4)
            };

            // Footer (Bottom) — Logout only, no divider above
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 56,
                BackColor = Theme.SidebarBg
            };

            var logout = new NavItemControl("logout", IconLogout, "Logout");
            logout.Dock = DockStyle.Fill;
            logout.ItemClicked += (s, e) => Logout();
            footer.Controls.Add(logout);

            // Header (Top) — logo only
            var header = BuildSidebarHeader();

            // Populate nav sections
            AddSectionHeader("Main");
            AddNavItem(KeyDashboard, IconDashboard, "Dashboard");
            AddNavItem(KeyPos, IconPos, "POS");
            AddNavItem(KeySales, IconSales, "Sales");

            AddSectionHeader("Catalog");
            AddNavItem(KeyItems, IconItems, "Items");
            AddNavItem(KeyCategories, IconCategories, "Categories");
            AddNavItem(KeySuppliers, IconSuppliers, "Suppliers");

            AddSectionHeader("Stock");
            AddNavItem(KeyInventory, IconInventory, "Inventory");
            AddNavItem(KeyStockIn, IconStockIn, "Stock-In");
            AddNavItem(KeyPurchaseOrders, IconPurchaseOrders, "Purchase Orders");

            AddSectionHeader("Admin");
            AddNavItem(KeyUsers, IconUsers, "Users");
            AddNavItem(KeyReports, IconReports, "Reports");
            AddNavItem(KeySettings, IconSettings, "Settings");

            // Add to sidebar (docking order)
            _sidebar.Controls.Add(_navList);     // Fill
            _sidebar.Controls.Add(footer);        // Bottom
            _sidebar.Controls.Add(header);        // Top
            _sidebar.Controls.Add(rightBorder);   // Right

            _rootLayout.Controls.Add(_sidebar, 0, 0);
        }

        private Panel BuildSidebarHeader()
        {
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 130,
                BackColor = Theme.SidebarBg
            };

            // Real logo drawn directly on the light sidebar — no tile,
            // no border. Aspect ratio is preserved automatically.
            // Widened to fill nearly the whole sidebar width.
            var logo = new LogoPlaceholder
            {
                Width = 205,
                Height = 110,
                Radius = 0,
                TileColor = Color.Transparent,
                LetterColor = Theme.Primary,
                Letter = "T",
                ForcePlaceholder = false
            };

            header.Resize += (s, e) =>
            {
                logo.Location = new Point(
                    (header.ClientSize.Width - logo.Width) / 2,
                    (header.ClientSize.Height - logo.Height) / 2);
            };

            header.Controls.Add(logo);
            return header;
        }

        private void AddSectionHeader(string text)
        {
            var label = new Label
            {
                Text = text.ToUpperInvariant(),
                Font = new Font(Theme.UiFontFamily, 8F, FontStyle.Bold),
                ForeColor = Theme.SidebarSectionHeader,
                AutoSize = false,
                Width = Theme.SidebarWidth,
                Height = 24,
                Padding = new Padding(20, 0, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 12, 0, 4)
            };
            _navList.Controls.Add(label);
        }

        private void AddNavItem(string key, string icon, string label)
        {
            var item = new NavItemControl(key, icon, label);
            item.ItemClicked += (s, e) => NavigateTo(key);

            _navList.Controls.Add(item);
            _navItems[key] = item;
        }

        // ============================================================
        // CONTENT HOST
        // ============================================================

        private void BuildContentHost()
        {
            _contentHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Background,
                Padding = new Padding(0)
            };
            ContentPanel.Controls.Add(_contentHost);
            _contentHost.BringToFront();
        }

        // ============================================================
        // NAVIGATION
        // ============================================================

        private void NavigateTo(string key)
        {
            _currentKey = key;
            UpdateActiveNav(key);

            if (key == KeyDashboard)
            {
                HeaderTitle = "Dashboard";
                var home = new DashboardHomeControl();
                home.NavigateRequested += navKey =>
                {
                    if (navKey == "inventory") NavigateTo(KeyInventory);
                };
                home.NavigateToSalesRequested += (from, toExclusive) =>
                {
                    // toExclusive is exclusive; SalesHistoryControl wants inclusive To.
                    NavigateToSalesWithRange(from, toExclusive.AddDays(-1));
                };
                ShowContent(home);
            }
            else if (key == KeyItems)
            {
                HeaderTitle = "Items";
                ShowContent(new ItemsControl());
            }
            else if (key == KeyInventory)
            {
                HeaderTitle = "Inventory";
                ShowContent(new InventoryControl());
            }
            else if (key == KeyStockIn)
            {
                HeaderTitle = "Stock-In";
                ShowContent(new StockInControl());
            }
            else if (key == KeyPurchaseOrders)
            {
                HeaderTitle = "Purchase Orders";
                ShowContent(new PurchaseOrdersControl());
            }
            else if (key == KeySuppliers)
            {
                HeaderTitle = "Suppliers";
                ShowContent(new SupplierControl());
            }
            else if (key == KeyCategories)
            {
                HeaderTitle = "Categories";
                ShowContent(new CategoryControl());
            }
            else if (key == KeySales)
            {
                HeaderTitle = "Sales";
                ShowContent(new SalesHistoryControl());
            }
            else if (key == KeyPos)
            {
                HeaderTitle = "POS";
                ShowContent(new PosControl());
            }
            else if (key == KeyUsers)
            {
                HeaderTitle = "Users";
                ShowContent(new UserManagementControl());
            }
            else if (key == KeyReports)
            {
                HeaderTitle = "Reports";
                ShowContent(new ReportsControl());
            }
            else if (key == KeySettings)
            {
                HeaderTitle = "Settings";
                ShowContent(new SettingsControl());
            }
        }

        private void NavigateToSalesWithRange(DateTime fromInclusive, DateTime toInclusive)
        {
            _currentKey = KeySales;
            UpdateActiveNav(KeySales);
            HeaderTitle = "Sales";
            ShowContent(new SalesHistoryControl(fromInclusive, toInclusive));
        }

        private void ShowContent(Control content)
        {
            if (content == null) return;

            if (_currentContent != null && !_currentContent.IsDisposed)
            {
                _contentHost.Controls.Remove(_currentContent);
                _currentContent.Dispose();
            }

            _currentContent = content;
            content.Dock = DockStyle.Fill;
            _contentHost.Controls.Add(content);
            content.BringToFront();

            Animator.SlideInControl(content, -24, 180);
        }

        private void UpdateActiveNav(string key)
        {
            foreach (var kv in _navItems)
            {
                kv.Value.IsActive = (kv.Key == key);
            }
        }

        // ============================================================
        // LOGOUT
        // ============================================================

        private void Logout()
        {
            var result = MessageBox.Show(
                "Log out and return to the login screen?",
                "Confirm Logout",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes) return;

            SessionManager.Logout();
            Close();
        }

        // ============================================================
        // NAV ITEM CONTROL (light theme)
        // ============================================================

        private class NavItemControl : Panel
        {
            public string Key { get; }
            public event EventHandler ItemClicked;

            private readonly Label _iconLabel;
            private readonly Label _textLabel;
            private bool _isActive;

            public NavItemControl(string key, string icon, string text)
            {
                Key = key;

                Height = 42;
                Width = Theme.SidebarWidth;
                Margin = new Padding(0);
                BackColor = Theme.SidebarBg;
                Cursor = Cursors.Hand;

                _iconLabel = new Label
                {
                    Text = icon,
                    Font = new Font(Theme.IconFontFamily, 11F, FontStyle.Regular),
                    ForeColor = Theme.SidebarNavIcon,
                    AutoSize = false,
                    Size = new Size(28, 42),
                    Location = new Point(20, 0),
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };

                _textLabel = new Label
                {
                    Text = text,
                    Font = Theme.FontBody,
                    ForeColor = Theme.SidebarNavText,
                    AutoSize = false,
                    Height = 42,
                    Location = new Point(54, 0),
                    Width = Theme.SidebarWidth - 54 - 8,
                    TextAlign = ContentAlignment.MiddleLeft,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                };

                Controls.Add(_iconLabel);
                Controls.Add(_textLabel);

                Resize += (s, e) =>
                {
                    _textLabel.Width = Math.Max(0, Width - _textLabel.Left - 8);
                };

                MouseEnter += (s, e) => ApplyHover(true);
                MouseLeave += (s, e) => ApplyHover(false);
                Click += (s, e) => ItemClicked?.Invoke(this, EventArgs.Empty);

                _iconLabel.MouseEnter += (s, e) => ApplyHover(true);
                _iconLabel.MouseLeave += (s, e) => ApplyHover(false);
                _iconLabel.Click += (s, e) => ItemClicked?.Invoke(this, EventArgs.Empty);

                _textLabel.MouseEnter += (s, e) => ApplyHover(true);
                _textLabel.MouseLeave += (s, e) => ApplyHover(false);
                _textLabel.Click += (s, e) => ItemClicked?.Invoke(this, EventArgs.Empty);
            }

            [System.ComponentModel.DesignerSerializationVisibility(
                System.ComponentModel.DesignerSerializationVisibility.Hidden)]
            public bool IsActive
            {
                get => _isActive;
                set
                {
                    if (_isActive == value) return;
                    _isActive = value;

                    if (_isActive)
                    {
                        BackColor = Theme.SidebarActive;
                        _iconLabel.ForeColor = Theme.SidebarActiveText;
                        _textLabel.ForeColor = Theme.SidebarActiveText;
                    }
                    else
                    {
                        BackColor = Theme.SidebarBg;
                        _iconLabel.ForeColor = Theme.SidebarNavIcon;
                        _textLabel.ForeColor = Theme.SidebarNavText;
                    }
                }
            }

            private void ApplyHover(bool hover)
            {
                if (_isActive) return;
                BackColor = hover ? Theme.SidebarHover : Theme.SidebarBg;
            }
        }
    }

    // ================================================================
    // PLACEHOLDER CONTROL
    // ================================================================

    public class PlaceholderControl : UserControl
    {
        public PlaceholderControl(string title, string message)
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(Theme.SpacingXl);

            var titleLabel = new Label
            {
                Text = title,
                Font = new Font(Theme.UiFontFamily, 20F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 40,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var msgLabel = new Label
            {
                Text = message,
                Font = Theme.FontBody,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 26,
                TextAlign = ContentAlignment.MiddleLeft
            };

            Controls.Add(msgLabel);
            Controls.Add(titleLabel);
        }
    }
}