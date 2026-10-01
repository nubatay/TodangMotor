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
        private RoundedComboBox _periodCombo;
        private Button _btnPdf;
        private Button _btnExcel;
        private Label _countLabel;

        private DataGridView _grid;

        private Button _btnView;
        private Button _btnVoid;
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
            Controls.Add(BuildGrid());        // Fill
            Controls.Add(BuildBottomBar());   // Bottom
            Controls.Add(BuildFilterBar());   // Top
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

            // ---- From date picker ----
            var lblFrom = new Label
            {
                Text = "From:",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(50, 20),
                Location = new Point(276, 24),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _dtpFrom = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "MMM d, yyyy",
                Font = Theme.FontBody,
                Location = new Point(328, 14),
                Size = new Size(170, 32),
                MaxDate = DateTime.Today,
                Value = DateTime.Today
            };
            _dtpFrom.ValueChanged += (s, e) => ApplyFilters();

            // ---- Period dropdown ----
            var lblPeriod = new Label
            {
                Text = "Period:",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(56, 20),
                Location = new Point(510, 24),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _periodCombo = new RoundedComboBox
            {
                Width = 130,
                Location = new Point(570, 12)
            };
            _periodCombo.Items.Add("Day");
            _periodCombo.Items.Add("Week");
            _periodCombo.Items.Add("Month");
            _periodCombo.SelectedIndex = 0;
            _periodCombo.SelectedIndexChanged += (s, e) => ApplyFilters();

            // ---- PDF / Excel (right-aligned, Owner-only) ----
            _btnPdf = UiFactory.CreateButton("PDF", UiFactory.ButtonStyle.Secondary, 80, 36);
            _btnPdf.Click += async (s, e) => await GenerateReportAsync("PDF");

            _btnExcel = UiFactory.CreateButton("Excel", UiFactory.ButtonStyle.Secondary, 80, 36);
            _btnExcel.Click += async (s, e) => await GenerateReportAsync("Excel");

            // Owner-only export buttons.
            _btnPdf.Visible = SessionManager.IsOwner;
            _btnExcel.Visible = SessionManager.IsOwner;

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
            bar.Controls.Add(lblPeriod);
            bar.Controls.Add(_periodCombo);
            bar.Controls.Add(_btnPdf);
            bar.Controls.Add(_btnExcel);
            bar.Controls.Add(_countLabel);

            // Pin PDF/Excel to the right edge.
            bar.Resize += (s, e) =>
            {
                if (bar.ClientSize.Width <= 0) return;
                const int rightPad = 4;
                const int gap = 6;

                int x = bar.ClientSize.Width - rightPad;
                x -= _btnExcel.Width;
                _btnExcel.Location = new Point(x, 14);

                x -= gap;
                x -= _btnPdf.Width;
                _btnPdf.Location = new Point(x, 14);
            };

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

            _grid.CellFormatting += Grid_CellFormatting;

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

        private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (e.RowIndex >= _grid.Rows.Count) return;

            var row = _grid.Rows[e.RowIndex];
            if (!_grid.Columns.Contains("Status")) return;

            var statusVal = row.Cells["Status"].Value?.ToString();
            if (statusVal == "Void")
            {
                e.CellStyle.BackColor = Color.FromArgb(255, 235, 235);
                e.CellStyle.SelectionBackColor = Color.FromArgb(255, 220, 220);
                e.CellStyle.ForeColor = Color.FromArgb(170, 40, 40);
                e.CellStyle.SelectionForeColor = Color.FromArgb(170, 40, 40);

                if (_grid.Columns[e.ColumnIndex].Name == "Status")
                    e.CellStyle.Font = Theme.FontBodyBold;
            }
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

            _btnVoid = UiFactory.CreateButton("Void Sale", UiFactory.ButtonStyle.Ghost, 110, 40);
            _btnVoid.Location = new Point(132, 14);
            _btnVoid.Click += (s, e) => OpenDetailForSelected();

            _btnRefresh = UiFactory.CreateButton("Refresh", UiFactory.ButtonStyle.Ghost, 100, 40);
            _btnRefresh.Location = new Point(250, 14);
            _btnRefresh.Click += async (s, e) => await ReloadAllAsync();

            _statusLabel = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(360, 14),
                Height = 40,
                Width = 500,
                TextAlign = ContentAlignment.MiddleLeft,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            bar.Controls.Add(_btnView);
            bar.Controls.Add(_btnVoid);
            bar.Controls.Add(_btnRefresh);
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

        /// <summary>
        /// Reads the anchor date and period dropdown, computes
        /// (fromInclusive, toExclusive, label).
        /// Anchor is exactly what the user picked — never auto-snapped.
        /// </summary>
        private (DateTime From, DateTime ToExclusive, string Label) GetSelectedRange()
        {
            DateTime anchor = _dtpFrom.Value.Date;
            string period = _periodCombo?.SelectedItem as string ?? "Day";

            switch (period)
            {
                case "Week":
                    {
                        var to = anchor.AddDays(7);
                        return (anchor, to, $"Week starting {anchor:MMM d, yyyy}");
                    }
                case "Month":
                    {
                        var to = anchor.AddMonths(1);
                        return (anchor, to, $"Month starting {anchor:MMM d, yyyy}");
                    }
                case "Day":
                default:
                    {
                        var to = anchor.AddDays(1);
                        return (anchor, to, anchor.ToString("MMMM d, yyyy"));
                    }
            }
        }

        // ============================================================
        // FILTERS + GRID BINDING
        // ============================================================

        private void ApplyFilters()
        {
            if (_allSales == null) return;

            var (from, to, _) = GetSelectedRange();

            IEnumerable<Sale> filtered = _allSales;

            // Date filter
            filtered = filtered.Where(s => s.SaleDate >= from && s.SaleDate < to);

            // Search filter
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
                    Subtotal = s.Subtotal.ToString("N2"),
                    Status = s.Status ?? "Completed"
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
            SetWeight("Status", 100, "Status");

            if (_grid.Rows.Count > 0)
                _grid.ClearSelection();

            int completed = rows.Count(r => r.Status == "Completed");
            int voids = rows.Count(r => r.Status == "Void");

            _countLabel.Text =
                $"Showing {rows.Count} sale(s)   ·   " +
                $"Completed: {completed}   Void: {voids}";
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
        // REVENUE REPORT EXPORT
        // ============================================================

        private async Task GenerateReportAsync(string format)
        {
            if (_isBusy) return;

            if (!SessionManager.IsOwner)
            {
                MessageBox.Show(
                    "Only the Owner can generate revenue reports.",
                    "Access Denied",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            SetBusy(true);
            try
            {
                var (from, to, rangeLabel) = GetSelectedRange();

                var result = await _salesService.GenerateRevenueReportAsync(from, to, rangeLabel);

                if (!result.Success)
                {
                    MessageBox.Show(
                        result.ErrorMessage,
                        "Report Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                if (result.DailyRows.Count == 0)
                {
                    MessageBox.Show(
                        "No sales found in the selected period.",
                        "No Data",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                // Build report table.
                var table = new ReportTable
                {
                    Title = "Sales Revenue Report",
                    Subtitle = $"{rangeLabel}   ·   " +
                               $"{from:MMM d, yyyy} to {to.AddDays(-1):MMM d, yyyy}   ·   " +
                               $"Generated {DateTime.Now:MMM d, yyyy h:mm tt}",
                    FooterNote = result.IsCostComplete
                        ? "All rows have complete cost data. Net Income is exact."
                        : $"{result.DaysWithMissingCost} day(s) have incomplete cost data " +
                          "(sales made before cost tracking was enabled). " +
                          "Their Net Income is not calculated.",
                    SummaryItems = new List<(string Label, string Value)>
                    {
                        ("Total Revenue",   "₱ " + result.TotalRevenue.ToString("N2")),
                        ("Total Cost",      "₱ " + result.TotalCost.ToString("N2")),
                        ("Net Income",      "₱ " + result.TotalNetIncome.ToString("N2")),
                        ("Transactions",    result.TotalTransactions.ToString("N0")),
                        ("Voided (excl.)",  result.VoidedTransactions.ToString("N0"))
                    },
                    Headers = new List<string>
                    {
                        "Date", "Transactions", "Revenue (PHP)", "Cost (PHP)", "Net Income (PHP)"
                    }
                };

                // Rows — newest first.
                foreach (var row in result.DailyRows.OrderByDescending(r => r.SaleDay))
                {
                    string netIncome = row.HasCompleteCost
                        ? row.NetIncome!.Value.ToString("N2")
                        : "—";

                    table.Rows.Add(new List<string>
                    {
                        row.SaleDay.ToString("MMM d, yyyy"),
                        row.TransactionCount.ToString(),
                        row.Revenue.ToString("N2"),
                        row.HasCompleteCost ? row.KnownCost.ToString("N2") : "—",
                        netIncome
                    });
                }

                // Save dialog.
                string ext = format == "PDF" ? ".pdf" : ".xlsx";
                string filter = format == "PDF"
                    ? "PDF files (*.pdf)|*.pdf"
                    : "Excel files (*.xlsx)|*.xlsx";

                using var sfd = new SaveFileDialog
                {
                    Filter = filter,
                    DefaultExt = ext,
                    AddExtension = true,
                    FileName = $"SalesRevenue_{DateTime.Now:yyyyMMdd_HHmmss}{ext}",
                    InitialDirectory = GetDefaultReportFolder()
                };

                if (sfd.ShowDialog(FindForm()) != DialogResult.OK) return;

                string path = sfd.FileName;

                await Task.Run(() =>
                {
                    if (format == "PDF")
                        ReportService.ExportPdf(table, path);
                    else
                        ReportService.ExportExcel(table, path);
                });

                var openIt = MessageBox.Show(
                    $"Report saved to:\n{path}\n\nOpen the folder?",
                    "Report Saved",
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

                ShowStatus($"Report saved: {Path.GetFileName(path)}", isError: false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not generate report:\n\n{ex.Message}",
                    "Report Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

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

        // ============================================================
        // HELPERS
        // ============================================================

        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            _btnView.Enabled = !busy;
            _btnVoid.Enabled = !busy;
            _btnRefresh.Enabled = !busy;
            _btnPdf.Enabled = !busy;
            _btnExcel.Enabled = !busy;
        }

        private void ShowStatus(string message, bool isError)
        {
            if (_statusLabel == null) return;
            _statusLabel.ForeColor = isError ? Theme.Danger : Theme.Success;
            _statusLabel.Text = message ?? string.Empty;
        }
    }
}