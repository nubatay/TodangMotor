using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TodangMotor.Common;
using TodangMotor.Data;
using TodangMotor.Models;
using TodangMotor.Services;

namespace TodangMotor.Controls
{
    /// <summary>
    /// Category management view — list + toolbar, lives inside the
    /// Owner dashboard content area as a UserControl.
    /// Single-record Add/Edit is delegated to CategoryForm (popup).
    /// </summary>
    public class CategoryControl : UserControl
    {
        // ============================================================
        // SERVICES
        // ============================================================

        private readonly CategoryService _categoryService;

        // ============================================================
        // STATE
        // ============================================================

        private List<Category> _allCategories = new();

        private bool _isLoaded;
        private bool _isBusy;

        // ============================================================
        // UI CONTROLS
        // ============================================================

        private RoundedTextBox _searchBox;
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

        public CategoryControl()
        {
            _categoryService = new CategoryService(new CategoryRepository());

            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(Theme.SpacingLg);

            BuildLayout();

            Load += CategoryControl_Load;
        }

        private async void CategoryControl_Load(object sender, EventArgs e)
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
            // Add Fill control FIRST so it docks LAST and takes only the
            // space left after Top/Bottom take theirs.
            Controls.Add(BuildGrid());        // Fill
            Controls.Add(BuildBottomBar());   // Bottom
            Controls.Add(BuildFilterBar());   // Top
        }

        private Panel BuildFilterBar()
        {
            var bar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 96,
                BackColor = Theme.Background
            };

            _searchBox = UiFactory.CreateTextBox(320);
            _searchBox.Location = new Point(4, 12);
            _searchBox.Placeholder = "Search category name…";
            _searchBox.MaxLength = 100;
            _searchBox.TextChanged += (s, e) => ApplyFilters();

            var lblStatus = new Label
            {
                Text = "Status:",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = true,
                Location = new Point(344, 24),
                BackColor = Color.Transparent
            };

            _statusFilter = new RoundedComboBox
            {
                Width = 150,
                Location = new Point(397, 18)
            };
            _statusFilter.Items.Add("Active");
            _statusFilter.Items.Add("Inactive");
            _statusFilter.Items.Add("All");
            _statusFilter.SelectedIndex = 0;
            _statusFilter.SelectedIndexChanged += (s, e) => ApplyFilters();

