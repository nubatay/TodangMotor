using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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

        // ============================================================
        // UI CONTROLS — Products tab
        // ============================================================

        private TabControl _tabs;
        private TabPage _productsTab;
        private TabPage _lowStockTab;

        private RoundedTextBox _searchBox;
        private RoundedComboBox _categoryFilter;
        private RoundedComboBox _statusFilter;
        private Label _countLabel;

        private DataGridView _grid;

        private Button _btnAdd;
        private Button _btnEdit;
        private Button _btnDeactivate;
        private Button _btnReactivate;

        private Label _statusLabel;

        private RoundedComboBox _rangeCombo;
        private Button _btnPdf;
        private Button _btnExcel;

        // ============================================================
        // UI CONTROLS — Low Stocks tab
        // ============================================================

        private RoundedTextBox _lowSearchBox;
        private Label _lowCountLabel;
        private Button _btnSelectAll;
        private Button _btnClearSelection;
        private Button _btnLowPdf;
        private Button _btnLowExcel;
        private DataGridView _lowStockGrid;
        private Label _lowTotalsLabel;

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
                ItemSize = new Size(140, 36),
                SizeMode = TabSizeMode.Fixed,
                Appearance = TabAppearance.Normal,
                Padding = new Point(20, 6)
            };

            _productsTab = new TabPage("Products")
            {
                BackColor = Theme.Background,
                Padding = Padding.Empty
            };

            _lowStockTab = new TabPage("Low Stocks")
            {
                BackColor = Theme.Background,
                Padding = Padding.Empty
            };

            BuildProductsTab();
            BuildLowStockTab();

            _tabs.TabPages.Add(_productsTab);
            _tabs.TabPages.Add(_lowStockTab);

            Controls.Add(_tabs);
        }

        // ---------------- PRODUCTS TAB ----------------

        private void BuildProductsTab()
        {
            _productsTab.Controls.Add(BuildGrid());
            _productsTab.Controls.Add(BuildBottomBar());
            _productsTab.Controls.Add(BuildFilterBar());
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
            _searchBox.Placeholder = "Search product or brand…";
            _searchBox.MaxLength = 100;
            _searchBox.TextChanged += (s, e) => ApplyFilters();

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

            _categoryFilter = new RoundedComboBox
            {
                Width = 180,
                Location = new Point(336, 12)
            };
            _categoryFilter.SelectedIndexChanged += (s, e) => ApplyFilters();

            var lblStatus = new Label
            {
                Text = "Status:",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(56, 18),
                Location = new Point(526, 24),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _statusFilter = new RoundedComboBox
            {
                Width = 120,
                Location = new Point(584, 12)
            };
            _statusFilter.SelectedIndexChanged += (s, e) => ApplyFilters();

            _rangeCombo = new RoundedComboBox
            {
                Width = 130,
                Location = new Point(700, 12)
            };
            _rangeCombo.Items.AddRange(new object[] { "Today", "This Week", "This Month" });
            _rangeCombo.SelectedIndex = 0;

            _btnPdf = UiFactory.CreateButton("PDF", UiFactory.ButtonStyle.Secondary, 80, 36);
            _btnPdf.Location = new Point(840, 14);
            _btnPdf.Click += async (s, e) => await GenerateReportAsync("PDF");

            _btnExcel = UiFactory.CreateButton("Excel", UiFactory.ButtonStyle.Secondary, 80, 36);
            _btnExcel.Location = new Point(926, 14);
            _btnExcel.Click += async (s, e) => await GenerateReportAsync("Excel");

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
                BackColor = Color.Transparent
            };

            bar.Controls.Add(_searchBox);
            bar.Controls.Add(lblCategory);
            bar.Controls.Add(_categoryFilter);
            bar.Controls.Add(lblStatus);
            bar.Controls.Add(_statusFilter);
            bar.Controls.Add(_rangeCombo);
            bar.Controls.Add(_btnPdf);
            bar.Controls.Add(_btnExcel);
            bar.Controls.Add(_countLabel);

            bar.Resize += (s, e) => LayoutFilterBar(bar);

            return bar;
        }

        private void LayoutFilterBar(Panel bar)
        {
            if (bar == null) return;
            if (_rangeCombo == null || _btnPdf == null || _btnExcel == null || _countLabel == null) return;

            int barWidth = bar.ClientSize.Width;
            if (barWidth <= 0) return;

            const int rightPad = 4;
            const int gapSmall = 6;
            const int gapMedium = 10;

            int x = barWidth - rightPad;

            x -= _btnExcel.Width;
            _btnExcel.Location = new Point(x, 14);

            x -= gapSmall;
            x -= _btnPdf.Width;
            _btnPdf.Location = new Point(x, 14);

            x -= gapMedium;
            x -= _rangeCombo.Width;
            _rangeCombo.Location = new Point(x, 12);

            _countLabel.Width = Math.Max(200, barWidth - 8);
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
            _btnAdd.Click += (s, e) => OpenProductFormForAdd();

            _btnEdit = UiFactory.CreateButton("Edit", UiFactory.ButtonStyle.Secondary, 90, 40);
            _btnEdit.Location = new Point(122, 14);
            _btnEdit.Click += (s, e) => EditSelectedProduct();

            _btnDeactivate = UiFactory.CreateButton("Deactivate", UiFactory.ButtonStyle.Ghost, 110, 40);
            _btnDeactivate.Location = new Point(220, 14);
            _btnDeactivate.Click += async (s, e) => await DeactivateSelectedProductAsync();

            _btnReactivate = UiFactory.CreateButton("Reactivate", UiFactory.ButtonStyle.Ghost, 110, 40);
            _btnReactivate.Location = new Point(338, 14);
            _btnReactivate.Click += async (s, e) => await ReactivateSelectedProductAsync();

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

        // ---------------- LOW STOCKS TAB ----------------

        private void BuildLowStockTab()
        {
            // ---- Top toolbar: search + Select All / Clear + PDF/Excel ----
            var toolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 84,
                BackColor = Theme.Background
            };

            _lowSearchBox = UiFactory.CreateTextBox(300);
            _lowSearchBox.Location = new Point(4, 12);
            _lowSearchBox.Placeholder = "Search low-stock product or brand…";
            _lowSearchBox.MaxLength = 100;
            _lowSearchBox.TextChanged += (s, e) => RefreshLowStockGrid();

            _btnSelectAll = UiFactory.CreateButton("Select All", UiFactory.ButtonStyle.Secondary, 110, 40);
            _btnSelectAll.Location = new Point(324, 12);
            _btnSelectAll.Click += (s, e) => SetAllLowStockSelected(true);

            _btnClearSelection = UiFactory.CreateButton("Clear Selection", UiFactory.ButtonStyle.Ghost, 140, 40);
            _btnClearSelection.Location = new Point(442, 12);
            _btnClearSelection.Click += (s, e) => SetAllLowStockSelected(false);

            // PDF / Excel buttons right-aligned, matching the Products tab.
            _btnLowPdf = UiFactory.CreateButton("PDF", UiFactory.ButtonStyle.Secondary, 80, 36);
            _btnLowPdf.Location = new Point(700, 14);
            _btnLowPdf.Click += (s, e) => GenerateLowStockReport("PDF");

            _btnLowExcel = UiFactory.CreateButton("Excel", UiFactory.ButtonStyle.Secondary, 80, 36);
            _btnLowExcel.Location = new Point(786, 14);
            _btnLowExcel.Click += (s, e) => GenerateLowStockReport("Excel");

            _lowCountLabel = new Label
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

            toolbar.Controls.Add(_lowSearchBox);
            toolbar.Controls.Add(_btnSelectAll);
            toolbar.Controls.Add(_btnClearSelection);
            toolbar.Controls.Add(_btnLowPdf);
            toolbar.Controls.Add(_btnLowExcel);
            toolbar.Controls.Add(_lowCountLabel);

            // Right-align PDF / Excel on every resize.
            toolbar.Resize += (s, e) =>
            {
                if (toolbar.ClientSize.Width <= 0) return;
                const int rightPad = 4;
                const int gapSmall = 6;

                int x = toolbar.ClientSize.Width - rightPad;
                x -= _btnLowExcel.Width;
                _btnLowExcel.Location = new Point(x, 14);

                x -= gapSmall;
                x -= _btnLowPdf.Width;
                _btnLowPdf.Location = new Point(x, 14);

                _lowCountLabel.Width = Math.Max(200, toolbar.ClientSize.Width - 8);
            };

            // ---- Bottom footer: totals only ----
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 56,
                BackColor = Theme.Background
            };

            _lowTotalsLabel = new Label
            {
                Text = "Total selected: 0   ·   Total units: 0",
                Font = Theme.FontBodyBold,
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(4, 14),
                Height = 32,
                Width = 500,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            footer.Controls.Add(_lowTotalsLabel);

            // ---- Grid ----
            _lowStockGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 0, 4),
                EditMode = DataGridViewEditMode.EditOnEnter
            };
            UiFactory.StyleGrid(_lowStockGrid);
            _lowStockGrid.ReadOnly = false;
            _lowStockGrid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 251);
            _lowStockGrid.CurrentCellDirtyStateChanged += LowStockGrid_CurrentCellDirtyStateChanged;
            _lowStockGrid.CellValueChanged += LowStockGrid_CellValueChanged;
            _lowStockGrid.CellBeginEdit += LowStockGrid_CellBeginEdit;
            _lowStockGrid.CellEndEdit += LowStockGrid_CellEndEdit;

            PrimeLowStockColumns();

            // Dock order: Fill first, then Bottom, then Top.
            _lowStockTab.Controls.Add(_lowStockGrid);
            _lowStockTab.Controls.Add(footer);
            _lowStockTab.Controls.Add(toolbar);
        }

        private void PrimeLowStockColumns()
        {
            _lowStockGrid.AutoGenerateColumns = false;
            _lowStockGrid.Columns.Clear();

            _lowStockGrid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "Select",
                HeaderText = "",
                Width = 44,
                FillWeight = 40,
                ReadOnly = false,
                FalseValue = false,
                TrueValue = true
            });

            void AddReadOnly(string name, string header, int weight)
            {
                _lowStockGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = name,
                    HeaderText = header,
                    FillWeight = weight,
                    ReadOnly = true
                });
            }

            AddReadOnly("Product", "Product", 220);
            AddReadOnly("Brand", "Brand", 120);
            AddReadOnly("Category", "Category", 120);
            AddReadOnly("Unit", "Unit", 60);
            AddReadOnly("OnHand", "On Hand", 70);
            AddReadOnly("Reorder", "Reorder At", 80);

            _lowStockGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "OrderQty",
                HeaderText = "Order Qty",
                FillWeight = 90,
                ReadOnly = false
            });

            _lowStockGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ProductId",
                Visible = false
            });

            _lowStockGrid.Columns["Select"].HeaderCell.Style.Alignment =
                DataGridViewContentAlignment.MiddleCenter;
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
                RefreshLowStockGrid();
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

                _statusFilter.Items.Clear();
                _statusFilter.Items.Add("Active");
                _statusFilter.Items.Add("Inactive");
                _statusFilter.Items.Add("All");
                _statusFilter.SelectedIndex = 0;
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
        // PRODUCTS TAB: FILTERS + GRID BINDING
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
                     p.Brand.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0));
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
                    ProductName = p.ProductName ?? string.Empty,
                    Brand = p.Brand ?? string.Empty,
                    Category = _categoriesById.TryGetValue(p.CategoryId, out var c) ? c.CategoryName : "—",
                    Unit = p.Unit ?? string.Empty,
                    CostPrice = p.CostPrice.ToString("N2"),
                    SellPrice = p.SellingPrice.ToString("N2"),
                    OnHand = p.QuantityOnHand,
                    Reorder = p.ReorderLevel,
                    Status = p.IsActive ? "Active" : "Inactive"
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

            int total = _allProducts?.Count ?? 0;
            int active = _allProducts?.Count(p => p.IsActive) ?? 0;
            int inactive = total - active;

            string status = _statusFilter?.SelectedItem as string ?? "Active";

            _countLabel.Text =
                $"Showing {shownCount} of {total} product(s)   ·   " +
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

            if (_grid.Columns.Contains("ProductId"))
                _grid.Columns["ProductId"].Visible = false;

            SetWeight("ProductName", 240, "Product");
            SetWeight("Brand", 120, "Brand");
            SetWeight("Category", 120, "Category");
            SetWeight("Unit", 60, "Unit");
            SetWeight("CostPrice", 90, "Cost (PHP)");
            SetWeight("SellPrice", 90, "Selling (PHP)");
            SetWeight("OnHand", 70, "On Hand");
            SetWeight("Reorder", 70, "Reorder At");
            SetWeight("Status", 80, "Status");
        }

        private void ClearSelection()
        {
            if (_grid != null && _grid.Rows.Count > 0)
                _grid.ClearSelection();
        }

        private Product? GetSelectedProduct()
        {
            if (_grid == null || _grid.SelectedRows.Count == 0) return null;
            var row = _grid.SelectedRows[0];
            if (!_grid.Columns.Contains("ProductId")) return null;

            var value = row.Cells["ProductId"].Value;
            if (value == null) return null;

            int id = Convert.ToInt32(value);
            return _allProducts.FirstOrDefault(p => p.ProductId == id);
        }

        private void Grid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            EditSelectedProduct();
        }

        private void Grid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                EditSelectedProduct();
            }
        }

        private void OpenProductFormForAdd()
        {
            try
            {
                var parent = FindForm();
                using var form = new ProductForm();
                if (form.ShowDialog(parent) == DialogResult.OK)
                {
                    _ = ReloadAllAsync();
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Could not open Product form: {ex.Message}", isError: true);
            }
        }

        private void EditSelectedProduct()
        {
            var p = GetSelectedProduct();
            if (p == null)
            {
                ShowStatus("Please select a product first.", isError: true);
                return;
            }

            try
            {
                var parent = FindForm();
                using var form = new ProductForm(p);
                if (form.ShowDialog(parent) == DialogResult.OK)
                {
                    _ = ReloadAllAsync();
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Could not open Product form: {ex.Message}", isError: true);
            }
        }

        private async Task DeactivateSelectedProductAsync()
        {
            var p = GetSelectedProduct();
            if (p == null)
            {
                ShowStatus("Please select a product first.", isError: true);
                return;
            }
            if (!p.IsActive)
            {
                ShowStatus("This product is already inactive.", isError: true);
                return;
            }

            if (p.QuantityOnHand > 0)
            {
                var warn = MessageBox.Show(
                    $"This product still has {p.QuantityOnHand} units in stock.\n" +
                    "Deactivate anyway?",
                    "Confirm Deactivate",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (warn != DialogResult.Yes) return;
            }
            else
            {
                var confirm = MessageBox.Show(
                    "Are you sure you want to deactivate this product?",
                    "Confirm Deactivate",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes) return;
            }

            SetBusy(true);
            try
            {
                var (success, error) = await _productService.DeactivateAsync(p.ProductId);
                if (success)
                {
                    ShowStatus("Product deactivated.", isError: false);
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

        private async Task ReactivateSelectedProductAsync()
        {
            var p = GetSelectedProduct();
            if (p == null)
            {
                ShowStatus("Please select a product first.", isError: true);
                return;
            }
            if (p.IsActive)
            {
                ShowStatus("This product is already active.", isError: true);
                return;
            }

            SetBusy(true);
            try
            {
                var (success, error) = await _productService.ReactivateAsync(p.ProductId);
                if (success)
                {
                    ShowStatus("Product reactivated.", isError: false);
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
        // LOW STOCKS TAB: GRID POPULATION
        // ============================================================

        private void RefreshLowStockGrid()
        {
            if (_lowStockGrid == null) return;

            // Preserve existing selections and order-qty values across refreshes.
            var preserved = new Dictionary<int, (bool Selected, int OrderQty)>();
            foreach (DataGridViewRow row in _lowStockGrid.Rows)
            {
                if (row.Cells["ProductId"].Value == null) continue;
                int pid = Convert.ToInt32(row.Cells["ProductId"].Value);
                bool selected = row.Cells["Select"].Value is bool b && b;
                int qty = 0;
                int.TryParse(Convert.ToString(row.Cells["OrderQty"].Value), out qty);
                preserved[pid] = (selected, qty);
            }

            IEnumerable<Product> low = (_allProducts ?? new List<Product>())
                .Where(p => p.IsActive && p.QuantityOnHand <= p.ReorderLevel);

            string search = (_lowSearchBox?.Text ?? string.Empty).Trim();
            if (search.Length > 0)
            {
                low = low.Where(p =>
                    (!string.IsNullOrEmpty(p.ProductName) &&
                     p.ProductName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(p.Brand) &&
                     p.Brand.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            var ordered = low
                .OrderBy(p => p.ProductName)
                .ThenBy(p => p.Brand)
                .ToList();

            _lowStockGrid.CellValueChanged -= LowStockGrid_CellValueChanged;

            try
            {
                _lowStockGrid.Rows.Clear();

                foreach (var p in ordered)
                {
                    int rowIndex = _lowStockGrid.Rows.Add();
                    var row = _lowStockGrid.Rows[rowIndex];

                    bool selected = false;
                    int orderQty = 0;

                    if (preserved.TryGetValue(p.ProductId, out var prev))
                    {
                        selected = prev.Selected;
                        orderQty = prev.OrderQty;
                    }

                    if (selected && orderQty <= 0)
                        orderQty = SuggestedOrderQty(p);

                    row.Cells["Select"].Value = selected;
                    row.Cells["Product"].Value = p.ProductName ?? string.Empty;
                    row.Cells["Brand"].Value = p.Brand ?? string.Empty;
                    row.Cells["Category"].Value = _categoriesById.TryGetValue(p.CategoryId, out var c)
                        ? c.CategoryName : "—";
                    row.Cells["Unit"].Value = p.Unit ?? string.Empty;
                    row.Cells["OnHand"].Value = p.QuantityOnHand;
                    row.Cells["Reorder"].Value = p.ReorderLevel;
                    row.Cells["OrderQty"].Value = selected ? orderQty.ToString() : string.Empty;
                    row.Cells["ProductId"].Value = p.ProductId;

                    ApplyRowStyling(row, selected);
                }
            }
            finally
            {
                _lowStockGrid.CellValueChanged += LowStockGrid_CellValueChanged;
            }

            UpdateLowStockCount(ordered.Count);
            UpdateLowStockTotals();
        }

        private static int SuggestedOrderQty(Product p)
        {
            int need = p.ReorderLevel - p.QuantityOnHand;
            if (need < 0) need = 0;
            return need + 5;
        }

        private void ApplyRowStyling(DataGridViewRow row, bool selected)
        {
            var qtyCell = row.Cells["OrderQty"];
            qtyCell.Style.ForeColor = selected ? Theme.TextPrimary : Theme.TextMuted;
            qtyCell.Style.BackColor = selected ? Color.White : Color.FromArgb(245, 246, 250);

            foreach (var cellName in new[] { "Product", "Brand", "Category", "Unit", "OnHand", "Reorder" })
            {
                row.Cells[cellName].Style.ForeColor = Theme.TextPrimary;
            }
        }

        private void UpdateLowStockCount(int shown)
        {
            if (_lowCountLabel == null) return;

            int total = (_allProducts ?? new List<Product>())
                .Count(p => p.IsActive && p.QuantityOnHand <= p.ReorderLevel);

            _lowCountLabel.Text = $"Showing {shown} of {total} low-stock item(s)";
        }

        private void UpdateLowStockTotals()
        {
            if (_lowTotalsLabel == null || _lowStockGrid == null) return;

            int selectedCount = 0;
            int totalUnits = 0;

            foreach (DataGridViewRow row in _lowStockGrid.Rows)
            {
                if (row.Cells["Select"].Value is bool b && b)
                {
                    selectedCount++;
                    if (int.TryParse(Convert.ToString(row.Cells["OrderQty"].Value), out int q))
                        totalUnits += q;
                }
            }

            _lowTotalsLabel.Text =
                $"Total selected: {selectedCount}   ·   Total units: {totalUnits}";
        }

        // ============================================================
        // LOW STOCKS TAB: GRID EVENTS
        // ============================================================

        private void LowStockGrid_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
        {
            if (_lowStockGrid.IsCurrentCellDirty)
            {
                _lowStockGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void LowStockGrid_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var row = _lowStockGrid.Rows[e.RowIndex];
            string colName = _lowStockGrid.Columns[e.ColumnIndex].Name;

            if (colName == "Select")
            {
                bool selected = row.Cells["Select"].Value is bool b && b;
                if (selected)
                {
                    string existing = Convert.ToString(row.Cells["OrderQty"].Value);
                    if (!int.TryParse(existing, out int qty) || qty <= 0)
                    {
                        if (row.Cells["ProductId"].Value != null)
                        {
                            int pid = Convert.ToInt32(row.Cells["ProductId"].Value);
                            var prod = _allProducts.FirstOrDefault(p => p.ProductId == pid);
                            if (prod != null)
                                row.Cells["OrderQty"].Value = SuggestedOrderQty(prod).ToString();
                        }
                    }
                }
                else
                {
                    row.Cells["OrderQty"].Value = string.Empty;
                }

                ApplyRowStyling(row, selected);
                UpdateLowStockTotals();
            }
            else if (colName == "OrderQty")
            {
                UpdateLowStockTotals();
            }
        }

        private void LowStockGrid_CellBeginEdit(object? sender, DataGridViewCellCancelEventArgs e)
        {
            string colName = _lowStockGrid.Columns[e.ColumnIndex].Name;
            if (colName != "OrderQty") return;

            var row = _lowStockGrid.Rows[e.RowIndex];
            bool selected = row.Cells["Select"].Value is bool b && b;

            if (!selected)
            {
                e.Cancel = true;
            }
        }

        private void LowStockGrid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            string colName = _lowStockGrid.Columns[e.ColumnIndex].Name;
            if (colName != "OrderQty") return;

            var row = _lowStockGrid.Rows[e.RowIndex];
            string raw = Convert.ToString(row.Cells["OrderQty"].Value);

            if (!int.TryParse(raw, out int qty) || qty <= 0)
            {
                if (row.Cells["ProductId"].Value != null)
                {
                    int pid = Convert.ToInt32(row.Cells["ProductId"].Value);
                    var prod = _allProducts.FirstOrDefault(p => p.ProductId == pid);
                    if (prod != null)
                        row.Cells["OrderQty"].Value = SuggestedOrderQty(prod).ToString();
                }
                else
                {
                    row.Cells["OrderQty"].Value = string.Empty;
                }
            }

            UpdateLowStockTotals();
        }

        // ============================================================
        // LOW STOCKS TAB: SELECT ALL / CLEAR
        // ============================================================

        private void SetAllLowStockSelected(bool select)
        {
            if (_lowStockGrid == null) return;

            _lowStockGrid.CellValueChanged -= LowStockGrid_CellValueChanged;
            try
            {
                foreach (DataGridViewRow row in _lowStockGrid.Rows)
                {
                    row.Cells["Select"].Value = select;

                    if (select)
                    {
                        if (row.Cells["ProductId"].Value != null)
                        {
                            int pid = Convert.ToInt32(row.Cells["ProductId"].Value);
                            var prod = _allProducts.FirstOrDefault(p => p.ProductId == pid);
                            if (prod != null)
                            {
                                string existing = Convert.ToString(row.Cells["OrderQty"].Value);
                                if (!int.TryParse(existing, out int q) || q <= 0)
                                    row.Cells["OrderQty"].Value = SuggestedOrderQty(prod).ToString();
                            }
                        }
                    }
                    else
                    {
                        row.Cells["OrderQty"].Value = string.Empty;
                    }

                    ApplyRowStyling(row, select);
                }
            }
            finally
            {
                _lowStockGrid.CellValueChanged += LowStockGrid_CellValueChanged;
            }

            UpdateLowStockTotals();
        }

        // ============================================================
        // LOW STOCKS TAB: REPORT EXPORT
        // ============================================================

        private void GenerateLowStockReport(string format)
        {
            try
            {
                var selected = new List<(Product Product, int Qty)>();

                foreach (DataGridViewRow row in _lowStockGrid.Rows)
                {
                    if (!(row.Cells["Select"].Value is bool b) || !b) continue;
                    if (row.Cells["ProductId"].Value == null) continue;

                    int pid = Convert.ToInt32(row.Cells["ProductId"].Value);
                    var prod = _allProducts.FirstOrDefault(p => p.ProductId == pid);
                    if (prod == null) continue;

                    if (!int.TryParse(Convert.ToString(row.Cells["OrderQty"].Value), out int q) || q <= 0)
                        continue;

                    selected.Add((prod, q));
                }

                if (selected.Count == 0)
                {
                    MessageBox.Show(
                        "Please select at least one item and set an order quantity before generating a report.",
                        "Nothing Selected",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                var table = new ReportTable
                {
                    Title = "ORDER LIST",
                    Subtitle = "Todang Motor Parts & Accessories\n" +
                               "Lower Tamugan, Marilog District, Davao City\n" +
                               $"Generated: {DateTime.Now:MMMM d, yyyy h:mm tt}",
                    FooterNote = $"Total items: {selected.Count}   ·   " +
                                 $"Total units: {selected.Sum(x => x.Qty)}",
                    Headers = new List<string> { "Product", "Brand", "Unit", "Qty" }
                };

                foreach (var (product, qty) in selected.OrderBy(x => x.Product.ProductName))
                {
                    table.Rows.Add(new List<string>
                    {
                        product.ProductName ?? string.Empty,
                        product.Brand ?? string.Empty,
                        product.Unit ?? string.Empty,
                        qty.ToString()
                    });
                }

                string ext = format == "PDF" ? ".pdf" : ".xlsx";
                string filter = format == "PDF"
                    ? "PDF files (*.pdf)|*.pdf"
                    : "Excel files (*.xlsx)|*.xlsx";

                using var sfd = new SaveFileDialog
                {
                    Filter = filter,
                    DefaultExt = ext,
                    AddExtension = true,
                    FileName = $"OrderList_{DateTime.Now:yyyyMMdd_HHmmss}{ext}",
                    InitialDirectory = GetDefaultReportFolder()
                };

                if (sfd.ShowDialog(FindForm()) != DialogResult.OK) return;

                string path = sfd.FileName;

                if (format == "PDF")
                    ReportService.ExportPdf(table, path);
                else
                    ReportService.ExportExcel(table, path);

                var openIt = MessageBox.Show(
                    $"Report saved to:\n{path}\n\nOpen the folder?",
                    "Report Saved",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (openIt == DialogResult.Yes)
                {
                    try
                    {
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
                    }
                    catch { /* ignore */ }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not generate report:\n\n{ex.Message}",
                    "Report Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // ============================================================
        // PRODUCTS TAB: STOCK MOVEMENT REPORT
        // ============================================================

        private async Task GenerateReportAsync(string format)
        {
            if (_isBusy) return;

            try
            {
                var (from, to, rangeLabel) = GetSelectedRange();

                SetBusy(true);
                var movements = await _stockMovementService.GetMovementsInRangeAsync(from, to);

                if (movements == null || movements.Count == 0)
                {
                    ShowStatus("No stock movements in this range.", isError: true);
                    return;
                }

                var productLookup = _allProducts.ToDictionary(p => p.ProductId);

                var grouped = movements
                    .Where(m => productLookup.ContainsKey(m.ProductId))
                    .GroupBy(m => m.ProductId)
                    .Select(g =>
                    {
                        var ordered = g.OrderBy(m => m.MovementDate).ThenBy(m => m.MovementId).ToList();

                        var reasonText = string.Join(", ",
                            ordered.Select(m => m.MovementType)
                                   .Distinct()
                                   .Select(FriendlyMovementType));

                        return new
                        {
                            Product = productLookup[g.Key],
                            First = ordered.First(),
                            Last = ordered.Last(),
                            Net = ordered.Sum(m => m.QuantityChange),
                            Reason = string.IsNullOrEmpty(reasonText) ? "—" : reasonText
                        };
                    })
                    .OrderBy(x => x.Product.ProductName)
                    .ThenBy(x => x.Product.Brand)
                    .ToList();

                var table = new ReportTable
                {
                    Title = "Stock Movement Report",
                    Subtitle = $"{rangeLabel}  ·  Generated {DateTime.Now:MMM d, yyyy h:mm tt}",
                    FooterNote = $"Todang Motor Parts & Accessories  ·  {grouped.Count} product(s) with movement",
                    Headers = new List<string>
                    {
                        "Product", "Brand", "Starting Qty", "Ending Qty", "Net Change", "Reason"
                    }
                };

                foreach (var g in grouped)
                {
                    table.Rows.Add(new List<string>
                    {
                        g.Product.ProductName ?? string.Empty,
                        g.Product.Brand ?? string.Empty,
                        g.First.QuantityBefore.ToString(),
                        g.Last.QuantityAfter.ToString(),
                        (g.Net > 0 ? "+" : "") + g.Net.ToString(),
                        g.Reason
                    });
                }

                string ext = format switch
                {
                    "PDF" => ".pdf",
                    _ => ".xlsx"
                };

                string filter = format switch
                {
                    "PDF" => "PDF files (*.pdf)|*.pdf",
                    _ => "Excel files (*.xlsx)|*.xlsx"
                };

                using var sfd = new SaveFileDialog
                {
                    Filter = filter,
                    DefaultExt = ext,
                    AddExtension = true,
                    FileName = $"StockMovement_{DateTime.Now:yyyyMMdd_HHmmss}{ext}",
                    InitialDirectory = GetDefaultReportFolder()
                };

                if (sfd.ShowDialog(FindForm()) != DialogResult.OK) return;

                var form = FindForm();
                if (form != null) form.Cursor = Cursors.WaitCursor;
                try
                {
                    string path = sfd.FileName;

                    await Task.Run(() =>
                    {
                        if (format == "PDF")
                            ReportService.ExportPdf(table, path);
                        else
                            ReportService.ExportExcel(table, path);
                    });

                    var openIt = MessageBox.Show(
                        $"Report saved to:\n{path}\n\nOpen the folder?",
                        "Report Saved",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (openIt == DialogResult.Yes)
                    {
                        try
                        {
                            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
                        }
                        catch { /* ignore */ }
                    }

                    ShowStatus($"Report saved: {Path.GetFileName(path)}", isError: false);
                }
                finally
                {
                    if (form != null) form.Cursor = Cursors.Default;
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Could not generate report: {ex.Message}", isError: true);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private static string FriendlyMovementType(string movementType)
        {
            return movementType switch
            {
                "Adjustment" => "Owner Adjustment",
                "Sale" => "POS Sale",
                "StockIn" => "Stock-In",
                "Initial" => "Initial Stock",
                _ => movementType ?? "—"
            };
        }

        private (DateTime from, DateTime to, string label) GetSelectedRange()
        {
            DateTime now = DateTime.Now;
            string choice = _rangeCombo?.SelectedItem as string ?? "Today";

            switch (choice)
            {
                case "This Week":
                    {
                        var from = now.Date.AddDays(-6);
                        var to = now.Date.AddDays(1);
                        return (from, to, $"{from:MMM d} – {now:MMM d, yyyy}");
                    }
                case "This Month":
                    {
                        var from = now.Date.AddDays(-29);
                        var to = now.Date.AddDays(1);
                        return (from, to, $"{from:MMM d} – {now:MMM d, yyyy}");
                    }
                default:
                    {
                        var from = now.Date;
                        var to = from.AddDays(1);
                        return (from, to, now.ToString("MMMM d, yyyy"));
                    }
            }
        }

        private static string GetDefaultReportFolder()
        {
            try
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string folder = Path.Combine(docs, "TodangMotor", "Reports");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);
                return folder;
            }
            catch
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
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
            _btnPdf.Enabled = !busy;
            _btnExcel.Enabled = !busy;
        }

        private void ShowStatus(string message, bool isError)
        {
            if (_statusLabel == null) return;
            _statusLabel.ForeColor = isError ? Theme.Danger : Theme.Success;
            _statusLabel.Text = message ?? string.Empty;
        }
    }
}