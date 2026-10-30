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
    public class SalesHistoryControl : UserControl
    {
        private readonly SalesService _salesService = new();

        private List<Sale> _allSales = new();

        private readonly DateTime? _initialFrom;
        private readonly DateTime? _initialTo;

        private bool _isLoaded;
        private bool _isBusy;

        private RoundedTextBox _searchBox;
        private DateTimePicker _dtpFrom;
        private DateTimePicker _dtpTo;
        private Panel _customPickerPanel;
        private Button _activeChip;
        private readonly List<Button> _presetChips = new();
        private Label _countLabel;

        private DataGridView _grid;

        private Button _btnRefresh;
        private Label _statusLabel;

        public SalesHistoryControl() : this(null, null) { }

        public SalesHistoryControl(DateTime? initialFrom, DateTime? initialTo)
        {
            _initialFrom = initialFrom?.Date;
            _initialTo = initialTo?.Date;

            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(UiFactory.PagePadding);

            BuildLayout();

            Load += SalesHistoryControl_Load;
        }

        private async void SalesHistoryControl_Load(object sender, EventArgs e)
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
            Controls.Add(BuildGrid());
            Controls.Add(BuildBottomBar());
            Controls.Add(BuildFilterBar());
        }

        private Panel BuildFilterBar()
        {
            var bar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 128,
                BackColor = Theme.Background
            };

            // ---- Row 1: Search + count ----
            _searchBox = UiFactory.CreateTextBox(320);
            _searchBox.Location = new Point(0, 16);
            _searchBox.Placeholder = "Search by invoice number…";
            _searchBox.MaxLength = 100;
            _searchBox.TextChanged += (s, e) => ApplyFilters();

            _countLabel = new Label
            {
                Text = "Loading…",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(344, 26),
                Height = 24,
                Width = 600,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            bar.Controls.Add(_searchBox);
            bar.Controls.Add(_countLabel);

            // ---- Row 2: Preset chips ----
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
                var chip = CreatePresetChip(label, key);
                chip.Location = new Point(x, 72);
                bar.Controls.Add(chip);
                _presetChips.Add(chip);
                x += chip.Width + 8;
            }

            // ---- Custom pickers (hidden until Custom chip is selected) ----
            _customPickerPanel = new Panel
            {
                Location = new Point(x + 8, 72),
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

            DateTime fromValue = ResolveFromValue();

            _dtpFrom = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "MMM d, yyyy",
                Font = Theme.FontBody,
                Location = new Point(48, 6),
                Size = new Size(160, 32),
                MaxDate = DateTime.Today,
                Value = fromValue
            };
            _dtpFrom.ValueChanged += (s, e) => ApplyFilters();

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

            DateTime toValue = ResolveToValue(fromValue);

            _dtpTo = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "MMM d, yyyy",
                Font = Theme.FontBody,
                Location = new Point(248, 6),
                Size = new Size(160, 32),
                MaxDate = DateTime.Today,
                Value = toValue
            };
            _dtpTo.ValueChanged += (s, e) => ApplyFilters();

            _customPickerPanel.Controls.Add(lblFrom);
            _customPickerPanel.Controls.Add(_dtpFrom);
            _customPickerPanel.Controls.Add(lblTo);
            _customPickerPanel.Controls.Add(_dtpTo);
            bar.Controls.Add(_customPickerPanel);

            // Initial state: if a range was provided, start in Custom mode.
            if (_initialFrom.HasValue || _initialTo.HasValue)
            {
                var customChip = _presetChips.FirstOrDefault(c => (string?)c.Tag == "custom");
                if (customChip != null) SetActiveChip(customChip);
                _customPickerPanel.Visible = true;
            }
            else
            {
                SetActiveChip(_presetChips[0]); // "Today"
            }

            return bar;
        }

        private DateTime ResolveFromValue()
        {
            DateTime v = _initialFrom ?? DateTime.Today;
            if (v > DateTime.Today) v = DateTime.Today;
            return v;
        }

        private DateTime ResolveToValue(DateTime fromValue)
        {
            DateTime v = _initialTo ?? DateTime.Today;
            if (v > DateTime.Today) v = DateTime.Today;
            if (v < fromValue) v = fromValue;
            return v;
        }

        private Button CreatePresetChip(string label, string key)
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
                SetActiveChip(chip);
                ApplyPreset(key);
            };

            return chip;
        }

        private void SetActiveChip(Button chip)
        {
            _activeChip = chip;

            foreach (var c in _presetChips)
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

        private void ApplyPreset(string key)
        {
            DateTime today = DateTime.Today;

            switch (key)
            {
                case "today":
                    _dtpFrom.Value = today;
                    _dtpTo.Value = today;
                    _customPickerPanel.Visible = false;
                    break;
                case "7d":
                    _dtpFrom.Value = today.AddDays(-6);
                    _dtpTo.Value = today;
                    _customPickerPanel.Visible = false;
                    break;
                case "30d":
                    _dtpFrom.Value = today.AddDays(-29);
                    _dtpTo.Value = today;
                    _customPickerPanel.Visible = false;
                    break;
                case "month":
                    _dtpFrom.Value = new DateTime(today.Year, today.Month, 1);
                    _dtpTo.Value = today;
                    _customPickerPanel.Visible = false;
                    break;
                case "custom":
                    _customPickerPanel.Visible = true;
                    _dtpFrom.Focus();
                    return; // wait for user input
            }

            ApplyFilters();
        }

        private DataGridView BuildGrid()
        {
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 0, 0)
            };
            UiFactory.StyleGridModern(_grid);
            _grid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                OpenDetailForSelected();
            };
            _grid.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    OpenDetailForSelected();
                }
            };
            return _grid;
        }

        private Panel BuildBottomBar()
        {
            var bar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 80,
                BackColor = Theme.Background
            };

            _statusLabel = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(4, 24),
                Height = 40,
                Width = 600,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            _btnRefresh = UiFactory.CreateButton("Refresh", UiFactory.ButtonStyle.Primary, 140, UiFactory.PrimaryButtonHeight);
            _btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnRefresh.Click += async (s, e) => await ReloadAllAsync();

            bar.Controls.Add(_statusLabel);
            bar.Controls.Add(_btnRefresh);

            bar.Resize += (s, e) =>
            {
                _btnRefresh.Location = new Point(bar.ClientSize.Width - _btnRefresh.Width, 24);
                _statusLabel.Width = Math.Max(100, _btnRefresh.Left - _statusLabel.Left - 16);
            };

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
                var (success, error, sales) = await _salesService.GetAllSalesAsync();
                if (!success)
                {
                    _allSales = new List<Sale>();
                    ShowStatus(error, isError: true);
                    ApplyFilters();
                    return;
                }

                _allSales = sales ?? new List<Sale>();
                ApplyFilters();
            }
            finally
            {
                SetBusy(false);
            }
        }

        private (DateTime From, DateTime ToExclusive) GetSelectedRange()
        {
            DateTime from = _dtpFrom.Value.Date;
            DateTime to = _dtpTo.Value.Date;
            if (to < from) to = from;
            return (from, to.AddDays(1));
        }

        private void ApplyFilters()
        {
            if (_allSales == null) return;

            var (from, to) = GetSelectedRange();

            IEnumerable<Sale> filtered = _allSales
                .Where(s => s.SaleDate >= from && s.SaleDate < to);

            string search = (_searchBox?.Text ?? string.Empty).Trim();
            if (search.Length > 0)
            {
                filtered = filtered.Where(s =>
                    !string.IsNullOrEmpty(s.InvoiceNo) &&
                    s.InvoiceNo.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            var rows = filtered
                .OrderByDescending(s => s.SaleDate)
                .ThenByDescending(s => s.SaleId)
                .Select(s => new
                {
                    s.SaleId,
                    Invoice = s.InvoiceNo ?? string.Empty,
                    Date = s.SaleDate.ToString("MMM d, yyyy h:mm tt"),
                    Customer = string.IsNullOrEmpty(s.CustomerName) ? "Walk-in" : s.CustomerName,
                    Payment = s.PaymentMethod ?? string.Empty,
                    Subtotal = s.Subtotal.ToString("N2")
                })
                .ToList();

            _grid.DataSource = rows;

            if (_grid.Columns.Contains("SaleId"))
                _grid.Columns["SaleId"].Visible = false;

            void SetWeight(string col, int weight, string header = null)
            {
                if (!_grid.Columns.Contains(col)) return;
                _grid.Columns[col].FillWeight = weight;
                if (header != null) _grid.Columns[col].HeaderText = header;
            }

            SetWeight("Invoice", 170, "Invoice No.");
            SetWeight("Date", 190, "Date");
            SetWeight("Customer", 180, "Customer");
            SetWeight("Payment", 100, "Payment");
            SetWeight("Subtotal", 110, "Subtotal (PHP)");

            if (_grid.Rows.Count > 0)
                _grid.ClearSelection();

            _countLabel.Text = $"Showing {rows.Count} sale(s)";
        }

        private Sale? GetSelectedSale()
        {
            if (_grid == null || _grid.SelectedRows.Count == 0) return null;
            var row = _grid.SelectedRows[0];
            if (!_grid.Columns.Contains("SaleId")) return null;

            var val = row.Cells["SaleId"].Value;
            if (val == null) return null;

            int id = Convert.ToInt32(val);
            return _allSales.FirstOrDefault(s => s.SaleId == id);
        }

        private void OpenDetailForSelected()
        {
            var sale = GetSelectedSale();
            if (sale == null)
            {
                ShowStatus("Please select a sale first.", isError: true);
                return;
            }

            try
            {
                var parent = FindForm();
                using var form = new SaleDetailForm(sale.SaleId);
                form.ShowDialog(parent);
                _ = ReloadAllAsync();
            }
            catch (Exception ex)
            {
                ShowStatus($"Could not open sale detail: {ex.Message}", isError: true);
            }
        }

        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            _btnRefresh.Enabled = !busy;
        }

        private void ShowStatus(string message, bool isError)
        {
            if (_statusLabel == null) return;
            _statusLabel.ForeColor = isError ? Theme.Danger : Theme.Success;
            _statusLabel.Text = message ?? string.Empty;
        }
    }
}