            _countLabel = new Label
            {
                Text = "Loading…",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(4, 68),
                Height = 20,
                Width = 700,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            bar.Controls.Add(_searchBox);
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
            _btnAdd.Location = new Point(4, 12);
            _btnAdd.Click += (s, e) => OpenCategoryFormForAdd();

            _btnEdit = UiFactory.CreateButton("Edit", UiFactory.ButtonStyle.Secondary, 90, 40);
            _btnEdit.Location = new Point(122, 12);
            _btnEdit.Click += (s, e) => EditSelectedCategory();

            _btnDeactivate = UiFactory.CreateButton("Deactivate", UiFactory.ButtonStyle.Ghost, 110, 40);
            _btnDeactivate.Location = new Point(220, 12);
            _btnDeactivate.Click += async (s, e) => await DeactivateSelectedCategoryAsync();

            _btnReactivate = UiFactory.CreateButton("Reactivate", UiFactory.ButtonStyle.Ghost, 110, 40);
            _btnReactivate.Location = new Point(338, 12);
            _btnReactivate.Click += async (s, e) => await ReactivateSelectedCategoryAsync();

            _statusLabel = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(468, 12),
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
                var cats = await _categoryService.GetAllCategoriesAsync();
                _allCategories = cats?.ToList() ?? new List<Category>();
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
            if (_allCategories == null) return;

            IEnumerable<Category> filtered = _allCategories;

            string search = (_searchBox?.Text ?? string.Empty).Trim();
            if (search.Length > 0)
            {
                filtered = filtered.Where(c =>
                    !string.IsNullOrEmpty(c.CategoryName) &&
                    c.CategoryName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            string status = _statusFilter?.SelectedItem as string;
            if (status == "Active")
                filtered = filtered.Where(c => c.IsActive);
            else if (status == "Inactive")
                filtered = filtered.Where(c => !c.IsActive);

            var rows = filtered
                .OrderBy(c => c.CategoryName)
                .Select(c => new
                {
                    c.CategoryId,
                    CategoryName = c.CategoryName ?? string.Empty,
                    Status = c.IsActive ? "Active" : "Inactive"
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

            int total = _allCategories?.Count ?? 0;
            int active = _allCategories?.Count(c => c.IsActive) ?? 0;
            int inactive = total - active;

            string status = _statusFilter?.SelectedItem as string ?? "Active";

            _countLabel.Text =
                $"Showing {shownCount} of {total} categor{(total == 1 ? "y" : "ies")}   ·   " +
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

            if (_grid.Columns.Contains("CategoryId"))
                _grid.Columns["CategoryId"].Visible = false;

            SetWeight("CategoryName", 400, "Category");
            SetWeight("Status", 120, "Status");
        }

        private void ClearSelection()
        {
            if (_grid != null && _grid.Rows.Count > 0)
                _grid.ClearSelection();
        }

        // ============================================================
        // SELECTION HELPERS
        // ============================================================

        private Category? GetSelectedCategory()
        {
            if (_grid == null || _grid.SelectedRows.Count == 0) return null;
            var row = _grid.SelectedRows[0];
            if (!_grid.Columns.Contains("CategoryId")) return null;

            var value = row.Cells["CategoryId"].Value;
            if (value == null) return null;

            int id = Convert.ToInt32(value);
            return _allCategories.FirstOrDefault(c => c.CategoryId == id);
        }

        // ============================================================
        // ROW EVENTS
        // ============================================================

        private void Grid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            EditSelectedCategory();
        }

        private void Grid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                EditSelectedCategory();
            }
        }

        // ============================================================
        // ADD / EDIT
        // ============================================================

        private void OpenCategoryFormForAdd()
        {
            try
            {
                var parent = FindForm();
                using var form = new Forms.CategoryForm();
                if (form.ShowDialog(parent) == DialogResult.OK)
                {
                    _ = ReloadAllAsync();
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Could not open Category form: {ex.Message}", isError: true);
            }
        }

        private void EditSelectedCategory()
        {
            var c = GetSelectedCategory();
            if (c == null)
            {
                ShowStatus("Please select a category first.", isError: true);
                return;
            }

            try
            {
                var parent = FindForm();
                using var form = new Forms.CategoryForm(c);
                if (form.ShowDialog(parent) == DialogResult.OK)
                {
                    _ = ReloadAllAsync();
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Could not open Category form: {ex.Message}", isError: true);
            }
        }

        // ============================================================
        // DEACTIVATE / REACTIVATE
        // ============================================================

        private async Task DeactivateSelectedCategoryAsync()
        {
            var c = GetSelectedCategory();
            if (c == null)
            {
                ShowStatus("Please select a category first.", isError: true);
                return;
            }
            if (!c.IsActive)
            {
                ShowStatus("This category is already inactive.", isError: true);
                return;
            }

            var confirm = MessageBox.Show(
                $"Are you sure you want to deactivate \"{c.CategoryName}\"?",
                "Confirm Deactivate",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            SetBusy(true);
            try
            {
                var (success, error) = await _categoryService.DeactivateCategoryAsync(c.CategoryId);
                if (success)
                {
                    ShowStatus("Category deactivated.", isError: false);
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

        private async Task ReactivateSelectedCategoryAsync()
        {
            var c = GetSelectedCategory();
            if (c == null)
            {
                ShowStatus("Please select a category first.", isError: true);
                return;
            }
            if (c.IsActive)
            {
                ShowStatus("This category is already active.", isError: true);
                return;
            }

            SetBusy(true);
            try
            {
                var (success, error) = await _categoryService.ReactivateCategoryAsync(c.CategoryId);
                if (success)
                {
                    ShowStatus("Category reactivated.", isError: false);
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