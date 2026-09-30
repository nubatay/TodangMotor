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
    /// Same workflow as the old popup, but as a UserControl so it fits
    /// the uniform sidebar navigation.
    /// </summary>
    public class StockInControl : UserControl
    {
        // ============================================================
        // SERVICES
        // ============================================================

        private readonly StockInService _stockInService = new();
        private readonly ProductService _productService = new();

        // ============================================================
        // STATE
        // ============================================================

        private List<Supplier> _suppliers = new();
        private List<Product> _products = new();
        private readonly Dictionary<string, Product> _productByDisplay = new();
        private readonly List<WorkingLine> _lines = new();

        private bool _isLoaded;
        private bool _isBusy;

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

        // ============================================================
        // UI CONTROLS
        // ============================================================

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

        private Button _btnRemoveLine;
        private Button _btnClearForm;
        private Button _btnComplete;
        private Label _lblStatus;

        // ---- Layout constants ----
        private const int PadX = 24;
        private const int LabelCol = 120;
        private const int FieldCol = PadX + LabelCol + 12;
        private const int RowH = 60;
        private const int FieldH = 40;

        // ============================================================
        // CONSTRUCTION
        // ============================================================

        public StockInControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(Theme.SpacingLg);

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
            // Docking order: Fill first, then Bottom, then Top.
            Controls.Add(BuildGridBlock());        // Fill
            Controls.Add(BuildBottomBar());        // Bottom
            Controls.Add(BuildBuilderBlock());     // Top (lower)
            Controls.Add(BuildHeaderBlock());      // Top (upper, added last)
        }

        // ---------------- HEADER (DELIVERY DETAILS) ----------------

        private Panel BuildHeaderBlock()
        {
            var block = new Panel
            {
                Dock = DockStyle.Top,
                Height = 230,
                BackColor = Theme.Background
            };

            block.Controls.Add(MakeSectionTitle("DELIVERY DETAILS", PadX, 8));

            int y = 42;

            // Row 1: Supplier (left) + Delivery Date (right)
            block.Controls.Add(MakeRowLabel("Supplier:", PadX, y, LabelCol));

            _cmbSupplier = new RoundedComboBox
            {
                Width = 320,
                Location = new Point(FieldCol, y)
            };
            block.Controls.Add(_cmbSupplier);

            var lblDate = MakeRowLabel("Delivery Date:", 0, y, 120);
            block.Controls.Add(lblDate);

            _dtpDeliveryDate = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "MMMM d, yyyy",
                Font = Theme.FontBody,
                Size = new Size(220, 32),
                Location = new Point(0, y + 5),
                MaxDate = DateTime.Today,
                Value = DateTime.Today
            };
            block.Controls.Add(_dtpDeliveryDate);

            y += RowH;

            // Row 2: Reference No (full width)
            block.Controls.Add(MakeRowLabel("Reference No:", PadX, y, LabelCol));

            _txtReferenceNo = UiFactory.CreateTextBox(400);
            _txtReferenceNo.Location = new Point(FieldCol, y);
            _txtReferenceNo.Placeholder = "Optional — supplier invoice or DR number";
            _txtReferenceNo.MaxLength = 50;
            block.Controls.Add(_txtReferenceNo);

            y += RowH;

            // Row 3: Notes (full width)
            block.Controls.Add(MakeRowLabel("Notes:", PadX, y, LabelCol));

            _txtNotes = UiFactory.CreateTextBox(400);
            _txtNotes.Location = new Point(FieldCol, y);
            _txtNotes.Placeholder = "Optional — e.g. regular monthly order";
            _txtNotes.MaxLength = 250;
            block.Controls.Add(_txtNotes);

            // Responsive widths: fields stretch, date pins right.
            block.Resize += (s, e) =>
            {
                int right = block.ClientSize.Width - PadX;
                _txtReferenceNo.Width = Math.Max(200, right - FieldCol);
                _txtNotes.Width = Math.Max(200, right - FieldCol);

                int dateX = right - _dtpDeliveryDate.Width;
                _dtpDeliveryDate.Left = dateX;
                lblDate.Left = dateX - lblDate.Width - 12;
            };

            return block;
        }

        // ---------------- BUILDER (ADD LINE ITEM) ----------------

        private Panel BuildBuilderBlock()
        {
            var block = new Panel
            {
                Dock = DockStyle.Top,
                Height = 170,
                BackColor = Theme.Background
            };

            block.Controls.Add(MakeSectionTitle("ADD LINE ITEM", PadX, 8));

            int y = 42;

            block.Controls.Add(MakeRowLabel("Product:", PadX, y, LabelCol));

            _cmbProduct = new RoundedComboBox
            {
                Width = 400,
                Location = new Point(FieldCol, y)
            };
            _cmbProduct.SelectedIndexChanged += (s, e) => OnProductPicked();
            block.Controls.Add(_cmbProduct);

            _btnAddLine = UiFactory.CreateButton("+ Add Line", UiFactory.ButtonStyle.Primary, 160, FieldH);
            _btnAddLine.Location = new Point(0, y);
            _btnAddLine.Click += (s, e) => AddLine();
            block.Controls.Add(_btnAddLine);

            y += RowH;

            block.Controls.Add(MakeRowLabel("Quantity:", PadX, y, LabelCol));

            _txtQty = UiFactory.CreateTextBox(120);
            _txtQty.Location = new Point(FieldCol, y);
            _txtQty.MaxLength = 8;
            block.Controls.Add(_txtQty);

            var lblCost = MakeRowLabel("Unit Cost (PHP):", FieldCol + 140, y, 150);
            block.Controls.Add(lblCost);

            _txtUnitCost = UiFactory.CreateTextBox(160);
            _txtUnitCost.Location = new Point(FieldCol + 300, y);
            _txtUnitCost.MaxLength = 12;
            block.Controls.Add(_txtUnitCost);

            block.Resize += (s, e) =>
            {
                int right = block.ClientSize.Width - PadX;
                _cmbProduct.Width = Math.Max(300, right - FieldCol - _btnAddLine.Width - 12);
                _btnAddLine.Left = right - _btnAddLine.Width;
                _btnAddLine.Top = 42;
            };

            return block;
        }

        // ---------------- GRID (LINE ITEMS) ----------------

        private Panel BuildGridBlock()
        {
            var block = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Background
            };

            // Top: section title + totals
            var topRow = new Panel
            {
                Dock = DockStyle.Top,
                Height = 42,
                BackColor = Theme.Background
            };

            topRow.Controls.Add(MakeSectionTitle("LINE ITEMS", PadX, 12));

            _lblTotals = new Label
            {
                Text = "Total items: 0   ·   Total cost: PHP 0.00",
                Font = Theme.FontBodyBold,
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(0, 12),
                Height = 22,
                Width = 400,
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent
            };
            topRow.Controls.Add(_lblTotals);

            topRow.Resize += (s, e) =>
            {
                _lblTotals.Left = topRow.ClientSize.Width - _lblTotals.Width - PadX;
            };

            // Bottom: warning text (only shows when cost exceeds selling)
            var bottomRow = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                BackColor = Theme.Background
            };

            _lblWarning = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.Danger,
                AutoSize = false,
                Location = new Point(PadX, 6),
                Height = 34,
                Width = 400,
                TextAlign = ContentAlignment.TopLeft,
                BackColor = Color.Transparent
            };
            bottomRow.Controls.Add(_lblWarning);

            bottomRow.Resize += (s, e) =>
            {
                _lblWarning.Width = Math.Max(200, bottomRow.ClientSize.Width - PadX * 2);
            };

            // Grid
            var gridWrapper = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(PadX, 0, PadX, 0),
                BackColor = Theme.Background
            };

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 0, 4)
            };
            UiFactory.StyleGrid(_grid);
            _grid.BackgroundColor = Color.FromArgb(250, 251, 254);
            _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 251);
            PrimeGridColumns();
            gridWrapper.Controls.Add(_grid);

            block.Controls.Add(gridWrapper);   // Fill
            block.Controls.Add(bottomRow);     // Bottom
            block.Controls.Add(topRow);        // Top

            return block;
        }

        // ---------------- BOTTOM BAR ----------------

        private Panel BuildBottomBar()
        {
            var bar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 80,
                BackColor = Theme.Background
            };

            const int btnH = 40;

            _btnRemoveLine = UiFactory.CreateButton("Remove Selected", UiFactory.ButtonStyle.Ghost, 170, btnH);
            _btnRemoveLine.Location = new Point(PadX, 20);
            _btnRemoveLine.Click += (s, e) => RemoveSelectedLine();

            _btnClearForm = UiFactory.CreateButton("Clear Form", UiFactory.ButtonStyle.Secondary, 130, btnH);
            _btnClearForm.Location = new Point(PadX + 180, 20);
            _btnClearForm.Click += (s, e) => ClearForm();

            _lblStatus = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.Danger,
                AutoSize = false,
                Location = new Point(PadX + 320, 20),
                Height = btnH,
                Width = 200,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _btnComplete = UiFactory.CreateButton("Complete Stock-In", UiFactory.ButtonStyle.Primary, 190, 46);
            _btnComplete.Location = new Point(0, 17);
            _btnComplete.Click += async (s, e) => await CompleteAsync();

            bar.Controls.Add(_btnRemoveLine);
            bar.Controls.Add(_btnClearForm);
            bar.Controls.Add(_lblStatus);
            bar.Controls.Add(_btnComplete);

            bar.Resize += (s, e) =>
            {
                _btnComplete.Left = bar.ClientSize.Width - _btnComplete.Width - PadX;
                _lblStatus.Width = Math.Max(
                    100,
                    bar.ClientSize.Width - PadX - _btnComplete.Width - 20 - _lblStatus.Left);
            };

            return bar;
        }

        // ============================================================
        // SMALL LABEL HELPERS
        // ============================================================

        private Label MakeSectionTitle(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Font = new Font(Theme.UiFontFamily, 9F, FontStyle.Bold),
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
                Size = new Size(width, FieldH),
                Location = new Point(x, y),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
        }

        private void PrimeGridColumns()
        {
            _grid.AutoGenerateColumns = false;
            _grid.Columns.Clear();

            void AddCol(string prop, string header, int weight, bool visible = true)
            {
                _grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = prop,
                    DataPropertyName = prop,
                    HeaderText = header,
                    FillWeight = weight,
                    Visible = visible
                });
            }

            AddCol("Product", "Product", 240);
            AddCol("Brand", "Brand", 140);
            AddCol("Unit", "Unit", 60);
            AddCol("Quantity", "Qty", 70);
            AddCol("UnitCost", "Unit Cost (PHP)", 110);
            AddCol("Total", "Line Total (PHP)", 130);
            AddCol("ProductId", "ProductId", 100, visible: false);
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
                _products = await _stockInService.GetActiveProductsAsync();

                _cmbSupplier.Items.Clear();
                foreach (var s in _suppliers)
                    _cmbSupplier.Items.Add(s.SupplierName);
                if (_cmbSupplier.Items.Count > 0)
                    _cmbSupplier.SelectedIndex = 0;

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

                if (_suppliers.Count == 0)
                    ShowError("No active suppliers available. Add or reactivate a supplier first.");
                else if (_products.Count == 0)
                    ShowError("No active products available. Add or reactivate a product first.");

                RefreshGrid();
            }
            finally
            {
                SetBusy(false);
            }
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

        private void RemoveSelectedLine()
        {
            if (_grid.SelectedRows.Count == 0)
            {
                ShowError("Select a line in the grid to remove.");
                return;
            }

            var cell = _grid.SelectedRows[0].Cells["ProductId"];
            if (cell == null || cell.Value == null) return;

            int id = Convert.ToInt32(cell.Value);
            var line = _lines.FirstOrDefault(l => l.ProductId == id);
            if (line != null)
            {
                _lines.Remove(line);
                RefreshGrid();
                ShowInfo("Line removed.");
            }
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

            if (_cmbSupplier.Items.Count > 0)
                _cmbSupplier.SelectedIndex = 0;
            if (_cmbProduct.Items.Count > 0)
                _cmbProduct.SelectedIndex = 0;

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
        }

        private void UpdateTotals()
        {
            int totalQty = _lines.Sum(l => l.Quantity);
            decimal totalCost = _lines.Sum(l => l.LineTotal);

            _lblTotals.Text = $"Total items: {totalQty}   ·   Total cost: PHP {totalCost:N2}";
            if (_lblTotals.Parent != null)
            {
                _lblTotals.Left = _lblTotals.Parent.ClientSize.Width - _lblTotals.Width - PadX;
            }

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
                ShowError("Please select a supplier.");
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

                // Prompt-after-save for cost updates.
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

                // Success: show message, clear form for the next delivery.
                ShowInfo($"Stock-In #{result.StockInId} saved. " +
                         $"{savedLineCount} item(s), {savedUnitCount} unit(s) added. Inventory updated.");

                _lines.Clear();
                _txtReferenceNo.Text = string.Empty;
                _txtNotes.Text = string.Empty;
                _dtpDeliveryDate.Value = DateTime.Today;
                _txtQty.Text = string.Empty;
                _txtUnitCost.Text = string.Empty;

                if (_cmbSupplier.Items.Count > 0)
                    _cmbSupplier.SelectedIndex = 0;
                if (_cmbProduct.Items.Count > 0)
                    _cmbProduct.SelectedIndex = 0;

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
            var s = _suppliers.FirstOrDefault(x =>
                string.Equals(x.SupplierName, name, StringComparison.OrdinalIgnoreCase));
            return s?.SupplierId ?? 0;
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            _btnComplete.Enabled = !busy;
            _btnAddLine.Enabled = !busy;
            _btnRemoveLine.Enabled = !busy;
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