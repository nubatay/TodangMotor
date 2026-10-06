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
    /// Inventory module — three tabs:
    ///   1. Stock Levels    — read-only stock grid + Adjust Stock action
    ///   2. Low Stocks      — two sub-tabs: Out of Stock / Low Stock (read-only)
    ///   3. Movement History — chronological movements with filters
    /// Catalog management (Add/Edit/Deactivate) lives in the Items module.
    /// </summary>
    public class InventoryControl : UserControl
    {
        // ============================================================
        // SERVICES
        // ============================================================

        private readonly ProductService _productService = new();
        private readonly CategoryService _categoryService;
        private readonly StockMovementService _stockMovementService = new();

        // ============================================================
        // STATE
        // ============================================================

        private List<Product> _allProducts = new();
        private List<Category> _allCategories = new();
        private Dictionary<int, Category> _categoriesById = new();

        private bool _isLoaded;
        private bool _isBusy;
        private bool _movementHistoryLoaded;

        // ============================================================
        // TAB CONTAINER
        // ============================================================

        private TabControl _tabs;
        private TabPage _stockLevelsTab;
        private TabPage _lowStocksTab;
        private TabPage _movementHistoryTab;

        // ============================================================
        // TAB 1 — STOCK LEVELS
        // ============================================================

        private RoundedTextBox _slSearchBox;
        private RoundedComboBox _slCategoryFilter;
        private RoundedComboBox _slStatusFilter;
        private Label _slCountLabel;
        private DataGridView _slGrid;
        private Button _slBtnAdjustStock;
        private Label _slStatusLabel;

        // ============================================================
        // TAB 2 — LOW STOCKS (two sub-tabs)
        // ============================================================

        private TabControl _lowTabs;
        private TabPage _outOfStockPage;
        private TabPage _lowStockPage;
        private DataGridView _outOfStockGrid;
        private DataGridView _lowStockGrid;
        private Label _lowSummaryLabel;

        // ============================================================
        // TAB 3 — MOVEMENT HISTORY
        // ============================================================

        private RoundedTextBox _mhSearchBox;
        private RoundedComboBox _mhTypeFilter;
        private DateTimePicker _mhFromDate;
        private DateTimePicker _mhToDate;
        private Label _mhCountLabel;
        private DataGridView _mhGrid;
        private Label _mhStatusLabel;

        // ============================================================
        // CONSTRUCTION
        // ============================================================

        public InventoryControl()
        {
            _categoryService = new CategoryService(new CategoryRepository());

            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(Theme.SpacingLg);

            BuildLayout();

            Load += InventoryControl_Load;
        }

        private async void InventoryControl_Load(object sender, EventArgs e)
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
            _tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = Theme.FontBody,
                ItemSize = new Size(180, 38),
                SizeMode = TabSizeMode.Fixed,
                Appearance = TabAppearance.Normal,
                Padding = new Point(20, 6)
            };

            _stockLevelsTab = new TabPage("Stock Levels") { BackColor = Theme.Background, Padding = Padding.Empty };
            _lowStocksTab = new TabPage("Low Stocks") { BackColor = Theme.Background, Padding = Padding.Empty };
            _movementHistoryTab = new TabPage("Movement History") { BackColor = Theme.Background, Padding = Padding.Empty };

            BuildStockLevelsTab();
            BuildLowStocksTab();
            BuildMovementHistoryTab();

            _tabs.TabPages.Add(_stockLevelsTab);
            _tabs.TabPages.Add(_lowStocksTab);
            _tabs.TabPages.Add(_movementHistoryTab);

            _tabs.SelectedIndexChanged += async (s, e) =>
            {
                if (_tabs.SelectedTab == _movementHistoryTab && !_movementHistoryLoaded)
                {
                    _movementHistoryLoaded = true;
                    await LoadMovementHistoryAsync();
                }
            };

            Controls.Add(_tabs);
        }

        // ============================================================
        // TAB 1 — STOCK LEVELS
        // ============================================================

        private void BuildStockLevelsTab()
        {
            // Filter bar (Top)
            var bar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 84,
                BackColor = Theme.Background
            };

            _slSearchBox = UiFactory.CreateTextBox(240);
            _slSearchBox.Location = new Point(4, 12);
            _slSearchBox.Placeholder = "Search product or brand…";
            _slSearchBox.MaxLength = 150;
            _slSearchBox.TextChanged += (s, e) => ApplyStockLevelsFilter();

            var lblCategory = new Label
            {
                Text = "Category:",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(80, 20),
                Location = new Point(252, 24),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _slCategoryFilter = new RoundedComboBox
            {
                Width = 180,
                Location = new Point(336, 12)
            };
            _slCategoryFilter.SelectedIndexChanged += (s, e) => ApplyStockLevelsFilter();

            var lblStatus = new Label
            {
                Text = "Status:",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(56, 20),
                Location = new Point(526, 24),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _slStatusFilter = new RoundedComboBox
            {
                Width = 140,
                Location = new Point(584, 12)
            };
            _slStatusFilter.Items.Add("All");
            _slStatusFilter.Items.Add("OK");
            _slStatusFilter.Items.Add("Low");
            _slStatusFilter.Items.Add("Out of stock");
            _slStatusFilter.SelectedIndex = 0;
            _slStatusFilter.SelectedIndexChanged += (s, e) => ApplyStockLevelsFilter();

            _slCountLabel = new Label
            {
                Text = "Loading…",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(4, 58),
                Height = 20,
                Width = 700,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            bar.Controls.Add(_slSearchBox);
            bar.Controls.Add(lblCategory);
            bar.Controls.Add(_slCategoryFilter);
            bar.Controls.Add(lblStatus);
            bar.Controls.Add(_slStatusFilter);
            bar.Controls.Add(_slCountLabel);

            // Grid (Fill)
            _slGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 0, 4)
            };
            UiFactory.StyleGrid(_slGrid);
            _slGrid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 251);

            // Bottom bar
            var bottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 68,
                BackColor = Theme.Background
            };

            _slBtnAdjustStock = UiFactory.CreateButton("Adjust Stock", UiFactory.ButtonStyle.Primary, 140, 40);
            _slBtnAdjustStock.Location = new Point(4, 14);
            _slBtnAdjustStock.Click += async (s, e) => await AdjustStockAsync();

            _slStatusLabel = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(160, 14),
                Height = 40,
                Width = 500,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            bottom.Controls.Add(_slBtnAdjustStock);
            bottom.Controls.Add(_slStatusLabel);

            // Docking order
            _stockLevelsTab.Controls.Add(_slGrid);
            _stockLevelsTab.Controls.Add(bottom);
            _stockLevelsTab.Controls.Add(bar);
        }

        // ============================================================
        // TAB 2 — LOW STOCKS
        // ============================================================

        private void BuildLowStocksTab()
        {
            _lowTabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = Theme.FontSmall,
                ItemSize = new Size(200, 34),
                SizeMode = TabSizeMode.Fixed,
                Appearance = TabAppearance.Normal,
                Padding = new Point(14, 4)
            };

            _outOfStockPage = new TabPage("Out of Stock (0)") { BackColor = Theme.Background, Padding = Padding.Empty };
            _lowStockPage = new TabPage("Low Stock (0)") { BackColor = Theme.Background, Padding = Padding.Empty };

            _outOfStockGrid = BuildLowStockGrid();
            _lowStockGrid = BuildLowStockGrid();

            _outOfStockPage.Controls.Add(_outOfStockGrid);
            _lowStockPage.Controls.Add(_lowStockGrid);

            _lowTabs.TabPages.Add(_outOfStockPage);
            _lowTabs.TabPages.Add(_lowStockPage);

            // Summary label at bottom
            _lowSummaryLabel = new Label
            {
                Text = "Loading…",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Dock = DockStyle.Bottom,
                Height = 32,
                Padding = new Padding(4, 0, 4, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Theme.Background
            };

            _lowStocksTab.Controls.Add(_lowTabs);
            _lowStocksTab.Controls.Add(_lowSummaryLabel);
        }

        private DataGridView BuildLowStockGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 0, 4)
            };
            UiFactory.StyleGrid(grid);
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 251);
            return grid;
        }

        // ============================================================
        // TAB 3 — MOVEMENT HISTORY
        // ============================================================

        private void BuildMovementHistoryTab()
        {
            // Filter bar
            var bar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 84,
                BackColor = Theme.Background
            };

            _mhSearchBox = UiFactory.CreateTextBox(220);
            _mhSearchBox.Location = new Point(4, 12);
            _mhSearchBox.Placeholder = "Search product or brand…";
            _mhSearchBox.MaxLength = 150;
            _mhSearchBox.TextChanged += async (s, e) => await LoadMovementHistoryAsync();

            var lblType = new Label
            {
                Text = "Type:",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(40, 20),
                Location = new Point(232, 24),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _mhTypeFilter = new RoundedComboBox
            {
                Width = 140,
                Location = new Point(276, 12)
            };
            _mhTypeFilter.Items.Add("All");
            _mhTypeFilter.Items.Add("Sale");
            _mhTypeFilter.Items.Add("Stock-In");
            _mhTypeFilter.Items.Add("Adjustment");
            _mhTypeFilter.Items.Add("Void");
            _mhTypeFilter.SelectedIndex = 0;
            _mhTypeFilter.SelectedIndexChanged += async (s, e) => await LoadMovementHistoryAsync();

            var lblFrom = new Label
            {
                Text = "From:",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(40, 20),
                Location = new Point(432, 24),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _mhFromDate = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "MMM d, yyyy",
                Font = Theme.FontBody,
                Location = new Point(474, 12),
                Size = new Size(140, 32),
                MaxDate = DateTime.Today,
                Value = DateTime.Today
            };
            _mhFromDate.ValueChanged += async (s, e) => await LoadMovementHistoryAsync();

            var lblTo = new Label
            {
                Text = "To:",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(28, 20),
                Location = new Point(622, 24),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _mhToDate = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "MMM d, yyyy",
                Font = Theme.FontBody,
                Location = new Point(652, 12),
                Size = new Size(140, 32),
                MaxDate = DateTime.Today,
                Value = DateTime.Today
            };
            _mhToDate.ValueChanged += async (s, e) => await LoadMovementHistoryAsync();

            _mhCountLabel = new Label
            {
                Text = "Loading…",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(4, 58),
                Height = 20,
                Width = 900,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            bar.Controls.Add(_mhSearchBox);
            bar.Controls.Add(lblType);
            bar.Controls.Add(_mhTypeFilter);
            bar.Controls.Add(lblFrom);
            bar.Controls.Add(_mhFromDate);
            bar.Controls.Add(lblTo);
            bar.Controls.Add(_mhToDate);
            bar.Controls.Add(_mhCountLabel);

            // Grid
            _mhGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 0, 4)
            };
            UiFactory.StyleGrid(_mhGrid);
            _mhGrid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 251);
            _mhGrid.CellFormatting += MhGrid_CellFormatting;
            PrimeMovementGridColumns();

            // Bottom bar
            var bottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 68,
                BackColor = Theme.Background
            };

            var btnRefresh = UiFactory.CreateButton("Refresh", UiFactory.ButtonStyle.Secondary, 110, 40);
            btnRefresh.Location = new Point(4, 14);
            btnRefresh.Click += async (s, e) => await LoadMovementHistoryAsync();

            _mhStatusLabel = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(130, 14),
                Height = 40,
                Width = 500,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            bottom.Controls.Add(btnRefresh);
            bottom.Controls.Add(_mhStatusLabel);

            _movementHistoryTab.Controls.Add(_mhGrid);
            _movementHistoryTab.Controls.Add(bottom);
            _movementHistoryTab.Controls.Add(bar);
        }

        private void PrimeMovementGridColumns()
        {
            _mhGrid.AutoGenerateColumns = false;
            _mhGrid.Columns.Clear();

            void AddCol(string prop, string header, int weight, bool rightAlign = false)
            {
                var col = new DataGridViewTextBoxColumn
                {
                    Name = prop,
                    DataPropertyName = prop,
                    HeaderText = header,
                    FillWeight = weight,
                    ReadOnly = true
                };
                if (rightAlign)
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                _mhGrid.Columns.Add(col);
            }

            AddCol("DateDisplay", "Date & Time", 170);
            AddCol("Item", "Item", 240);
            AddCol("TypeLabel", "Type", 90);
            AddCol("StockIn", "Stock In", 80, true);
            AddCol("StockOut", "Stock Out", 80, true);
            AddCol("StartQty", "Start Qty", 70, true);
            AddCol("EndQty", "End Qty", 70, true);
        }

        private void MhGrid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (e.RowIndex >= _mhGrid.Rows.Count) return;

            var row = _mhGrid.Rows[e.RowIndex];
            if (!_mhGrid.Columns.Contains("TypeLabel")) return;

            var typeVal = row.Cells["TypeLabel"].Value?.ToString() ?? string.Empty;
            string colName = _mhGrid.Columns[e.ColumnIndex].Name;

            if (colName == "TypeLabel")
            {
                switch (typeVal)
                {
                    case "Stock-In": e.CellStyle.ForeColor = Theme.Success; break;
                    case "Sale": e.CellStyle.ForeColor = Theme.Danger; break;
                    case "Adjustment": e.CellStyle.ForeColor = Theme.Primary; break;
                    case "Void": e.CellStyle.ForeColor = Color.FromArgb(200, 130, 40); break;
                    default: e.CellStyle.ForeColor = Theme.TextPrimary; break;
                }
                e.CellStyle.Font = Theme.FontBodyBold;
            }
            else if (colName == "StockIn" && !string.IsNullOrEmpty(row.Cells["StockIn"].Value?.ToString()))
            {
                e.CellStyle.ForeColor = Theme.Success;
            }
            else if (colName == "StockOut" && !string.IsNullOrEmpty(row.Cells["StockOut"].Value?.ToString()))
            {
                e.CellStyle.ForeColor = Theme.Danger;
            }
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
                ApplyStockLevelsFilter();
                RefreshLowStocksTab();
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

                _slCategoryFilter.Items.Clear();
                _slCategoryFilter.Items.Add("All Categories");
                foreach (var c in _allCategories)
                    _slCategoryFilter.Items.Add(c.CategoryName);
                _slCategoryFilter.SelectedIndex = 0;
            }
            catch
            {
                ShowStockLevelsStatus("Could not load categories.", true);
            }
        }

        private async Task LoadProductsAsync()
        {
            var (success, error, products) = await _productService.GetAllAsync();
            if (!success)
            {
                ShowStockLevelsStatus(error, true);
                _allProducts = new List<Product>();
                return;
            }
            _allProducts = products ?? new List<Product>();
        }

        // ============================================================
        // TAB 1 — STOCK LEVELS LOGIC
        // ============================================================

        private void ApplyStockLevelsFilter()
        {
            if (_allProducts == null) return;

            IEnumerable<Product> filtered = _allProducts.Where(p => p.IsActive);

            string search = (_slSearchBox?.Text ?? string.Empty).Trim();
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

            string cat = _slCategoryFilter?.SelectedItem as string;
            if (!string.IsNullOrEmpty(cat) && cat != "All Categories")
            {
                var catObj = _allCategories.FirstOrDefault(c =>
                    string.Equals(c.CategoryName, cat, StringComparison.OrdinalIgnoreCase));
                if (catObj != null)
                    filtered = filtered.Where(p => p.CategoryId == catObj.CategoryId);
            }

            string statusFilter = _slStatusFilter?.SelectedItem as string ?? "All";

            var rows = filtered
                .OrderBy(p => p.ProductName)
                .ThenBy(p => p.Brand)
                .Select(p =>
                {
                    string stockStatus = p.QuantityOnHand == 0
                        ? "Out of stock"
                        : (p.QuantityOnHand <= p.ReorderLevel ? "Low" : "OK");

                    return new
                    {
                        p.ProductId,
                        Product = p.ProductName ?? string.Empty,
                        Brand = p.Brand ?? string.Empty,
                        Category = _categoriesById.TryGetValue(p.CategoryId, out var c) ? c.CategoryName : "—",
                        Unit = p.Unit ?? string.Empty,
                        OnHand = p.QuantityOnHand,
                        Reorder = p.ReorderLevel,
                        Status = stockStatus
                    };
                })
                .Where(r =>
                    statusFilter == "All" ||
                    string.Equals(r.Status, statusFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();

            _slGrid.DataSource = rows;

            if (_slGrid.Columns.Contains("ProductId"))
                _slGrid.Columns["ProductId"].Visible = false;

            void SetWeight(string col, int weight, string header = null)
            {
                if (!_slGrid.Columns.Contains(col)) return;
                _slGrid.Columns[col].FillWeight = weight;
                if (header != null) _slGrid.Columns[col].HeaderText = header;
            }

            SetWeight("Product", 240, "Product");
            SetWeight("Brand", 140, "Brand");
            SetWeight("Category", 120, "Category");
            SetWeight("Unit", 60, "Unit");
            SetWeight("OnHand", 80, "On Hand");
            SetWeight("Reorder", 80, "Reorder At");
            SetWeight("Status", 110, "Stock Status");

            if (_slGrid.Rows.Count > 0)
                _slGrid.ClearSelection();

            // Color-code Stock Status.
            foreach (DataGridViewRow row in _slGrid.Rows)
            {
                if (row.Cells["Status"].Value is string s)
                {
                    if (s == "Out of stock")
                        row.Cells["Status"].Style.ForeColor = Theme.Danger;
                    else if (s == "Low")
                        row.Cells["Status"].Style.ForeColor = Color.FromArgb(200, 130, 40);
                    else
                        row.Cells["Status"].Style.ForeColor = Theme.Success;
                }
            }

            _slCountLabel.Text =
                $"Showing {rows.Count} of {_allProducts.Count(p => p.IsActive)} active item(s)";
        }

        private Product? GetSelectedStockLevelProduct()
        {
            if (_slGrid == null || _slGrid.SelectedRows.Count == 0) return null;
            var row = _slGrid.SelectedRows[0];
            if (!_slGrid.Columns.Contains("ProductId")) return null;

            var val = row.Cells["ProductId"].Value;
            if (val == null) return null;

            int id = Convert.ToInt32(val);
            return _allProducts.FirstOrDefault(p => p.ProductId == id);
        }

        private async Task AdjustStockAsync()
        {
            var p = GetSelectedStockLevelProduct();
            if (p == null)
            {
                ShowStockLevelsStatus("Please select an item first.", true);
                return;
            }

            using var form = new AdjustStockForm(p);
            var result = form.ShowDialog(FindForm());

            if (result == DialogResult.OK)
            {
                ShowStockLevelsStatus("Stock adjusted successfully.", false);
                await ReloadAllAsync();
            }
        }

        // ============================================================
        // TAB 2 — LOW STOCKS LOGIC
        // ============================================================

        private void RefreshLowStocksTab()
        {
            if (_allProducts == null) return;

            var active = _allProducts.Where(p => p.IsActive).ToList();

            var outOfStock = active
                .Where(p => p.QuantityOnHand == 0)
                .OrderBy(p => p.ProductName)
                .ThenBy(p => p.Brand)
                .Select(p => new
                {
                    p.ProductId,
                    Product = p.ProductName ?? string.Empty,
                    Brand = p.Brand ?? string.Empty,
                    Unit = p.Unit ?? string.Empty,
                    OnHand = p.QuantityOnHand,
                    Reorder = p.ReorderLevel
                })
                .ToList();

            var lowStock = active
                .Where(p => p.QuantityOnHand > 0 && p.QuantityOnHand <= p.ReorderLevel)
                .OrderBy(p => p.QuantityOnHand - p.ReorderLevel)
                .ThenBy(p => p.ProductName)
                .Select(p => new
                {
                    p.ProductId,
                    Product = p.ProductName ?? string.Empty,
                    Brand = p.Brand ?? string.Empty,
                    Unit = p.Unit ?? string.Empty,
                    OnHand = p.QuantityOnHand,
                    Reorder = p.ReorderLevel
                })
                .ToList();

            _outOfStockGrid.DataSource = outOfStock;
            _lowStockGrid.DataSource = lowStock;

            void ConfigureGrid(DataGridView grid)
            {
                if (grid.Columns.Contains("ProductId"))
                    grid.Columns["ProductId"].Visible = false;

                void SetWeight(string col, int weight, string header = null)
                {
                    if (!grid.Columns.Contains(col)) return;
                    grid.Columns[col].FillWeight = weight;
                    if (header != null) grid.Columns[col].HeaderText = header;
                }

                SetWeight("Product", 240, "Product");
                SetWeight("Brand", 140, "Brand");
                SetWeight("Unit", 60, "Unit");
                SetWeight("OnHand", 80, "On Hand");
                SetWeight("Reorder", 80, "Reorder At");

                if (grid.Rows.Count > 0)
                    grid.ClearSelection();
            }

            ConfigureGrid(_outOfStockGrid);
            ConfigureGrid(_lowStockGrid);

            // Update sub-tab labels with counts.
            _outOfStockPage.Text = $"Out of Stock ({outOfStock.Count})";
            _lowStockPage.Text = $"Low Stock ({lowStock.Count})";

            _lowSummaryLabel.Text =
                $"{outOfStock.Count} item(s) completely out of stock   ·   " +
                $"{lowStock.Count} item(s) running low";
        }

        // ============================================================
        // TAB 3 — MOVEMENT HISTORY LOGIC
        // ============================================================

        private async Task LoadMovementHistoryAsync()
        {
            if (_mhGrid == null) return;

            DateTime from = _mhFromDate.Value.Date;
            DateTime to = _mhToDate.Value.Date;

            if (to < from)
            {
                _mhCountLabel.Text = "The 'To' date must be on or after the 'From' date.";
                _mhCountLabel.ForeColor = Theme.Danger;
                _mhGrid.DataSource = null;
                return;
            }

            DateTime fromInclusive = from;
            DateTime toExclusive = to.AddDays(1);  // include the whole "To" day

            string? search = string.IsNullOrWhiteSpace(_mhSearchBox.Text) ? null : _mhSearchBox.Text.Trim();
            string? type = _mhTypeFilter.SelectedItem as string;
            if (type == "All") type = null;

            // Map UI labels to DB types.
            if (type == "Stock-In") type = "StockIn";

            try
            {
                var (success, error, movements) = await _stockMovementService.GetFilteredAsync(
                    fromInclusive, toExclusive, search, type);

                if (!success)
                {
                    _mhCountLabel.Text = error;
                    _mhCountLabel.ForeColor = Theme.Danger;
                    _mhGrid.DataSource = null;
                    return;
                }

                var rows = movements
                    .Select(m =>
                    {
                        string item = string.IsNullOrWhiteSpace(m.ProductBrand)
                            ? (m.ProductName ?? "(unknown item)")
                            : $"{m.ProductName} — {m.ProductBrand}";

                        string typeLabel = m.MovementType switch
                        {
                            "StockIn" => "Stock-In",
                            "Sale" => "Sale",
                            "Adjustment" => "Adjustment",
                            "Void" => "Void",
                            "Initial" => "Initial",
                            _ => m.MovementType
                        };

                        return new
                        {
                            DateDisplay = m.MovementDate.ToString("MMM d, yyyy · h:mm tt"),
                            Item = item,
                            TypeLabel = typeLabel,
                            StockIn = m.QuantityChange > 0 ? $"+{m.QuantityChange}" : "",
                            StockOut = m.QuantityChange < 0 ? $"{m.QuantityChange}" : "",
                            StartQty = m.QuantityBefore,
                            EndQty = m.QuantityAfter
                        };
                    })
                    .ToList();

                _mhGrid.DataSource = rows;

                if (_mhGrid.Rows.Count > 0)
                    _mhGrid.ClearSelection();

                _mhCountLabel.ForeColor = Theme.TextSecondary;
                _mhCountLabel.Text =
                    $"Showing {rows.Count} movement(s) from " +
                    $"{from:MMM d, yyyy} to {to:MMM d, yyyy}";
            }
            catch (Exception ex)
            {
                _mhCountLabel.Text = $"Could not load movements: {ex.Message}";
                _mhCountLabel.ForeColor = Theme.Danger;
                _mhGrid.DataSource = null;
            }
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            _slBtnAdjustStock.Enabled = !busy;
        }

        private void ShowStockLevelsStatus(string message, bool isError)
        {
            if (_slStatusLabel == null) return;
            _slStatusLabel.ForeColor = isError ? Theme.Danger : Theme.Success;
            _slStatusLabel.Text = message ?? string.Empty;
        }
    }
}