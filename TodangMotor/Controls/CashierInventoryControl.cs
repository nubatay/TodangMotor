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
    /// Cashier-side inventory — read-only view of active products.
    /// Shows current stock levels so the cashier can inform customers
    /// without leaving the POS. No Adjust action (Owner-only in
    /// InventoryControl).
    /// </summary>
    public class CashierInventoryControl : UserControl
    {
        private readonly SalesService _salesService = new();
        private readonly CategoryService _categoryService = new(new CategoryRepository());

        private List<CashierProductView> _allProducts = new();
        private List<Category> _allCategories = new();

        private bool _isLoaded;

        // Fixed column widths (excluding Product, which is dynamic).
        private const int ColBrandW = 130;
        private const int ColUnitW = 72;
        private const int ColPriceW = 120;
        private const int ColOnHandW = 120;
        private const int ColReorderW = 115;
        private const int ColStatusW = 165;

        private RoundedTextBox _searchBox;
        private RoundedComboBox _categoryFilter;
        private Label _countLabel;
        private DataGridView _grid;

        public CashierInventoryControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(UiFactory.PagePadding);

            BuildLayout();

            Load += CashierInventoryControl_Load;
        }

        private async void CashierInventoryControl_Load(object sender, EventArgs e)
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

            // ---- Row 1: Search + Category + Count ----
            _searchBox = UiFactory.CreateTextBox(320);
            _searchBox.Location = new Point(0, 16);
            _searchBox.Placeholder = "Search product or brand…";
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
                Width = 220,
                Location = new Point(lblCategory.Right + 8, 16)
            };
            _categoryFilter.SelectedIndexChanged += (s, e) => ApplyFilters();

            _countLabel = new Label
            {
                Text = "Loading…",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(_categoryFilter.Right + 24, 26),
                Height = 24,
                Width = 500,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            bar.Controls.Add(_searchBox);
            bar.Controls.Add(lblCategory);
            bar.Controls.Add(_categoryFilter);
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

            // Text columns — left aligned
            AddTextCol("Product", "Product", 400, true);
            AddTextCol("Brand", "Brand", ColBrandW, true);

            // Unit — center
            AddTextCol("Unit", "Unit", ColUnitW, true, DataGridViewContentAlignment.MiddleCenter);

            // Price — right
            AddTextCol("SellingPrice", "Price (₱)", ColPriceW, true, DataGridViewContentAlignment.MiddleRight);

            // Numbers — right
            AddTextCol("OnHand", "On Hand", ColOnHandW, true, DataGridViewContentAlignment.MiddleRight);
            AddTextCol("ReorderAt", "Reorder", ColReorderW, true, DataGridViewContentAlignment.MiddleRight);

            // Status — center
            AddTextCol("StockStatus", "Stock Status", ColStatusW, true, DataGridViewContentAlignment.MiddleCenter);

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
                ColBrandW + ColUnitW + ColPriceW +
                ColOnHandW + ColReorderW + ColStatusW;

            int available = _grid.ClientSize.Width - others - 24;
            if (available < 220) available = 220;

            _grid.Columns["Product"].Width = available;
        }

        // ============================================================
        // DATA LOAD
        // ============================================================

        private async Task ReloadAllAsync()
        {
            try
            {
                try
                {
                    var cats = await _categoryService.GetAllCategoriesAsync();
                    _allCategories = cats?.ToList() ?? new List<Category>();

                    _categoryFilter.Items.Clear();
                    _categoryFilter.Items.Add("All Categories");
                    foreach (var c in _allCategories)
                        _categoryFilter.Items.Add(c.CategoryName);
                    _categoryFilter.SelectedIndex = 0;
                }
                catch { /* non-critical */ }

                var (success, error, products) =
                    await _salesService.GetActiveProductsForCashierAsync();

                if (!success)
                {
                    _allProducts = new List<CashierProductView>();
                    _countLabel.Text = error ?? "Could not load products.";
                    _countLabel.ForeColor = Theme.Danger;
                    return;
                }

                _allProducts = products ?? new List<CashierProductView>();
                ApplyFilters();
            }
            catch (Exception ex)
            {
                _countLabel.Text = $"Could not load products: {ex.Message}";
                _countLabel.ForeColor = Theme.Danger;
            }
        }

        // ============================================================
        // FILTERS + GRID BINDING
        // ============================================================

        private void ApplyFilters()
        {
            if (_allProducts == null) return;

            IEnumerable<CashierProductView> filtered = _allProducts;

            string search = (_searchBox?.Text ?? string.Empty).Trim();
            if (search.Length > 0)
            {
                filtered = filtered.Where(p =>
                    (!string.IsNullOrEmpty(p.ProductName) &&
                     p.ProductName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(p.Brand) &&
                     p.Brand.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            string selectedCat = _categoryFilter?.SelectedItem as string;
            if (!string.IsNullOrEmpty(selectedCat) && selectedCat != "All Categories")
            {
                var catObj = _allCategories.FirstOrDefault(c =>
                    string.Equals(c.CategoryName, selectedCat, StringComparison.OrdinalIgnoreCase));
                if (catObj != null)
                    filtered = filtered.Where(p => p.CategoryId == catObj.CategoryId);
            }

            var rows = filtered
                .OrderBy(p => p.ProductName)
                .ThenBy(p => p.Brand)
                .Select(p => new
                {
                    p.ProductId,
                    Product = p.ProductName ?? string.Empty,
                    Brand = p.Brand ?? string.Empty,
                    Unit = p.Unit ?? string.Empty,
                    SellingPrice = p.SellingPrice.ToString("N2"),
                    OnHand = p.QuantityOnHand,
                    ReorderAt = p.ReorderLevel,
                    StockStatus = p.QuantityOnHand == 0
                                    ? "Out of stock"
                                    : p.QuantityOnHand <= p.ReorderLevel
                                        ? "Low"
                                        : "OK"
                })
                .ToList();

            _grid.DataSource = rows;

            if (_grid.Columns.Contains("ProductId"))
                _grid.Columns["ProductId"].Visible = false;

            if (_grid.Rows.Count > 0)
                _grid.ClearSelection();

            // Color-code Stock Status per row
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.Cells["StockStatus"].Value is string status)
                {
                    if (status == "Out of stock")
                    {
                        row.Cells["StockStatus"].Style.ForeColor = Theme.Danger;
                        row.Cells["StockStatus"].Style.Font = Theme.FontBodyBold;
                    }
                    else if (status == "Low")
                    {
                        row.Cells["StockStatus"].Style.ForeColor = Color.FromArgb(200, 130, 40);
                        row.Cells["StockStatus"].Style.Font = Theme.FontBodyBold;
                    }
                    else
                    {
                        row.Cells["StockStatus"].Style.ForeColor = Theme.Success;
                        row.Cells["StockStatus"].Style.Font = Theme.FontBody;
                    }
                }
            }

            _countLabel.ForeColor = Theme.TextSecondary;
            _countLabel.Text = $"Showing {rows.Count} of {_allProducts.Count} active product(s)";

            // Recalculate Product width after data changes
            FitProductColumn();
        }
    }
}