using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TodangMotor.Common;
using TodangMotor.Data;
using TodangMotor.Models;
using TodangMotor.Services;
using static TodangMotor.Services.DashboardService;

namespace TodangMotor.Controls
{
    public class DashboardHomeControl : UserControl
    {
        private readonly DashboardService _dashboardService = new();

        public event Action<string>? NavigateRequested;
        public event Action<DateTime, DateTime>? NavigateToSalesRequested;

        private bool _isLoading;
        private System.Windows.Forms.Timer _refreshTimer;

        // ---- Filter ----
        private DateTimePicker _dtpFrom;
        private DateTimePicker _dtpTo;
        private Panel _customPickerPanel;
        private Button _activeChip;
        private readonly List<Button> _presetChips = new();

        // ---- Cards ----
        private RoundedPanel _cardSales;
        private RoundedPanel _cardNetProfit;
        private RoundedPanel _cardTransactions;
        private RoundedPanel _cardItemsSold;
        private RoundedPanel _cardStockAlerts;

        private Label _salesValue, _salesSub;
        private Label _netProfitValue, _netProfitSub;
        private Label _transactionsValue, _transactionsSub;
        private Label _itemsSoldValue, _itemsSoldSub;
        private Label _stockAlertsValue, _stockAlertsSub;

        // ---- Charts ----
        private SalesBarChart _salesChart;
        private Label _salesChartTitle;
        private HorizontalBarChart _topProductsChart;
        private DonutChart _categoryDonut;

        // ---- Lists ----
        private FlowLayoutPanel _recentSalesList;
        private FlowLayoutPanel _stockAlertsList;

        public DashboardHomeControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);

            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(Theme.SpacingLg);

            BuildLayout();

            _refreshTimer = new System.Windows.Forms.Timer { Interval = 30_000 };
            _refreshTimer.Tick += async (s, e) => await SilentRefreshAsync();

