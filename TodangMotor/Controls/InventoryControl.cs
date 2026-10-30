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
    /// Inventory module — two tabs:
    ///   1. Stock Levels    — grid with status chips + per-row Adjust action
    ///   2. Movement History — chronological movements with date chips
    /// Catalog management (Add/Edit/Deactivate) lives in the Items module.
    /// </summary>
    public class InventoryControl : UserControl
    {
        private readonly ProductService _productService = new();
        private readonly CategoryService _categoryService;
        private readonly StockMovementService _stockMovementService = new();

        private List<Product> _allProducts = new();
        private List<Category> _allCategories = new();
        private Dictionary<int, Category> _categoriesById = new();

        private bool _isLoaded;
        private bool _movementHistoryLoaded;

        private const string AdjustIcon = "\uE70F"; // Segoe MDL2 Assets — Edit (pencil)

        // Cached icon font for the manual CellPainting draw — created once, not per-cell.
        private readonly Font _adjustIconFont = new Font(Theme.IconFontFamily, 14F, FontStyle.Regular);

        // Fixed widths — used both when creating columns and when calculating
        // the Product column's width. Keeping them in one place prevents the
        // mismatch that caused the pen column to shift on refresh.
        private const int ColBrandW = 130;
        private const int ColCategoryW = 140;
        private const int ColUnitW = 72;
        private const int ColOnHandW = 120;
        private const int ColReorderW = 115;
        private const int ColStatusW = 165;
        private const int ColAdjustW = 60;

        // ---- Tab container ----
        private TabControl _tabs;
        private TabPage _stockLevelsTab;
        private TabPage _movementHistoryTab;

        // ---- Tab 1 — Stock Levels ----
        private RoundedTextBox _slSearchBox;
        private RoundedComboBox _slCategoryFilter;
        private Label _slCountLabel;
        private Button _slActiveStatusChip;
        private readonly List<Button> _slStatusChips = new();
        private DataGridView _slGrid;

        // ---- Tab 2 — Movement History ----
        private RoundedTextBox _mhSearchBox;
        private RoundedComboBox _mhTypeFilter;
        private DateTimePicker _mhFromDate;
        private DateTimePicker _mhToDate;
        private Panel _mhCustomPickerPanel;
        private Button _mhActiveDateChip;
        private readonly List<Button> _mhDateChips = new();
        private Label _mhCountLabel;
        private DataGridView _mhGrid;
        private Label _mhStatusLabel;
        private Panel _mhFilterBar;

        // ============================================================
        // CONSTRUCTION
        // ============================================================

        public InventoryControl()
        {
            _categoryService = new CategoryService(new CategoryRepository());

            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(UiFactory.PagePadding);

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
                Font = new Font(Theme.UiFontFamily, 11F, FontStyle.Regular),
                ItemSize = new Size(200, 42),
                SizeMode = TabSizeMode.Fixed,
                Appearance = TabAppearance.Normal,
                Padding = new Point(20, 8)
            };

            _stockLevelsTab = new TabPage("Stock Levels")
            {
                BackColor = Theme.Background,
                Padding = Padding.Empty
            };
            _movementHistoryTab = new TabPage("Movement History")
            {
                BackColor = Theme.Background,
                Padding = Padding.Empty
            };

            BuildStockLevelsTab();
            BuildMovementHistoryTab();

            _tabs.TabPages.Add(_stockLevelsTab);
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
            var bar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 128,
                BackColor = Theme.Background
            };

            // ---- Row 1: Search + Category + Count ----
            _slSearchBox = UiFactory.CreateTextBox(300);
            _slSearchBox.Location = new Point(0, 16);
            _slSearchBox.Placeholder = "Search product, brand, or SKU…";
            _slSearchBox.MaxLength = 150;
            _slSearchBox.TextChanged += (s, e) => ApplyStockLevelsFilter();

            var lblCategory = new Label
            {
                Text = "Category:",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(80, 24),
                Location = new Point(_slSearchBox.Right + 24, 24),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _slCategoryFilter = new RoundedComboBox
            {
                Width = 200,
                Location = new Point(lblCategory.Right + 8, 16)
            };
            _slCategoryFilter.SelectedIndexChanged += (s, e) => ApplyStockLevelsFilter();

            _slCountLabel = new Label
            {
                Text = "Loading…",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(_slCategoryFilter.Right + 24, 26),
                Height = 24,
                Width = 400,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            bar.Controls.Add(_slSearchBox);
            bar.Controls.Add(lblCategory);
            bar.Controls.Add(_slCategoryFilter);
            bar.Controls.Add(_slCountLabel);

            // ---- Row 2: Status chips ----
            var statuses = new (string Label, string Key)[]
            {
                ("All",           "all"),
                ("OK",            "ok"),
                ("Low",           "low"),
                ("Out of Stock",  "out")
            };

            int x = 0;
            foreach (var (label, key) in statuses)
            {
                var chip = CreateStatusChip(label, key);
                chip.Location = new Point(x, 76);
                bar.Controls.Add(chip);
                _slStatusChips.Add(chip);
                x += chip.Width + 8;
            }
            SetActiveStatusChip(_slStatusChips[0]); // Default = All

            // ---- Grid ----
            _slGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 0, 0)
            };
            UiFactory.StyleGridModern(_slGrid);
            _slGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            _slGrid.CellClick += SlGrid_CellClick;
            _slGrid.CellMouseEnter += SlGrid_CellMouseEnter;
            _slGrid.CellMouseLeave += (s, e) => _slGrid.Cursor = Cursors.Default;
            _slGrid.CellPainting += SlGrid_CellPainting;
            _slGrid.CellDoubleClick += async (s, e) =>
            {
                if (e.RowIndex < 0) return;
                await OpenAdjustForRowAsync(e.RowIndex);
            };

            PrimeStockLevelsColumns();

            _stockLevelsTab.Controls.Add(_slGrid);
            _stockLevelsTab.Controls.Add(bar);
        }

        private void SlGrid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (_slGrid.Columns[e.ColumnIndex].Name != "Adjust") return;

            e.PaintBackground(e.CellBounds, true);

            // Always paint with the real color — no white-on-selection transparency.
            using var font = new Font(Theme.IconFontFamily, 14F, FontStyle.Regular);

            TextRenderer.DrawText(
                e.Graphics,
                AdjustIcon,
                _adjustIconFont,
                e.CellBounds,
                Theme.Primary,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix);

            e.Handled = true;
        }

        private Button CreateStatusChip(string label, string key)
        {
            var chip = new Button
            {
                Text = label,
                Font = Theme.FontBodyBold,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Surface,
                ForeColor = Theme.TextSecondary,
                Cursor = Cursors.Hand,
                Height = 40,
                AutoSize = false,
                Tag = key,
                Padding = new Padding(0)
            };
            chip.FlatAppearance.BorderSize = 1;
            chip.FlatAppearance.BorderColor = Theme.PanelBorder;
            chip.FlatAppearance.MouseOverBackColor = Color.FromArgb(240, 244, 255);

            using (var g = chip.CreateGraphics())
            {
                var size = g.MeasureString(label, chip.Font);
                chip.Width = (int)size.Width + 32;
            }

            Theme.ApplyRoundedRegion(chip, Theme.Radius);
            chip.Resize += (s, e) => Theme.ApplyRoundedRegion(chip, Theme.Radius);

            chip.Click += (s, e) =>
            {
                SetActiveStatusChip(chip);
                ApplyStockLevelsFilter();
            };

            return chip;
        }

        private void SetActiveStatusChip(Button chip)
        {
            _slActiveStatusChip = chip;

            foreach (var c in _slStatusChips)
            {
                bool active = (c == chip);
                c.BackColor = active ? Theme.Primary : Theme.Surface;
                c.ForeColor = active ? Color.White : Theme.TextSecondary;
                c.FlatAppearance.BorderColor = active ? Theme.Primary : Theme.PanelBorder;
                c.FlatAppearance.MouseOverBackColor = active
                    ? Theme.PrimaryHover
                    : Color.FromArgb(240, 244, 255);
            }
        }

        private void PrimeStockLevelsColumns()
        {
            _slGrid.AutoGenerateColumns = false;
            _slGrid.Columns.Clear();
            _slGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

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
                _slGrid.Columns.Add(col);
            }

            // Product width is applied dynamically in FitStockLevelsProductColumn().
            AddTextCol("Product", "Product", 400, true);
            AddTextCol("Brand", "Brand", ColBrandW, true);
            AddTextCol("Category", "Category", ColCategoryW, true);
            AddTextCol("Unit", "Unit", ColUnitW, true, DataGridViewContentAlignment.MiddleCenter);
            AddTextCol("OnHand", "On Hand", ColOnHandW, true, DataGridViewContentAlignment.MiddleRight);
            AddTextCol("Reorder", "Reorder", ColReorderW, true, DataGridViewContentAlignment.MiddleRight);
            AddTextCol("Status", "Status", ColStatusW, true, DataGridViewContentAlignment.MiddleCenter);

            // Adjust action column — pencil icon. Fixed width, no sort glyph,
            // no user resize — keeps the column perfectly stable across refreshes.
            // Adjust action column — pencil icon, painted via CellPainting.
            var adjustCol = new DataGridViewTextBoxColumn
            {
                Name = "Adjust",
                HeaderText = "",
                Width = ColAdjustW,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                Resizable = DataGridViewTriState.False
            };
            _slGrid.Columns.Add(adjustCol);

            _slGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ProductId",
                DataPropertyName = "ProductId",
                Visible = false
            });

            _slGrid.Resize += (s, e) => FitStockLevelsProductColumn();
        }

        private void FitStockLevelsProductColumn()
        {
            if (_slGrid == null || !_slGrid.Columns.Contains("Product")) return;

            int others =
                ColBrandW + ColCategoryW + ColUnitW +
                ColOnHandW + ColReorderW + ColStatusW + ColAdjustW;

            // 24px allowance for the vertical scrollbar and grid chrome.
            int available = _slGrid.ClientSize.Width - others - 24;
            if (available < 220) available = 220;

            _slGrid.Columns["Product"].Width = available;
        }

        private void SlGrid_CellMouseEnter(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            _slGrid.Cursor = _slGrid.Columns[e.ColumnIndex].Name == "Adjust"
                ? Cursors.Hand
                : Cursors.Default;
        }

        private async void SlGrid_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (_slGrid.Columns[e.ColumnIndex].Name != "Adjust") return;

            await OpenAdjustForRowAsync(e.RowIndex);

            // Clear selection so the row doesn't stay highlighted after adjust.
            _slGrid.ClearSelection();
        }

        private async Task OpenAdjustForRowAsync(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _slGrid.Rows.Count) return;

            var idCell = _slGrid.Rows[rowIndex].Cells["ProductId"];
            if (idCell?.Value == null) return;

            int id = Convert.ToInt32(idCell.Value);
            var product = _allProducts.FirstOrDefault(p => p.ProductId == id);
            if (product == null)
            {
                ShowStockLevelsStatus("Product not found. Please refresh.", true);
                return;
            }

            try
            {
                using var form = new AdjustStockForm(product);
                var result = form.ShowDialog(FindForm());

                if (result == DialogResult.OK)
                {
                    ShowStockLevelsStatus("Stock adjusted successfully.", false);
                    await ReloadAllAsync();
                }
            }
            catch (Exception ex)
            {
                ShowStockLevelsStatus($"Could not open Adjust Stock: {ex.Message}", true);
            }
        }

        // ============================================================
        // TAB 2 — MOVEMENT HISTORY
        // ============================================================

        private void BuildMovementHistoryTab()
        {
            _mhFilterBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 128,
                BackColor = Theme.Background
            };

            // ---- Row 1: Search + Type + Count ----
            _mhSearchBox = UiFactory.CreateTextBox(280);
            _mhSearchBox.Location = new Point(0, 16);
            _mhSearchBox.Placeholder = "Search product or brand…";
            _mhSearchBox.MaxLength = 150;
            _mhSearchBox.TextChanged += async (s, e) => await LoadMovementHistoryAsync();

            var lblType = new Label
            {
                Text = "Type:",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(48, 24),
                Location = new Point(_mhSearchBox.Right + 24, 24),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _mhTypeFilter = new RoundedComboBox
            {
                Width = 160,
                Location = new Point(lblType.Right + 8, 16)
            };
            _mhTypeFilter.Items.Add("All");
            _mhTypeFilter.Items.Add("Sale");
            _mhTypeFilter.Items.Add("Stock-In");
            _mhTypeFilter.Items.Add("Adjustment");
            _mhTypeFilter.SelectedIndex = 0;
            _mhTypeFilter.SelectedIndexChanged += async (s, e) => await LoadMovementHistoryAsync();

            _mhCountLabel = new Label
            {
                Text = "Loading…",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(_mhTypeFilter.Right + 24, 26),
                Height = 24,
                Width = 500,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            _mhFilterBar.Controls.Add(_mhSearchBox);
            _mhFilterBar.Controls.Add(lblType);
            _mhFilterBar.Controls.Add(_mhTypeFilter);
            _mhFilterBar.Controls.Add(_mhCountLabel);

            // ---- Row 2: Date chips ----
            var presets = new (string Label, string Key)[]
            {
                ("Today",        "today"),
                ("Last 7 days",  "7d"),
                ("Last 30 days", "30d"),
                ("This month",   "month"),
                ("Custom",       "custom")
            };

            int x = 0;
            foreach (var (label, key) in presets)
            {
                var chip = CreateDateChip(label, key);
                chip.Location = new Point(x, 76);
                _mhFilterBar.Controls.Add(chip);
                _mhDateChips.Add(chip);
                x += chip.Width + 8;
            }

            // ---- Custom pickers (hidden by default) ----
            _mhCustomPickerPanel = new Panel
            {
                Location = new Point(x + 8, 76),
                Height = 40,
                Width = 424,
                BackColor = Color.Transparent,
                Visible = false
            };

            var lblFrom = new Label
            {
                Text = "From",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(44, 40),
                Location = new Point(0, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _mhFromDate = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "MMM d, yyyy",
                Font = Theme.FontBody,
                Location = new Point(48, 6),
                Size = new Size(160, 32),
                MaxDate = DateTime.Today,
                Value = DateTime.Today.AddDays(-29)
            };
            _mhFromDate.ValueChanged += async (s, e) => await LoadMovementHistoryAsync();

            var lblTo = new Label
            {
                Text = "To",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(28, 40),
                Location = new Point(216, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _mhToDate = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "MMM d, yyyy",
                Font = Theme.FontBody,
                Location = new Point(248, 6),
                Size = new Size(160, 32),
                MaxDate = DateTime.Today,
                Value = DateTime.Today
            };
            _mhToDate.ValueChanged += async (s, e) => await LoadMovementHistoryAsync();

            _mhCustomPickerPanel.Controls.Add(lblFrom);
            _mhCustomPickerPanel.Controls.Add(_mhFromDate);
            _mhCustomPickerPanel.Controls.Add(lblTo);
            _mhCustomPickerPanel.Controls.Add(_mhToDate);
            _mhFilterBar.Controls.Add(_mhCustomPickerPanel);

            // Default = Last 30 days
            var defaultChip = _mhDateChips.FirstOrDefault(c => (string?)c.Tag == "30d");
            if (defaultChip != null) SetActiveDateChip(defaultChip);
            _mhFromDate.Value = DateTime.Today.AddDays(-29);
            _mhToDate.Value = DateTime.Today;

            // ---- Grid ----
            _mhGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 0, 0)
            };
            UiFactory.StyleGridModern(_mhGrid);
            _mhGrid.CellFormatting += MhGrid_CellFormatting;
            PrimeMovementGridColumns();

            // ---- Bottom bar ----
            var bottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 88,
                BackColor = Theme.Background
            };

            var btnRefresh = UiFactory.CreateButton("Refresh", UiFactory.ButtonStyle.Primary,
                140, UiFactory.PrimaryButtonHeight);
            btnRefresh.Location = new Point(0, 20);
            btnRefresh.Click += async (s, e) => await LoadMovementHistoryAsync();

            _mhStatusLabel = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(btnRefresh.Right + 24, 20),
                Height = UiFactory.PrimaryButtonHeight,
                Width = 500,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            bottom.Controls.Add(btnRefresh);
            bottom.Controls.Add(_mhStatusLabel);

            _movementHistoryTab.Controls.Add(_mhGrid);
            _movementHistoryTab.Controls.Add(bottom);
            _movementHistoryTab.Controls.Add(_mhFilterBar);
        }

        private Button CreateDateChip(string label, string key)
        {
            var chip = new Button
            {
                Text = label,
                Font = Theme.FontBodyBold,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Surface,
                ForeColor = Theme.TextSecondary,
                Cursor = Cursors.Hand,
                Height = 40,
                AutoSize = false,
                Tag = key,
                Padding = new Padding(0)
            };
            chip.FlatAppearance.BorderSize = 1;
            chip.FlatAppearance.BorderColor = Theme.PanelBorder;
            chip.FlatAppearance.MouseOverBackColor = Color.FromArgb(240, 244, 255);

            using (var g = chip.CreateGraphics())
            {
                var size = g.MeasureString(label, chip.Font);
                chip.Width = (int)size.Width + 32;
            }

            Theme.ApplyRoundedRegion(chip, Theme.Radius);
            chip.Resize += (s, e) => Theme.ApplyRoundedRegion(chip, Theme.Radius);

            chip.Click += async (s, e) =>
            {
                SetActiveDateChip(chip);
                await ApplyDatePresetAsync(key);
            };

            return chip;
        }

        private void SetActiveDateChip(Button chip)
        {
            _mhActiveDateChip = chip;

            foreach (var c in _mhDateChips)
            {
                bool active = (c == chip);
                c.BackColor = active ? Theme.Primary : Theme.Surface;
                c.ForeColor = active ? Color.White : Theme.TextSecondary;
                c.FlatAppearance.BorderColor = active ? Theme.Primary : Theme.PanelBorder;
                c.FlatAppearance.MouseOverBackColor = active
                    ? Theme.PrimaryHover
                    : Color.FromArgb(240, 244, 255);
            }
        }

        private async Task ApplyDatePresetAsync(string key)
        {
            DateTime today = DateTime.Today;

            switch (key)
            {
                case "today":
                    _mhFromDate.Value = today;
                    _mhToDate.Value = today;
                    _mhCustomPickerPanel.Visible = false;
                    break;
                case "7d":
                    _mhFromDate.Value = today.AddDays(-6);
                    _mhToDate.Value = today;
                    _mhCustomPickerPanel.Visible = false;
                    break;
                case "30d":
                    _mhFromDate.Value = today.AddDays(-29);
                    _mhToDate.Value = today;
                    _mhCustomPickerPanel.Visible = false;
                    break;
                case "month":
                    _mhFromDate.Value = new DateTime(today.Year, today.Month, 1);
                    _mhToDate.Value = today;
                    _mhCustomPickerPanel.Visible = false;
                    break;
                case "custom":
                    _mhCustomPickerPanel.Visible = true;
                    _mhFromDate.Focus();
                    return; // wait for the user to change dates
            }

            await LoadMovementHistoryAsync();
        }

        private void PrimeMovementGridColumns()
        {
            _mhGrid.AutoGenerateColumns = false;
            _mhGrid.Columns.Clear();

            void AddTextCol(string prop, string header, int weight, bool readOnly,
                DataGridViewContentAlignment align = DataGridViewContentAlignment.MiddleLeft)
            {
                var col = new DataGridViewTextBoxColumn
                {
                    Name = prop,
                    DataPropertyName = prop,
                    HeaderText = header,
                    FillWeight = weight,
                    ReadOnly = readOnly,
                    SortMode = DataGridViewColumnSortMode.NotSortable
                };
                if (align != DataGridViewContentAlignment.MiddleLeft)
                    col.DefaultCellStyle.Alignment = align;
                _mhGrid.Columns.Add(col);
            }

            AddTextCol("DateDisplay", "Date & Time", 180, true);
            AddTextCol("Item", "Item", 260, true);
            AddTextCol("TypeLabel", "Type", 90, true, DataGridViewContentAlignment.MiddleCenter);
            AddTextCol("StockIn", "Stock In", 80, true, DataGridViewContentAlignment.MiddleRight);
            AddTextCol("StockOut", "Stock Out", 85, true, DataGridViewContentAlignment.MiddleRight);
            AddTextCol("StartQty", "Start Qty", 80, true, DataGridViewContentAlignment.MiddleRight);
            AddTextCol("EndQty", "End Qty", 80, true, DataGridViewContentAlignment.MiddleRight);
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
                e.CellStyle.Font = Theme.FontBodyBold;
            }
            else if (colName == "StockOut" && !string.IsNullOrEmpty(row.Cells["StockOut"].Value?.ToString()))
            {
                e.CellStyle.ForeColor = Theme.Danger;
                e.CellStyle.Font = Theme.FontBodyBold;
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
        // STOCK LEVELS LOGIC
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

            string statusFilterKey = (_slActiveStatusChip?.Tag as string) ?? "all";

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
                {
                    switch (statusFilterKey)
                    {
                        case "ok": return r.Status == "OK";
                        case "low": return r.Status == "Low";
                        case "out": return r.Status == "Out of stock";
                        default: return true;
                    }
                })
                .ToList();

            _slGrid.DataSource = rows;

            if (_slGrid.Columns.Contains("ProductId"))
                _slGrid.Columns["ProductId"].Visible = false;

            if (_slGrid.Rows.Count > 0)
                _slGrid.ClearSelection();

            foreach (DataGridViewRow row in _slGrid.Rows)
            {
                if (row.Cells["Status"].Value is string s)
                {
                    if (s == "Out of stock")
                    {
                        row.Cells["Status"].Style.ForeColor = Theme.Danger;
                        row.Cells["Status"].Style.Font = Theme.FontBodyBold;
                    }
                    else if (s == "Low")
                    {
                        row.Cells["Status"].Style.ForeColor = Color.FromArgb(200, 130, 40);
                        row.Cells["Status"].Style.Font = Theme.FontBodyBold;
                    }
                    else
                    {
                        row.Cells["Status"].Style.ForeColor = Theme.Success;
                        row.Cells["Status"].Style.Font = Theme.FontBody;
                    }
                }
            }

            int totalActive = _allProducts.Count(p => p.IsActive);
            _slCountLabel.Text = $"Showing {rows.Count} of {totalActive} active item(s)";

            // Recalculate Product column after any row count change
            // (scrollbar may appear/disappear).
            FitStockLevelsProductColumn();
        }

        // ============================================================
        // MOVEMENT HISTORY LOGIC
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
            DateTime toExclusive = to.AddDays(1);

            string? search = string.IsNullOrWhiteSpace(_mhSearchBox.Text) ? null : _mhSearchBox.Text.Trim();
            string? type = _mhTypeFilter.SelectedItem as string;
            if (type == "All") type = null;
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
                    $"Showing {rows.Count} movement(s) from {from:MMM d, yyyy} to {to:MMM d, yyyy}";
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
            _slSearchBox.Enabled = !busy;
            _slCategoryFilter.Enabled = !busy;
            _slGrid.Enabled = !busy;
        }

        private void ShowStockLevelsStatus(string message, bool isError)
        {
            if (_slCountLabel == null) return;
            _slCountLabel.ForeColor = isError ? Theme.Danger : Theme.Success;
            _slCountLabel.Text = message ?? string.Empty;
        }
    }
}