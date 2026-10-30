using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TodangMotor.Common;
using TodangMotor.Data;
using TodangMotor.Forms;
using TodangMotor.Models;
using TodangMotor.Services;

namespace TodangMotor.Controls
{
    /// <summary>
    /// Items module — the product master catalog.
    /// Owner-only. Add via popup; Edit / Deactivate / Reactivate via per-row icons.
    /// No stock quantities shown here — that belongs to Inventory.
    /// </summary>
    public class ItemsControl : UserControl
    {
        private readonly ProductService _productService = new();
        private readonly CategoryService _categoryService = new(new CategoryRepository());

        private List<Product> _allProducts = new();
        private List<Category> _allCategories = new();
        private Dictionary<int, Category> _categoriesById = new();

        private bool _isLoaded;

        private const string PencilIcon = "\uE70F";
        private const string BanIcon = "\uE711";
        private const string CheckIcon = "\uE73E";

        // ---- Fixed column widths (Product is dynamic) ----
        private const int ColBrandW = 130;
        private const int ColCategoryW = 130;
        private const int ColUnitW = 72;
        private const int ColCostW = 118;
        private const int ColSellW = 130;
        private const int ColReorderW = 110;
        private const int ColSupplierW = 160;
        private const int ColStatusW = 120;
        private const int ColActionW = 60;

        private RoundedTextBox _searchBox;
        private RoundedComboBox _categoryFilter;
        private RoundedComboBox _statusFilter;
        private Label _countLabel;

        private DataGridView _grid;

        private Button _btnAdd;
        private Label _statusLabel;

        public ItemsControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(UiFactory.PagePadding);

            BuildLayout();

            Load += ItemsControl_Load;
        }

        private async void ItemsControl_Load(object sender, EventArgs e)
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

            _searchBox = UiFactory.CreateTextBox(300);
            _searchBox.Location = new Point(0, 16);
            _searchBox.Placeholder = "Search product, brand, or SKU…";
            _searchBox.MaxLength = 150;
            _searchBox.TextChanged += (s, e) => ApplyFilters();

            var lblCategory = new Label
            {
                Text = "Category:",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(80, 24),
                Location = new Point(_searchBox.Right + 24, 24),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _categoryFilter = new RoundedComboBox
            {
                Width = 200,
                Location = new Point(lblCategory.Right + 8, 16)
            };
            _categoryFilter.SelectedIndexChanged += (s, e) => ApplyFilters();

            var lblStatus = new Label
            {
                Text = "Status:",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(56, 24),
                Location = new Point(_categoryFilter.Right + 24, 24),
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
                Width = 900,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            bar.Controls.Add(_searchBox);
            bar.Controls.Add(lblCategory);
            bar.Controls.Add(_categoryFilter);
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
            _grid.Resize += (s, e) => FitProductColumn();

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

            // Product width applied in FitProductColumn().
            AddTextCol("Product", "Product", 400, true);
            AddTextCol("Brand", "Brand", ColBrandW, true);
            AddTextCol("Category", "Category", ColCategoryW, true);
            AddTextCol("Unit", "Unit", ColUnitW, true, DataGridViewContentAlignment.MiddleCenter);
            AddTextCol("Cost", "Cost (₱)", ColCostW, true, DataGridViewContentAlignment.MiddleRight);
            AddTextCol("Selling", "Selling (₱)", ColSellW, true, DataGridViewContentAlignment.MiddleRight);
            AddTextCol("Reorder", "Reorder", ColReorderW, true, DataGridViewContentAlignment.MiddleRight);
            AddTextCol("Supplier", "Supplier", ColSupplierW, true);
            AddTextCol("Status", "Status", ColStatusW, true, DataGridViewContentAlignment.MiddleCenter);

            // Edit icon
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "EditIcon",
                HeaderText = "",
                Width = ColActionW,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                Resizable = DataGridViewTriState.False
            });

