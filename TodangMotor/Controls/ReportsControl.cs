using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TodangMotor.Common;
using TodangMotor.Data;
using TodangMotor.Models;
using TodangMotor.Services;

namespace TodangMotor.Controls
{
    /// <summary>
    /// Reports Hub — Owner-only. Currently a single Sales Revenue Report.
    /// Date chips pick the range; the preview updates automatically;
    /// "Generate PDF" exports whatever is currently shown.
    /// </summary>
    public class ReportsControl : UserControl
    {
        private readonly SalesService _salesService = new();

        private bool _isBusy;
        private RevenueReportResult? _currentResult;
        private string _currentRangeLabel = string.Empty;

        // ---- Date chips ----
        private readonly List<Button> _dateChips = new();
        private Button? _activeDateChip;
        private Panel _customPickerPanel = null!;
        private DateTimePicker _dtpFrom = null!;
        private DateTimePicker _dtpTo = null!;

        // ---- Preview ----
        private Label _countLabel = null!;
        private DataGridView _previewGrid = null!;
        private Label _revenueValue = null!;
        private Label _costValue = null!;
        private Label _netValue = null!;
        private Label _txValue = null!;

        // ---- Actions ----
        private Button _btnGenerate = null!;
        private Label _lblStatus = null!;

        // Fixed preview grid widths (Date is dynamic).
        private const int ColTxW = 150;
        private const int ColRevW = 150;
        private const int ColCostW = 150;
        private const int ColNetW = 180;

        public ReportsControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(UiFactory.PagePadding);

            BuildLayout();

            Load += ReportsControl_Load;
        }

        private async void ReportsControl_Load(object? sender, EventArgs e)
        {
            await RefreshPreviewAsync();
        }

