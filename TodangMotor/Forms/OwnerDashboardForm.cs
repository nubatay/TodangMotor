using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using TodangMotor.Common;
using TodangMotor.Controls;

namespace TodangMotor.Forms
{
    /// <summary>
    /// Owner dashboard shell. Full-height sidebar on the left, header bar
    /// only over the content area on the right.
    /// All sidebar items swap UserControls into the content area.
    /// </summary>
    public class OwnerDashboardForm : ShellForm
    {
        // ============================================================
        // NAV DEFINITION
        // ============================================================

        private const string KeyDashboard = "dashboard";
        private const string KeyInventory = "inventory";
        private const string KeyStockIn = "stockin";
        private const string KeySuppliers = "suppliers";
        private const string KeyCategories = "categories";
        private const string KeySales = "sales";
        private const string KeyUsers = "users";
        private const string KeySettings = "settings";

        private const string IconDashboard = "\uE80F";
        private const string IconInventory = "\uE7B8";
        private const string IconStockIn = "\uE710";
        private const string IconSuppliers = "\uE716";
        private const string IconCategories = "\uE8EC";
        private const string IconSales = "\uE719";
        private const string IconUsers = "\uE77B";
        private const string IconSettings = "\uE713";
        private const string IconLogout = "\uE7E8";

        // ============================================================
        // FIELDS
        // ============================================================

        private TableLayoutPanel _rootLayout;
        private Panel _mainArea;
        private Panel _sidebar;
        private Panel _contentHost;
        private FlowLayoutPanel _navList;
        private Dictionary<string, NavItemControl> _navItems = new();

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
            WindowState = FormWindowState.Maximized;
            BackColor = Theme.Background;

            HeaderTitleLabel.Padding = new Padding(20, 0, 0, 0);

            RestructureLayout();

            BuildSidebar();
            BuildContentHost();
            UpdateActiveNav(KeyDashboard);

            EnsureChromeButtons();

            Shown += OwnerDashboardForm_Shown;
        }

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

            _navList = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = false,
                BackColor = Theme.SidebarBg,
                Padding = new Padding(0, 6, 0, 6)
            };

            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 74,
                BackColor = Theme.SidebarBg
            };

            var divider = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = Theme.DividerDark
            };

            var logout = new NavItemControl(
                "logout", IconLogout, "Logout",
                Theme.SidebarBg, Theme.SidebarHover, Theme.SidebarHover);
            logout.Dock = DockStyle.Fill;
            logout.ItemClicked += (s, e) => Logout();

            footer.Controls.Add(logout);
            footer.Controls.Add(divider);

            var header = BuildSidebarHeader();

            _sidebar.Controls.Add(_navList);
            _sidebar.Controls.Add(footer);
            _sidebar.Controls.Add(header);

            AddNavItem(KeyDashboard, IconDashboard, "Dashboard");
            AddNavItem(KeyInventory, IconInventory, "Inventory");
            AddNavItem(KeyStockIn, IconStockIn, "Stock-In");
            AddNavItem(KeySuppliers, IconSuppliers, "Suppliers");
            AddNavItem(KeyCategories, IconCategories, "Categories");
            AddNavItem(KeySales, IconSales, "Sales");
            AddNavItem(KeyUsers, IconUsers, "Users");
            AddNavItem(KeySettings, IconSettings, "Settings");

            _rootLayout.Controls.Add(_sidebar, 0, 0);
        }

        private Panel BuildSidebarHeader()
        {
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 132,
                BackColor = Theme.SidebarBg
            };

            // Small blue tile with a white "T" — no image loaded.
            var logo = new LogoPlaceholder
            {
                Width = 48,
                Height = 48,
                Radius = 12,
                Location = new Point(20, 32),
                TileColor = Theme.Primary,
                LetterColor = Color.White,
                Letter = "T",
                ForcePlaceholder = true
            };

            var ownerName = new Label
            {
                Text = SessionManager.CurrentUser?.FullName ?? "Owner",
                Font = new Font(Theme.UiFontFamily, 12F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                Location = new Point(80, 30),
                Size = new Size(128, 30),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                AutoEllipsis = false
            };

            var roleLabel = new Label
            {
                Text = "Owner",
                Font = Theme.FontSmall,
                ForeColor = Color.FromArgb(170, 182, 215),
                AutoSize = false,
                Location = new Point(80, 60),
                Size = new Size(128, 18),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            var divider = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = Theme.DividerDark
            };

            header.Controls.Add(logo);
            header.Controls.Add(ownerName);
            header.Controls.Add(roleLabel);
            header.Controls.Add(divider);

            return header;
        }

        private void AddNavItem(string key, string icon, string label)
        {
            var item = new NavItemControl(key, icon, label,
                Theme.SidebarBg, Theme.SidebarHover, Theme.SidebarActive);
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
                    else if (navKey == "sales") NavigateTo(KeySales);
                };
                ShowContent(home);
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
            else if (key == KeyUsers)
            {
                HeaderTitle = "Users";
                ShowContent(new UserManagementControl());
            }
            else if (key == KeySettings)
            {
                HeaderTitle = "Settings";
                ShowContent(new SettingsControl());
            }
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
        // NAV ITEM CONTROL
        // ============================================================

        private class NavItemControl : Panel
        {
            public string Key { get; }
            public event EventHandler ItemClicked;

            private readonly Label _iconLabel;
            private readonly Label _textLabel;
            private readonly Color _bgNormal;
            private readonly Color _bgHover;
            private readonly Color _bgActive;
            private bool _isActive;

            public NavItemControl(string key, string icon, string text,
                Color bgNormal, Color bgHover, Color bgActive)
            {
                Key = key;
                _bgNormal = bgNormal;
                _bgHover = bgHover;
                _bgActive = bgActive;

                Height = 46;
                Width = Theme.SidebarWidth;
                Margin = new Padding(0);
                BackColor = bgNormal;
                Cursor = Cursors.Hand;

                _iconLabel = new Label
                {
                    Text = icon,
                    Font = new Font(Theme.IconFontFamily, 12F, FontStyle.Regular),
                    ForeColor = Theme.TextOnSidebar,
                    AutoSize = false,
                    Size = new Size(36, 46),
                    Location = new Point(18, 0),
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };

                _textLabel = new Label
                {
                    Text = text,
                    Font = Theme.FontBody,
                    ForeColor = Theme.TextOnSidebar,
                    AutoSize = false,
                    Height = 46,
                    Location = new Point(58, 0),
                    Width = Theme.SidebarWidth - 58 - 12,
                    TextAlign = ContentAlignment.MiddleLeft,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                };

                Controls.Add(_iconLabel);
                Controls.Add(_textLabel);

                Resize += (s, e) =>
                {
                    _textLabel.Width = Math.Max(0, Width - _textLabel.Left - 12);
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
                    BackColor = _isActive ? _bgActive : _bgNormal;
                }
            }

            private void ApplyHover(bool hover)
            {
                if (_isActive) return;
                BackColor = hover ? _bgHover : _bgNormal;
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