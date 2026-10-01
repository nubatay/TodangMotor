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
    public class PosControl : UserControl
    {
        private readonly SalesService _salesService = new();
        private readonly CategoryService _categoryService = new(new CategoryRepository());

        private List<CashierProductView> _allProducts = new();
        private List<Category> _categories = new();
        private readonly List<CartLine> _cart = new();

        private bool _isLoaded;
        private bool _isBusy;
        private bool _isUpdatingPayment;

        private class CartLine
        {
            public int ProductId { get; set; }
            public string ProductName { get; set; } = string.Empty;
            public string Brand { get; set; } = string.Empty;
            public string Unit { get; set; } = string.Empty;
            public decimal UnitPrice { get; set; }
            public int Quantity { get; set; }
            public int MaxOnHand { get; set; }
            public decimal LineTotal => UnitPrice * Quantity;
        }

        private RoundedTextBox _searchBox;
        private RoundedComboBox _categoryFilter;
        private RoundedTextBox _qtyBox;
        private Button _btnAddToCart;
        private DataGridView _productGrid;
        private Label _productCountLabel;

        private DataGridView _cartGrid;
        private Label _subtotalBigLabel;
        private Button _btnRemoveLine;
        private Button _btnClearCart;

        private RoundedTextBox _customerNameBox;
        private RoundedComboBox _paymentMethodCombo;
        private RoundedTextBox _tenderedBox;

        private Label _changeLabel;
        private Button _btnCompleteSale;
        private Label _statusLabel;

        private Button _btnT100, _btnT200, _btnT500, _btnT1000;

        // Stored so we can reposition on resize.
        private Panel _bottomPanel;
        private int _actionButtonsY;

        public PosControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(Theme.SpacingLg);

            BuildLayout();

            Load += PosControl_Load;
        }

        private async void PosControl_Load(object sender, EventArgs e)
        {
            if (_isLoaded) return;
            _isLoaded = true;
            await LoadLookupsAsync();
        }

        private void BuildLayout()
        {
            var split = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Theme.Background,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58f));
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42f));
            split.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            split.Controls.Add(BuildProductsPane(), 0, 0);
            split.Controls.Add(BuildCartPane(), 1, 0);

            Controls.Add(split);
        }

        // ---------------- LEFT PANE ----------------

        private Panel BuildProductsPane()
        {
            var pane = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Background,
                Padding = new Padding(0, 0, 12, 0)
            };

            var filterBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 84,
                BackColor = Theme.Background
            };

            _searchBox = UiFactory.CreateTextBox(240);
            _searchBox.Location = new Point(0, 12);
            _searchBox.Placeholder = "Search product or brand…";
            _searchBox.MaxLength = 100;
            _searchBox.TextChanged += (s, e) => ApplyProductFilter();

            var lblCat = new Label
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
                Width = 220,
                Location = new Point(336, 12)
            };
            _categoryFilter.SelectedIndexChanged += (s, e) => ApplyProductFilter();

            _productCountLabel = new Label
            {
                Text = "Loading…",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(0, 58),
                Height = 20,
                Width = 500,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            filterBar.Controls.Add(_searchBox);
            filterBar.Controls.Add(lblCat);
            filterBar.Controls.Add(_categoryFilter);
            filterBar.Controls.Add(_productCountLabel);

            var actionBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 68,
                BackColor = Theme.Background
            };

            var lblQty = new Label
            {
                Text = "Quantity:",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(80, 40),
                Location = new Point(0, 14),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _qtyBox = UiFactory.CreateTextBox(90);
            _qtyBox.Location = new Point(86, 14);
            _qtyBox.MaxLength = 6;
            _qtyBox.Text = "1";

            _btnAddToCart = UiFactory.CreateButton(
                "Add to Cart", UiFactory.ButtonStyle.Primary, 150, 40);
            _btnAddToCart.Location = new Point(188, 14);
            _btnAddToCart.Click += (s, e) => AddSelectedToCart();

            actionBar.Controls.Add(lblQty);
            actionBar.Controls.Add(_qtyBox);
            actionBar.Controls.Add(_btnAddToCart);

            _productGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 0, 4)
            };
            UiFactory.StyleGrid(_productGrid);
            _productGrid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 251);

            // 0-stock highlight (red) — also flags low stock (amber).
            _productGrid.CellFormatting += ProductGrid_CellFormatting;

            _productGrid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                AddSelectedToCart();
            };
            _productGrid.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    AddSelectedToCart();
                }
            };

            pane.Controls.Add(_productGrid);
            pane.Controls.Add(actionBar);
            pane.Controls.Add(filterBar);

            return pane;
        }

        private void ProductGrid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (e.RowIndex >= _productGrid.Rows.Count) return;
            if (!_productGrid.Columns.Contains("OnHand")) return;

            var onHandVal = _productGrid.Rows[e.RowIndex].Cells["OnHand"].Value;
            if (onHandVal == null) return;

            if (int.TryParse(onHandVal.ToString(), out int qty))
            {
                if (qty == 0)
                {
                    // Red — out of stock.
                    e.CellStyle.ForeColor = Color.FromArgb(180, 40, 40);
                    e.CellStyle.SelectionForeColor = Color.FromArgb(180, 40, 40);
                    e.CellStyle.BackColor = Color.FromArgb(255, 240, 240);
                    e.CellStyle.SelectionBackColor = Color.FromArgb(255, 225, 225);
                }
                else if (qty <= 5)
                {
                    // Amber — low stock warning.
                    e.CellStyle.ForeColor = Color.FromArgb(200, 130, 40);
                    e.CellStyle.SelectionForeColor = Color.FromArgb(200, 130, 40);
                }
            }
        }

        // ---------------- RIGHT PANE ----------------

        private Panel BuildCartPane()
        {
            var pane = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Background,
                Padding = new Padding(12, 0, 0, 0)
            };

            _bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 420,
                BackColor = Theme.Background
            };

            int y = 8;

            _subtotalBigLabel = new Label
            {
                Text = "Subtotal: ₱ 0.00",
                Font = new Font(Theme.UiFontFamily, 16F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(4, y),
                Size = new Size(360, 34),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            _bottomPanel.Controls.Add(_subtotalBigLabel);
            y += 42;

            var lblCust = new Label
            {
                Text = "Customer Name (optional):",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(300, 18),
                Location = new Point(4, y),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            _bottomPanel.Controls.Add(lblCust);
            y += 22;

            _customerNameBox = UiFactory.CreateTextBox(360);
            _customerNameBox.Location = new Point(4, y);
            _customerNameBox.MaxLength = 100;
            _customerNameBox.Placeholder = "Leave blank for walk-in";
            _bottomPanel.Controls.Add(_customerNameBox);
            y += 50;

            var lblPay = new Label
            {
                Text = "Payment Method:",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(180, 18),
                Location = new Point(4, y),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            _bottomPanel.Controls.Add(lblPay);

            var lblTendered = new Label
            {
                Text = "Amount Tendered:",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(180, 18),
                Location = new Point(196, y),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            _bottomPanel.Controls.Add(lblTendered);
            y += 22;

            _paymentMethodCombo = new RoundedComboBox
            {
                Width = 180,
                Location = new Point(4, y)
            };
            _paymentMethodCombo.Items.Add("Cash");
            _paymentMethodCombo.Items.Add("GCash");
            _paymentMethodCombo.SelectedIndex = 0;
            _paymentMethodCombo.SelectedIndexChanged += (s, e) => OnPaymentMethodChanged();
            _bottomPanel.Controls.Add(_paymentMethodCombo);

            _tenderedBox = UiFactory.CreateTextBox(180);
            _tenderedBox.Location = new Point(196, y);
            _tenderedBox.MaxLength = 12;
            _tenderedBox.TextChanged += (s, e) => OnTenderedChanged();
            _bottomPanel.Controls.Add(_tenderedBox);
            y += 50;

            _btnT100 = MakeQuickTender(100);
            _btnT200 = MakeQuickTender(200);
            _btnT500 = MakeQuickTender(500);
            _btnT1000 = MakeQuickTender(1000);

            _btnT100.Location = new Point(4, y);
            _btnT200.Location = new Point(94, y);
            _btnT500.Location = new Point(184, y);
            _btnT1000.Location = new Point(274, y);

            _bottomPanel.Controls.Add(_btnT100);
            _bottomPanel.Controls.Add(_btnT200);
            _bottomPanel.Controls.Add(_btnT500);
            _bottomPanel.Controls.Add(_btnT1000);
            y += 46;

            _changeLabel = new Label
            {
                Text = "Change: ₱ 0.00",
                Font = new Font(Theme.UiFontFamily, 12F, FontStyle.Bold),
                ForeColor = Theme.Success,
                AutoSize = false,
                Location = new Point(4, y),
                Size = new Size(360, 26),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            _bottomPanel.Controls.Add(_changeLabel);
            y += 34;

            _statusLabel = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.Danger,
                AutoSize = false,
                Location = new Point(4, y),
                Height = 40,
                Width = 360,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            _bottomPanel.Controls.Add(_statusLabel);
            y += 48;

            // Store the y for the button row so Resize can use it.
            _actionButtonsY = y;

            // Buttons — all same width, evenly spaced, left-aligned.
            // Positions set in a Resize handler to keep them clean at any width.
            const int btnW = 118;
            const int btnH = 40;

            _btnRemoveLine = UiFactory.CreateButton("Remove Line", UiFactory.ButtonStyle.Secondary, btnW, btnH);
            _btnRemoveLine.Click += (s, e) => RemoveSelectedCartLine();

            _btnClearCart = UiFactory.CreateButton("Clear Cart", UiFactory.ButtonStyle.Secondary, btnW, btnH);
            _btnClearCart.Click += (s, e) => ClearCart();

            _btnCompleteSale = UiFactory.CreateButton("Complete Sale", UiFactory.ButtonStyle.Primary, btnW, btnH);
            _btnCompleteSale.Click += async (s, e) => await CompleteSaleAsync();

            _bottomPanel.Controls.Add(_btnRemoveLine);
            _bottomPanel.Controls.Add(_btnClearCart);
            _bottomPanel.Controls.Add(_btnCompleteSale);

            _bottomPanel.Resize += (s, e) => LayoutActionButtons();

            // Cart grid
            _cartGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 0, 4),
                EditMode = DataGridViewEditMode.EditOnEnter
            };
            UiFactory.StyleGrid(_cartGrid);
            _cartGrid.ReadOnly = false;
            _cartGrid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 251);
            _cartGrid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_cartGrid.IsCurrentCellDirty)
                    _cartGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _cartGrid.CellValueChanged += CartGrid_CellValueChanged;
            _cartGrid.CellEndEdit += CartGrid_CellEndEdit;
            PrimeCartColumns();

            pane.Controls.Add(_cartGrid);
            pane.Controls.Add(_bottomPanel);

            return pane;
        }

        private void LayoutActionButtons()
        {
            if (_bottomPanel == null || _btnRemoveLine == null) return;

            const int leftPad = 4;
            const int gap = 8;
            int btnW = _btnRemoveLine.Width;
            int y = _actionButtonsY;

            _btnRemoveLine.Location = new Point(leftPad, y);
            _btnClearCart.Location = new Point(leftPad + btnW + gap, y);
            _btnCompleteSale.Location = new Point(leftPad + (btnW + gap) * 2, y);
        }

        private Button MakeQuickTender(decimal amount)
        {
            var b = UiFactory.CreateButton(
                "₱" + amount.ToString("N0"),
                UiFactory.ButtonStyle.Secondary,
                86, 36);
            b.Click += (s, e) =>
            {
                if (_paymentMethodCombo.SelectedItem as string == "Cash")
                {
                    _tenderedBox.Text = amount.ToString("0.00");
                    OnTenderedChanged();
                }
            };
            return b;
        }

        private void PrimeCartColumns()
        {
            _cartGrid.AutoGenerateColumns = false;
            _cartGrid.Columns.Clear();

            void AddCol(string prop, string header, int weight, bool readOnly)
            {
                _cartGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = prop,
                    DataPropertyName = prop,
                    HeaderText = header,
                    FillWeight = weight,
                    ReadOnly = readOnly
                });
            }

            AddCol("Product", "Item", 200, true);
            AddCol("Quantity", "Qty", 60, false);
            AddCol("UnitPrice", "Price", 80, true);
            AddCol("LineTotal", "Total", 100, true);

            _cartGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ProductId",
                DataPropertyName = "ProductId",
                Visible = false
            });
        }

        // ============================================================
        // DATA LOAD
        // ============================================================

        private async Task LoadLookupsAsync()
        {
            SetBusy(true);
            try
            {
                try
                {
                    var cats = await _categoryService.GetAllCategoriesAsync();
                    _categories = cats?.ToList() ?? new List<Category>();

                    _categoryFilter.Items.Clear();
                    _categoryFilter.Items.Add("All Categories");
                    foreach (var c in _categories)
                        _categoryFilter.Items.Add(c.CategoryName);
                    _categoryFilter.SelectedIndex = 0;
                }
                catch
                {
                    ShowError("Could not load categories.");
                }

                var (success, error, products) =
                    await _salesService.GetActiveProductsForCashierAsync();

                if (!success)
                {
                    _allProducts = new List<CashierProductView>();
                    ShowError(error);
                    return;
                }

                _allProducts = products ?? new List<CashierProductView>();
                ApplyProductFilter();
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void ApplyProductFilter()
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
                var catObj = _categories.FirstOrDefault(c =>
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
                    Product = p.ProductName,
                    Brand = p.Brand,
                    Unit = p.Unit,
                    Price = p.SellingPrice.ToString("N2"),
                    OnHand = p.QuantityOnHand
                })
                .ToList();

            _productGrid.DataSource = rows;

            if (_productGrid.Columns.Contains("ProductId"))
                _productGrid.Columns["ProductId"].Visible = false;

            void SetWeight(string col, int weight, string header = null)
            {
                if (!_productGrid.Columns.Contains(col)) return;
                _productGrid.Columns[col].FillWeight = weight;
                if (header != null) _productGrid.Columns[col].HeaderText = header;
            }

            SetWeight("Product", 240, "Product");
            SetWeight("Brand", 140, "Brand");
            SetWeight("Unit", 60, "Unit");
            SetWeight("Price", 100, "Price (PHP)");
            SetWeight("OnHand", 80, "On Hand");

            if (_productGrid.Rows.Count > 0)
                _productGrid.ClearSelection();

            _productCountLabel.Text =
                $"Showing {rows.Count} of {_allProducts.Count} product(s)";
        }

        // ============================================================
        // CART OPERATIONS
        // ============================================================

        private CashierProductView? GetSelectedProduct()
        {
            if (_productGrid == null || _productGrid.SelectedRows.Count == 0) return null;
            var row = _productGrid.SelectedRows[0];
            if (!_productGrid.Columns.Contains("ProductId")) return null;

            var val = row.Cells["ProductId"].Value;
            if (val == null) return null;

            int id = Convert.ToInt32(val);
            return _allProducts.FirstOrDefault(p => p.ProductId == id);
        }

        private void AddSelectedToCart()
        {
            var p = GetSelectedProduct();
            if (p == null)
            {
                ShowError("Please select a product first.");
                return;
            }

            if (!int.TryParse((_qtyBox.Text ?? "1").Trim(), out int qty) || qty <= 0)
            {
                ShowErrorBox("Quantity must be a whole number greater than zero.");
                _qtyBox.FocusInput();
                return;
            }

            var existing = _cart.FirstOrDefault(c => c.ProductId == p.ProductId);
            int currentInCart = existing?.Quantity ?? 0;

            if (currentInCart + qty > p.QuantityOnHand)
            {
                ShowErrorBox(
                    $"Not enough stock for \"{p.ProductName}\".\n\n" +
                    $"On hand: {p.QuantityOnHand}\n" +
                    $"Already in cart: {currentInCart}\n" +
                    $"Trying to add: {qty}");
                return;
            }

            if (existing != null)
            {
                existing.Quantity += qty;
            }
            else
            {
                _cart.Add(new CartLine
                {
                    ProductId = p.ProductId,
                    ProductName = p.ProductName,
                    Brand = p.Brand,
                    Unit = p.Unit,
                    UnitPrice = p.SellingPrice,
                    Quantity = qty,
                    MaxOnHand = p.QuantityOnHand
                });
            }

            RefreshCartGrid();
            _qtyBox.Text = "1";
            ShowInfo($"Added {qty} × {p.ProductName} to cart.");
        }

        private void RemoveSelectedCartLine()
        {
            if (_cartGrid.SelectedRows.Count == 0)
            {
                ShowError("Select a line in the cart to remove.");
                return;
            }

            var cell = _cartGrid.SelectedRows[0].Cells["ProductId"];
            if (cell == null || cell.Value == null) return;

            int id = Convert.ToInt32(cell.Value);
            var line = _cart.FirstOrDefault(c => c.ProductId == id);
            if (line != null)
            {
                _cart.Remove(line);
                RefreshCartGrid();
                ShowInfo("Line removed.");
            }
        }

        private void ClearCart()
        {
            if (_cart.Count == 0) return;

            var confirm = MessageBox.Show(
                "Discard all items in the cart?",
                "Confirm Clear Cart",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            _cart.Clear();
            _tenderedBox.Text = string.Empty;
            _customerNameBox.Text = string.Empty;
            RefreshCartGrid();
            ShowInfo("Cart cleared.");
        }

        private void RefreshCartGrid()
        {
            var rows = _cart
                .Select(c => new
                {
                    ProductId = c.ProductId,
                    Product = string.IsNullOrEmpty(c.Brand)
                                ? c.ProductName
                                : $"{c.ProductName} — {c.Brand}",
                    Quantity = c.Quantity,
                    UnitPrice = c.UnitPrice.ToString("N2"),
                    LineTotal = c.LineTotal.ToString("N2")
                })
                .ToList();

            _cartGrid.DataSource = rows;

            if (_cartGrid.Rows.Count > 0)
                _cartGrid.ClearSelection();

            UpdateCartTotals();
        }

        private void UpdateCartTotals()
        {
            decimal subtotal = _cart.Sum(c => c.LineTotal);
            _subtotalBigLabel.Text = $"Subtotal: ₱ {subtotal:N2}";
            RecalculateChange(subtotal);
        }

        private void RecalculateChange(decimal subtotal)
        {
            string method = _paymentMethodCombo?.SelectedItem as string ?? "Cash";

            if (method == "GCash")
            {
                _changeLabel.ForeColor = Theme.TextPrimary;
                _changeLabel.Text = "Change: ₱ 0.00";
                return;
            }

            if (decimal.TryParse((_tenderedBox.Text ?? "").Trim(),
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out decimal tendered)
                && tendered >= subtotal)
            {
                decimal change = tendered - subtotal;
                _changeLabel.ForeColor = Theme.Success;
                _changeLabel.Text = $"Change: ₱ {change:N2}";
            }
            else
            {
                _changeLabel.ForeColor = Theme.TextMuted;
                _changeLabel.Text = "Change: ₱ 0.00";
            }
        }

        private void CartGrid_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (_cartGrid.Columns[e.ColumnIndex].Name != "Quantity") return;

            var row = _cartGrid.Rows[e.RowIndex];
            if (row.Cells["ProductId"].Value == null) return;

            int productId = Convert.ToInt32(row.Cells["ProductId"].Value);
            var line = _cart.FirstOrDefault(c => c.ProductId == productId);
            if (line == null) return;

            string raw = Convert.ToString(row.Cells["Quantity"].Value);

            if (!int.TryParse(raw, out int qty) || qty <= 0)
            {
                ShowErrorBox("Quantity must be a positive whole number.");
                row.Cells["Quantity"].Value = line.Quantity;
                return;
            }

            if (qty > line.MaxOnHand)
            {
                ShowErrorBox(
                    $"Not enough stock for \"{line.ProductName}\".\n\n" +
                    $"On hand: {line.MaxOnHand}\n" +
                    $"Requested: {qty}");
                row.Cells["Quantity"].Value = line.Quantity;
                return;
            }

            line.Quantity = qty;
            row.Cells["LineTotal"].Value = (line.UnitPrice * qty).ToString("N2");
            UpdateCartTotals();
        }

        private void CartGrid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (_cartGrid.Columns[e.ColumnIndex].Name != "Quantity") return;

            var row = _cartGrid.Rows[e.RowIndex];
            if (row.Cells["ProductId"].Value == null) return;

            int productId = Convert.ToInt32(row.Cells["ProductId"].Value);
            var line = _cart.FirstOrDefault(c => c.ProductId == productId);
            if (line == null) return;

            row.Cells["Quantity"].Value = line.Quantity;
        }

        private void OnPaymentMethodChanged()
        {
            if (_isUpdatingPayment) return;
            _isUpdatingPayment = true;

            try
            {
                string method = _paymentMethodCombo.SelectedItem as string ?? "Cash";
                decimal subtotal = _cart.Sum(c => c.LineTotal);

                if (method == "GCash")
                {
                    _tenderedBox.Text = subtotal.ToString("0.00");
                    _tenderedBox.Enabled = false;
                    _btnT100.Enabled = _btnT200.Enabled = _btnT500.Enabled = _btnT1000.Enabled = false;
                }
                else
                {
                    _tenderedBox.Text = string.Empty;
                    _tenderedBox.Enabled = true;
                    _btnT100.Enabled = _btnT200.Enabled = _btnT500.Enabled = _btnT1000.Enabled = true;
                }

                RecalculateChange(subtotal);
            }
            finally
            {
                _isUpdatingPayment = false;
            }
        }

        private void OnTenderedChanged()
        {
            if (_isUpdatingPayment) return;
            RecalculateChange(_cart.Sum(c => c.LineTotal));
        }

        private async Task CompleteSaleAsync()
        {
            if (_isBusy) return;

            if (_cart.Count == 0)
            {
                ShowErrorBox("Add at least one item to the cart first.");
                return;
            }

            string method = _paymentMethodCombo.SelectedItem as string ?? "Cash";

            decimal tendered = 0m;
            if (method == "Cash")
            {
                if (!decimal.TryParse((_tenderedBox.Text ?? "").Trim(),
                        System.Globalization.NumberStyles.Number,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out tendered))
                {
                    ShowErrorBox("Please enter a valid amount tendered.");
                    _tenderedBox.FocusInput();
                    return;
                }

                decimal subtotal = _cart.Sum(c => c.LineTotal);
                if (tendered < subtotal)
                {
                    ShowErrorBox(
                        $"Amount tendered (₱{tendered:N2}) is less than the " +
                        $"subtotal (₱{subtotal:N2}).\n\n" +
                        "Please enter a larger amount.");
                    _tenderedBox.FocusInput();
                    return;
                }
            }
            else
            {
                tendered = _cart.Sum(c => c.LineTotal);
            }

            var requestLines = _cart
                .Select(c => new SaleRequestLine
                {
                    ProductId = c.ProductId,
                    Quantity = c.Quantity
                })
                .ToList();

            SetBusy(true);
            try
            {
                var (success, error, _saleId, invoiceNo) = await _salesService.CompleteSaleAsync(
                    requestLines, method, tendered, _customerNameBox.Text);

                if (!success)
                {
                    ShowErrorBox(error);
                    return;
                }

                decimal subtotalFinal = _cart.Sum(c => c.LineTotal);
                decimal change = method == "Cash" ? tendered - subtotalFinal : 0m;

                MessageBox.Show(
                    $"Sale completed.\n\n" +
                    $"Invoice:  {invoiceNo}\n" +
                    $"Total:    ₱ {subtotalFinal:N2}\n" +
                    $"Paid:     ₱ {tendered:N2}  ({method})\n" +
                    $"Change:   ₱ {change:N2}",
                    "Sale Successful",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                _cart.Clear();
                _tenderedBox.Text = string.Empty;
                _customerNameBox.Text = string.Empty;
                _paymentMethodCombo.SelectedIndex = 0;
                RefreshCartGrid();
                ShowInfo($"Invoice {invoiceNo} saved.");

                await LoadLookupsAsync();
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            _btnAddToCart.Enabled = !busy;
            _btnCompleteSale.Enabled = !busy;
            _btnClearCart.Enabled = !busy;
            _btnRemoveLine.Enabled = !busy;
            _searchBox.Enabled = !busy;
            _categoryFilter.Enabled = !busy;
            _qtyBox.Enabled = !busy;
            _customerNameBox.Enabled = !busy;
            _paymentMethodCombo.Enabled = !busy;
        }

        private void ShowError(string message)
        {
            if (_statusLabel == null) return;
            _statusLabel.ForeColor = Theme.Danger;
            _statusLabel.Text = message ?? string.Empty;
        }

        private void ShowInfo(string message)
        {
            if (_statusLabel == null) return;
            _statusLabel.ForeColor = Theme.Success;
            _statusLabel.Text = message ?? string.Empty;
        }

        private void ShowErrorBox(string message)
        {
            ShowError(message);
            MessageBox.Show(message, "Invalid Action",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}