            // Toggle icon (activate / deactivate)
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
                Name = "ProductId",
                DataPropertyName = "ProductId",
                Visible = false
            });
        }

        private void FitProductColumn()
        {
            if (_grid == null || !_grid.Columns.Contains("Product")) return;

            int others =
                ColBrandW + ColCategoryW + ColUnitW +
                ColCostW + ColSellW + ColReorderW + ColSupplierW +
                ColStatusW + ColActionW + ColActionW;

            int available = _grid.ClientSize.Width - others - 24;
            if (available < 220) available = 220;

            _grid.Columns["Product"].Width = available;
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

            var idCell = _grid.Rows[e.RowIndex].Cells["ProductId"];
            if (idCell?.Value == null) return;

            int id = Convert.ToInt32(idCell.Value);
            var product = _allProducts.FirstOrDefault(p => p.ProductId == id);
            if (product == null) return;

            if (colName == "EditIcon")
            {
                OpenProductFormForEdit(product);
            }
            else
            {
                if (product.IsActive) _ = DeactivateProductAsync(product);
                else _ = ReactivateProductAsync(product);
            }

            _grid.ClearSelection();
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
            _btnAdd.Click += (s, e) => OpenProductFormForAdd();

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
        // DATA LOAD
        // ============================================================

        private async Task ReloadAllAsync()
        {
            SetBusy(true);
            try
            {
                await LoadCategoriesAsync();
                await LoadProductsAsync();
                ApplyFilters();
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task LoadCategoriesAsync()
        {
            try
            {
                var cats = await _categoryService.GetAllCategoriesAsync();
                _allCategories = cats?.ToList() ?? new List<Category>();
                _categoriesById = _allCategories.ToDictionary(c => c.CategoryId);

                _categoryFilter.Items.Clear();
                _categoryFilter.Items.Add("All Categories");
                foreach (var c in _allCategories)
                    _categoryFilter.Items.Add(c.CategoryName);
                _categoryFilter.SelectedIndex = 0;

                if (_statusFilter.Items.Count == 0)
                {
                    _statusFilter.Items.Add("Active");
                    _statusFilter.Items.Add("Inactive");
                    _statusFilter.Items.Add("All");
                    _statusFilter.SelectedIndex = 0;
                }
            }
            catch (Exception)
            {
                ShowStatus("Could not load categories.", isError: true);
            }
        }

        private async Task LoadProductsAsync()
        {
            var (success, error, products) = await _productService.GetAllAsync();
            if (!success)
            {
                ShowStatus(error, isError: true);
                _allProducts = new List<Product>();
                return;
            }
            _allProducts = products ?? new List<Product>();
        }

        // ============================================================
        // FILTERS + GRID BINDING
        // ============================================================

        private void ApplyFilters()
        {
            if (_allProducts == null) return;

            IEnumerable<Product> filtered = _allProducts;

            string search = (_searchBox?.Text ?? string.Empty).Trim();
            if (search.Length > 0)
            {
                filtered = filtered.Where(p =>
                    (!string.IsNullOrEmpty(p.ProductName) &&
                     p.ProductName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(p.Brand) &&
                     p.Brand.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(p.SKU) &&
                     p.SKU.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            string cat = _categoryFilter?.SelectedItem as string;
            if (!string.IsNullOrEmpty(cat) && cat != "All Categories")
            {
                var catObj = _allCategories.FirstOrDefault(c =>
                    string.Equals(c.CategoryName, cat, StringComparison.OrdinalIgnoreCase));
                if (catObj != null)
                    filtered = filtered.Where(p => p.CategoryId == catObj.CategoryId);
            }

            string status = _statusFilter?.SelectedItem as string;
            if (status == "Active")
                filtered = filtered.Where(p => p.IsActive);
            else if (status == "Inactive")
                filtered = filtered.Where(p => !p.IsActive);

            var rows = filtered
                .OrderBy(p => p.ProductName)
                .ThenBy(p => p.Brand)
                .Select(p => new
                {
                    p.ProductId,
                    Product = p.ProductName ?? string.Empty,
                    Brand = p.Brand ?? string.Empty,
                    Category = _categoriesById.TryGetValue(p.CategoryId, out var c) ? c.CategoryName : "—",
                    Unit = p.Unit ?? string.Empty,
                    Cost = p.CostPrice.ToString("N2"),
                    Selling = p.SellingPrice.ToString("N2"),
                    Reorder = p.ReorderLevel.ToString(),
                    Supplier = string.IsNullOrEmpty(p.PrimarySupplierName) ? "—" : p.PrimarySupplierName,
                    Status = p.IsActive ? "Active" : "Inactive"
                })
                .ToList();

            _grid.DataSource = rows;

            if (_grid.Columns.Contains("ProductId"))
                _grid.Columns["ProductId"].Visible = false;

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
            FitProductColumn();
        }

        private void UpdateCountLabel(int shownCount)
        {
            if (_countLabel == null) return;

            int total = _allProducts?.Count ?? 0;
            int active = _allProducts?.Count(p => p.IsActive) ?? 0;
            int inactive = total - active;

            string status = _statusFilter?.SelectedItem as string ?? "Active";

            _countLabel.Text =
                $"Showing {shownCount} of {total} item(s)   ·   " +
                $"Status filter: {status}   ·   " +
                $"Active: {active}   Inactive: {inactive}";
        }

        // ============================================================
        // SELECTION
        // ============================================================

        private Product? GetProductAtRow(int rowIndex)
        {
            if (_grid == null) return null;
            if (rowIndex < 0 || rowIndex >= _grid.Rows.Count) return null;
            if (!_grid.Columns.Contains("ProductId")) return null;

            var value = _grid.Rows[rowIndex].Cells["ProductId"].Value;
            if (value == null) return null;

            int id = Convert.ToInt32(value);
            return _allProducts.FirstOrDefault(p => p.ProductId == id);
        }

        // ============================================================
        // ROW EVENTS
        // ============================================================

        private void Grid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var p = GetProductAtRow(e.RowIndex);
            if (p != null) OpenProductFormForEdit(p);
        }

        private void Grid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;

            if (_grid.CurrentRow == null) return;
            var p = GetProductAtRow(_grid.CurrentRow.Index);
            if (p != null) OpenProductFormForEdit(p);
        }

        // ============================================================
        // ADD / EDIT
        // ============================================================

        private void OpenProductFormForAdd()
        {
            try
            {
                var parent = FindForm();
                using var form = new ProductForm();
                if (form.ShowDialog(parent) == DialogResult.OK)
                    _ = ReloadAllAsync();
            }
            catch (Exception ex)
            {
                ShowStatus($"Could not open Product form: {ex.Message}", isError: true);
            }
        }

        private void OpenProductFormForEdit(Product product)
        {
            try
            {
                var parent = FindForm();
                using var form = new ProductForm(product);
                if (form.ShowDialog(parent) == DialogResult.OK)
                    _ = ReloadAllAsync();
            }
            catch (Exception ex)
            {
                ShowStatus($"Could not open Product form: {ex.Message}", isError: true);
            }
        }

        // ============================================================
        // DEACTIVATE / REACTIVATE
        // ============================================================

        private async Task DeactivateProductAsync(Product p)
        {
            string confirmMessage = p.QuantityOnHand > 0
                ? $"This item still has {p.QuantityOnHand} units in stock.\nDeactivate anyway?"
                : $"Are you sure you want to deactivate \"{p.ProductName}\"?";

            var confirm = MessageBox.Show(
                confirmMessage,
                "Confirm Deactivate",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;

            SetBusy(true);
            try
            {
                var (success, error) = await _productService.DeactivateAsync(p.ProductId);
                if (success)
                {
                    ShowStatus("Item deactivated.", isError: false);
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

        private async Task ReactivateProductAsync(Product p)
        {
            SetBusy(true);
            try
            {
                var (success, error) = await _productService.ReactivateAsync(p.ProductId);
                if (success)
                {
                    ShowStatus("Item reactivated.", isError: false);
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