using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TodangMotor.Common;
using TodangMotor.Forms;
using TodangMotor.Models;
using TodangMotor.Services;

namespace TodangMotor.Controls
{
    /// <summary>
    /// Purchase Orders module — two sub-tabs:
    ///   1. Build PO   — supplier + order date + line items → save & export PDF
    ///   2. PO History — searchable list of past POs → view / re-export PDF
    /// Owner-only.
    /// </summary>
    public class PurchaseOrdersControl : UserControl
    {
        private readonly PurchaseOrderService _poService = new();

        private List<Supplier> _suppliers = new();
        private List<Product> _products = new();

        private readonly Dictionary<string, Supplier> _supplierByDisplay = new();
        private readonly Dictionary<string, Product> _productByDisplay = new();

        private readonly List<WorkingLine> _lines = new();

        private bool _isLoaded;
        private bool _isBusy;
        private bool _suppressSupplierChange;
        private bool _historyLoaded;

        private const string AllSuppliersDisplay = "(All Suppliers)";

        // ---- Fixed column widths (Product is dynamic) ----
        // Build grid
        private const int ColBBrandW = 150;
        private const int ColBUnitW = 80;
        private const int ColBQtyW = 75;
        private const int ColBRemoveW = 55;

        // History grid
        private const int ColHPONumW = 220;
        private const int ColHDateW = 180;
        private const int ColHItemsW = 100;
        // Supplier column is dynamic

        private class WorkingLine
        {
            public int ProductId { get; set; }
            public string ProductName { get; set; } = string.Empty;
            public string Brand { get; set; } = string.Empty;
            public string Unit { get; set; } = string.Empty;
            public int Quantity { get; set; }
        }

        // ---- UI controls ----
        private TabControl _subTabs;
        private TabPage _buildPage;
        private TabPage _historyPage;

        // ---- Build PO page ----
        private RoundedComboBox _cmbSupplier;
        private DateTimePicker _dtpOrderDate;
        private RoundedTextBox _txtNotes;

        private RoundedComboBox _cmbProduct;
        private RoundedTextBox _txtQty;
        private Button _btnAddLine;

        private DataGridView _buildGrid;
        private Label _buildTotals;

        private Button _btnClearForm;
        private Button _btnSaveAndPdf;
        private Label _buildStatus;

        // ---- History page ----
        private RoundedTextBox _historySearch;
        private DataGridView _historyGrid;
        private Button _btnExportPdf;
        private Button _btnRefresh;
        private Label _historyStatus;
        private Label _historyCount;

        private List<PurchaseOrder> _historyOrders = new();

        public PurchaseOrdersControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(UiFactory.PagePadding);

            BuildLayout();

            Load += PurchaseOrdersControl_Load;
        }

        private async void PurchaseOrdersControl_Load(object sender, EventArgs e)
        {
            if (_isLoaded) return;
            _isLoaded = true;
            await LoadBuildLookupsAsync();
        }

        // ============================================================
        // LAYOUT
        // ============================================================

        private void BuildLayout()
        {
            _subTabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font(Theme.UiFontFamily, 11F, FontStyle.Regular),
                ItemSize = new Size(200, 42),
                SizeMode = TabSizeMode.Fixed,
                Appearance = TabAppearance.Normal,
                Padding = new Point(20, 8)
            };

            _buildPage = new TabPage("Build PO")
            {
                BackColor = Theme.Background,
                Padding = Padding.Empty
            };
            _historyPage = new TabPage("PO History")
            {
                BackColor = Theme.Background,
                Padding = Padding.Empty
            };

            BuildBuildPage();
            BuildHistoryPage();

            _subTabs.TabPages.Add(_buildPage);
            _subTabs.TabPages.Add(_historyPage);

            _subTabs.SelectedIndexChanged += async (s, e) =>
            {
                if (_subTabs.SelectedTab == _historyPage && !_historyLoaded)
                {
                    _historyLoaded = true;
                    await LoadHistoryAsync();
                }
            };

