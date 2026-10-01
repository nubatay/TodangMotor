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
    public class CashierInventoryControl : UserControl
    {
        private readonly SalesService _salesService = new();
        private readonly CategoryService _categoryService = new(new CategoryRepository());

        private List<CashierProductView> _allProducts = new();
        private List<Category> _allCategories = new();

        private bool _isLoaded;

        private RoundedTextBox _searchBox;
        private RoundedComboBox _categoryFilter;
        private Label _countLabel;
        private DataGridView _grid;

        public CashierInventoryControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(Theme.SpacingLg);

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
                Height = 84,
                BackColor = Theme.Background
            };

            _searchBox = UiFactory.CreateTextBox(260);
            _searchBox.Location = new Point(4, 12);
            _searchBox.Placeholder = "Search product or brand…";
            _searchBox.MaxLength = 100;
            _searchBox.TextChanged += (s, e) => ApplyFilters();

            // Wider label so "Category:" isn't clipped.
            var lblCategory = new Label
            {
                Text = "Category:",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(80, 20),
                Location = new Point(276, 24),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            // Wider dropdown so "All Categories" isn't clipped.
            _categoryFilter = new RoundedComboBox
            {
                Width = 220,
                Location = new Point(360, 12)
            };
            _categoryFilter.SelectedIndexChanged += (s, e) => ApplyFilters();

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
                Margin = new Padding(0, 4, 0, 4)
            };
            UiFactory.StyleGrid(_grid);
            _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 251);
            return _grid;
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

            void SetWeight(string col, int weight, string header = null)
            {
                if (!_grid.Columns.Contains(col)) return;
                _grid.Columns[col].FillWeight = weight;
                if (header != null) _grid.Columns[col].HeaderText = header;
            }

            SetWeight("Product", 240, "Product");
            SetWeight("Brand", 140, "Brand");
            SetWeight("Unit", 60, "Unit");
            SetWeight("SellingPrice", 110, "Price (PHP)");
            SetWeight("OnHand", 80, "On Hand");
            SetWeight("ReorderAt", 80, "Reorder At");
            SetWeight("StockStatus", 110, "Stock Status");

            if (_grid.Rows.Count > 0)
                _grid.ClearSelection();

            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.Cells["StockStatus"].Value is string status)
                {
                    if (status == "Out of stock")
                        row.Cells["StockStatus"].Style.ForeColor = Theme.Danger;
                    else if (status == "Low")
                        row.Cells["StockStatus"].Style.ForeColor = Color.FromArgb(200, 130, 40);
                    else
                        row.Cells["StockStatus"].Style.ForeColor = Theme.Success;
                }
            }

            _countLabel.ForeColor = Theme.TextSecondary;
            _countLabel.Text = $"Showing {rows.Count} of {_allProducts.Count} active product(s)";
        }
    }
}