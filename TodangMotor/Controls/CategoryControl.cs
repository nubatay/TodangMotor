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
    /// Category management view. Add via popup; Edit / Deactivate / Reactivate
    /// via per-row icon actions.
    /// </summary>
    public class CategoryControl : UserControl
    {
        private readonly CategoryService _categoryService;

        private List<Category> _allCategories = new();

        private bool _isLoaded;

        // Icons (Segoe MDL2 Assets)
        private const string PencilIcon = "\uE70F";
        private const string BanIcon = "\uE711";
        private const string CheckIcon = "\uE73E";

        private const int ColStatusW = 140;
        private const int ColActionW = 60;

        private RoundedTextBox _searchBox;
        private RoundedComboBox _statusFilter;
        private Label _countLabel;

        private DataGridView _grid;

        private Button _btnAdd;
        private Label _statusLabel;

        public CategoryControl()
        {
            _categoryService = new CategoryService(new CategoryRepository());

            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(UiFactory.PagePadding);

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

            _searchBox = UiFactory.CreateTextBox(320);
            _searchBox.Location = new Point(0, 16);
            _searchBox.Placeholder = "Search category name…";
            _searchBox.MaxLength = 100;
            _searchBox.TextChanged += (s, e) => ApplyFilters();

            var lblStatus = new Label
            {
                Text = "Status:",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(56, 24),
                Location = new Point(_searchBox.Right + 24, 24),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _statusFilter = new RoundedComboBox
            {
                Width = 160,
                Location = new Point(lblStatus.Right + 8, 16)
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
                Location = new Point(0, 76),
                Height = 22,
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
            _grid.Resize += (s, e) => FitCategoryColumn();

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

            AddTextCol("CategoryName", "Category", 500, true);
            AddTextCol("Status", "Status", ColStatusW, true, DataGridViewContentAlignment.MiddleCenter);

            // Edit — pencil icon
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "EditIcon",
                HeaderText = "",
                Width = ColActionW,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                Resizable = DataGridViewTriState.False
            });

            // Toggle — ban (active) / check (inactive)
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
                Name = "CategoryId",
                DataPropertyName = "CategoryId",
                Visible = false
            });
        }

        private void FitCategoryColumn()
        {
            if (_grid == null || !_grid.Columns.Contains("CategoryName")) return;

            int others = ColStatusW + ColActionW + ColActionW;
            int available = _grid.ClientSize.Width - others - 24;
            if (available < 220) available = 220;

            _grid.Columns["CategoryName"].Width = available;
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

            var idCell = _grid.Rows[e.RowIndex].Cells["CategoryId"];
            if (idCell?.Value == null) return;

            int id = Convert.ToInt32(idCell.Value);
            var category = _allCategories.FirstOrDefault(c => c.CategoryId == id);
            if (category == null) return;

            if (colName == "EditIcon")
            {
                OpenCategoryFormForEdit(category);
            }
            else
            {
                if (category.IsActive) _ = DeactivateCategoryAsync(category);
                else _ = ReactivateCategoryAsync(category);
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
            _btnAdd.Click += (s, e) => OpenCategoryFormForAdd();

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

        // ============================================================
        // DATA
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

            if (_grid.Columns.Contains("CategoryId"))
                _grid.Columns["CategoryId"].Visible = false;

            if (_grid.Rows.Count > 0)
                _grid.ClearSelection();

            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.Cells["Status"].Value is string s)
                {
                    row.Cells["Status"].Style.ForeColor =
                        s == "Active" ? Theme.Success : Theme.TextMuted;
                    row.Cells["Status"].Style.Font = Theme.FontBodyBold;
                }
            }

            UpdateCountLabel(rows.Count);
            FitCategoryColumn();
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

        // ============================================================
        // ROW EVENTS
        // ============================================================

        private void Grid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var idCell = _grid.Rows[e.RowIndex].Cells["CategoryId"];
            if (idCell?.Value == null) return;

            int id = Convert.ToInt32(idCell.Value);
            var category = _allCategories.FirstOrDefault(c => c.CategoryId == id);
            if (category != null) OpenCategoryFormForEdit(category);
        }

        private void Grid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;

            if (_grid.CurrentRow == null) return;
            var idCell = _grid.CurrentRow.Cells["CategoryId"];
            if (idCell?.Value == null) return;

            int id = Convert.ToInt32(idCell.Value);
            var category = _allCategories.FirstOrDefault(c => c.CategoryId == id);
            if (category != null) OpenCategoryFormForEdit(category);
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
                    _ = ReloadAllAsync();
            }
            catch (Exception ex)
            {
                ShowStatus($"Could not open Category form: {ex.Message}", isError: true);
            }
        }

        private void OpenCategoryFormForEdit(Category category)
        {
            try
            {
                var parent = FindForm();
                using var form = new Forms.CategoryForm(category);
                if (form.ShowDialog(parent) == DialogResult.OK)
                    _ = ReloadAllAsync();
            }
            catch (Exception ex)
            {
                ShowStatus($"Could not open Category form: {ex.Message}", isError: true);
            }
        }

        // ============================================================
        // DEACTIVATE / REACTIVATE
        // ============================================================

        private async Task DeactivateCategoryAsync(Category c)
        {
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

        private async Task ReactivateCategoryAsync(Category c)
        {
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