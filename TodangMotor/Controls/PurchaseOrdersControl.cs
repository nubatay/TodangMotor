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
        // ============================================================
        // SERVICES
        // ============================================================

        private readonly PurchaseOrderService _poService = new();

        // ============================================================
        // STATE
        // ============================================================

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

        private class WorkingLine
        {
            public int ProductId { get; set; }
            public string ProductName { get; set; } = string.Empty;
            public string Brand { get; set; } = string.Empty;
            public string Unit { get; set; } = string.Empty;
            public int Quantity { get; set; }
        }

        // ============================================================
        // UI CONTROLS
        // ============================================================

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

        private Button _btnRemoveLine;
        private Button _btnClearForm;
        private Button _btnSaveAndPdf;
        private Label _buildStatus;

        // ---- History page ----
        private RoundedTextBox _historySearch;
        private DataGridView _historyGrid;
        private Button _btnViewDetail;
        private Button _btnExportPdf;
        private Button _btnRefresh;
        private Label _historyStatus;
        private Label _historyCount;

        // ============================================================
        // CONSTRUCTION
        // ============================================================

        public PurchaseOrdersControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(Theme.SpacingLg);

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
                Font = Theme.FontBody,
                ItemSize = new Size(180, 38),
                SizeMode = TabSizeMode.Fixed,
                Appearance = TabAppearance.Normal,
                Padding = new Point(20, 6)
            };

            _buildPage = new TabPage("Build PO") { BackColor = Theme.Background, Padding = Padding.Empty };
            _historyPage = new TabPage("PO History") { BackColor = Theme.Background, Padding = Padding.Empty };

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
            // Header block — supplier + order date + notes
            var headerBlock = new Panel
            {
                Dock = DockStyle.Top,
                Height = 176,
                BackColor = Theme.Background,
                Padding = new Padding(24, 12, 24, 0)
            };

            int y = 12;

            var lblSupplier = MakeRowLabel("Supplier:", 24, y);
            headerBlock.Controls.Add(lblSupplier);

            _cmbSupplier = new RoundedComboBox
            {
                Width = 320,
                Location = new Point(144, y)
            };
            _cmbSupplier.SelectedIndexChanged += async (s, e) => await OnSupplierChangedAsync();
            headerBlock.Controls.Add(_cmbSupplier);

            var lblDate = MakeRowLabel("Order Date:", 0, y);
            headerBlock.Controls.Add(lblDate);

            _dtpOrderDate = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "MMMM d, yyyy",
                Font = Theme.FontBody,
                Size = new Size(200, 32),
                Location = new Point(0, y + 5),
                Value = DateTime.Today
            };
            headerBlock.Controls.Add(_dtpOrderDate);

            y += 60;

            var lblNotes = MakeRowLabel("Notes:", 24, y);
            headerBlock.Controls.Add(lblNotes);

            _txtNotes = UiFactory.CreateTextBox(400);
            _txtNotes.Location = new Point(144, y);
            _txtNotes.Placeholder = "Optional — e.g. Urgent restock";
            _txtNotes.MaxLength = 500;
            headerBlock.Controls.Add(_txtNotes);

            // Responsive layout — date picker pinned right
            headerBlock.Resize += (s, e) =>
            {
                int right = headerBlock.ClientSize.Width - 24;
                _txtNotes.Width = Math.Max(200, right - 144);

                int dateX = right - _dtpOrderDate.Width;
                _dtpOrderDate.Left = dateX;
                lblDate.Left = dateX - lblDate.Width - 12;
            };

            // Builder row — product + qty + add
            var builderRow = new Panel
            {
                Dock = DockStyle.Top,
                Height = 76,
                BackColor = Theme.Background,
                Padding = new Padding(24, 8, 24, 0)
            };

            var lblProduct = MakeRowLabel("Product:", 24, 16);
            builderRow.Controls.Add(lblProduct);

            _cmbProduct = new RoundedComboBox
            {
                Width = 420,
                Location = new Point(144, 14)
            };
            builderRow.Controls.Add(_cmbProduct);

            var lblQty = MakeRowLabel("Qty:", 0, 16);
            builderRow.Controls.Add(lblQty);

            _txtQty = UiFactory.CreateTextBox(100);
            _txtQty.Location = new Point(0, 14);
            _txtQty.MaxLength = 8;
            _txtQty.Text = "1";
            builderRow.Controls.Add(_txtQty);

            _btnAddLine = UiFactory.CreateButton("+ Add", UiFactory.ButtonStyle.Primary, 110, 40);
            _btnAddLine.Location = new Point(0, 14);
            _btnAddLine.Click += (s, e) => AddLine();
            builderRow.Controls.Add(_btnAddLine);

            builderRow.Resize += (s, e) =>
            {
                int right = builderRow.ClientSize.Width - 24;

                _btnAddLine.Left = right - _btnAddLine.Width;

                int qtyX = _btnAddLine.Left - 12 - _txtQty.Width;
                _txtQty.Left = qtyX;
                lblQty.Left = qtyX - lblQty.Width - 8;

                int available = qtyX - 12 - 144;
                _cmbProduct.Width = Math.Max(200, available);
            };

            // Grid block
            var gridBlock = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Background,
                Padding = new Padding(24, 4, 24, 8)
            };

            _buildGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 0, 4)
            };
            UiFactory.StyleGrid(_buildGrid);
            _buildGrid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 251);
            PrimeBuildGridColumns();

            _buildTotals = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                Font = Theme.FontBodyBold,
                ForeColor = Theme.TextPrimary,
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent,
                Text = "Total items: 0   ·   Total units: 0"
            };

            gridBlock.Controls.Add(_buildGrid);
            gridBlock.Controls.Add(_buildTotals);

            // Bottom bar
            var bottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 76,
                BackColor = Theme.Background,
                Padding = new Padding(24, 0, 24, 0)
            };

            _btnRemoveLine = UiFactory.CreateButton("Remove Selected", UiFactory.ButtonStyle.Ghost, 160, 40);
            _btnRemoveLine.Location = new Point(24, 18);
            _btnRemoveLine.Click += (s, e) => RemoveSelectedLine();
            bottomBar.Controls.Add(_btnRemoveLine);

            _btnClearForm = UiFactory.CreateButton("Clear Form", UiFactory.ButtonStyle.Secondary, 130, 40);
            _btnClearForm.Location = new Point(196, 18);
            _btnClearForm.Click += (s, e) => ClearForm();
            bottomBar.Controls.Add(_btnClearForm);

            _buildStatus = new Label
            {
                Font = Theme.FontSmall,
                ForeColor = Theme.Danger,
                AutoSize = false,
                Location = new Point(340, 18),
                Height = 40,
                Width = 200,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            bottomBar.Controls.Add(_buildStatus);

            _btnSaveAndPdf = UiFactory.CreateButton(
                "Save PO & Generate PDF",
                UiFactory.ButtonStyle.Primary,
                210, 44);
            _btnSaveAndPdf.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnSaveAndPdf.Click += async (s, e) => await SaveAndExportAsync();
            bottomBar.Controls.Add(_btnSaveAndPdf);

            bottomBar.Resize += (s, e) =>
            {
                _btnSaveAndPdf.Left = bottomBar.ClientSize.Width - _btnSaveAndPdf.Width - 24;
                _buildStatus.Width = Math.Max(100,
                    _btnSaveAndPdf.Left - _buildStatus.Left - 12);
            };

            // Docking order for the page
            _buildPage.Controls.Add(gridBlock);
            _buildPage.Controls.Add(bottomBar);
            _buildPage.Controls.Add(builderRow);
            _buildPage.Controls.Add(headerBlock);
        }

        private void PrimeBuildGridColumns()
        {
            _buildGrid.AutoGenerateColumns = false;
            _buildGrid.Columns.Clear();

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
                _buildGrid.Columns.Add(col);
            }

            AddCol("Product", "Product", 300);
            AddCol("Brand", "Brand", 160);
            AddCol("Unit", "Unit", 80);
            AddCol("Quantity", "Qty", 90, true);
        }

        // ============================================================
        // PO HISTORY PAGE
        // ============================================================

        private void BuildHistoryPage()
        {
            // Filter bar
            var bar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 84,
                BackColor = Theme.Background
            };

            _historySearch = UiFactory.CreateTextBox(320);
            _historySearch.Location = new Point(4, 12);
            _historySearch.Placeholder = "Search PO number or supplier…";
            _historySearch.MaxLength = 150;
            _historySearch.TextChanged += (s, e) => ApplyHistoryFilter();

            _historyCount = new Label
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

            bar.Controls.Add(_historySearch);
            bar.Controls.Add(_historyCount);

            // Grid
            _historyGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 0, 4)
            };
            UiFactory.StyleGrid(_historyGrid);
            _historyGrid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 251);
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

            // Bottom bar
            var bottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 76,
                BackColor = Theme.Background
            };

            // ---- Left group: view / export ----
            _btnViewDetail = UiFactory.CreateButton("View Detail", UiFactory.ButtonStyle.Secondary, 130, 40);
            _btnViewDetail.Location = new Point(4, 18);
            _btnViewDetail.Click += (s, e) => OpenDetailForSelected();

            _btnExportPdf = UiFactory.CreateButton("Export PDF", UiFactory.ButtonStyle.Primary, 130, 40);
            _btnExportPdf.Location = new Point(146, 18);
            _btnExportPdf.Click += async (s, e) => await ExportSelectedAsync();

            // ---- Center: status ----
            _historyStatus = new Label
            {
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(288, 18),
                Height = 40,
                Width = 300,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            // ---- Right: refresh ----
            _btnRefresh = UiFactory.CreateButton("Refresh", UiFactory.ButtonStyle.Ghost, 110, 40);
            _btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnRefresh.Click += async (s, e) => await LoadHistoryAsync();

            bottomBar.Controls.Add(_btnViewDetail);
            bottomBar.Controls.Add(_btnExportPdf);
            bottomBar.Controls.Add(_historyStatus);
            bottomBar.Controls.Add(_btnRefresh);

            // Position Refresh on the right edge; status fills the middle.
            bottomBar.Resize += (s, e) =>
            {
                _btnRefresh.Left = bottomBar.ClientSize.Width - _btnRefresh.Width - 4;
                _historyStatus.Width = Math.Max(100,
                    _btnRefresh.Left - _historyStatus.Left - 12);
            };

            _historyPage.Controls.Add(_historyGrid);
            _historyPage.Controls.Add(bottomBar);
            _historyPage.Controls.Add(bar);
        }

        // ============================================================
        // SMALL HELPERS
        // ============================================================

        private Label MakeRowLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(120, 40),
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
                // Suppliers
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

                // Products (all initially)
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

        private void RemoveSelectedLine()
        {
            if (_buildGrid.SelectedRows.Count == 0)
            {
                ShowBuildStatus("Select a line to remove.", true);
                return;
            }

            // Find by matching Product name + brand because grid rows are a projection.
            var row = _buildGrid.SelectedRows[0];
            string productName = row.Cells["Product"].Value?.ToString() ?? string.Empty;
            string brand = row.Cells["Brand"].Value?.ToString() ?? string.Empty;

            var line = _lines.FirstOrDefault(l =>
                l.ProductName == productName && l.Brand == brand);

            if (line != null)
            {
                _lines.Remove(line);
                RefreshBuildGrid();
                ShowBuildStatus("Line removed.", false);
            }
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

                // Load the saved PO's detail so we can build the PDF.
                // We need to find the PO we just created — query by number.
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

                // Prompt for PDF destination.
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

                // Reset the form.
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

                _historyLoaded = false; // force reload next time History tab opens
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

                // Store in a field so ApplyHistoryFilter can reuse.
                _historyOrders = orders ?? new List<PurchaseOrder>();
                ApplyHistoryFilter();
            }
            catch (Exception ex)
            {
                _historyCount.Text = $"Could not load POs: {ex.Message}";
                _historyCount.ForeColor = Theme.Danger;
            }
        }

        private List<PurchaseOrder> _historyOrders = new();

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

            void SetWeight(string col, int weight, string header = null)
            {
                if (!_historyGrid.Columns.Contains(col)) return;
                _historyGrid.Columns[col].FillWeight = weight;
                if (header != null) _historyGrid.Columns[col].HeaderText = header;
            }

            SetWeight("PONumber", 200, "PO Number");
            SetWeight("OrderDate", 160, "Order Date");
            SetWeight("SupplierName", 260, "Supplier");
            SetWeight("ItemCount", 100, "Items");

            if (_historyGrid.Rows.Count > 0)
                _historyGrid.ClearSelection();

            _historyCount.ForeColor = Theme.TextSecondary;
            _historyCount.Text = $"Showing {rows.Count} Purchase Order(s)";
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

                // After closing, reload history in case something changed.
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
            _btnRemoveLine.Enabled = !busy;
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