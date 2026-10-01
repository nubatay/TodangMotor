using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TodangMotor.Common;
using TodangMotor.Forms;
using TodangMotor.Models;
using TodangMotor.Services;

namespace TodangMotor.Controls
{
    /// <summary>
    /// User Management list view — Owner-only. Shows all users,
    /// supports Add / Edit / Deactivate / Reactivate.
    /// </summary>
    public class UserManagementControl : UserControl
    {
        // ============================================================
        // SERVICES
        // ============================================================

        private readonly UserService _userService = new();

        // ============================================================
        // STATE
        // ============================================================

        private List<User> _allUsers = new();

        private bool _isLoaded;
        private bool _isBusy;

        // ============================================================
        // UI CONTROLS
        // ============================================================

        private RoundedTextBox _searchBox;
        private RoundedComboBox _roleFilter;
        private RoundedComboBox _statusFilter;
        private Label _countLabel;

        private DataGridView _grid;

        private Button _btnAdd;
        private Button _btnEdit;
        private Button _btnDeactivate;
        private Button _btnReactivate;

        private Label _statusLabel;

        // ============================================================
        // CONSTRUCTION
        // ============================================================

        public UserManagementControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(Theme.SpacingLg);

            BuildLayout();

            Load += UserManagementControl_Load;
        }

        private async void UserManagementControl_Load(object sender, EventArgs e)
        {
            if (_isLoaded) return;
            _isLoaded = true;
            await ReloadAllAsync();
        }

        // ============================================================
        // LAYOUT
        // ============================================================

        private void BuildLayout()
        {
            Controls.Add(BuildGrid());        // Fill
            Controls.Add(BuildBottomBar());   // Bottom
            Controls.Add(BuildFilterBar());   // Top
        }

        private Panel BuildFilterBar()
        {
            var bar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 84,
                BackColor = Theme.Background
            };

            _searchBox = UiFactory.CreateTextBox(240);
            _searchBox.Location = new Point(4, 12);
            _searchBox.Placeholder = "Search username or full name…";
            _searchBox.MaxLength = 100;
            _searchBox.TextChanged += (s, e) => ApplyFilters();

