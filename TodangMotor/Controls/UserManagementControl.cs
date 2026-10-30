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
    public class UserManagementControl : UserControl
    {
        private readonly UserService _userService = new();

        private List<User> _allUsers = new();

        private bool _isLoaded;

        private const string PencilIcon = "\uE70F";
        private const string BanIcon = "\uE711";
        private const string CheckIcon = "\uE73E";

        private const int ColRoleW = 120;
        private const int ColStatusW = 140;
        private const int ColCreatedW = 160;
        private const int ColActionW = 60;

        private RoundedTextBox _searchBox;
        private RoundedComboBox _roleFilter;
        private RoundedComboBox _statusFilter;
        private Label _countLabel;

        private DataGridView _grid;

        private Button _btnAdd;
        private Label _statusLabel;

        public UserManagementControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(UiFactory.PagePadding);

            BuildLayout();

            Load += UserManagementControl_Load;
        }

        private async void UserManagementControl_Load(object sender, EventArgs e)
        {
            if (_isLoaded) return;
            _isLoaded = true;
            await ReloadAllAsync();
        }

        private void BuildLayout()
        {
            Controls.Add(BuildGrid());
            Controls.Add(BuildBottomBar());
            Controls.Add(BuildFilterBar());
        }

        private Panel BuildFilterBar()
        {
            var bar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 108,
                BackColor = Theme.Background
            };

            _searchBox = UiFactory.CreateTextBox(300);
            _searchBox.Location = new Point(0, 16);
            _searchBox.Placeholder = "Search username or full name…";
            _searchBox.MaxLength = 100;
            _searchBox.TextChanged += (s, e) => ApplyFilters();

            var lblRole = new Label
            {
                Text = "Role:",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(48, 24),
                Location = new Point(_searchBox.Right + 24, 24),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _roleFilter = new RoundedComboBox
            {
                Width = 160,
                Location = new Point(lblRole.Right + 8, 16)
            };
            _roleFilter.SelectedIndexChanged += (s, e) => ApplyFilters();

            var lblStatus = new Label
            {
                Text = "Status:",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(56, 24),
                Location = new Point(_roleFilter.Right + 24, 24),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _statusFilter = new RoundedComboBox
            {
                Width = 160,
                Location = new Point(lblStatus.Right + 8, 16)
            };
            _statusFilter.SelectedIndexChanged += (s, e) => ApplyFilters();

            _countLabel = new Label
            {
                Text = "Loading…",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(0, 76),
                Height = 22,
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
                Margin = new Padding(0, 8, 0, 0)
            };
            UiFactory.StyleGridModern(_grid);
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            _grid.CellDoubleClick += Grid_CellDoubleClick;
            _grid.CellClick += Grid_CellClick;
            _grid.CellPainting += Grid_CellPainting;
            _grid.CellMouseEnter += Grid_CellMouseEnter;
            _grid.CellMouseLeave += (s, e) => _grid.Cursor = Cursors.Default;
            _grid.KeyDown += Grid_KeyDown;

            PrimeGridColumns();
            _grid.Resize += (s, e) => FitDynamicColumns();

            return _grid;
        }

        private void PrimeGridColumns()
        {
            _grid.AutoGenerateColumns = false;
            _grid.Columns.Clear();
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            void AddTextCol(string prop, string header, int width, bool readOnly,
                DataGridViewContentAlignment align = DataGridViewContentAlignment.MiddleLeft)
            {
                var col = new DataGridViewTextBoxColumn
                {
                    Name = prop,
                    DataPropertyName = prop,
                    HeaderText = header,
                    Width = width,
                    ReadOnly = readOnly,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                    Resizable = DataGridViewTriState.False
                };
                if (align != DataGridViewContentAlignment.MiddleLeft)
                    col.DefaultCellStyle.Alignment = align;
                _grid.Columns.Add(col);
            }

            AddTextCol("Username", "Username", 260, true);
            AddTextCol("FullName", "Full Name", 360, true);
            AddTextCol("Role", "Role", ColRoleW, true, DataGridViewContentAlignment.MiddleCenter);
            AddTextCol("Status", "Status", ColStatusW, true, DataGridViewContentAlignment.MiddleCenter);
            AddTextCol("CreatedAt", "Created", ColCreatedW, true, DataGridViewContentAlignment.MiddleRight);

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "EditIcon",
                HeaderText = "",
                Width = ColActionW,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                Resizable = DataGridViewTriState.False
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ToggleIcon",
                HeaderText = "",
                Width = ColActionW,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                Resizable = DataGridViewTriState.False
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "UserId",
                DataPropertyName = "UserId",
                Visible = false
            });
        }

        private void FitDynamicColumns()
        {
            if (_grid == null) return;
            if (!_grid.Columns.Contains("Username")) return;
            if (!_grid.Columns.Contains("FullName")) return;

            int fixedW = ColRoleW + ColStatusW + ColCreatedW + ColActionW + ColActionW;
            int available = _grid.ClientSize.Width - fixedW - 24;
            if (available < 300) available = 300;

            int userW = (int)(available * 0.42);
            int fullW = available - userW;

            _grid.Columns["Username"].Width = userW;
            _grid.Columns["FullName"].Width = fullW;
        }

        private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            string colName = _grid.Columns[e.ColumnIndex].Name;
            if (colName != "EditIcon" && colName != "ToggleIcon") return;

            e.PaintBackground(e.CellBounds, true);

            string glyph;
            Color color;

            if (colName == "EditIcon")
            {
                glyph = PencilIcon;
                color = Theme.Primary;
            }
            else
            {
                string status = _grid.Rows[e.RowIndex].Cells["Status"].Value?.ToString() ?? "";
                bool isActive = status == "Active";

                if (isActive)
                {
                    glyph = BanIcon;
                    color = Color.FromArgb(200, 130, 40);
                }
                else
                {
                    glyph = CheckIcon;
                    color = Theme.Success;
                }
            }

            using var font = new Font(Theme.IconFontFamily, 14F, FontStyle.Regular);
            TextRenderer.DrawText(
                e.Graphics,
                glyph,
                font,
                e.CellBounds,
                color,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix);

            e.Handled = true;
        }

        private void Grid_CellMouseEnter(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string colName = _grid.Columns[e.ColumnIndex].Name;
            _grid.Cursor = (colName == "EditIcon" || colName == "ToggleIcon")
                ? Cursors.Hand
                : Cursors.Default;
        }

        private void Grid_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            string colName = _grid.Columns[e.ColumnIndex].Name;
            if (colName != "EditIcon" && colName != "ToggleIcon") return;

            var idCell = _grid.Rows[e.RowIndex].Cells["UserId"];
            if (idCell?.Value == null) return;

            int id = Convert.ToInt32(idCell.Value);
            var user = _allUsers.FirstOrDefault(u => u.UserId == id);
            if (user == null) return;

            if (colName == "EditIcon")
            {
                OpenUserFormForEdit(user);
            }
            else
            {
                if (user.IsActive) _ = DeactivateUserAsync(user);
                else _ = ReactivateUserAsync(user);
            }
        }

        private Panel BuildBottomBar()
        {
            var bar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 88,
                BackColor = Theme.Background
            };

            _btnAdd = UiFactory.CreateButton("Add New", UiFactory.ButtonStyle.Primary, 140, UiFactory.PrimaryButtonHeight);
            _btnAdd.Location = new Point(0, 20);
            _btnAdd.Click += (s, e) => OpenUserFormForAdd();

            _statusLabel = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(_btnAdd.Right + 24, 20),
                Height = UiFactory.PrimaryButtonHeight,
                Width = 600,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            bar.Controls.Add(_btnAdd);
            bar.Controls.Add(_statusLabel);

            return bar;
        }

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

        private void ApplyFilters()
        {
            if (_allUsers == null) return;

            IEnumerable<User> filtered = _allUsers;

            string search = (_searchBox?.Text ?? string.Empty).Trim();
            if (search.Length > 0)
            {
                filtered = filtered.Where(u =>
                    (!string.IsNullOrEmpty(u.Username) &&
                     u.Username.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(u.FullName) &&
                     u.FullName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            string role = _roleFilter?.SelectedItem as string;
            if (!string.IsNullOrEmpty(role) && role != "All Roles")
                filtered = filtered.Where(u => u.Role == role);

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

            if (_grid.Columns.Contains("UserId"))
                _grid.Columns["UserId"].Visible = false;

            if (_grid.Rows.Count > 0)
                _grid.ClearSelection();

            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.Cells["Role"].Value is string r)
                {
                    row.Cells["Role"].Style.ForeColor = r == "Owner"
                        ? Theme.Primary
                        : Theme.TextSecondary;
                    row.Cells["Role"].Style.Font = Theme.FontBodyBold;
                }

                if (row.Cells["Status"].Value is string s)
                {
                    row.Cells["Status"].Style.ForeColor =
                        s == "Active" ? Theme.Success : Theme.TextMuted;
                    row.Cells["Status"].Style.Font = Theme.FontBodyBold;
                }
            }

            UpdateCountLabel(rows.Count);
            FitDynamicColumns();
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

        private void Grid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var idCell = _grid.Rows[e.RowIndex].Cells["UserId"];
            if (idCell?.Value == null) return;

            int id = Convert.ToInt32(idCell.Value);
            var user = _allUsers.FirstOrDefault(u => u.UserId == id);
            if (user != null) OpenUserFormForEdit(user);
        }

        private void Grid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;

            if (_grid.CurrentRow == null) return;
            var idCell = _grid.CurrentRow.Cells["UserId"];
            if (idCell?.Value == null) return;

            int id = Convert.ToInt32(idCell.Value);
            var user = _allUsers.FirstOrDefault(u => u.UserId == id);
            if (user != null) OpenUserFormForEdit(user);
        }

        private void OpenUserFormForAdd()
        {
            try
            {
                var parent = FindForm();
                using var form = new UserForm();
                if (form.ShowDialog(parent) == DialogResult.OK)
                    _ = ReloadAllAsync();
            }
            catch (Exception ex)
            {
                ShowStatus($"Could not open User form: {ex.Message}", isError: true);
            }
        }

        private void OpenUserFormForEdit(User user)
        {
            try
            {
                var parent = FindForm();
                using var form = new UserForm(user);
                if (form.ShowDialog(parent) == DialogResult.OK)
                    _ = ReloadAllAsync();
            }
            catch (Exception ex)
            {
                ShowStatus($"Could not open User form: {ex.Message}", isError: true);
            }
        }

        private async Task DeactivateUserAsync(User u)
        {
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

        private async Task ReactivateUserAsync(User u)
        {
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

        private void SetBusy(bool busy)
        {
            _btnAdd.Enabled = !busy;
            _grid.Enabled = !busy;
        }

        private void ShowStatus(string message, bool isError)
        {
            if (_statusLabel == null) return;
            _statusLabel.ForeColor = isError ? Theme.Danger : Theme.Success;
            _statusLabel.Text = message ?? string.Empty;
        }
    }
}