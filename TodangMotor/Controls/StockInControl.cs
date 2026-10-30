using System;
using System.Collections.Generic;
using System.Drawing;
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
    /// Stock-In entry, live inside the dashboard content area.
    /// The product dropdown filters by the selected supplier
    /// (products whose primary OR alternate supplier matches).
    /// </summary>
    public class StockInControl : UserControl
    {
        private readonly StockInService _stockInService = new();
        private readonly ProductService _productService = new();

        private List<Supplier> _suppliers = new();
        private List<Product> _products = new();

        private readonly Dictionary<string, Product> _productByDisplay = new();
        private readonly Dictionary<string, Supplier> _supplierByDisplay = new();

        private readonly List<WorkingLine> _lines = new();

        private bool _isLoaded;
        private bool _isBusy;
        private bool _suppressSupplierChange;

        private const string AllSuppliersDisplay = "(All Suppliers)";

        // Fixed column widths (Product is dynamic).
        private const int ColBrandW = 150;
        private const int ColUnitW = 80;
        private const int ColQtyW = 75;
        private const int ColUnitCostW = 155;
        private const int ColTotalW = 155;
        private const int ColRemoveW = 55;

        private class WorkingLine
        {
            public int ProductId { get; set; }
            public string ProductName { get; set; } = string.Empty;
            public string Brand { get; set; } = string.Empty;
            public string Unit { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public decimal UnitCost { get; set; }
            public decimal LineTotal => Quantity * UnitCost;
        }

        // ---- UI controls ----
        private RoundedComboBox _cmbSupplier;
        private DateTimePicker _dtpDeliveryDate;
        private RoundedTextBox _txtReferenceNo;
        private RoundedTextBox _txtNotes;

        private RoundedComboBox _cmbProduct;
        private RoundedTextBox _txtQty;
        private RoundedTextBox _txtUnitCost;
        private Button _btnAddLine;

        private DataGridView _grid;
        private Label _lblTotals;
        private Label _lblWarning;

        private Button _btnClearForm;
        private Button _btnComplete;
        private Label _lblStatus;

        public StockInControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(UiFactory.PagePadding);

            BuildLayout();

            Load += StockInControl_Load;
        }

        private async void StockInControl_Load(object sender, EventArgs e)
        {
            if (_isLoaded) return;
            _isLoaded = true;
            await LoadLookupsAsync();
        }

        // ============================================================
        // LAYOUT
        // ============================================================

        private void BuildLayout()
        {
            Controls.Add(BuildGridBlock());
            Controls.Add(BuildBottomBar());
            Controls.Add(BuildBuilderBlock());
            Controls.Add(BuildHeaderBlock());
        }

        private Panel BuildHeaderBlock()
        {
            var block = new Panel
            {
                Dock = DockStyle.Top,
                Height = 240,
                BackColor = Theme.Background
            };

            block.Controls.Add(MakeSectionTitle("DELIVERY DETAILS", 0, 8));

            int y = 48;
            const int labelW = 130;
            const int fieldX = 140;

            block.Controls.Add(MakeRowLabel("Supplier:", 0, y, labelW));

            _cmbSupplier = new RoundedComboBox
            {
                Width = 340,
                Location = new Point(fieldX, y)
            };
            _cmbSupplier.SelectedIndexChanged += async (s, e) => await OnSupplierChangedAsync();
            block.Controls.Add(_cmbSupplier);

            var lblDate = MakeRowLabel("Delivery Date:", 0, y, 130);
            block.Controls.Add(lblDate);

            _dtpDeliveryDate = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "MMMM d, yyyy",
                Font = Theme.FontBody,
                Size = new Size(220, 32),
                Location = new Point(0, y + 6),
                MaxDate = DateTime.Today,
                Value = DateTime.Today
            };
            block.Controls.Add(_dtpDeliveryDate);

            y += 64;

            block.Controls.Add(MakeRowLabel("Reference No:", 0, y, labelW));

            _txtReferenceNo = UiFactory.CreateTextBox(400);
            _txtReferenceNo.Location = new Point(fieldX, y);
            _txtReferenceNo.Placeholder = "Optional — supplier invoice or DR number";
            _txtReferenceNo.MaxLength = 50;
            block.Controls.Add(_txtReferenceNo);

            y += 64;

            block.Controls.Add(MakeRowLabel("Notes:", 0, y, labelW));

            _txtNotes = UiFactory.CreateTextBox(400);
            _txtNotes.Location = new Point(fieldX, y);
            _txtNotes.Placeholder = "Optional — e.g. regular monthly order";
            _txtNotes.MaxLength = 250;
            block.Controls.Add(_txtNotes);

            block.Resize += (s, e) =>
            {
                int right = block.ClientSize.Width;
                _txtReferenceNo.Width = Math.Max(200, right - fieldX);
                _txtNotes.Width = Math.Max(200, right - fieldX);

                int dateX = right - _dtpDeliveryDate.Width;
                _dtpDeliveryDate.Left = dateX;
                lblDate.Left = dateX - lblDate.Width - 12;
            };

            return block;
        }

        private Panel BuildBuilderBlock()
        {
            var block = new Panel
            {
                Dock = DockStyle.Top,
                Height = 190,
                BackColor = Theme.Background
            };

            block.Controls.Add(MakeSectionTitle("ADD LINE ITEM", 0, 8));

            int y = 48;
            const int labelW = 130;
            const int fieldX = 140;

            // ---- Row 1: Product + Add Line ----
            block.Controls.Add(MakeRowLabel("Product:", 0, y, labelW));

            _cmbProduct = new RoundedComboBox
            {
                Width = 400,
                Location = new Point(fieldX, y)
            };
            _cmbProduct.SelectedIndexChanged += (s, e) => OnProductPicked();
            block.Controls.Add(_cmbProduct);

            _btnAddLine = UiFactory.CreateButton("+ Add Line", UiFactory.ButtonStyle.Primary, 170, UiFactory.PrimaryButtonHeight);
            _btnAddLine.Location = new Point(0, y);
            _btnAddLine.Click += (s, e) => AddLine();
            block.Controls.Add(_btnAddLine);

            y += 64;

            // ---- Row 2: Quantity + Unit Cost ----
            block.Controls.Add(MakeRowLabel("Quantity:", 0, y, labelW));

            _txtQty = UiFactory.CreateTextBox(120);
            _txtQty.Location = new Point(fieldX, y);
            _txtQty.MaxLength = 8;
            block.Controls.Add(_txtQty);

            var lblCost = MakeRowLabel("Unit Cost (₱):", fieldX + 152, y, 140);
            block.Controls.Add(lblCost);

            _txtUnitCost = UiFactory.CreateTextBox(180);
            _txtUnitCost.Location = new Point(fieldX + 300, y);
            _txtUnitCost.MaxLength = 12;
            block.Controls.Add(_txtUnitCost);

            block.Resize += (s, e) =>
            {
                int right = block.ClientSize.Width;
                _cmbProduct.Width = Math.Max(280, right - fieldX - _btnAddLine.Width - 16);
                _btnAddLine.Left = right - _btnAddLine.Width;
                _btnAddLine.Top = 48;
            };

            return block;
        }

        private Panel BuildGridBlock()
        {
            var block = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Background
            };

            // ---- Top row: section title + totals ----
            var topRow = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = Theme.Background
            };

            topRow.Controls.Add(MakeSectionTitle("LINE ITEMS", 0, 14));

            _lblTotals = new Label
            {
                Text = "Total items: 0   ·   Total cost: ₱ 0.00",
                Font = Theme.FontBodyBold,
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(0, 14),
                Height = 24,
                Width = 400,
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent
            };
            topRow.Controls.Add(_lblTotals);

            topRow.Resize += (s, e) =>
            {
                _lblTotals.Left = topRow.ClientSize.Width - _lblTotals.Width;
            };

            // ---- Bottom row: warning ----
            var bottomRow = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 44,
                BackColor = Theme.Background
            };

            _lblWarning = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.Danger,
                AutoSize = false,
                Location = new Point(0, 8),
                Height = 36,
                Width = 400,
                TextAlign = ContentAlignment.TopLeft,
                BackColor = Color.Transparent
            };
            bottomRow.Controls.Add(_lblWarning);

            bottomRow.Resize += (s, e) =>
            {
                _lblWarning.Width = Math.Max(200, bottomRow.ClientSize.Width);
            };

            // ---- Grid ----
            var gridWrapper = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 8, 0, 8),
                BackColor = Theme.Background
            };

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };
            UiFactory.StyleGridModern(_grid);
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            _grid.CellClick += Grid_CellClick;
            _grid.CellMouseEnter += Grid_CellMouseEnter;
            _grid.CellPainting += Grid_CellPainting;
            _grid.CellMouseLeave += (s, e) => _grid.Cursor = Cursors.Default;
            _grid.CellPainting += Grid_CellPainting;

            PrimeGridColumns();

            _grid.Resize += (s, e) => FitProductColumn();

            gridWrapper.Controls.Add(_grid);

            block.Controls.Add(gridWrapper);
            block.Controls.Add(bottomRow);
            block.Controls.Add(topRow);

            return block;
        }

        private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (_grid.Columns[e.ColumnIndex].Name != "Remove") return;

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

        private Panel BuildBottomBar()
        {
            var bar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 88,
                BackColor = Theme.Background
            };

            _btnClearForm = UiFactory.CreateButton("Clear Form", UiFactory.ButtonStyle.Secondary, 150, UiFactory.PrimaryButtonHeight);
            _btnClearForm.Location = new Point(0, 20);
            _btnClearForm.Click += (s, e) => ClearForm();

            _lblStatus = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.Danger,
                AutoSize = false,
                Location = new Point(_btnClearForm.Right + 24, 20),
                Height = UiFactory.PrimaryButtonHeight,
                Width = 300,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            _btnComplete = UiFactory.CreateButton("Complete Stock-In", UiFactory.ButtonStyle.Primary, 200, UiFactory.PrimaryButtonHeight);
            _btnComplete.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnComplete.Click += async (s, e) => await CompleteAsync();

            bar.Controls.Add(_btnClearForm);
            bar.Controls.Add(_lblStatus);
            bar.Controls.Add(_btnComplete);

            bar.Resize += (s, e) =>
            {
                _btnComplete.Left = bar.ClientSize.Width - _btnComplete.Width;
                _lblStatus.Width = Math.Max(100,
                    _btnComplete.Left - _lblStatus.Left - 16);
            };

            return bar;
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

            // Text — left
            AddTextCol("Product", "Product", 400, true);
            AddTextCol("Brand", "Brand", ColBrandW, true);

            // Unit — center
            AddTextCol("Unit", "Unit", ColUnitW, true, DataGridViewContentAlignment.MiddleCenter);

            // Numbers — right
            AddTextCol("Quantity", "Qty", ColQtyW, true, DataGridViewContentAlignment.MiddleRight);
            AddTextCol("UnitCost", "Unit Cost (₱)", ColUnitCostW, true, DataGridViewContentAlignment.MiddleRight);
            AddTextCol("Total", "Line Total (₱)", ColTotalW, true, DataGridViewContentAlignment.MiddleRight);

            // Remove — × per row
            var removeCol = new DataGridViewTextBoxColumn
            {
                Name = "Remove",
                HeaderText = "",
                Width = ColRemoveW,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                Resizable = DataGridViewTriState.False,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Padding = new Padding(0)
                }
            };
            _grid.Columns.Add(removeCol);

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
                ColBrandW + ColUnitW + ColQtyW + ColUnitCostW + ColTotalW + ColRemoveW;

            int available = _grid.ClientSize.Width - others - 24;
            if (available < 220) available = 220;

            _grid.Columns["Product"].Width = available;
        }

        private void Grid_CellMouseEnter(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            _grid.Cursor = _grid.Columns[e.ColumnIndex].Name == "Remove"
                ? Cursors.Hand
                : Cursors.Default;
        }

        private void Grid_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (_grid.Columns[e.ColumnIndex].Name != "Remove") return;

            var idCell = _grid.Rows[e.RowIndex].Cells["ProductId"];
            if (idCell?.Value == null) return;

            int id = Convert.ToInt32(idCell.Value);
            var line = _lines.FirstOrDefault(l => l.ProductId == id);
            if (line != null)
            {
                _lines.Remove(line);
                RefreshGrid();
                ShowInfo("Line removed.");
            }
        }

        // ============================================================
        // LOAD LOOKUPS
        // ============================================================

        private async Task LoadLookupsAsync()
        {
            SetBusy(true);
            try
            {
                _suppliers = await _stockInService.GetActiveSuppliersAsync();

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
                    ShowError("No active suppliers available. Add or reactivate a supplier first.");
                else if (_products.Count == 0)
                    ShowError("No active products available for this supplier. Add or reactivate an item first.");

                RefreshGrid();
            }
            finally
            {
                SetBusy(false);
            }
        }

        // ============================================================
        // SUPPLIER → PRODUCT FILTER
        // ============================================================

        private async Task OnSupplierChangedAsync()
        {
            if (_suppressSupplierChange) return;
            await ReloadProductsForCurrentSupplierAsync();
        }

        private async Task ReloadProductsForCurrentSupplierAsync()
        {
            int supplierId = GetSelectedSupplierId();
            _products = await _stockInService.GetActiveProductsBySupplierAsync(supplierId);

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

            _txtQty.Text = string.Empty;
            _txtUnitCost.Text = string.Empty;
        }

        // ============================================================
        // LINE BUILDER
        // ============================================================

        private void OnProductPicked()
        {
            var p = GetSelectedProduct();
            if (p == null) return;

            _txtUnitCost.Text = p.CostPrice.ToString("0.00");
            if (string.IsNullOrEmpty(_txtQty.Text) || _txtQty.Text == ".")
                _txtQty.Text = "1";
        }

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
                ShowError("Please select a product.");
                return;
            }

            if (!int.TryParse(_txtQty.Text.Trim(), out int qty) || qty <= 0)
            {
                ShowError("Quantity must be a whole number greater than zero.");
                _txtQty.FocusInput();
                return;
            }

            if (!decimal.TryParse(_txtUnitCost.Text.Trim(),
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out decimal cost))
            {
                ShowError("Unit cost must be a valid number.");
                _txtUnitCost.FocusInput();
                return;
            }

            if (cost < 0)
            {
                ShowError("Unit cost cannot be negative.");
                _txtUnitCost.FocusInput();
                return;
            }

            if (_lines.Any(l => l.ProductId == product.ProductId))
            {
                ShowError($"\"{product.ProductName}\" is already in this Stock-In. Remove the existing line first.");
                return;
            }

            _lines.Add(new WorkingLine
            {
                ProductId = product.ProductId,
                ProductName = product.ProductName ?? string.Empty,
                Brand = product.Brand ?? string.Empty,
                Unit = product.Unit ?? string.Empty,
                Quantity = qty,
                UnitCost = cost
            });

            RefreshGrid();

            _txtQty.Text = string.Empty;
            _txtUnitCost.Text = string.Empty;
            _cmbProduct.Focus();
            ShowInfo("Line added.");
        }

        private void ClearForm()
        {
            if (_lines.Count > 0)
            {
                var confirm = MessageBox.Show(
                    "Discard this Stock-In and clear the form?",
                    "Confirm Clear",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes) return;
            }

            _lines.Clear();
            _txtReferenceNo.Text = string.Empty;
            _txtNotes.Text = string.Empty;
            _dtpDeliveryDate.Value = DateTime.Today;
            _txtQty.Text = string.Empty;
            _txtUnitCost.Text = string.Empty;

            _suppressSupplierChange = true;
            if (_cmbSupplier.Items.Count > 0)
                _cmbSupplier.SelectedIndex = 0;
            _suppressSupplierChange = false;

            _ = ReloadProductsForCurrentSupplierAsync();

            RefreshGrid();
            ShowInfo("Form cleared.");
        }

        private void RefreshGrid()
        {
            var rows = _lines
                .Select(l => new
                {
                    ProductId = l.ProductId,
                    Product = l.ProductName,
                    Brand = l.Brand,
                    Unit = l.Unit,
                    Quantity = l.Quantity,
                    UnitCost = l.UnitCost.ToString("N2"),
                    Total = l.LineTotal.ToString("N2")
                })
                .ToList();

            _grid.DataSource = rows;

            if (_grid.Rows.Count > 0)
                _grid.ClearSelection();

            UpdateTotals();
            FitProductColumn();
        }

        private void UpdateTotals()
        {
            int totalQty = _lines.Sum(l => l.Quantity);
            decimal totalCost = _lines.Sum(l => l.LineTotal);

            _lblTotals.Text = $"Total items: {totalQty}   ·   Total cost: ₱ {totalCost:N2}";
            if (_lblTotals.Parent != null)
                _lblTotals.Left = _lblTotals.Parent.ClientSize.Width - _lblTotals.Width;

            var overpriced = new List<string>();
            foreach (var line in _lines)
            {
                var p = _products.FirstOrDefault(x => x.ProductId == line.ProductId);
                if (p != null && line.UnitCost > p.SellingPrice)
                    overpriced.Add($"{p.ProductName} (cost {line.UnitCost:N2} vs selling {p.SellingPrice:N2})");
            }

            _lblWarning.Text = overpriced.Count == 0
                ? string.Empty
                : $"⚠ Unit cost exceeds selling price for {overpriced.Count} item(s): " +
                  string.Join("; ", overpriced) +
                  ".";
        }

        // ============================================================
        // COMPLETE
        // ============================================================

        private async Task CompleteAsync()
        {
            if (_isBusy) return;

            if (_lines.Count == 0)
            {
                ShowError("Add at least one line item before completing.");
                return;
            }

            int supplierId = GetSelectedSupplierId();
            if (supplierId <= 0)
            {
                ShowError("Please select a specific supplier (not \"All Suppliers\") before completing.");
                return;
            }

            var requestLines = _lines.Select(l => new StockInRequestLine
            {
                ProductId = l.ProductId,
                Quantity = l.Quantity,
                UnitCost = l.UnitCost
            }).ToList();

            int savedLineCount = _lines.Count;
            int savedUnitCount = _lines.Sum(l => l.Quantity);

            SetBusy(true);
            try
            {
                var result = await _stockInService.CompleteStockInAsync(
                    supplierId,
                    _dtpDeliveryDate.Value,
                    _txtReferenceNo.Text,
                    _txtNotes.Text,
                    requestLines);

                if (!result.Success)
                {
                    ShowError(result.ErrorMessage);
                    return;
                }

                var changes = result.CostComparisons.Where(c => c.HasChange).ToList();
                var skipped = result.CostComparisons.Where(c => !c.CanUpdate).ToList();

                if (changes.Count > 0 || skipped.Count > 0)
                {
                    var parent = FindForm();
                    using var prompt = new CostUpdatePromptForm(changes, skipped);
                    var choice = parent != null ? prompt.ShowDialog(parent) : prompt.ShowDialog();

                    if (choice == DialogResult.Yes && changes.Count > 0)
                    {
                        var updates = changes.Select(c => (c.ProductId, c.NewCost)).ToList();
                        var updateResult = await _productService.UpdateCostPricesAsync(updates);
                        if (!updateResult.Success)
                        {
                            MessageBox.Show(
                                "Stock-In was saved, but some product cost prices could not be updated:\n\n" +
                                updateResult.ErrorMessage,
                                "Cost Update Warning",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                        }
                    }
                }

                ShowInfo($"Stock-In #{result.StockInId} saved. " +
                         $"{savedLineCount} item(s), {savedUnitCount} unit(s) added. Inventory updated.");

                _lines.Clear();
                _txtReferenceNo.Text = string.Empty;
                _txtNotes.Text = string.Empty;
                _dtpDeliveryDate.Value = DateTime.Today;
                _txtQty.Text = string.Empty;
                _txtUnitCost.Text = string.Empty;

                _suppressSupplierChange = true;
                if (_cmbSupplier.Items.Count > 0)
                    _cmbSupplier.SelectedIndex = 0;
                _suppressSupplierChange = false;

                await ReloadProductsForCurrentSupplierAsync();

                RefreshGrid();
            }
            finally
            {
                SetBusy(false);
            }
        }

        private int GetSelectedSupplierId()
        {
            var name = _cmbSupplier.SelectedItem as string;
            if (string.IsNullOrEmpty(name)) return 0;
            if (name == AllSuppliersDisplay) return 0;

            return _supplierByDisplay.TryGetValue(name, out var s) ? s.SupplierId : 0;
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            _btnComplete.Enabled = !busy;
            _btnAddLine.Enabled = !busy;
            _btnClearForm.Enabled = !busy;
            _cmbSupplier.Enabled = !busy;
            _cmbProduct.Enabled = !busy;
            _txtQty.Enabled = !busy;
            _txtUnitCost.Enabled = !busy;
            _txtReferenceNo.Enabled = !busy;
            _txtNotes.Enabled = !busy;
            _dtpDeliveryDate.Enabled = !busy;
        }

        private void ShowError(string message)
        {
            _lblStatus.ForeColor = Theme.Danger;
            _lblStatus.Text = message ?? string.Empty;
        }

        private void ShowInfo(string message)
        {
            _lblStatus.ForeColor = Theme.Success;
            _lblStatus.Text = message ?? string.Empty;
        }
    }
}