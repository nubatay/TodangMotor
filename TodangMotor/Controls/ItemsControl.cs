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
    /// Owner-only. Handles Add / Edit / Deactivate / Reactivate.
    /// No stock quantities shown here — that belongs to Inventory.
    /// </summary>
    public class ItemsControl : UserControl
    {
        // ============================================================
        // SERVICES
        // ============================================================

        private readonly ProductService _productService = new();
        private readonly CategoryService _categoryService = new(new CategoryRepository());

        // ============================================================
        // STATE
        // ============================================================

        private List<Product> _allProducts = new();
        private List<Category> _allCategories = new();
        private Dictionary<int, Category> _categoriesById = new();

        private bool _isLoaded;
        private bool _isBusy;

        // ============================================================
        // UI CONTROLS
        // ============================================================

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

        // ============================================================
        // CONSTRUCTION
        // ============================================================

        public ItemsControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(Theme.SpacingLg);

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
            // Fill first, then Bottom, then Top.
            Controls.Add(BuildGrid());
            Controls.Add(BuildBottomBar());
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

            _searchBox = UiFactory.CreateTextBox(240);
            _searchBox.Location = new Point(4, 12);
            _searchBox.Placeholder = "Search product or brand…";
            _searchBox.MaxLength = 150;
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
                Size = new Size(56, 20),
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
            _grid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                EditSelectedProduct();
            };
            _grid.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    EditSelectedProduct();
                }
            };
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
                    SKU = p.SKU ?? "—",
                    Category = _categoriesById.TryGetValue(p.CategoryId, out var c) ? c.CategoryName : "—",
                    Unit = p.Unit ?? string.Empty,
                    Cost = p.CostPrice.ToString("N2"),
                    Selling = p.SellingPrice.ToString("N2"),
                    Reorder = p.ReorderLevel,
                    Supplier = p.PrimarySupplierName ?? "—",
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
                $"Showing {shownCount} of {total} item(s)   ·   " +
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

            SetWeight("Product", 200, "Product");
            SetWeight("Brand", 110, "Brand");
            SetWeight("SKU", 100, "SKU");
            SetWeight("Category", 110, "Category");
            SetWeight("Unit", 50, "Unit");
            SetWeight("Cost", 80, "Cost (PHP)");
            SetWeight("Selling", 80, "Selling (PHP)");
            SetWeight("Reorder", 70, "Reorder At");
            SetWeight("Supplier", 130, "Supplier");
            SetWeight("Status", 70, "Status");
        }

        private void ClearSelection()
        {
            if (_grid != null && _grid.Rows.Count > 0)
                _grid.ClearSelection();
        }

        // ============================================================
        // SELECTION HELPER
        // ============================================================

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
                ShowStatus("Please select an item first.", isError: true);
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

        // ============================================================
        // DEACTIVATE / REACTIVATE
        // ============================================================

        private async Task DeactivateSelectedProductAsync()
        {
            var p = GetSelectedProduct();
            if (p == null)
            {
                ShowStatus("Please select an item first.", isError: true);
                return;
            }
            if (!p.IsActive)
            {
                ShowStatus("This item is already inactive.", isError: true);
                return;
            }

            if (p.QuantityOnHand > 0)
            {
                var warn = MessageBox.Show(
                    $"This item still has {p.QuantityOnHand} units in stock.\n" +
                    "Deactivate anyway?",
                    "Confirm Deactivate",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (warn != DialogResult.Yes) return;
            }
            else
            {
                var confirm = MessageBox.Show(
                    "Are you sure you want to deactivate this item?",
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

        private async Task ReactivateSelectedProductAsync()
        {
            var p = GetSelectedProduct();
            if (p == null)
            {
                ShowStatus("Please select an item first.", isError: true);
                return;
            }
            if (p.IsActive)
            {
                ShowStatus("This item is already active.", isError: true);
                return;
            }

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