        // ============================================================
        // LAYOUT
        // ============================================================

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Theme.Background,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));   // date chips
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // main
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));   // bottom bar

            root.Controls.Add(BuildFilterBar(), 0, 0);
            root.Controls.Add(BuildMainArea(), 0, 1);
            root.Controls.Add(BuildBottomBar(), 0, 2);

            Controls.Add(root);
        }

        private Panel BuildFilterBar()
        {
            var bar = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Background
            };

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
                chip.Location = new Point(x, 12);
                bar.Controls.Add(chip);
                _dateChips.Add(chip);
                x += chip.Width + 8;
            }

            // ---- Custom pickers (hidden until "Custom" is selected) ----
            _customPickerPanel = new Panel
            {
                Location = new Point(x + 8, 12),
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

            _dtpFrom = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "MMM d, yyyy",
                Font = Theme.FontBody,
                Location = new Point(48, 6),
                Size = new Size(160, 32),
                MaxDate = DateTime.Today,
                Value = DateTime.Today.AddDays(-29)
            };
            _dtpFrom.ValueChanged += async (s, e) => await RefreshPreviewAsync();

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

            _dtpTo = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "MMM d, yyyy",
                Font = Theme.FontBody,
                Location = new Point(248, 6),
                Size = new Size(160, 32),
                MaxDate = DateTime.Today,
                Value = DateTime.Today
            };
            _dtpTo.ValueChanged += async (s, e) => await RefreshPreviewAsync();

            _customPickerPanel.Controls.Add(lblFrom);
            _customPickerPanel.Controls.Add(_dtpFrom);
            _customPickerPanel.Controls.Add(lblTo);
            _customPickerPanel.Controls.Add(_dtpTo);
            bar.Controls.Add(_customPickerPanel);

            // Default = Last 30 days
            var defaultChip = _dateChips.FirstOrDefault(c => (string?)c.Tag == "30d");
            if (defaultChip != null) SetActiveDateChip(defaultChip);
            _dtpFrom.Value = DateTime.Today.AddDays(-29);
            _dtpTo.Value = DateTime.Today;

            return bar;
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
            _activeDateChip = chip;

            foreach (var c in _dateChips)
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
                    return; // wait for user to change dates
            }

            await RefreshPreviewAsync();
        }

        private Panel BuildMainArea()
        {
            var main = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Theme.Background,
                Margin = new Padding(0, 8, 0, 8),
                Padding = Padding.Empty
            };
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));   // title + count
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 148));  // summary cards
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // grid

            main.Controls.Add(BuildTitleRow(), 0, 0);
            main.Controls.Add(BuildSummaryRow(), 0, 1);
            main.Controls.Add(BuildPreviewGrid(), 0, 2);

            return main;
        }

        private Panel BuildTitleRow()
        {
            var row = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Background
            };

            var title = new Label
            {
                Text = "Sales Revenue Report — Preview",
                Font = new Font(Theme.UiFontFamily, 14F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(0, 12),
                Height = 32,
                Width = 500,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _countLabel = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(0, 12),
                Height = 32,
                Width = 500,
                TextAlign = ContentAlignment.MiddleRight,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            row.Controls.Add(title);
            row.Controls.Add(_countLabel);

            row.Resize += (s, e) =>
            {
                _countLabel.Left = row.ClientSize.Width - _countLabel.Width;
            };

            return row;
        }

        private TableLayoutPanel BuildSummaryRow()
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Theme.Background,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            for (int i = 0; i < 4; i++)
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            row.Controls.Add(BuildStatCard("Total Revenue", out _revenueValue), 0, 0);
            row.Controls.Add(BuildStatCard("Total Cost", out _costValue), 1, 0);
            row.Controls.Add(BuildStatCard("Net Income", out _netValue), 2, 0);
            row.Controls.Add(BuildStatCard("Transactions", out _txValue), 3, 0);

            return row;
        }

        private RoundedPanel BuildStatCard(string titleText, out Label valueLabel)
        {
            var card = UiFactory.CreateCard();
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(12, 8, 12, 8);

            var title = new Label
            {
                Text = titleText,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(20, 16),
                Height = 20,
                Width = 240,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            var value = new Label
            {
                Text = "—",
                Font = new Font(Theme.UiFontFamily, 20F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(20, 52),
                Height = 50,
                Width = 260,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };
            valueLabel = value;

            card.Controls.Add(title);
            card.Controls.Add(value);

            return card;
        }

        private DataGridView BuildPreviewGrid()
        {
            _previewGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(8, 0, 8, 0),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true
            };
            UiFactory.StyleGridModern(_previewGrid);
            _previewGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            PrimePreviewColumns();

            _previewGrid.Resize += (s, e) => FitPreviewDateColumn();

            return _previewGrid;
        }

        private void PrimePreviewColumns()
        {
            _previewGrid.AutoGenerateColumns = false;
            _previewGrid.Columns.Clear();

            void AddTextCol(string prop, string header, int width,
                DataGridViewContentAlignment align = DataGridViewContentAlignment.MiddleLeft)
            {
                var col = new DataGridViewTextBoxColumn
                {
                    Name = prop,
                    DataPropertyName = prop,
                    HeaderText = header,
                    Width = width,
                    ReadOnly = true,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                    Resizable = DataGridViewTriState.False
                };
                if (align != DataGridViewContentAlignment.MiddleLeft)
                    col.DefaultCellStyle.Alignment = align;
                _previewGrid.Columns.Add(col);
            }

            AddTextCol("Date", "Date", 400, DataGridViewContentAlignment.MiddleLeft);
            AddTextCol("Transactions", "Transactions", ColTxW, DataGridViewContentAlignment.MiddleRight);
            AddTextCol("Revenue", "Revenue (₱)", ColRevW, DataGridViewContentAlignment.MiddleRight);
            AddTextCol("Cost", "Cost (₱)", ColCostW, DataGridViewContentAlignment.MiddleRight);
            AddTextCol("NetIncome", "Net Income (₱)", ColNetW, DataGridViewContentAlignment.MiddleRight);
        }

        private void FitPreviewDateColumn()
        {
            if (_previewGrid == null || !_previewGrid.Columns.Contains("Date")) return;

            int others = ColTxW + ColRevW + ColCostW + ColNetW;
            int available = _previewGrid.ClientSize.Width - others - 24;
            if (available < 200) available = 200;

            _previewGrid.Columns["Date"].Width = available;
        }

        private Panel BuildBottomBar()
        {
            var bar = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Background
            };

            _lblStatus = new Label
            {
                Text = "Ready.",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(0, 20),
                Height = UiFactory.PrimaryButtonHeight,
                Width = 600,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _btnGenerate = UiFactory.CreateButton(
                "Generate PDF",
                UiFactory.ButtonStyle.Primary,
                200,
                UiFactory.PrimaryButtonHeight);
            _btnGenerate.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnGenerate.Click += async (s, e) => await GeneratePdfAsync();

            bar.Controls.Add(_lblStatus);
            bar.Controls.Add(_btnGenerate);

            bar.Resize += (s, e) =>
            {
                _btnGenerate.Left = bar.ClientSize.Width - _btnGenerate.Width;
                _lblStatus.Width = Math.Max(200, _btnGenerate.Left - 16);
            };

            return bar;
        }

        // ============================================================
        // PREVIEW REFRESH
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

        private async Task RefreshPreviewAsync()
        {
            if (_isBusy) return;

            var (from, toExclusive, rangeLabel) = GetSelectedRange();
            _currentRangeLabel = rangeLabel;

            SetBusy(true);
            try
            {
                var result = await _salesService.GenerateRevenueReportAsync(from, toExclusive, rangeLabel);

                if (!result.Success)
                {
                    _currentResult = null;
                    ClearPreview();
                    ShowStatus(result.ErrorMessage, true);
                    return;
                }

                _currentResult = result;

                // ---- Summary cards ----
                _revenueValue.Text = "₱ " + result.TotalRevenue.ToString("N2");
                _costValue.Text = "₱ " + result.TotalCost.ToString("N2");
                _netValue.Text = "₱ " + result.TotalNetIncome.ToString("N2");
                _txValue.Text = result.TotalTransactions.ToString("N0");

                // ---- Grid rows ----
                var rows = result.DailyRows
                    .OrderByDescending(r => r.SaleDay)
                    .Select(r => new
                    {
                        Date = r.SaleDay.ToString("MMM d, yyyy"),
                        Transactions = r.TransactionCount,
                        Revenue = r.Revenue.ToString("N2"),
                        Cost = r.HasCompleteCost ? r.KnownCost.ToString("N2") : "—",
                        NetIncome = r.HasCompleteCost ? r.NetIncome!.Value.ToString("N2") : "—"
                    })
                    .ToList();

                _previewGrid.DataSource = rows;
                if (_previewGrid.Rows.Count > 0)
                    _previewGrid.ClearSelection();

                FitPreviewDateColumn();

                // ---- Count label ----
                if (result.DailyRows.Count == 0)
                {
                    _countLabel.Text = "No sales in this period.";
                    _countLabel.ForeColor = Theme.TextMuted;
                    ShowStatus("No sales in the selected range.", false);
                }
                else
                {
                    _countLabel.Text = $"{result.DailyRows.Count} day(s)   ·   {rangeLabel}";
                    _countLabel.ForeColor = Theme.TextSecondary;
                    ShowStatus("Preview ready. Click Generate PDF to export.", false);
                }
            }
            catch (Exception ex)
            {
                _currentResult = null;
                ClearPreview();
                ShowStatus($"Could not load preview: {ex.Message}", true);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void ClearPreview()
        {
            _revenueValue.Text = "—";
            _costValue.Text = "—";
            _netValue.Text = "—";
            _txValue.Text = "—";
            _previewGrid.DataSource = null;
            _countLabel.Text = string.Empty;
        }

        // ============================================================
        // PDF EXPORT
        // ============================================================

        private async Task GeneratePdfAsync()
        {
            if (_isBusy) return;

            if (_currentResult == null || _currentResult.DailyRows.Count == 0)
            {
                MessageBox.Show(
                    "There is no data to export for the selected range.",
                    "Nothing to Export",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var table = BuildReportTable(_currentResult, _currentRangeLabel);

            using var sfd = new SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                DefaultExt = ".pdf",
                AddExtension = true,
                FileName = $"SalesReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf",
                InitialDirectory = GetDefaultReportFolder()
            };

            if (sfd.ShowDialog(FindForm()) != DialogResult.OK)
            {
                ShowStatus("Export cancelled.", false);
                return;
            }

            string path = sfd.FileName;
            SetBusy(true);
            try
            {
                await Task.Run(() => ReportService.ExportPdf(table, path));

                var openIt = MessageBox.Show(
                    $"PDF saved to:\n{path}\n\nOpen the folder?",
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

                ShowStatus($"Saved: {Path.GetFileName(path)}", false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not generate PDF.\n\n{ex.Message}",
                    "PDF Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                ShowStatus("PDF export failed.", true);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private static ReportTable BuildReportTable(RevenueReportResult result, string rangeLabel)
        {
            var table = new ReportTable
            {
                Title = "Sales Revenue Report",
                Subtitle = $"{rangeLabel}   ·   Generated {DateTime.Now:MMM d, yyyy h:mm tt}",
                FooterNote = result.IsCostComplete
                    ? "All rows have complete cost data. Net Income is exact."
                    : $"{result.DaysWithMissingCost} day(s) have incomplete cost data.",
                SummaryItems = new List<(string Label, string Value)>
                {
                    ("Total Revenue", "₱ " + result.TotalRevenue.ToString("N2")),
                    ("Total Cost",    "₱ " + result.TotalCost.ToString("N2")),
                    ("Net Income",    "₱ " + result.TotalNetIncome.ToString("N2")),
                    ("Transactions",  result.TotalTransactions.ToString("N0"))
                },
                Headers = new List<string>
                {
                    "Date", "Transactions", "Revenue (PHP)", "Cost (PHP)", "Net Income (PHP)"
                }
            };

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

            return table;
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
            _btnGenerate.Enabled = !busy;
            _dtpFrom.Enabled = !busy;
            _dtpTo.Enabled = !busy;
            foreach (var c in _dateChips) c.Enabled = !busy;
        }

        private void ShowStatus(string message, bool isError)
        {
            if (_lblStatus == null) return;
            _lblStatus.ForeColor = isError ? Theme.Danger : Theme.Success;
            _lblStatus.Text = message ?? string.Empty;
        }
    }
}