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

        private const string CartIcon = "\uE7BF"; // Segoe MDL2 Assets — ShoppingCart

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

        private class ProductGridRow
        {
            public int ProductId { get; set; }
            public string Product { get; set; } = string.Empty;
            public string Brand { get; set; } = string.Empty;
            public string Unit { get; set; } = string.Empty;
            public string Price { get; set; } = string.Empty;
            public int OnHand { get; set; }
            public int Qty { get; set; } = 1;
        }

        private RoundedTextBox _searchBox;
        private RoundedComboBox _categoryFilter;
        private DataGridView _productGrid;
        private Label _productCountLabel;

        private DataGridView _cartGrid;
        private Label _lblSubtotalCaption;
        private Label _lblSubtotalValue;
        private Label _lblChangeCaption;
        private Label _lblChangeValue;
        private Button _btnClearCart;

        private Label _lblCustomer;
        private RoundedTextBox _customerNameBox;
        private Label _lblPay;
        private Label _lblTendered;
        private RoundedComboBox _paymentMethodCombo;
        private RoundedTextBox _tenderedBox;
        private Label _lblGcashRef;         
        private RoundedTextBox _gcashRefBox;

        private Button _btnCompleteSale;
        private Label _statusLabel;

        

        private Panel _bottomPanel;
        private Panel _emptyCartOverlay;

        public PosControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(UiFactory.PagePadding);

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
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62f));
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
            split.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            split.Controls.Add(BuildProductsPane(), 0, 0);
            split.Controls.Add(BuildCartPane(), 1, 0);

            Controls.Add(split);
        }

        // ---------------- LEFT PANE ----------------

        private RoundedPanel BuildProductsPane()
        {
            var card = UiFactory.CreateCard();
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(0, 0, UiFactory.TileGap, 0);

            var filterBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 104,
                BackColor = Theme.Surface
            };

            _searchBox = UiFactory.CreateTextBox(320);
            _searchBox.Location = new Point(0, 16);
            _searchBox.Placeholder = "Search product or brand…";
            _searchBox.MaxLength = 100;
            _searchBox.TextChanged += (s, e) => ApplyProductFilter();

            var lblCat = new Label
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
                Location = new Point(lblCat.Right + 8, 16)
            };
            _categoryFilter.SelectedIndexChanged += (s, e) => ApplyProductFilter();

            _productCountLabel = new Label
            {
                Text = "Loading…",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(0, 68),
                Height = 22,
                Width = 500,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            filterBar.Controls.Add(_searchBox);
            filterBar.Controls.Add(lblCat);
            filterBar.Controls.Add(_categoryFilter);
            filterBar.Controls.Add(_productCountLabel);

            _productGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 0, 0),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                EditMode = DataGridViewEditMode.EditOnEnter
            };
            UiFactory.StyleGridModern(_productGrid);
            _productGrid.ReadOnly = false;
            // NOTE: no AlternatingRowsDefaultCellStyle — it overrides the icon column colors
            _productGrid.CellFormatting += ProductGrid_CellFormatting;
            _productGrid.CellClick += ProductGrid_CellClick;
            _productGrid.CellMouseEnter += ProductGrid_CellMouseEnter;
            _productGrid.CellMouseLeave += (s, e) => _productGrid.Cursor = Cursors.Default;
            _productGrid.CellEndEdit += ProductGrid_CellEndEdit;
            _productGrid.KeyDown += ProductGrid_KeyDown;

            PrimeProductColumns();

            card.Controls.Add(_productGrid);
            card.Controls.Add(filterBar);

            return card;
        }

        private void PrimeProductColumns()
        {
            _productGrid.AutoGenerateColumns = false;
            _productGrid.Columns.Clear();

            void AddTextCol(string prop, string header, int weight, bool readOnly,
                DataGridViewContentAlignment align = DataGridViewContentAlignment.MiddleLeft)
            {
                var col = new DataGridViewTextBoxColumn
                {
                    Name = prop,
                    DataPropertyName = prop,
                    HeaderText = header,
                    FillWeight = weight,
                    ReadOnly = readOnly
                };
                if (align != DataGridViewContentAlignment.MiddleLeft)
                    col.DefaultCellStyle.Alignment = align;
                _productGrid.Columns.Add(col);
            }

            AddTextCol("Product", "Product", 200, true);
            AddTextCol("Brand", "Brand", 110, true);
            AddTextCol("Unit", "Unit", 55, true, DataGridViewContentAlignment.MiddleCenter);
            AddTextCol("Price", "Price (PHP)", 110, true, DataGridViewContentAlignment.MiddleRight);
            AddTextCol("OnHand", "On Hand", 100, true, DataGridViewContentAlignment.MiddleRight);
            AddTextCol("Qty", "Qty", 55, false, DataGridViewContentAlignment.MiddleCenter);

            // Add-to-cart icon column (soft button look)
            var addCol = new DataGridViewTextBoxColumn
            {
                Name = "AddBtn",
                HeaderText = "",
                FillWeight = 60,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Font = new Font(Theme.IconFontFamily, 15F, FontStyle.Regular),
                    ForeColor = Theme.Primary,
                    SelectionForeColor = Color.White,
                    BackColor = Color.FromArgb(232, 238, 255),
                    SelectionBackColor = Theme.Primary,
                    NullValue = CartIcon,
                    Padding = new Padding(0)
                }
            };
            _productGrid.Columns.Add(addCol);

            _productGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ProductId",
                DataPropertyName = "ProductId",
                Visible = false
            });
        }

        private void ProductGrid_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (_productGrid.Columns[e.ColumnIndex].Name != "AddBtn") return;

            var dataRow = _productGrid.Rows[e.RowIndex].DataBoundItem as ProductGridRow;
            if (dataRow == null) return;

            AddProductToCart(dataRow.ProductId, dataRow.Qty);
        }

        private void ProductGrid_CellMouseEnter(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            _productGrid.Cursor = _productGrid.Columns[e.ColumnIndex].Name == "AddBtn"
                ? Cursors.Hand
                : Cursors.Default;
        }

        private void ProductGrid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (_productGrid.Columns[e.ColumnIndex].Name != "Qty") return;

            var cell = _productGrid.Rows[e.RowIndex].Cells["Qty"];
            string raw = Convert.ToString(cell.Value);
            var dataRow = _productGrid.Rows[e.RowIndex].DataBoundItem as ProductGridRow;

            if (dataRow == null) return;

            if (!int.TryParse(raw, out int qty) || qty <= 0)
            {
                cell.Value = 1;
                dataRow.Qty = 1;
                return;
            }

            dataRow.Qty = qty;
        }

        private void ProductGrid_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;

            if (_productGrid.CurrentRow == null) return;
            var dataRow = _productGrid.CurrentRow.DataBoundItem as ProductGridRow;
            if (dataRow == null) return;

            AddProductToCart(dataRow.ProductId, dataRow.Qty);
        }

        private void ProductGrid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (e.RowIndex >= _productGrid.Rows.Count) return;

            string colName = _productGrid.Columns[e.ColumnIndex].Name;

            if (colName == "Qty")
            {
                e.CellStyle.Font = Theme.FontBodyBold;
                e.CellStyle.ForeColor = Theme.Primary;
                e.CellStyle.SelectionForeColor = Theme.Primary;
                return;
            }

            if (colName != "OnHand") return;

            var onHandVal = _productGrid.Rows[e.RowIndex].Cells["OnHand"].Value;
            if (onHandVal == null) return;

            if (int.TryParse(onHandVal.ToString(), out int qty))
            {
                if (qty == 0)
                {
                    e.CellStyle.ForeColor = Theme.Danger;
                    e.CellStyle.SelectionForeColor = Theme.Danger;
                    e.CellStyle.BackColor = Color.FromArgb(255, 240, 240);
                    e.CellStyle.SelectionBackColor = Color.FromArgb(255, 225, 225);
                    e.CellStyle.Font = Theme.FontBodyBold;
                }
                else if (qty <= 5)
                {
                    e.CellStyle.ForeColor = Color.FromArgb(200, 130, 40);
                    e.CellStyle.SelectionForeColor = Color.FromArgb(200, 130, 40);
                    e.CellStyle.Font = Theme.FontBodyBold;
                }
            }
        }

        // ---------------- RIGHT PANE ----------------

        private RoundedPanel BuildCartPane()
        {
            var card = UiFactory.CreateCard();
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(UiFactory.TileGap, 0, 0, 0);

            _bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 452,
                BackColor = Theme.Surface
            };

            // ---- Subtotal / Change (two-line layout) ----
            _lblSubtotalCaption = new Label
            {
                Text = "Subtotal",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            _lblSubtotalValue = new Label
            {
                Text = "₱ 0.00",
                Font = new Font(Theme.UiFontFamily, 20F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            _lblChangeCaption = new Label
            {
                Text = "Change",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent
            };
            _lblChangeValue = new Label
            {
                Text = "₱ 0.00",
                Font = new Font(Theme.UiFontFamily, 20F, FontStyle.Bold),
                ForeColor = Theme.TextMuted,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent
            };

            _lblCustomer = new Label
            {
                Text = "Customer Name (optional)",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _customerNameBox = UiFactory.CreateTextBox(200);
            _customerNameBox.MaxLength = 100;
            _customerNameBox.Placeholder = "Leave blank for walk-in";

            _lblPay = new Label
            {
                Text = "Payment Method",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _lblTendered = new Label
            {
                Text = "Amount Tendered",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _paymentMethodCombo = new RoundedComboBox { Width = 180 };
            _paymentMethodCombo.Items.Add("Cash");
            _paymentMethodCombo.Items.Add("GCash");
            _paymentMethodCombo.SelectedIndex = 0;
            _paymentMethodCombo.SelectedIndexChanged += (s, e) => OnPaymentMethodChanged();

            _tenderedBox = UiFactory.CreateTextBox(180);
            _tenderedBox.MaxLength = 12;
            _tenderedBox.TextChanged += (s, e) => OnTenderedChanged();

            _lblGcashRef = new Label
            {
                Text = "GCash Ref No:",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Visible = false
            };

            _gcashRefBox = UiFactory.CreateTextBox(180);
            _gcashRefBox.MaxLength = 50;
            _gcashRefBox.Placeholder = "e.g. 1002345678901";
            _gcashRefBox.Visible = false;
            _gcashRefBox.TextChanged += (s, e) => RefreshCompleteButtonState(); 

            _statusLabel = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.Danger,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _btnClearCart = UiFactory.CreateButton("Clear Cart", UiFactory.ButtonStyle.Secondary, 140, UiFactory.PrimaryButtonHeight);
            _btnClearCart.Click += (s, e) => ClearCart();

            _btnCompleteSale = UiFactory.CreateButton("Complete Sale", UiFactory.ButtonStyle.Primary, 180, UiFactory.PrimaryButtonHeight);
            _btnCompleteSale.Click += async (s, e) => await CompleteSaleAsync();
            _btnCompleteSale.Enabled = false;

            _bottomPanel.Controls.Add(_lblSubtotalCaption);
            _bottomPanel.Controls.Add(_lblSubtotalValue);
            _bottomPanel.Controls.Add(_lblChangeCaption);
            _bottomPanel.Controls.Add(_lblChangeValue);
            _bottomPanel.Controls.Add(_lblCustomer);
            _bottomPanel.Controls.Add(_customerNameBox);
            _bottomPanel.Controls.Add(_lblPay);
            _bottomPanel.Controls.Add(_lblTendered);
            _bottomPanel.Controls.Add(_paymentMethodCombo);
            _bottomPanel.Controls.Add(_tenderedBox);
            
            _bottomPanel.Controls.Add(_statusLabel);
            _bottomPanel.Controls.Add(_btnClearCart);
            _bottomPanel.Controls.Add(_btnCompleteSale); 
            _bottomPanel.Controls.Add(_lblGcashRef);
            _bottomPanel.Controls.Add(_gcashRefBox);

            _bottomPanel.Resize += (s, e) => LayoutBottomPanel();

            _cartGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 0, 0),
                EditMode = DataGridViewEditMode.EditOnEnter
            };
            UiFactory.StyleGridModern(_cartGrid);
            _cartGrid.ReadOnly = false;
            _cartGrid.GridColor = Theme.PanelBorder;
            _cartGrid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
            _cartGrid.ScrollBars = ScrollBars.Both;
            _cartGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(225, 230, 240);
            _cartGrid.CellPainting += CartGrid_CellPainting;
            _cartGrid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_cartGrid.IsCurrentCellDirty)
                    _cartGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _cartGrid.CellValueChanged += CartGrid_CellValueChanged;
            _cartGrid.CellEndEdit += CartGrid_CellEndEdit;
            PrimeCartColumns();

            var gridDivider = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = Theme.PanelBorder
            };

            card.Controls.Add(_cartGrid);
            card.Controls.Add(gridDivider);
            card.Controls.Add(_bottomPanel);

            _emptyCartOverlay = new Panel
            {
                BackColor = Theme.Surface
            };

            var emptyCartLabel = new Label
            {
                Text = "Your cart is empty.\r\n\r\nPick a product from the list on the left,\r\n" +
                       "type a quantity, then click the cart icon.",
                Font = Theme.FontBody,
                ForeColor = Theme.TextMuted,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(24)
            };
            _emptyCartOverlay.Controls.Add(emptyCartLabel);

            card.Controls.Add(_emptyCartOverlay);

            _cartGrid.Resize += (s, e) => PositionEmptyCartOverlay();
            card.Resize += (s, e) => PositionEmptyCartOverlay();
            card.HandleCreated += (s, e) => PositionEmptyCartOverlay();

            return card;
        }

        private void CartGrid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (_cartGrid.Columns[e.ColumnIndex].Name != "Remove") return;

            e.PaintBackground(e.CellBounds, true);

            using var font = new Font(Theme.IconFontFamily, 14F, FontStyle.Regular);
            TextRenderer.DrawText(
                e.Graphics,
                "\uE711",
                font,
                e.CellBounds,
                Theme.Danger,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix);

            e.Handled = true;
        }

        private void LayoutBottomPanel()
        {
            if (_bottomPanel == null) return;

            int w = _bottomPanel.ClientSize.Width;
            if (w <= 0) return;

            const int pad = UiFactory.CardPadding;   // 24
            const int gap = 16;
            int availW = w - pad * 2;
            int halfW = (availW - gap) / 2;

            int y = 20;

            // Row 1 — Subtotal caption + value (left) · Change caption + value (right)
            // 24px height so descenders (g, p, y) in "Subtotal" and "Change" render fully.
            _lblSubtotalCaption.Location = new Point(pad, y);
            _lblSubtotalCaption.Size = new Size(halfW, 24);

            _lblChangeCaption.Location = new Point(pad + halfW + gap, y);
            _lblChangeCaption.Size = new Size(halfW, 24);
            y += 24;

            // 20F bold needs 44px — 32 was clipping the descenders.
            _lblSubtotalValue.Location = new Point(pad, y);
            _lblSubtotalValue.Size = new Size(halfW, 44);

            _lblChangeValue.Location = new Point(pad + halfW + gap, y);
            _lblChangeValue.Size = new Size(halfW, 44);
            y += 44 + gap;

            // Row 2 — Customer label + field
            _lblCustomer.Location = new Point(pad, y);
            _lblCustomer.Size = new Size(availW, 22);
            y += 22;

            _customerNameBox.Location = new Point(pad, y);
            _customerNameBox.Width = availW;
            y += 44 + gap;

            // Row 3 — Payment + Tendered/GCash labels
            bool isGcash = _paymentMethodCombo.SelectedItem as string == "GCash";

            _lblPay.Location = new Point(pad, y);
            _lblPay.Size = new Size(halfW, 22);

            if (isGcash)
            {
                _lblTendered.Visible = false;

                _lblGcashRef.Visible = true;
                _lblGcashRef.Location = new Point(pad + halfW + gap, y);
                _lblGcashRef.Size = new Size(halfW, 22);
            }
            else
            {
                _lblTendered.Visible = true;
                _lblTendered.Location = new Point(pad + halfW + gap, y);
                _lblTendered.Size = new Size(halfW, 22);

                _lblGcashRef.Visible = false;
            }
            y += 22;

            // Row 4 — Combo + Tendered/GCash box
            _paymentMethodCombo.Location = new Point(pad, y);
            _paymentMethodCombo.Width = halfW;

            if (isGcash)
            {
                _tenderedBox.Visible = false;

                _gcashRefBox.Visible = true;
                _gcashRefBox.Location = new Point(pad + halfW + gap, y);
                _gcashRefBox.Width = halfW;
            }
            else
            {
                _tenderedBox.Visible = true;
                _tenderedBox.Location = new Point(pad + halfW + gap, y);
                _tenderedBox.Width = halfW;

                _gcashRefBox.Visible = false;
            }
            y += 44 + gap;

            // Row 5 — Quick tender buttons (only visible for Cash)
            

            // Row 6 — Status
            _statusLabel.Location = new Point(pad, y);
            _statusLabel.Size = new Size(availW, 24);
            y += 24 + 20;   // extra breathing room before the button row

            // Row 7 — Action buttons
            _btnClearCart.Location = new Point(pad, y);
            _btnCompleteSale.Location = new Point(w - pad - _btnCompleteSale.Width, y);
        }

        private void PositionEmptyCartOverlay()
        {
            if (_emptyCartOverlay == null || _emptyCartOverlay.IsDisposed) return;
            if (_cartGrid == null || _cartGrid.IsDisposed) return;

            _emptyCartOverlay.Bounds = _cartGrid.Bounds;
            _emptyCartOverlay.BringToFront();
        }

        

        private void PrimeCartColumns()
        {
            _cartGrid.AutoGenerateColumns = false;
            _cartGrid.Columns.Clear();

            void AddCol(string prop, string header, int weight, bool readOnly,
                DataGridViewContentAlignment align = DataGridViewContentAlignment.MiddleLeft)
            {
                var col = new DataGridViewTextBoxColumn
                {
                    Name = prop,
                    DataPropertyName = prop,
                    HeaderText = header,
                    FillWeight = weight,
                    ReadOnly = readOnly
                };
                if (align != DataGridViewContentAlignment.MiddleLeft)
                    col.DefaultCellStyle.Alignment = align;
                _cartGrid.Columns.Add(col);
            }

            AddCol("Product", "Item", 220, true);
            AddCol("Quantity", "Qty", 55, false, DataGridViewContentAlignment.MiddleCenter);
            AddCol("UnitPrice", "Price", 85, true, DataGridViewContentAlignment.MiddleRight);
            AddCol("LineTotal", "Total", 95, true, DataGridViewContentAlignment.MiddleRight);
            AddCol("Remove", "", 55, true, DataGridViewContentAlignment.MiddleCenter);

            // Style the Remove column — red ×, no padding, larger font
            var removeCol = _cartGrid.Columns["Remove"];
            removeCol.DefaultCellStyle.Padding = new Padding(0);

            _cartGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ProductId",
                DataPropertyName = "ProductId",
                Visible = false
            });

            _cartGrid.CellClick += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (_cartGrid.Columns[e.ColumnIndex].Name != "Remove") return;

                var idCell = _cartGrid.Rows[e.RowIndex].Cells["ProductId"];
                if (idCell?.Value == null) return;

                int id = Convert.ToInt32(idCell.Value);
                var line = _cart.FirstOrDefault(c => c.ProductId == id);
                if (line != null)
                {
                    _cart.Remove(line);
                    RefreshCartGrid();
                    ShowInfo("Line removed.");
                }
            };

            _cartGrid.CellMouseEnter += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                _cartGrid.Cursor = _cartGrid.Columns[e.ColumnIndex].Name == "Remove"
                    ? Cursors.Hand
                    : Cursors.Default;
            };
            _cartGrid.CellMouseLeave += (s, e) => _cartGrid.Cursor = Cursors.Default;
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
                .Select(p => new ProductGridRow
                {
                    ProductId = p.ProductId,
                    Product = p.ProductName,
                    Brand = p.Brand,
                    Unit = p.Unit,
                    Price = p.SellingPrice.ToString("N2"),
                    OnHand = p.QuantityOnHand,
                    Qty = 1
                })
                .ToList();

            _productGrid.DataSource = rows;

            if (_productGrid.Columns.Contains("ProductId"))
                _productGrid.Columns["ProductId"].Visible = false;

            if (_productGrid.Rows.Count > 0)
                _productGrid.ClearSelection();

            _productCountLabel.Text =
                $"Showing {rows.Count} of {_allProducts.Count} product(s)";
        }

        // ============================================================
        // CART OPERATIONS
        // ============================================================

        private void AddProductToCart(int productId, int qty)
        {
            var p = _allProducts.FirstOrDefault(x => x.ProductId == productId);
            if (p == null)
            {
                ShowError("Product no longer available.");
                return;
            }

            if (qty <= 0)
            {
                ShowErrorBox("Quantity must be a whole number greater than zero.");
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
            ShowInfo($"Added {qty} × {p.ProductName} to cart.");
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

            if (_emptyCartOverlay != null && !_emptyCartOverlay.IsDisposed)
            {
                _emptyCartOverlay.Visible = _cart.Count == 0;
                if (_emptyCartOverlay.Visible)
                    PositionEmptyCartOverlay();
            }

            UpdateCartTotals();
        }

        private void UpdateCartTotals()
        {
            decimal subtotal = _cart.Sum(c => c.LineTotal);
            _lblSubtotalValue.Text = "₱ " + subtotal.ToString("N2");
            RecalculateChange(subtotal);
        }

        private void RecalculateChange(decimal subtotal)
        {
            string method = _paymentMethodCombo?.SelectedItem as string ?? "Cash";

            if (method == "GCash")
            {
                _lblChangeValue.ForeColor = Theme.TextPrimary;
                _lblChangeValue.Text = "₱ 0.00";
            }
            else if (string.IsNullOrWhiteSpace(_tenderedBox?.Text))
            {
                _lblChangeValue.ForeColor = Theme.TextMuted;
                _lblChangeValue.Text = "₱ 0.00";
            }
            else if (!decimal.TryParse(_tenderedBox.Text.Trim(),
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out decimal tendered))
            {
                _lblChangeValue.ForeColor = Theme.Danger;
                _lblChangeValue.Text = "Invalid amount";
            }
            else if (tendered < subtotal)
            {
                decimal shortfall = subtotal - tendered;
                _lblChangeValue.ForeColor = Theme.Danger;
                _lblChangeValue.Text = $"₱ {shortfall:N2} short";
            }
            else
            {
                decimal change = tendered - subtotal;
                _lblChangeValue.ForeColor = Theme.Success;
                _lblChangeValue.Text = "₱ " + change.ToString("N2");
            }

            RefreshCompleteButtonState();
        }

        private void RefreshCompleteButtonState()
        {
            if (_btnCompleteSale == null) return;

            // 1. Must have items in the cart
            if (_isBusy || _cart == null || _cart.Count == 0)
            {
                _btnCompleteSale.Enabled = false;
                return;
            }

            string method = _paymentMethodCombo?.SelectedItem as string ?? "Cash";

            // 2. GCash Logic
            if (method == "GCash")
            {
                string gcashRef = (_gcashRefBox?.Text ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(gcashRef))
                {
                    _btnCompleteSale.Enabled = false;
                    return;
                }

                _btnCompleteSale.Enabled = true;
                return;
            }

            // 3. Cash Logic
            if (string.IsNullOrWhiteSpace(_tenderedBox?.Text))
            {
                _btnCompleteSale.Enabled = false;
                return;
            }

            if (!decimal.TryParse(_tenderedBox.Text.Trim(),
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out decimal tendered))
            {
                _btnCompleteSale.Enabled = false;
                return;
            }

            decimal subtotal = _cart.Sum(c => c.LineTotal);
            _btnCompleteSale.Enabled = tendered >= subtotal;
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
                bool isGcash = method == "GCash";

                if (isGcash)
                {
                    _tenderedBox.Text = string.Empty; // No longer auto-fills
                    _tenderedBox.Enabled = false;
                    _gcashRefBox.Text = string.Empty;
                }
                else
                {
                    _tenderedBox.Text = string.Empty;
                    _tenderedBox.Enabled = true;
                    _gcashRefBox.Text = string.Empty;
                }

                _lblGcashRef.Visible = isGcash;
                _gcashRefBox.Visible = isGcash;
                _tenderedBox.Visible = !isGcash;

                RecalculateChange(subtotal);
                LayoutBottomPanel();
            }
            finally
            {
                _isUpdatingPayment = false;
                LayoutBottomPanel();
                RefreshCompleteButtonState();
            }
        }

        private void OnTenderedChanged()
        {
            if (_isUpdatingPayment) return;

            // Recalculate change and explicitly refresh the button state
            RecalculateChange(_cart.Sum(c => c.LineTotal));
            RefreshCompleteButtonState();
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
                var (success, error, saleId, invoiceNo) = await _salesService.CompleteSaleAsync(
                    requestLines, method, tendered, _customerNameBox.Text, _gcashRefBox.Text);

                if (!success)
                {
                    ShowErrorBox(error);
                    return;
                }

                _cart.Clear();
                _tenderedBox.Text = string.Empty;
                _customerNameBox.Text = string.Empty;
                _gcashRefBox.Text = string.Empty;
                _paymentMethodCombo.SelectedIndex = 0;
                RefreshCartGrid();
                ShowInfo($"Invoice {invoiceNo} saved.");

                using var receipt = new ReceiptForm(saleId);
                receipt.ShowDialog(this);

                await LoadLookupsAsync();
                _searchBox.FocusInput();
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            _btnClearCart.Enabled = !busy;
            _searchBox.Enabled = !busy;
            _categoryFilter.Enabled = !busy;
            _customerNameBox.Enabled = !busy;
            _paymentMethodCombo.Enabled = !busy;
            _productGrid.Enabled = !busy;
            _cartGrid.Enabled = !busy;

            RefreshCompleteButtonState();
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