            Controls.Add(_subTabs);
        }

        // ============================================================
        // BUILD PO PAGE
        // ============================================================

        private void BuildBuildPage()
        {
            // ---- Header block: supplier + order date + notes ----
            var headerBlock = new Panel
            {
                Dock = DockStyle.Top,
                Height = 190,
                BackColor = Theme.Background
            };

            headerBlock.Controls.Add(MakeSectionTitle("ORDER DETAILS", 0, 8));

            int y = 48;
            const int labelW = 130;
            const int fieldX = 140;

            headerBlock.Controls.Add(MakeRowLabel("Supplier:", 0, y, labelW));

            _cmbSupplier = new RoundedComboBox
            {
                Width = 340,
                Location = new Point(fieldX, y)
            };
            _cmbSupplier.SelectedIndexChanged += async (s, e) => await OnSupplierChangedAsync();
            headerBlock.Controls.Add(_cmbSupplier);

            var lblDate = MakeRowLabel("Order Date:", 0, y, 130);
            headerBlock.Controls.Add(lblDate);

            _dtpOrderDate = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "MMMM d, yyyy",
                Font = Theme.FontBody,
                Size = new Size(220, 32),
                Location = new Point(0, y + 6),
                Value = DateTime.Today
            };
            headerBlock.Controls.Add(_dtpOrderDate);

            y += 64;

            headerBlock.Controls.Add(MakeRowLabel("Notes:", 0, y, labelW));

            _txtNotes = UiFactory.CreateTextBox(400);
            _txtNotes.Location = new Point(fieldX, y);
            _txtNotes.Placeholder = "Optional — e.g. urgent restock";
            _txtNotes.MaxLength = 500;
            headerBlock.Controls.Add(_txtNotes);

            headerBlock.Resize += (s, e) =>
            {
                int right = headerBlock.ClientSize.Width;
                _txtNotes.Width = Math.Max(200, right - fieldX);

                int dateX = right - _dtpOrderDate.Width;
                _dtpOrderDate.Left = dateX;
                lblDate.Left = dateX - lblDate.Width - 12;
            };

            // ---- Builder row: product + qty + add ----
            var builderRow = new Panel
            {
                Dock = DockStyle.Top,
                Height = 190,
                BackColor = Theme.Background
            };

            builderRow.Controls.Add(MakeSectionTitle("ADD LINE ITEM", 0, 8));

            int by = 48;

            builderRow.Controls.Add(MakeRowLabel("Product:", 0, by, labelW));

            _cmbProduct = new RoundedComboBox
            {
                Width = 400,
                Location = new Point(fieldX, by)
            };
            builderRow.Controls.Add(_cmbProduct);

            _btnAddLine = UiFactory.CreateButton("+ Add Line", UiFactory.ButtonStyle.Primary, 170, UiFactory.PrimaryButtonHeight);
            _btnAddLine.Location = new Point(0, by);
            _btnAddLine.Click += (s, e) => AddLine();
            builderRow.Controls.Add(_btnAddLine);

            by += 64;

            builderRow.Controls.Add(MakeRowLabel("Quantity:", 0, by, labelW));

            _txtQty = UiFactory.CreateTextBox(120);
            _txtQty.Location = new Point(fieldX, by);
            _txtQty.MaxLength = 8;
            _txtQty.Text = "1";
            builderRow.Controls.Add(_txtQty);

            builderRow.Resize += (s, e) =>
            {
                int right = builderRow.ClientSize.Width;

                _btnAddLine.Left = right - _btnAddLine.Width;
                _btnAddLine.Top = 48;

                _cmbProduct.Width = Math.Max(280, right - fieldX - _btnAddLine.Width - 16);
            };

            // ---- Grid block ----
            var gridBlock = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Background
            };

            var topRow = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = Theme.Background
            };

            topRow.Controls.Add(MakeSectionTitle("LINE ITEMS", 0, 14));

            _buildTotals = new Label
            {
                Text = "Total items: 0   ·   Total units: 0",
                Font = Theme.FontBodyBold,
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(0, 14),
                Height = 24,
                Width = 400,
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent
            };
            topRow.Controls.Add(_buildTotals);

            topRow.Resize += (s, e) =>
            {
                _buildTotals.Left = topRow.ClientSize.Width - _buildTotals.Width;
            };

            var gridWrapper = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 8, 0, 8),
                BackColor = Theme.Background
            };

            _buildGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };
            UiFactory.StyleGridModern(_buildGrid);
            _buildGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            _buildGrid.CellClick += BuildGrid_CellClick;
            _buildGrid.CellMouseEnter += BuildGrid_CellMouseEnter;
            _buildGrid.CellPainting += BuildGrid_CellPainting;
            _buildGrid.CellMouseLeave += (s, e) => _buildGrid.Cursor = Cursors.Default;
            _buildGrid.CellPainting += BuildGrid_CellPainting;
            PrimeBuildGridColumns();

            _buildGrid.Resize += (s, e) => FitBuildProductColumn();

            gridWrapper.Controls.Add(_buildGrid);

            gridBlock.Controls.Add(gridWrapper);
            gridBlock.Controls.Add(topRow);

            // ---- Bottom bar ----
            var bottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 88,
                BackColor = Theme.Background
            };

            _btnClearForm = UiFactory.CreateButton("Clear Form", UiFactory.ButtonStyle.Secondary, 150, UiFactory.PrimaryButtonHeight);
            _btnClearForm.Location = new Point(0, 20);
            _btnClearForm.Click += (s, e) => ClearForm();
            bottomBar.Controls.Add(_btnClearForm);

            _buildStatus = new Label
            {
                Font = Theme.FontSmall,
                ForeColor = Theme.Danger,
                AutoSize = false,
                Location = new Point(_btnClearForm.Right + 24, 20),
                Height = UiFactory.PrimaryButtonHeight,
                Width = 300,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            bottomBar.Controls.Add(_buildStatus);

            _btnSaveAndPdf = UiFactory.CreateButton(
                "Save PO & Generate PDF",
                UiFactory.ButtonStyle.Primary,
                220, UiFactory.PrimaryButtonHeight);
            _btnSaveAndPdf.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnSaveAndPdf.Click += async (s, e) => await SaveAndExportAsync();
            bottomBar.Controls.Add(_btnSaveAndPdf);

            bottomBar.Resize += (s, e) =>
            {
                _btnSaveAndPdf.Left = bottomBar.ClientSize.Width - _btnSaveAndPdf.Width;
                _buildStatus.Width = Math.Max(100,
                    _btnSaveAndPdf.Left - _buildStatus.Left - 16);
            };

            _buildPage.Controls.Add(gridBlock);
            _buildPage.Controls.Add(bottomBar);
            _buildPage.Controls.Add(builderRow);
            _buildPage.Controls.Add(headerBlock);
        }

        private void BuildGrid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (_buildGrid.Columns[e.ColumnIndex].Name != "Remove") return;

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

        private void PrimeBuildGridColumns()
        {
            _buildGrid.AutoGenerateColumns = false;
            _buildGrid.Columns.Clear();
            _buildGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

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
                _buildGrid.Columns.Add(col);
            }

            AddTextCol("Product", "Product", 400, true);
            AddTextCol("Brand", "Brand", ColBBrandW, true);
            AddTextCol("Unit", "Unit", ColBUnitW, true, DataGridViewContentAlignment.MiddleCenter);
            AddTextCol("Quantity", "Qty", ColBQtyW, true, DataGridViewContentAlignment.MiddleRight);

            // Remove — × per row
            var removeCol = new DataGridViewTextBoxColumn
            {
                Name = "Remove",
                HeaderText = "",
                Width = ColBRemoveW,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                Resizable = DataGridViewTriState.False,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Padding = new Padding(0)
                }
            };
            _buildGrid.Columns.Add(removeCol);

            _buildGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ProductId",
                DataPropertyName = "ProductId",
                Visible = false
            });
        }

        private void FitBuildProductColumn()
        {
            if (_buildGrid == null || !_buildGrid.Columns.Contains("Product")) return;

            int others = ColBBrandW + ColBUnitW + ColBQtyW + ColBRemoveW;
            int available = _buildGrid.ClientSize.Width - others - 24;
            if (available < 220) available = 220;

            _buildGrid.Columns["Product"].Width = available;
        }

        private void BuildGrid_CellMouseEnter(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            _buildGrid.Cursor = _buildGrid.Columns[e.ColumnIndex].Name == "Remove"
                ? Cursors.Hand
                : Cursors.Default;
        }

        private void BuildGrid_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (_buildGrid.Columns[e.ColumnIndex].Name != "Remove") return;

            var idCell = _buildGrid.Rows[e.RowIndex].Cells["ProductId"];
            if (idCell?.Value == null) return;

            int id = Convert.ToInt32(idCell.Value);
            var line = _lines.FirstOrDefault(l => l.ProductId == id);
            if (line != null)
            {
                _lines.Remove(line);
                RefreshBuildGrid();
                ShowBuildStatus("Line removed.", false);
            }
        }

        // ============================================================
        // PO HISTORY PAGE
        // ============================================================

        private void BuildHistoryPage()
        {
            var bar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 108,
                BackColor = Theme.Background
            };

            _historySearch = UiFactory.CreateTextBox(340);
            _historySearch.Location = new Point(0, 16);
            _historySearch.Placeholder = "Search PO number or supplier…";
            _historySearch.MaxLength = 150;
            _historySearch.TextChanged += (s, e) => ApplyHistoryFilter();

            _historyCount = new Label
            {
                Text = "Loading…",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(_historySearch.Right + 24, 26),
                Height = 24,
                Width = 500,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            bar.Controls.Add(_historySearch);
            bar.Controls.Add(_historyCount);

            // Grid
            _historyGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 0, 0)
            };
            UiFactory.StyleGridModern(_historyGrid);
            _historyGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            _historyGrid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                OpenDetailForSelected();
            };
            _historyGrid.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    OpenDetailForSelected();
                }
            };
            PrimeHistoryGridColumns();

            _historyGrid.Resize += (s, e) => FitHistorySupplierColumn();

            // Bottom bar
            var bottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 88,
                BackColor = Theme.Background
            };

            _btnExportPdf = UiFactory.CreateButton("Export PDF", UiFactory.ButtonStyle.Primary, 150, UiFactory.PrimaryButtonHeight);
            _btnExportPdf.Location = new Point(0, 20);
            _btnExportPdf.Click += async (s, e) => await ExportSelectedAsync();

            _historyStatus = new Label
            {
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(_btnExportPdf.Right + 24, 20),
                Height = UiFactory.PrimaryButtonHeight,
                Width = 300,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            _btnRefresh = UiFactory.CreateButton("Refresh", UiFactory.ButtonStyle.Secondary, 140, UiFactory.PrimaryButtonHeight);
            _btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnRefresh.Click += async (s, e) => await LoadHistoryAsync();

            bottomBar.Controls.Add(_btnExportPdf);
            bottomBar.Controls.Add(_historyStatus);
            bottomBar.Controls.Add(_btnRefresh);

            bottomBar.Resize += (s, e) =>
            {
                _btnRefresh.Left = bottomBar.ClientSize.Width - _btnRefresh.Width;
                _historyStatus.Width = Math.Max(100,
                    _btnRefresh.Left - _historyStatus.Left - 16);
            };

            _historyPage.Controls.Add(_historyGrid);
            _historyPage.Controls.Add(bottomBar);
            _historyPage.Controls.Add(bar);
        }

        private void PrimeHistoryGridColumns()
        {
            _historyGrid.AutoGenerateColumns = false;
            _historyGrid.Columns.Clear();
            _historyGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

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
                _historyGrid.Columns.Add(col);
            }

            AddTextCol("PONumber", "PO Number", ColHPONumW, true);
            AddTextCol("OrderDate", "Order Date", ColHDateW, true);
            AddTextCol("SupplierName", "Supplier", 400, true);
            AddTextCol("ItemCount", "Items", ColHItemsW, true, DataGridViewContentAlignment.MiddleRight);

            _historyGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "PurchaseOrderId",
                DataPropertyName = "PurchaseOrderId",
                Visible = false
            });
        }

        private void FitHistorySupplierColumn()
        {
            if (_historyGrid == null || !_historyGrid.Columns.Contains("SupplierName")) return;

            int others = ColHPONumW + ColHDateW + ColHItemsW;
            int available = _historyGrid.ClientSize.Width - others - 24;
            if (available < 200) available = 200;

            _historyGrid.Columns["SupplierName"].Width = available;
        }

        // ============================================================
        // SMALL HELPERS
        // ============================================================

        private Label MakeSectionTitle(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Font = new Font(Theme.UiFontFamily, 10F, FontStyle.Bold),
                ForeColor = Theme.Primary,
                AutoSize = true,
                Location = new Point(x, y),
                BackColor = Color.Transparent
            };
        }

        private Label MakeRowLabel(string text, int x, int y, int width)
        {
            return new Label
            {
                Text = text,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(width, UiFactory.InputHeight),
                Location = new Point(x, y),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
        }

        // ============================================================
        // LOOKUPS
        // ============================================================

        private async Task LoadBuildLookupsAsync()
        {
            SetBuildBusy(true);
            try
            {
                _suppliers = await _poService.GetActiveSuppliersAsync();

                _supplierByDisplay.Clear();
                _cmbSupplier.Items.Clear();
                _cmbSupplier.Items.Add(AllSuppliersDisplay);

                foreach (var s in _suppliers)
                {
                    _supplierByDisplay[s.SupplierName] = s;
                    _cmbSupplier.Items.Add(s.SupplierName);
                }

                _suppressSupplierChange = true;
                if (_cmbSupplier.Items.Count > 0)
                    _cmbSupplier.SelectedIndex = 0;
                _suppressSupplierChange = false;

                await ReloadProductsForCurrentSupplierAsync();

                if (_suppliers.Count == 0)
                    ShowBuildStatus("No active suppliers available. Add or reactivate a supplier first.", true);
                else if (_products.Count == 0)
                    ShowBuildStatus("No active products available for this supplier.", true);

                RefreshBuildGrid();
            }
            finally
            {
                SetBuildBusy(false);
            }
        }

        private async Task OnSupplierChangedAsync()
        {
            if (_suppressSupplierChange) return;
            await ReloadProductsForCurrentSupplierAsync();
        }

        private async Task ReloadProductsForCurrentSupplierAsync()
        {
            int supplierId = GetSelectedSupplierId();
            _products = await _poService.GetProductsBySupplierAsync(supplierId);

            _productByDisplay.Clear();
            _cmbProduct.Items.Clear();

            foreach (var p in _products)
            {
                string display = $"{p.ProductName} — {p.Brand}";
                _productByDisplay[display] = p;
                _cmbProduct.Items.Add(display);
            }

            if (_cmbProduct.Items.Count > 0)
                _cmbProduct.SelectedIndex = 0;

            _txtQty.Text = "1";
        }

        private int GetSelectedSupplierId()
        {
            var name = _cmbSupplier.SelectedItem as string;
            if (string.IsNullOrEmpty(name)) return 0;
            if (name == AllSuppliersDisplay) return 0;

            return _supplierByDisplay.TryGetValue(name, out var s) ? s.SupplierId : 0;
        }

        // ============================================================
        // BUILD — LINE MANAGEMENT
        // ============================================================

        private Product? GetSelectedProduct()
        {
            var display = _cmbProduct.SelectedItem as string;
            if (string.IsNullOrEmpty(display)) return null;
            return _productByDisplay.TryGetValue(display, out var p) ? p : null;
        }

        private void AddLine()
        {
            var product = GetSelectedProduct();
            if (product == null)
            {
                ShowBuildStatus("Please select a product.", true);
                return;
            }

            if (!int.TryParse((_txtQty.Text ?? string.Empty).Trim(), out int qty) || qty <= 0)
            {
                ShowBuildStatus("Quantity must be a whole number greater than zero.", true);
                _txtQty.FocusInput();
                return;
            }

            if (_lines.Any(l => l.ProductId == product.ProductId))
            {
                ShowBuildStatus($"\"{product.ProductName}\" is already in this PO. Remove it first to change qty.", true);
                return;
            }

            _lines.Add(new WorkingLine
            {
                ProductId = product.ProductId,
                ProductName = product.ProductName ?? string.Empty,
                Brand = product.Brand ?? string.Empty,
                Unit = product.Unit ?? string.Empty,
                Quantity = qty
            });

            RefreshBuildGrid();
            _txtQty.Text = "1";
            _cmbProduct.Focus();
            ShowBuildStatus("Line added.", false);
        }

        private void ClearForm()
        {
            if (_lines.Count > 0)
            {
                var confirm = MessageBox.Show(
                    "Discard this PO and clear the form?",
                    "Confirm Clear",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes) return;
            }

            _lines.Clear();
            _txtNotes.Text = string.Empty;
            _dtpOrderDate.Value = DateTime.Today;
            _txtQty.Text = "1";

            _suppressSupplierChange = true;
            if (_cmbSupplier.Items.Count > 0)
                _cmbSupplier.SelectedIndex = 0;
            _suppressSupplierChange = false;

            _ = ReloadProductsForCurrentSupplierAsync();

            RefreshBuildGrid();
            ShowBuildStatus("Form cleared.", false);
        }

        private void RefreshBuildGrid()
        {
            var rows = _lines
                .Select(l => new
                {
                    ProductId = l.ProductId,
                    Product = l.ProductName,
                    Brand = l.Brand,
                    Unit = l.Unit,
                    Quantity = l.Quantity
                })
                .ToList();

            _buildGrid.DataSource = rows;

            if (_buildGrid.Rows.Count > 0)
                _buildGrid.ClearSelection();

            int totalQty = _lines.Sum(l => l.Quantity);
            _buildTotals.Text = $"Total items: {_lines.Count}   ·   Total units: {totalQty}";

            FitBuildProductColumn();
        }

        // ============================================================
        // BUILD — SAVE + EXPORT
        // ============================================================

        private async Task SaveAndExportAsync()
        {
            if (_isBusy) return;

            if (_lines.Count == 0)
            {
                ShowBuildStatus("Add at least one line item first.", true);
                return;
            }

            int supplierId = GetSelectedSupplierId();
            if (supplierId <= 0)
            {
                ShowBuildStatus("Please select a specific supplier (not \"All Suppliers\").", true);
                return;
            }

            var requestLines = _lines
                .Select(l => new PurchaseOrderRequestLine
                {
                    ProductId = l.ProductId,
                    Quantity = l.Quantity
                })
                .ToList();

            SetBuildBusy(true);
            try
            {
                var (success, error, poNumber) = await _poService.SavePurchaseOrderAsync(
                    supplierId,
                    _dtpOrderDate.Value,
                    _txtNotes.Text,
                    requestLines);

                if (!success)
                {
                    ShowBuildStatus(error, true);
                    return;
                }

                var (detailOk, detailErr, detailList) = await _poService.GetAllAsync(poNumber);
                if (!detailOk || detailList.Count == 0)
                {
                    ShowBuildStatus($"PO {poNumber} saved but could not open for PDF.", true);
                    await ReloadProductsForCurrentSupplierAsync();
                    RefreshBuildGrid();
                    return;
                }

                var poHeader = detailList.FirstOrDefault(o => o.PONumber == poNumber);
                if (poHeader == null)
                {
                    ShowBuildStatus($"PO {poNumber} saved.", false);
                    _lines.Clear();
                    RefreshBuildGrid();
                    return;
                }

                var (detOk, detErr, detail) = await _poService.GetDetailAsync(poHeader.PurchaseOrderId);
                if (!detOk || detail == null)
                {
                    ShowBuildStatus($"PO {poNumber} saved but could not build PDF.", true);
                    return;
                }

                using var sfd = new SaveFileDialog
                {
                    Filter = "PDF files (*.pdf)|*.pdf",
                    DefaultExt = ".pdf",
                    AddExtension = true,
                    FileName = $"{poNumber}.pdf",
                    InitialDirectory = GetDefaultReportFolder()
                };

                if (sfd.ShowDialog(FindForm()) == DialogResult.OK)
                {
                    var table = PurchaseOrderDetailForm.BuildReportTable(detail);
                    string path = sfd.FileName;
                    await Task.Run(() => ReportService.ExportPdf(table, path));

                    var openIt = MessageBox.Show(
                        $"PO {poNumber} saved.\n\nPDF saved to:\n{path}\n\nOpen the folder?",
                        "Success",
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
                else
                {
                    MessageBox.Show(
                        $"PO {poNumber} saved without PDF export.",
                        "Saved",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

                _lines.Clear();
                _txtNotes.Text = string.Empty;
                _dtpOrderDate.Value = DateTime.Today;
                _txtQty.Text = "1";
                RefreshBuildGrid();

                _suppressSupplierChange = true;
                if (_cmbSupplier.Items.Count > 0)
                    _cmbSupplier.SelectedIndex = 0;
                _suppressSupplierChange = false;
                await ReloadProductsForCurrentSupplierAsync();

                _historyLoaded = false;
                ShowBuildStatus($"PO {poNumber} saved.", false);
            }
            finally
            {
                SetBuildBusy(false);
            }
        }

        // ============================================================
        // HISTORY — LOAD + FILTER
        // ============================================================

        private async Task LoadHistoryAsync()
        {
            try
            {
                var (success, error, orders) = await _poService.GetAllAsync();
                if (!success)
                {
                    _historyGrid.DataSource = null;
                    _historyCount.Text = error;
                    _historyCount.ForeColor = Theme.Danger;
                    return;
                }

                _historyOrders = orders ?? new List<PurchaseOrder>();
                ApplyHistoryFilter();
            }
            catch (Exception ex)
            {
                _historyCount.Text = $"Could not load POs: {ex.Message}";
                _historyCount.ForeColor = Theme.Danger;
            }
        }

        private void ApplyHistoryFilter()
        {
            IEnumerable<PurchaseOrder> filtered = _historyOrders;

            string search = (_historySearch?.Text ?? string.Empty).Trim();
            if (search.Length > 0)
            {
                filtered = filtered.Where(o =>
                    (!string.IsNullOrEmpty(o.PONumber) &&
                     o.PONumber.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(o.SupplierName) &&
                     o.SupplierName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            var rows = filtered
                .OrderByDescending(o => o.OrderDate)
                .ThenByDescending(o => o.PurchaseOrderId)
                .Select(o => new
                {
                    o.PurchaseOrderId,
                    PONumber = o.PONumber ?? string.Empty,
                    OrderDate = o.OrderDate.ToString("MMM d, yyyy"),
                    SupplierName = o.SupplierName ?? "—",
                    ItemCount = o.ItemCount
                })
                .ToList();

            _historyGrid.DataSource = rows;

            if (_historyGrid.Columns.Contains("PurchaseOrderId"))
                _historyGrid.Columns["PurchaseOrderId"].Visible = false;

            if (_historyGrid.Rows.Count > 0)
                _historyGrid.ClearSelection();

            _historyCount.ForeColor = Theme.TextSecondary;
            _historyCount.Text = $"Showing {rows.Count} Purchase Order(s)";

            FitHistorySupplierColumn();
        }

        // ============================================================
        // HISTORY — ACTIONS
        // ============================================================

        private PurchaseOrder? GetSelectedHistoryOrder()
        {
            if (_historyGrid == null || _historyGrid.SelectedRows.Count == 0) return null;
            var row = _historyGrid.SelectedRows[0];
            if (!_historyGrid.Columns.Contains("PurchaseOrderId")) return null;

            var val = row.Cells["PurchaseOrderId"].Value;
            if (val == null) return null;

            int id = Convert.ToInt32(val);
            return _historyOrders.FirstOrDefault(o => o.PurchaseOrderId == id);
        }

        private void OpenDetailForSelected()
        {
            var order = GetSelectedHistoryOrder();
            if (order == null)
            {
                ShowHistoryStatus("Please select a Purchase Order first.", true);
                return;
            }

            try
            {
                var parent = FindForm();
                using var form = new PurchaseOrderDetailForm(order.PurchaseOrderId);
                form.ShowDialog(parent);

                _ = LoadHistoryAsync();
            }
            catch (Exception ex)
            {
                ShowHistoryStatus($"Could not open PO detail: {ex.Message}", true);
            }
        }

        private async Task ExportSelectedAsync()
        {
            var order = GetSelectedHistoryOrder();
            if (order == null)
            {
                ShowHistoryStatus("Please select a Purchase Order first.", true);
                return;
            }

            try
            {
                var (success, error, detail) = await _poService.GetDetailAsync(order.PurchaseOrderId);
                if (!success || detail == null)
                {
                    ShowHistoryStatus(error ?? "Could not load PO.", true);
                    return;
                }

                using var sfd = new SaveFileDialog
                {
                    Filter = "PDF files (*.pdf)|*.pdf",
                    DefaultExt = ".pdf",
                    AddExtension = true,
                    FileName = $"{detail.Order.PONumber}.pdf",
                    InitialDirectory = GetDefaultReportFolder()
                };

                if (sfd.ShowDialog(FindForm()) != DialogResult.OK) return;

                string path = sfd.FileName;
                var table = PurchaseOrderDetailForm.BuildReportTable(detail);
                await Task.Run(() => ReportService.ExportPdf(table, path));

                var openIt = MessageBox.Show(
                    $"PDF saved to:\n{path}\n\nOpen the folder?",
                    "PDF Saved",
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

                ShowHistoryStatus($"PDF saved: {Path.GetFileName(path)}", false);
            }
            catch (Exception ex)
            {
                ShowHistoryStatus($"Could not export PDF: {ex.Message}", true);
            }
        }

        // ============================================================
        // HELPERS
        // ============================================================

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

        private void SetBuildBusy(bool busy)
        {
            _isBusy = busy;
            _btnSaveAndPdf.Enabled = !busy;
            _btnAddLine.Enabled = !busy;
            _btnClearForm.Enabled = !busy;
            _cmbSupplier.Enabled = !busy;
            _cmbProduct.Enabled = !busy;
            _txtQty.Enabled = !busy;
            _txtNotes.Enabled = !busy;
            _dtpOrderDate.Enabled = !busy;
        }

        private void ShowBuildStatus(string message, bool isError)
        {
            if (_buildStatus == null) return;
            _buildStatus.ForeColor = isError ? Theme.Danger : Theme.Success;
            _buildStatus.Text = message ?? string.Empty;
        }

        private void ShowHistoryStatus(string message, bool isError)
        {
            if (_historyStatus == null) return;
            _historyStatus.ForeColor = isError ? Theme.Danger : Theme.Success;
            _historyStatus.Text = message ?? string.Empty;
        }
    }
}