            var lblRole = new Label
            {
                Text = "Role:",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(40, 18),
                Location = new Point(252, 24),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _roleFilter = new RoundedComboBox
            {
                Width = 130,
                Location = new Point(296, 12)
            };
            _roleFilter.SelectedIndexChanged += (s, e) => ApplyFilters();

            var lblStatus = new Label
            {
                Text = "Status:",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(56, 18),
                Location = new Point(436, 24),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _statusFilter = new RoundedComboBox
            {
                Width = 120,
                Location = new Point(494, 12)
            };
            _statusFilter.SelectedIndexChanged += (s, e) => ApplyFilters();

            _countLabel = new Label
            {
                Text = "Loading…",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(4, 58),
                Height = 20,
                Width = 700,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            bar.Controls.Add(_searchBox);
            bar.Controls.Add(lblRole);
            bar.Controls.Add(_roleFilter);
            bar.Controls.Add(lblStatus);
            bar.Controls.Add(_statusFilter);
            bar.Controls.Add(_countLabel);

            return bar;
        }

        private DataGridView BuildGrid()
        {
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 0, 4)
            };
            UiFactory.StyleGrid(_grid);
            _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 251);
            _grid.CellDoubleClick += Grid_CellDoubleClick;
            _grid.KeyDown += Grid_KeyDown;
            return _grid;
        }

        private Panel BuildBottomBar()
        {
            var bar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 68,
                BackColor = Theme.Background
            };

            _btnAdd = UiFactory.CreateButton("Add New", UiFactory.ButtonStyle.Primary, 110, 40);
            _btnAdd.Location = new Point(4, 14);
            _btnAdd.Click += (s, e) => OpenUserFormForAdd();

            _btnEdit = UiFactory.CreateButton("Edit", UiFactory.ButtonStyle.Secondary, 90, 40);
            _btnEdit.Location = new Point(122, 14);
            _btnEdit.Click += (s, e) => EditSelectedUser();

            _btnDeactivate = UiFactory.CreateButton("Deactivate", UiFactory.ButtonStyle.Ghost, 110, 40);
            _btnDeactivate.Location = new Point(220, 14);
            _btnDeactivate.Click += async (s, e) => await DeactivateSelectedUserAsync();

            _btnReactivate = UiFactory.CreateButton("Reactivate", UiFactory.ButtonStyle.Ghost, 110, 40);
            _btnReactivate.Location = new Point(338, 14);
            _btnReactivate.Click += async (s, e) => await ReactivateSelectedUserAsync();

            _statusLabel = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(468, 14),
                Height = 40,
                Width = 500,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            bar.Controls.Add(_btnAdd);
            bar.Controls.Add(_btnEdit);
            bar.Controls.Add(_btnDeactivate);
            bar.Controls.Add(_btnReactivate);
            bar.Controls.Add(_statusLabel);

            return bar;
        }

        // ============================================================
        // DATA LOAD
        // ============================================================

        private async Task ReloadAllAsync()
        {
            SetBusy(true);
            try
            {
                var (success, error, users) = await _userService.GetAllAsync();
                if (!success)
                {
                    _allUsers = new List<User>();
                    ShowStatus(error, isError: true);
                    ApplyFilters();
                    return;
                }

                _allUsers = users ?? new List<User>();

                // Initialize the filter dropdowns once (preserves selection afterwards).
                if (_roleFilter.Items.Count == 0)
                {
                    _roleFilter.Items.Add("All Roles");
                    _roleFilter.Items.Add("Owner");
                    _roleFilter.Items.Add("Cashier");
                    _roleFilter.SelectedIndex = 0;
                }

                if (_statusFilter.Items.Count == 0)
                {
                    _statusFilter.Items.Add("Active");
                    _statusFilter.Items.Add("Inactive");
                    _statusFilter.Items.Add("All");
                    _statusFilter.SelectedIndex = 0;
                }

                ApplyFilters();
            }
            finally
            {
                SetBusy(false);
            }
        }

        // ============================================================
        // FILTERS + GRID BINDING
        // ============================================================

        private void ApplyFilters()
        {
            if (_allUsers == null) return;

            IEnumerable<User> filtered = _allUsers;

            // Search: username OR full name
            string search = (_searchBox?.Text ?? string.Empty).Trim();
            if (search.Length > 0)
            {
                filtered = filtered.Where(u =>
                    (!string.IsNullOrEmpty(u.Username) &&
                     u.Username.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(u.FullName) &&
                     u.FullName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            // Role filter
            string role = _roleFilter?.SelectedItem as string;
            if (!string.IsNullOrEmpty(role) && role != "All Roles")
                filtered = filtered.Where(u => u.Role == role);

            // Status filter
            string status = _statusFilter?.SelectedItem as string;
            if (status == "Active")
                filtered = filtered.Where(u => u.IsActive);
            else if (status == "Inactive")
                filtered = filtered.Where(u => !u.IsActive);

            var rows = filtered
                .OrderBy(u => u.Role == "Owner" ? 0 : 1)
                .ThenBy(u => u.Username)
                .Select(u => new
                {
                    u.UserId,
                    Username = u.Username ?? string.Empty,
                    FullName = u.FullName ?? string.Empty,
                    Role = u.Role ?? string.Empty,
                    Status = u.IsActive ? "Active" : "Inactive",
                    CreatedAt = u.CreatedAt.ToString("MMM d, yyyy")
                })
                .ToList();

            _grid.DataSource = rows;
            ApplyColumnLayout();
            ClearSelection();
            UpdateCountLabel(rows.Count);
        }

        private void UpdateCountLabel(int shownCount)
        {
            if (_countLabel == null) return;

            int total = _allUsers?.Count ?? 0;
            int active = _allUsers?.Count(u => u.IsActive) ?? 0;
            int inactive = total - active;

            string status = _statusFilter?.SelectedItem as string ?? "Active";

            _countLabel.Text =
                $"Showing {shownCount} of {total} user(s)   ·   " +
                $"Status filter: {status}   ·   " +
                $"Active: {active}   Inactive: {inactive}";
        }

        private void ApplyColumnLayout()
        {
            if (_grid == null || _grid.Columns.Count == 0) return;

            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            void SetWeight(string col, int weight, string header = null)
            {
                if (!_grid.Columns.Contains(col)) return;
                _grid.Columns[col].FillWeight = weight;
                if (header != null) _grid.Columns[col].HeaderText = header;
            }

            if (_grid.Columns.Contains("UserId"))
                _grid.Columns["UserId"].Visible = false;

            SetWeight("Username", 180, "Username");
            SetWeight("FullName", 280, "Full Name");
            SetWeight("Role", 100, "Role");
            SetWeight("Status", 100, "Status");
            SetWeight("CreatedAt", 130, "Created");
        }

        private void ClearSelection()
        {
            if (_grid != null && _grid.Rows.Count > 0)
                _grid.ClearSelection();
        }

        // ============================================================
        // SELECTION HELPER
        // ============================================================

        private User? GetSelectedUser()
        {
            if (_grid == null || _grid.SelectedRows.Count == 0) return null;
            var row = _grid.SelectedRows[0];
            if (!_grid.Columns.Contains("UserId")) return null;

            var value = row.Cells["UserId"].Value;
            if (value == null) return null;

            int id = Convert.ToInt32(value);
            return _allUsers.FirstOrDefault(u => u.UserId == id);
        }

        // ============================================================
        // ROW EVENTS
        // ============================================================

        private void Grid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            EditSelectedUser();
        }

        private void Grid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                EditSelectedUser();
            }
        }

        // ============================================================
        // ADD / EDIT
        // ============================================================

        private void OpenUserFormForAdd()
        {
            try
            {
                var parent = FindForm();
                using var form = new UserForm();
                if (form.ShowDialog(parent) == DialogResult.OK)
                {
                    _ = ReloadAllAsync();
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Could not open User form: {ex.Message}", isError: true);
            }
        }

        private void EditSelectedUser()
        {
            var u = GetSelectedUser();
            if (u == null)
            {
                ShowStatus("Please select a user first.", isError: true);
                return;
            }

            try
            {
                var parent = FindForm();
                using var form = new UserForm(u);
                if (form.ShowDialog(parent) == DialogResult.OK)
                {
                    _ = ReloadAllAsync();
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Could not open User form: {ex.Message}", isError: true);
            }
        }

        // ============================================================
        // DEACTIVATE / REACTIVATE
        // ============================================================

        private async Task DeactivateSelectedUserAsync()
        {
            var u = GetSelectedUser();
            if (u == null)
            {
                ShowStatus("Please select a user first.", isError: true);
                return;
            }
            if (!u.IsActive)
            {
                ShowStatus("This user is already inactive.", isError: true);
                return;
            }

            var confirm = MessageBox.Show(
                $"Are you sure you want to deactivate \"{u.Username}\"?\n\n" +
                "They will not be able to log in until reactivated.",
                "Confirm Deactivate",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;

            SetBusy(true);
            try
            {
                var (success, error) = await _userService.DeactivateAsync(u.UserId);
                if (success)
                {
                    ShowStatus("User deactivated.", isError: false);
                    await ReloadAllAsync();
                }
                else
                {
                    ShowStatus(error, isError: true);
                }
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task ReactivateSelectedUserAsync()
        {
            var u = GetSelectedUser();
            if (u == null)
            {
                ShowStatus("Please select a user first.", isError: true);
                return;
            }
            if (u.IsActive)
            {
                ShowStatus("This user is already active.", isError: true);
                return;
            }

            SetBusy(true);
            try
            {
                var (success, error) = await _userService.ReactivateAsync(u.UserId);
                if (success)
                {
                    ShowStatus("User reactivated.", isError: false);
                    await ReloadAllAsync();
                }
                else
                {
                    ShowStatus(error, isError: true);
                }
            }
            finally
            {
                SetBusy(false);
            }
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            _btnAdd.Enabled = !busy;
            _btnEdit.Enabled = !busy;
            _btnDeactivate.Enabled = !busy;
            _btnReactivate.Enabled = !busy;
        }

        private void ShowStatus(string message, bool isError)
        {
            if (_statusLabel == null) return;
            _statusLabel.ForeColor = isError ? Theme.Danger : Theme.Success;
            _statusLabel.Text = message ?? string.Empty;
        }
    }
}