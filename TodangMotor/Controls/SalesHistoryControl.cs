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
    /// Sales History — filterable list of sales with From/To date pickers.
    /// View-only. Revenue report generation lives in the Reports Hub (Owner-only).
    /// </summary>
    public class SalesHistoryControl : UserControl
    {
        // ============================================================
        // SERVICES
        // ============================================================

        private readonly SalesService _salesService = new();

        // ============================================================
        // STATE
        // ============================================================

        private List<Sale> _allSales = new();

        private bool _isLoaded;
        private bool _isBusy;

        // ============================================================
        // UI CONTROLS
        // ============================================================

        private RoundedTextBox _searchBox;
        private DateTimePicker _dtpFrom;
        private DateTimePicker _dtpTo;
        private Label _countLabel;

        private DataGridView _grid;

        private Button _btnView;
        private Button _btnRefresh;
        private Label _statusLabel;

        // ============================================================
        // CONSTRUCTION
        // ============================================================

        public SalesHistoryControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(Theme.SpacingLg);

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
                Height = 92,
                BackColor = Theme.Background
            };

            _searchBox = UiFactory.CreateTextBox(260);
            _searchBox.Location = new Point(4, 12);
            _searchBox.Placeholder = "Search by invoice number…";
            _searchBox.MaxLength = 100;
            _searchBox.TextChanged += (s, e) => ApplyFilters();

            var lblFrom = new Label
            {
                Text = "From:",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(46, 20),
                Location = new Point(276, 24),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _dtpFrom = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "MMM d, yyyy",
                Font = Theme.FontBody,
                Location = new Point(324, 14),
                Size = new Size(160, 32),
                MaxDate = DateTime.Today,
                Value = DateTime.Today
            };
            _dtpFrom.ValueChanged += (s, e) => ApplyFilters();

            var lblTo = new Label
            {
                Text = "To:",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(28, 20),
                Location = new Point(496, 24),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _dtpTo = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "MMM d, yyyy",
                Font = Theme.FontBody,
                Location = new Point(526, 14),
                Size = new Size(160, 32),
                MaxDate = DateTime.Today,
                Value = DateTime.Today
            };
            _dtpTo.ValueChanged += (s, e) => ApplyFilters();

            _countLabel = new Label
            {
                Text = "Loading…",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(4, 64),
                Height = 20,
                Width = 900,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            bar.Controls.Add(_searchBox);
            bar.Controls.Add(lblFrom);
            bar.Controls.Add(_dtpFrom);
            bar.Controls.Add(lblTo);
            bar.Controls.Add(_dtpTo);
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
                Height = 68,
                BackColor = Theme.Background
            };

            _btnView = UiFactory.CreateButton("View Detail", UiFactory.ButtonStyle.Secondary, 120, 40);
            _btnView.Location = new Point(4, 14);
            _btnView.Click += (s, e) => OpenDetailForSelected();

            _statusLabel = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(140, 14),
                Height = 40,
                Width = 400,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            _btnRefresh = UiFactory.CreateButton("Refresh", UiFactory.ButtonStyle.Ghost, 100, 40);
            _btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnRefresh.Click += async (s, e) => await ReloadAllAsync();

            bar.Controls.Add(_btnView);
            bar.Controls.Add(_statusLabel);
            bar.Controls.Add(_btnRefresh);

            bar.Resize += (s, e) =>
            {
                _btnRefresh.Left = bar.ClientSize.Width - _btnRefresh.Width - 4;
                _statusLabel.Width = Math.Max(100,
                    _btnRefresh.Left - _statusLabel.Left - 12);
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

        // ============================================================
        // RANGE HELPER
        // ============================================================

        private (DateTime From, DateTime ToExclusive, string Label) GetSelectedRange()
        {
            DateTime from = _dtpFrom.Value.Date;
            DateTime to = _dtpTo.Value.Date;

            if (to < from) to = from;

            string label = from == to
                ? from.ToString("MMMM d, yyyy")
                : $"{from:MMM d, yyyy} to {to:MMM d, yyyy}";

            return (from, to.AddDays(1), label);
        }

        // ============================================================
        // FILTERS + GRID BINDING
        // ============================================================

        private void ApplyFilters()
        {
            if (_allSales == null) return;

            var (from, to, _) = GetSelectedRange();

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

            SetWeight("Invoice", 160, "Invoice No.");
            SetWeight("Date", 180, "Date");
            SetWeight("Customer", 180, "Customer");
            SetWeight("Payment", 100, "Payment");
            SetWeight("Subtotal", 100, "Subtotal (PHP)");

            if (_grid.Rows.Count > 0)
                _grid.ClearSelection();

            _countLabel.Text = $"Showing {rows.Count} sale(s)";
        }

        // ============================================================
        // SELECTION
        // ============================================================

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

        // ============================================================
        // HELPERS
        // ============================================================

        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            _btnView.Enabled = !busy;
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