            Load += DashboardHomeControl_Load;
            VisibleChanged += DashboardHomeControl_VisibleChanged;
        }

        private async void DashboardHomeControl_Load(object sender, EventArgs e)
        {
            _refreshTimer.Start();
            await LoadDataAsync();
        }

        private async void DashboardHomeControl_VisibleChanged(object sender, EventArgs e)
        {
            if (!Visible) return;
            if (!IsHandleCreated) return;
            if (_isLoading) return;

            await SilentRefreshAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _refreshTimer != null)
            {
                _refreshTimer.Stop();
                _refreshTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            if (_isLoading) return;
            _isLoading = true;
            try
            {
                await RenderSnapshotAsync();
            }
            finally
            {
                _isLoading = false;
            }
        }

        private System.Threading.Tasks.Task SilentRefreshAsync()
        {
            if (IsDisposed || !IsHandleCreated) return System.Threading.Tasks.Task.CompletedTask;
            return LoadDataAsync();
        }

        private (DateTime From, DateTime ToExclusive) GetSelectedRange()
        {
            DateTime from = _dtpFrom.Value.Date;
            DateTime to = _dtpTo.Value.Date;
            if (to < from) to = from;
            return (from, to.AddDays(1));
        }

        // ============================================================
        // RENDER
        // ============================================================

        private async System.Threading.Tasks.Task RenderSnapshotAsync()
        {
            var (from, toExclusive) = GetSelectedRange();

            DashboardSnapshot snap;
            try
            {
                snap = await _dashboardService.GetSnapshotAsync(from, toExclusive);
            }
            catch { return; }

            if (IsDisposed) return;

            bool isOwner = SessionManager.IsOwner;
            string vsLabel = from == toExclusive.AddDays(-1)
                ? "vs prior day"
                : $"vs prior {(int)(toExclusive - from).TotalDays}d";

            _salesValue.Text = "₱" + snap.SalesTotal.ToString("N2");
            ApplyTrend(_salesSub, snap.SalesTrendPct, vsLabel);

            if (_cardNetProfit != null && isOwner)
            {
                _netProfitValue.Text = "₱" + snap.NetProfit.ToString("N2");
                decimal margin = snap.SalesTotal > 0
                    ? Math.Round(snap.NetProfit / snap.SalesTotal * 100m, 1)
                    : 0m;
                _netProfitSub.Text = $"{margin:0.#}% margin";
                _netProfitSub.ForeColor = Theme.TextMuted;
            }

            _transactionsValue.Text = snap.TransactionCount.ToString("N0");
            ApplyTrend(_transactionsSub, snap.TransactionsTrendPct, vsLabel);

            _itemsSoldValue.Text = snap.ItemsSold.ToString("N0") + " units";
            _itemsSoldSub.Text = snap.TransactionCount > 0
                ? $"avg {snap.ItemsSold / Math.Max(1, snap.TransactionCount):0.#} per sale"
                : "no items sold";
            _itemsSoldSub.ForeColor = Theme.TextMuted;

            int totalAlerts = snap.LowStockCount + snap.OutOfStockCount;
            _stockAlertsValue.Text = totalAlerts.ToString("N0");
            _stockAlertsValue.ForeColor = snap.OutOfStockCount > 0
                ? Theme.Danger
                : (snap.LowStockCount > 0 ? Color.FromArgb(200, 130, 40) : Theme.Success);

            if (totalAlerts == 0)
            {
                _stockAlertsSub.Text = "All products healthy";
                _stockAlertsSub.ForeColor = Theme.Success;
            }
            else
            {
                _stockAlertsSub.Text = $"{snap.LowStockCount} low  ·  {snap.OutOfStockCount} out";
                _stockAlertsSub.ForeColor = Theme.TextMuted;
            }

            _salesChartTitle.Text = snap.ChartTitle;
            _salesChart.SetData(snap.ChartBuckets);
            _salesChart.SetEmptyMessage(
                snap.ChartBuckets.All(b => b.Value == 0)
                    ? "No sales in this period."
                    : "");

            var topBars = snap.TopProducts
                .Select((p, i) => new ChartBar
                {
                    Label = p.ProductName ?? "—",
                    SubLabel = p.Brand ?? string.Empty,
                    Value = p.Revenue,
                    Color = Theme.ChartPalette[i % Theme.ChartPalette.Length]
                })
                .ToList();
            _topProductsChart.SetData(topBars);
            _topProductsChart.SetEmptyMessage("No sales in this period.");

            var categorySlices = snap.SalesByCategory
                .Select((c, i) => new ChartSlice
                {
                    Label = c.CategoryName ?? "—",
                    Value = c.Revenue,
                    Color = Theme.ChartPalette[i % Theme.ChartPalette.Length]
                })
                .ToList();
            _categoryDonut.SetData(categorySlices);
            _categoryDonut.SetCenterLabel("Revenue");

            SuspendLayout();
            PopulateRecentSales(snap.RecentSales);
            PopulateStockAlerts(snap.LowStockAlerts);
            ResumeLayout(true);
        }

        private void NavigateToSales()
        {
            var (from, toExclusive) = GetSelectedRange();
            NavigateToSalesRequested?.Invoke(from, toExclusive);
        }

        private void ApplyTrend(Label target, decimal? pct, string vsLabel)
        {
            if (target == null) return;

            if (pct == null)
            {
                target.Text = vsLabel;
                target.ForeColor = Theme.TextMuted;
                return;
            }

            decimal value = pct.Value;
            if (value == 0)
            {
                target.ForeColor = Theme.TextMuted;
                target.Text = $"— no change {vsLabel}";
                return;
            }

            if (value > 0)
            {
                target.ForeColor = Theme.Success;
                target.Text = $"▲ {value:0.#}% {vsLabel}";
            }
            else
            {
                target.ForeColor = Theme.Danger;
                target.Text = $"▼ {Math.Abs(value):0.#}% {vsLabel}";
            }
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
                RowCount = 4,
                BackColor = Theme.Background,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                AutoScroll = true
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));   // filter chips
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 160));  // cards
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 240));  // charts
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // bottom

            root.Controls.Add(BuildFilterRow(), 0, 0);
            root.Controls.Add(BuildCardsRow(), 0, 1);
            root.Controls.Add(BuildChartRow(), 0, 2);
            root.Controls.Add(BuildBottomBlock(), 0, 3);

            Controls.Add(root);
        }

        // ---- Filter row: preset chips + hidden custom pickers ----

        private Panel BuildFilterRow()
        {
            var row = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Background
            };

            // Preset chips
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
                chip.Location = new Point(x, 8);
                row.Controls.Add(chip);
                _presetChips.Add(chip);
                x += chip.Width + 8;
            }

            // Custom date range panel (hidden by default)
            _customPickerPanel = new Panel
            {
                Location = new Point(x + 8, 8),
                Height = 40,
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
                Value = DateTime.Today
            };
            _dtpFrom.ValueChanged += async (s, e) => await SilentRefreshAsync();

            var lblTo = new Label
            {
                Text = "To",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(32, 40),
                Location = new Point(220, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _dtpTo = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "MMM d, yyyy",
                Font = Theme.FontBody,
                Location = new Point(256, 6),
                Size = new Size(160, 32),
                MaxDate = DateTime.Today,
                Value = DateTime.Today
            };
            _dtpTo.ValueChanged += async (s, e) => await SilentRefreshAsync();

            _customPickerPanel.Controls.Add(lblFrom);
            _customPickerPanel.Controls.Add(_dtpFrom);
            _customPickerPanel.Controls.Add(lblTo);
            _customPickerPanel.Controls.Add(_dtpTo);
            _customPickerPanel.Width = 416;

            row.Controls.Add(_customPickerPanel);

            // Default to "Today"
            SetActiveChip(_presetChips[0]);

            return row;
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

            chip.Click += async (s, e) =>
            {
                SetActiveChip(chip);
                await ApplyPresetAsync(key);
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

        private async System.Threading.Tasks.Task ApplyPresetAsync(string key)
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
                    return; // don't refresh yet — wait for user to change dates
            }

            await SilentRefreshAsync();
        }

        // ---- Cards row ----

        private TableLayoutPanel BuildCardsRow()
        {
            bool isOwner = SessionManager.IsOwner;
            int cols = isOwner ? 5 : 4;

            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = cols,
                RowCount = 1,
                BackColor = Theme.Background,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            for (int i = 0; i < cols; i++)
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / cols));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            int col = 0;

            _cardSales = BuildStatCard("\uE719", "Sales", Theme.Primary, out _salesValue, out _salesSub);
            _cardSales.Margin = new Padding(8);
            row.Controls.Add(_cardSales, col++, 0);
            MakeCardClickable(_cardSales, NavigateToSales);

            if (isOwner)
            {
                _cardNetProfit = BuildStatCard("\uE9D9", "Net Profit", Theme.Primary, out _netProfitValue, out _netProfitSub);
                _cardNetProfit.Margin = new Padding(8);
                row.Controls.Add(_cardNetProfit, col++, 0);
                MakeCardClickable(_cardNetProfit, NavigateToSales);
            }

            _cardTransactions = BuildStatCard("\uE8EF", "Transactions", Theme.Primary, out _transactionsValue, out _transactionsSub);
            _cardTransactions.Margin = new Padding(8);
            row.Controls.Add(_cardTransactions, col++, 0);
            MakeCardClickable(_cardTransactions, NavigateToSales);

            _cardItemsSold = BuildStatCard("\uE7B8", "Items Sold", Theme.Primary, out _itemsSoldValue, out _itemsSoldSub);
            _cardItemsSold.Margin = new Padding(8);
            row.Controls.Add(_cardItemsSold, col++, 0);
            MakeCardClickable(_cardItemsSold, NavigateToSales);

            _cardStockAlerts = BuildStatCard("\uE7BA", "Stock Alerts", Theme.Danger, out _stockAlertsValue, out _stockAlertsSub);
            _cardStockAlerts.Margin = new Padding(8);
            row.Controls.Add(_cardStockAlerts, col++, 0);
            MakeCardClickable(_cardStockAlerts, () => NavigateRequested?.Invoke("inventory"));

            return row;
        }

        // ---- Charts row ----

        private TableLayoutPanel BuildChartRow()
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Theme.Background,
                Margin = new Padding(0, 8, 0, 8),
                Padding = Padding.Empty
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var chartCard = BuildChartCard("Sales Trend", out _salesChartTitle, out _salesChart);
            chartCard.Margin = new Padding(8, 0, 8, 0);
            _salesChart.BarClicked += (bucketLabel) => NavigateToSales();

            var topCard = BuildChartCard("Top Products", out _, out _topProductsChart);
            topCard.Margin = new Padding(8, 0, 8, 0);
            _topProductsChart.BarClicked += (bar) => NavigateRequested?.Invoke("items");

            row.Controls.Add(chartCard, 0, 0);
            row.Controls.Add(topCard, 1, 0);

            return row;
        }

        private RoundedPanel BuildChartCard(string title, out Label titleLabel, out SalesBarChart chart)
        {
            var card = BuildChartCardBase(title, out titleLabel);
            chart = new SalesBarChart { Dock = DockStyle.Fill };
            card.Controls.Add(chart);
            card.Controls.Add(titleLabel);
            return card;
        }

        private RoundedPanel BuildChartCard(string title, out Label titleLabel, out HorizontalBarChart chart)
        {
            var card = BuildChartCardBase(title, out titleLabel);
            chart = new HorizontalBarChart { Dock = DockStyle.Fill };
            card.Controls.Add(chart);
            card.Controls.Add(titleLabel);
            return card;
        }

        private RoundedPanel BuildChartCardBase(string title, out Label titleLabel)
        {
            var card = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Radius = Theme.RadiusCard,
                BorderColor = Theme.PanelBorder,
                BorderSize = 1,
                ShadowEnabled = true,
                BackColor = Theme.Surface,
                Padding = new Padding(24)
            };

            titleLabel = new Label
            {
                Text = title,
                Font = new Font(Theme.UiFontFamily, 13F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 32,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            return card;
        }

        // ---- Bottom block: 3 equal columns ----

        private TableLayoutPanel BuildBottomBlock()
        {
            var block = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Theme.Background,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            block.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            block.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            block.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            block.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // Column 1 — Sales by Category
            var catCard = BuildChartCardBase("Sales by Category", out var catTitle);
            _categoryDonut = new DonutChart { Dock = DockStyle.Fill };
            _categoryDonut.SetEmptyMessage("No sales in this period.");
            _categoryDonut.SliceClicked += (slice) => NavigateRequested?.Invoke("items");
            catCard.Controls.Add(_categoryDonut);
            catCard.Controls.Add(catTitle);
            catCard.Margin = new Padding(8, 0, 8, 0);

            // Column 2 — Recent Sales
            var recentCard = BuildListCard("Recent Sales", "\uE9D9", out _recentSalesList);
            recentCard.Margin = new Padding(8, 0, 8, 0);

            // Column 3 — Stock Alerts
            var stockCard = BuildListCard("Stock Alerts", "\uE7BA", out _stockAlertsList);
            stockCard.Margin = new Padding(8, 0, 8, 0);

            block.Controls.Add(catCard, 0, 0);
            block.Controls.Add(recentCard, 1, 0);
            block.Controls.Add(stockCard, 2, 0);

            return block;
        }

        // ============================================================
        // CARD BUILDERS
        // ============================================================

        private RoundedPanel BuildStatCard(string iconGlyph, string labelText, Color iconColor,
            out Label valueLabel, out Label subLabel)
        {
            var card = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Radius = Theme.RadiusCard,
                BorderColor = Theme.PanelBorder,
                BorderSize = 1,
                ShadowEnabled = true,
                BackColor = Theme.Surface,
                Padding = new Padding(24, 20, 24, 20)
            };

            var icon = new Label
            {
                Text = iconGlyph,
                Font = new Font(Theme.IconFontFamily, 16F, FontStyle.Regular),
                ForeColor = iconColor,
                AutoSize = false,
                Size = new Size(32, 28),
                Location = new Point(24, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            var caption = new Label
            {
                Text = labelText,
                Font = new Font(Theme.UiFontFamily, 10F, FontStyle.Bold),
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(160, 28),
                Location = new Point(60, 20),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };

            var value = new Label
            {
                Text = "—",
                Font = new Font(Theme.UiFontFamily, 22F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(24, 56),
                Size = new Size(220, 48),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };
            valueLabel = value;

            var sub = new Label
            {
                Text = "",
                Font = new Font(Theme.UiFontFamily, 10F, FontStyle.Regular),
                ForeColor = Theme.TextMuted,
                AutoSize = false,
                Location = new Point(24, 108),
                Size = new Size(220, 22),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };
            subLabel = sub;

            card.Controls.Add(icon);
            card.Controls.Add(caption);
            card.Controls.Add(value);
            card.Controls.Add(sub);

            card.Resize += (s, e) =>
            {
                int w = Math.Max(80, card.ClientSize.Width - 48);
                caption.Width = Math.Max(60, w - 40);
                value.Width = w;
                sub.Width = w;
            };

            return card;
        }

        private RoundedPanel BuildListCard(string title, string iconGlyph, out FlowLayoutPanel listPanel)
        {
            var card = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Radius = Theme.RadiusCard,
                BorderColor = Theme.PanelBorder,
                BorderSize = 1,
                ShadowEnabled = true,
                BackColor = Theme.Surface,
                Padding = new Padding(24)
            };

            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.Transparent
            };

            var icon = new Label
            {
                Text = iconGlyph,
                Font = new Font(Theme.IconFontFamily, 14F),
                ForeColor = Theme.Primary,
                AutoSize = false,
                Size = new Size(28, 28),
                Location = new Point(0, 6),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            var titleLabel = new Label
            {
                Text = title,
                Font = new Font(Theme.UiFontFamily, 13F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(36, 0),
                Height = 40,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            headerPanel.Controls.Add(icon);
            headerPanel.Controls.Add(titleLabel);
            headerPanel.Resize += (s, e) =>
            {
                titleLabel.Width = Math.Max(0, headerPanel.Width - titleLabel.Left);
            };

            var list = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 8, 0, 0)
            };
            listPanel = list;

            list.Resize += (s, e) => StretchRows(list);

            card.Controls.Add(list);
            card.Controls.Add(headerPanel);

            return card;
        }

        private void MakeCardClickable(RoundedPanel card, Action onClick)
        {
            EventHandler clickHandler = (s, e) => onClick();

            card.Click += clickHandler;
            card.Cursor = Cursors.Hand;

            foreach (Control child in card.Controls)
            {
                child.Cursor = Cursors.Hand;
                child.Click += clickHandler;
            }
        }

        // ============================================================
        // LIST POPULATION
        // ============================================================

        private void PopulateRecentSales(List<RecentSaleDisplay> sales)
        {
            _recentSalesList.Controls.Clear();

            if (sales == null || sales.Count == 0)
            {
                _recentSalesList.Controls.Add(BuildEmptyRow(
                    _recentSalesList, "No sales in this period.", Theme.TextMuted));
                return;
            }

            foreach (var sale in sales)
                _recentSalesList.Controls.Add(BuildSaleRow(sale));

            StretchRows(_recentSalesList);
        }

        private Control BuildSaleRow(RecentSaleDisplay display)
        {
            var sale = display.Sale;

            var row = new Panel
            {
                Width = 400,
                Height = 56,
                Margin = new Padding(0, 0, 0, 8),
                BackColor = Color.Transparent
            };

            // Primary label: ITEMS
            var itemsLabel = new Label
            {
                Text = display.ItemsSummary,
                Font = new Font(Theme.UiFontFamily, 10F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(0, 2),
                Size = new Size(220, 22),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoEllipsis = true
            };

            // Amount on the right
            var amountLabel = new Label
            {
                Text = "₱" + sale.Subtotal.ToString("N2"),
                Font = new Font(Theme.UiFontFamily, 11F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Size = new Size(140, 22),
                Location = new Point(240, 2),
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            // Secondary line: Invoice, Date, Payment
            var metaLabel = new Label
            {
                Text = $"{sale.InvoiceNo}   ·   {sale.SaleDate:MMM d, h:mm tt}   ·   {sale.PaymentMethod}",
                Font = new Font(Theme.UiFontFamily, 9F, FontStyle.Regular),
                ForeColor = Theme.TextMuted,
                AutoSize = false,
                Location = new Point(0, 28),
                Size = new Size(380, 18),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoEllipsis = true
            };

            row.Controls.Add(itemsLabel);
            row.Controls.Add(amountLabel);
            row.Controls.Add(metaLabel);

            row.Resize += (s, e) =>
            {
                itemsLabel.Width = Math.Max(60, row.Width - 150);
                amountLabel.Left = Math.Max(60, row.Width - 145);
                metaLabel.Width = row.Width;
            };

            return row;
        }

        private void PopulateStockAlerts(List<Product> products)
        {
            _stockAlertsList.Controls.Clear();

            if (products == null || products.Count == 0)
            {
                _stockAlertsList.Controls.Add(BuildEmptyRow(
                    _stockAlertsList, "All products are above reorder level.", Theme.Success));
                return;
            }

            var outOfStock = products.Where(p => p.QuantityOnHand == 0).ToList();
            var lowStock = products.Where(p => p.QuantityOnHand > 0).ToList();

            if (outOfStock.Count == 0 && lowStock.Count == 0)
            {
                _stockAlertsList.Controls.Add(BuildEmptyRow(
                    _stockAlertsList, "All products are above reorder level.", Theme.Success));
                return;
            }

            if (outOfStock.Count > 0)
            {
                _stockAlertsList.Controls.Add(BuildSectionHeader(
                    $"OUT OF STOCK ({outOfStock.Count})", Theme.Danger));

                foreach (var p in outOfStock)
                    _stockAlertsList.Controls.Add(BuildStockRow(p, isOutOfStock: true));
            }

            if (lowStock.Count > 0)
            {
                _stockAlertsList.Controls.Add(BuildSectionHeader(
                    $"LOW STOCK ({lowStock.Count})", Color.FromArgb(200, 130, 40)));

                foreach (var p in lowStock)
                    _stockAlertsList.Controls.Add(BuildStockRow(p, isOutOfStock: false));
            }

            StretchRows(_stockAlertsList);
        }

        private Control BuildSectionHeader(string text, Color color)
        {
            return new Label
            {
                Text = text,
                Font = new Font(Theme.UiFontFamily, 9F, FontStyle.Bold),
                ForeColor = color,
                AutoSize = false,
                Width = 300,
                Height = 24,
                Margin = new Padding(0, 8, 0, 8),
                Padding = new Padding(2, 0, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
        }

        private Control BuildStockRow(Product p, bool isOutOfStock)
        {
            var row = new Panel
            {
                Width = 300,
                Height = 48,
                Margin = new Padding(0, 0, 0, 4),
                BackColor = Color.Transparent
            };

            var nameLabel = new Label
            {
                Text = $"{p.ProductName} — {p.Brand}",
                Font = new Font(Theme.UiFontFamily, 10F, FontStyle.Regular),
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(0, 2),
                Size = new Size(200, 22),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoEllipsis = true
            };

            var qtyLabel = new Label
            {
                Text = $"{p.QuantityOnHand} / {p.ReorderLevel}",
                Font = new Font(Theme.UiFontFamily, 10F, FontStyle.Bold),
                ForeColor = isOutOfStock ? Theme.Danger : Color.FromArgb(200, 130, 40),
                AutoSize = false,
                Size = new Size(70, 22),
                Location = new Point(230, 2),
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            row.Controls.Add(nameLabel);
            row.Controls.Add(qtyLabel);

            row.Resize += (s, e) =>
            {
                nameLabel.Width = Math.Max(60, row.Width - 80);
                qtyLabel.Left = Math.Max(60, row.Width - 70);
            };

            return row;
        }

        private Control BuildEmptyRow(FlowLayoutPanel parent, string text, Color dotColor)
        {
            int initialWidth = parent != null && parent.ClientSize.Width > 0
                ? Math.Max(100, parent.ClientSize.Width - 4)
                : 300;

            var row = new Panel
            {
                Width = initialWidth,
                Height = 56,
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };

            var dot = new Label
            {
                Text = "●",
                Font = new Font(Theme.UiFontFamily, 12F),
                ForeColor = dotColor,
                AutoSize = false,
                Size = new Size(20, 56),
                Location = new Point(0, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            var msg = new Label
            {
                Text = text,
                Font = new Font(Theme.UiFontFamily, 10F, FontStyle.Regular),
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(24, 0),
                Height = 56,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            msg.Width = Math.Max(60, row.Width - 24);

            row.Controls.Add(dot);
            row.Controls.Add(msg);

            row.Resize += (s, e) => msg.Width = Math.Max(60, row.Width - 24);

            return row;
        }

        private void StretchRows(FlowLayoutPanel list)
        {
            if (list == null) return;
            int targetWidth = Math.Max(100, list.ClientSize.Width - 4);
            foreach (Control c in list.Controls)
            {
                if (c != null && !c.IsDisposed)
                    c.Width = targetWidth;
            }
        }
    }
}