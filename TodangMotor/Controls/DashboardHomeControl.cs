using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TodangMotor.Common;
using TodangMotor.Data;
using TodangMotor.Models;
using TodangMotor.Services;

namespace TodangMotor.Controls
{
    /// <summary>
    /// The dashboard home. Owner sees 4 cards (Sales, Net Profit,
    /// Transactions, Low Stock). Cashier sees 3 (no Net Profit).
    /// Range filter drives all range-dependent widgets. Auto-refreshes
    /// every 30 seconds, on filter change, and when the control becomes
    /// visible again.
    /// </summary>
    public class DashboardHomeControl : UserControl
    {
        private readonly DashboardService _dashboardService = new();

        public event Action<string>? NavigateRequested;

        private DashboardRange _range = DashboardRange.Today;
        private bool _isLoading;
        private bool _suppressFilterEvent;
        private System.Windows.Forms.Timer _refreshTimer;

        private RoundedComboBox _rangeCombo;

        private RoundedPanel _cardSales;
        private RoundedPanel _cardNetProfit;
        private RoundedPanel _cardTransactions;
        private RoundedPanel _cardLowStock;

        private Label _salesValue, _salesTrend;
        private Label _netProfitValue, _netProfitTrend;
        private Label _transactionsValue, _transactionsTrend;
        private Label _lowStockValue, _lowStockSub;

        private SalesBarChart _chart;
        private Label _chartTitle;

        private FlowLayoutPanel _topProductsList;
        private FlowLayoutPanel _recentSalesList;
        private FlowLayoutPanel _lowStockList;

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

        private async System.Threading.Tasks.Task RenderSnapshotAsync()
        {
            DashboardSnapshot snap;
            try
            {
                snap = await _dashboardService.GetSnapshotAsync(_range);
            }
            catch
            {
                return;
            }

            if (IsDisposed) return;

            _lowStockValue.Text = snap.LowStockCount.ToString("N0");
            _lowStockSub.Text = snap.OutOfStockCount > 0
                ? $"{snap.OutOfStockCount} out of stock"
                : (snap.LowStockCount == 0 ? "All products healthy" : "Needs restocking");

            _salesValue.Text = "₱" + snap.SalesTotal.ToString("N2");
            _transactionsValue.Text = snap.TransactionCount.ToString("N0");

            string vsLabel = _range switch
            {
                DashboardRange.Today => "vs yesterday",
                DashboardRange.ThisWeek => "vs last week",
                DashboardRange.ThisMonth => "vs last month",
                _ => ""
            };

            ApplyTrend(_salesTrend, snap.SalesTrendPct, vsLabel);
            ApplyTrend(_transactionsTrend, snap.TransactionsTrendPct, vsLabel);

            if (_cardNetProfit != null && SessionManager.IsOwner)
            {
                _netProfitValue.Text = "₱" + snap.NetProfit.ToString("N2");
                ApplyTrend(_netProfitTrend, snap.NetProfitTrendPct, vsLabel);
            }

            _chartTitle.Text = snap.ChartTitle;
            _chart.SetData(snap.ChartBuckets);
            _chart.SetEmptyMessage(
                snap.ChartBuckets.All(b => b.Value == 0)
                    ? "No sales in this period."
                    : "");

            PopulateTopProducts(snap.TopProducts);
            PopulateRecentSales(snap.RecentSales);
            PopulateLowStockList(snap.LowStockAlerts);
        }

        private void ApplyTrend(Label trendLabel, decimal? pct, string vsLabel)
        {
            if (trendLabel == null) return;

            if (pct == null || string.IsNullOrEmpty(vsLabel))
            {
                trendLabel.Text = "";
                return;
            }

            decimal value = pct.Value;

            if (value == 0)
            {
                trendLabel.ForeColor = Theme.TextMuted;
                trendLabel.Text = $"— no change {vsLabel}";
                return;
            }

            if (value > 0)
            {
                trendLabel.ForeColor = Theme.Success;
                trendLabel.Text = $"▲ {value:0.#}% {vsLabel}";
            }
            else
            {
                trendLabel.ForeColor = Theme.Danger;
                trendLabel.Text = $"▼ {Math.Abs(value):0.#}% {vsLabel}";
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
                Padding = Padding.Empty
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 172));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 260));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            root.Controls.Add(BuildFilterRow(), 0, 0);
            root.Controls.Add(BuildCardsRow(), 0, 1);
            root.Controls.Add(BuildChartRow(), 0, 2);
            root.Controls.Add(BuildBottomRow(), 0, 3);

            Controls.Add(root);
        }

        private Panel BuildFilterRow()
        {
            var row = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Background
            };

            var lblRange = new Label
            {
                Text = "Range:",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(56, 20),
                Location = new Point(0, 14),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _rangeCombo = new RoundedComboBox
            {
                Width = 150,
                Location = new Point(60, 4)
            };
            _rangeCombo.Items.Add("Today");
            _rangeCombo.Items.Add("This Week");
            _rangeCombo.Items.Add("This Month");
            _rangeCombo.Items.Add("All Time");

            _suppressFilterEvent = true;
            _rangeCombo.SelectedIndex = 0;
            _suppressFilterEvent = false;

            _rangeCombo.SelectedIndexChanged += (s, e) =>
            {
                if (_suppressFilterEvent) return;

                _range = _rangeCombo.SelectedIndex switch
                {
                    1 => DashboardRange.ThisWeek,
                    2 => DashboardRange.ThisMonth,
                    3 => DashboardRange.AllTime,
                    _ => DashboardRange.Today
                };

                _ = SilentRefreshAsync();
            };

            row.Controls.Add(lblRange);
            row.Controls.Add(_rangeCombo);

            return row;
        }

        private TableLayoutPanel BuildCardsRow()
        {
            bool isOwner = SessionManager.IsOwner;
            int columnCount = isOwner ? 4 : 3;

            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = columnCount,
                RowCount = 1,
                BackColor = Theme.Background,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            for (int i = 0; i < columnCount; i++)
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columnCount));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _cardSales = BuildStatCard("\uE719", "Sales", out _salesValue, out _salesTrend);
            _cardTransactions = BuildStatCard("\uE9D9", "Transactions", out _transactionsValue, out _transactionsTrend);
            _cardLowStock = BuildStatCard("\uE7BA", "Low Stock Items", out _lowStockValue, out _lowStockSub);

            int col = 0;

            _cardSales.Margin = new Padding(Theme.SpacingSm);
            row.Controls.Add(_cardSales, col++, 0);

            if (isOwner)
            {
                _cardNetProfit = BuildStatCard("\uE9D9", "Net Profit", out _netProfitValue, out _netProfitTrend);
                _cardNetProfit.Margin = new Padding(Theme.SpacingSm);
                row.Controls.Add(_cardNetProfit, col++, 0);
            }

            _cardTransactions.Margin = new Padding(Theme.SpacingSm);
            _cardLowStock.Margin = new Padding(Theme.SpacingSm);
            row.Controls.Add(_cardTransactions, col++, 0);
            row.Controls.Add(_cardLowStock, col++, 0);

            MakeCardClickable(_cardSales, "sales");
            MakeCardClickable(_cardTransactions, "sales");
            MakeCardClickable(_cardLowStock, "inventory");
            if (_cardNetProfit != null)
                MakeCardClickable(_cardNetProfit, "sales");

            return row;
        }

        private TableLayoutPanel BuildChartRow()
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Theme.Background,
                Margin = new Padding(0, Theme.SpacingSm, 0, Theme.SpacingSm),
                Padding = Padding.Empty
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var chartCard = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Radius = Theme.RadiusCard,
                BorderColor = Theme.Divider,
                BorderSize = 1,
                BackColor = Theme.Surface,
                Padding = new Padding(Theme.SpacingMd),
                Margin = new Padding(Theme.SpacingSm)
            };

            _chartTitle = new Label
            {
                Text = "Sales",
                Font = Theme.FontH3,
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _chart = new SalesBarChart
            {
                Dock = DockStyle.Fill
            };

            chartCard.Controls.Add(_chart);
            chartCard.Controls.Add(_chartTitle);

            var topCard = BuildListCard("Top Products", "\uE9D9", out _topProductsList);
            topCard.Margin = new Padding(Theme.SpacingSm);

            row.Controls.Add(chartCard, 0, 0);
            row.Controls.Add(topCard, 1, 0);

            return row;
        }

        private TableLayoutPanel BuildBottomRow()
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Theme.Background,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var recentCard = BuildListCard("Recent Sales", "\uE9D9", out _recentSalesList);
            recentCard.Margin = new Padding(Theme.SpacingSm);

            var lowCard = BuildListCard("Low Stock Alerts", "\uE7BA", out _lowStockList);
            lowCard.Margin = new Padding(Theme.SpacingSm);

            row.Controls.Add(recentCard, 0, 0);
            row.Controls.Add(lowCard, 1, 0);

            return row;
        }

        // ============================================================
        // CARD BUILDERS
        // ============================================================

        private RoundedPanel BuildStatCard(string iconGlyph, string labelText,
            out Label valueLabel, out Label trendLabel)
        {
            var card = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Radius = Theme.RadiusCard,
                BorderColor = Theme.Divider,
                BorderSize = 1,
                BackColor = Theme.Surface,
                Padding = new Padding(16, 12, 16, 12)
            };

            // Interior height = 172 - 24 = 148.
            // Stack: icon(40) + value(36) + label(20) + trend(18) + gaps(4+4+4) = 126.
            // Leaves 22px bottom breathing room.

            // ---- Icon (top-left, centered) ----
            var icon = new Label
            {
                Text = iconGlyph,
                Font = new Font(Theme.IconFontFamily, 22F, FontStyle.Regular),
                ForeColor = Theme.Primary,
                AutoSize = false,
                Size = new Size(44, 40),
                Location = new Point(12, 0),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            // ---- Value ----
            var val = new Label
            {
                Text = "—",
                Font = new Font(Theme.UiFontFamily, 20F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(12, 44),
                Size = new Size(240, 36),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };
            valueLabel = val;

            // ---- Caption ----
            var lab = new Label
            {
                Name = "CardLabel",
                Text = labelText,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(12, 84),
                Size = new Size(240, 20),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            // ---- Trend ----
            var trend = new Label
            {
                Text = "",
                Font = Theme.FontTiny,
                ForeColor = Theme.TextMuted,
                AutoSize = false,
                Location = new Point(12, 108),
                Size = new Size(240, 18),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };
            trendLabel = trend;

            card.Controls.Add(icon);
            card.Controls.Add(val);
            card.Controls.Add(lab);
            card.Controls.Add(trend);

            return card;
        }

        private RoundedPanel BuildListCard(string title, string iconGlyph, out FlowLayoutPanel listPanel)
        {
            var card = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Radius = Theme.RadiusCard,
                BorderColor = Theme.Divider,
                BorderSize = 1,
                BackColor = Theme.Surface,
                Padding = new Padding(Theme.SpacingMd)
            };

            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 34,
                BackColor = Color.Transparent
            };

            var icon = new Label
            {
                Text = iconGlyph,
                Font = new Font(Theme.IconFontFamily, 12F),
                ForeColor = Theme.Primary,
                AutoSize = false,
                Size = new Size(24, 24),
                Location = new Point(0, 5),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            var titleLabel = new Label
            {
                Text = title,
                Font = Theme.FontH3,
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(30, 0),
                Height = 34,
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
                Padding = new Padding(0, Theme.SpacingSm, 0, 0)
            };
            listPanel = list;

            list.Resize += (s, e) => StretchRows(list);

            card.Controls.Add(list);
            card.Controls.Add(headerPanel);

            return card;
        }

        private void MakeCardClickable(RoundedPanel card, string navKey)
        {
            EventHandler clickHandler = (s, e) => NavigateRequested?.Invoke(navKey);

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

        private void PopulateTopProducts(List<TopProductRow> products)
        {
            _topProductsList.Controls.Clear();

            if (products == null || products.Count == 0)
            {
                _topProductsList.Controls.Add(BuildEmptyRow(
                    _topProductsList,
                    "No sales in this period.",
                    Theme.TextMuted));
                return;
            }

            foreach (var p in products)
            {
                _topProductsList.Controls.Add(BuildTopProductRow(p));
            }

            StretchRows(_topProductsList);
        }

        private Control BuildTopProductRow(TopProductRow p)
        {
            var row = new Panel
            {
                Width = 300,
                Height = 44,
                Margin = new Padding(0, 0, 0, 4),
                BackColor = Color.Transparent
            };

            var nameLabel = new Label
            {
                Text = string.IsNullOrEmpty(p.Brand)
                    ? p.ProductName
                    : $"{p.ProductName} — {p.Brand}",
                Font = Theme.FontBody,
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(0, 3),
                Size = new Size(200, 20),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoEllipsis = true
            };

            var revenueLabel = new Label
            {
                Text = "₱" + p.Revenue.ToString("N2"),
                Font = Theme.FontBodyBold,
                ForeColor = Theme.Primary,
                AutoSize = false,
                Size = new Size(110, 20),
                Location = new Point(210, 3),
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            var qtyLabel = new Label
            {
                Text = $"{p.QuantitySold} unit(s) sold",
                Font = Theme.FontTiny,
                ForeColor = Theme.TextMuted,
                AutoSize = false,
                Location = new Point(0, 24),
                Size = new Size(300, 16),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            row.Controls.Add(nameLabel);
            row.Controls.Add(revenueLabel);
            row.Controls.Add(qtyLabel);

            row.Resize += (s, e) =>
            {
                nameLabel.Width = Math.Max(60, row.Width - 120);
                revenueLabel.Left = Math.Max(60, row.Width - 115);
                qtyLabel.Width = row.Width;
            };

            return row;
        }

        private void PopulateRecentSales(List<Sale> sales)
        {
            _recentSalesList.Controls.Clear();

            if (sales == null || sales.Count == 0)
            {
                _recentSalesList.Controls.Add(BuildEmptyRow(
                    _recentSalesList,
                    "No sales in this period.",
                    Theme.TextMuted));
                return;
            }

            foreach (var sale in sales)
            {
                _recentSalesList.Controls.Add(BuildSaleRow(sale));
            }

            StretchRows(_recentSalesList);
        }

        private Control BuildSaleRow(Sale sale)
        {
            bool isVoid = sale.Status == "Void";

            var row = new Panel
            {
                Width = 400,
                Height = 56,
                Margin = new Padding(0, 0, 0, 6),
                BackColor = Color.Transparent
            };

            var invoiceLabel = new Label
            {
                Text = sale.InvoiceNo ?? string.Empty,
                Font = Theme.FontBodyBold,
                ForeColor = isVoid ? Theme.Danger : Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(0, 4),
                Size = new Size(220, 20),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var amountLabel = new Label
            {
                Text = "₱" + sale.Subtotal.ToString("N2"),
                Font = Theme.FontBodyBold,
                ForeColor = isVoid ? Theme.Danger : Theme.TextPrimary,
                AutoSize = false,
                Size = new Size(140, 20),
                Location = new Point(240, 4),
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            string statusText = isVoid ? $"Void · {sale.PaymentMethod}" : (sale.PaymentMethod ?? "");

            var metaLabel = new Label
            {
                Text = $"{sale.SaleDate:MMM d, h:mm tt}   ·   {statusText}",
                Font = Theme.FontTiny,
                ForeColor = isVoid ? Theme.Danger : Theme.TextMuted,
                AutoSize = false,
                Location = new Point(0, 28),
                Size = new Size(380, 16),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            row.Controls.Add(invoiceLabel);
            row.Controls.Add(amountLabel);
            row.Controls.Add(metaLabel);

            row.Resize += (s, e) =>
            {
                invoiceLabel.Width = Math.Max(60, row.Width - 150);
                amountLabel.Left = Math.Max(60, row.Width - 145);
                metaLabel.Width = row.Width;
            };

            return row;
        }

        private void PopulateLowStockList(List<Product> products)
        {
            _lowStockList.Controls.Clear();

            if (products == null || products.Count == 0)
            {
                _lowStockList.Controls.Add(BuildEmptyRow(
                    _lowStockList,
                    "All products are above reorder level.",
                    Theme.Success));
                return;
            }

            foreach (var p in products)
            {
                _lowStockList.Controls.Add(BuildLowStockRow(p));
            }

            StretchRows(_lowStockList);
        }

        private Control BuildLowStockRow(Product p)
        {
            var row = new Panel
            {
                Width = 300,
                Height = 52,
                Margin = new Padding(0, 0, 0, 6),
                BackColor = Color.Transparent
            };

            var nameLabel = new Label
            {
                Text = $"{p.ProductName} — {p.Brand}",
                Font = Theme.FontBody,
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(0, 4),
                Size = new Size(200, 20),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoEllipsis = true
            };

            var qtyLabel = new Label
            {
                Text = $"{p.QuantityOnHand} / {p.ReorderLevel}",
                Font = Theme.FontBodyBold,
                ForeColor = Theme.Danger,
                AutoSize = false,
                Size = new Size(70, 20),
                Location = new Point(230, 4),
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            var subLabel = new Label
            {
                Text = $"On hand: {p.QuantityOnHand}   ·   Reorder at: {p.ReorderLevel}",
                Font = Theme.FontTiny,
                ForeColor = Theme.TextMuted,
                AutoSize = false,
                Location = new Point(0, 26),
                Size = new Size(300, 16),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            row.Controls.Add(nameLabel);
            row.Controls.Add(qtyLabel);
            row.Controls.Add(subLabel);

            row.Resize += (s, e) =>
            {
                nameLabel.Width = Math.Max(60, row.Width - 80);
                qtyLabel.Left = Math.Max(60, row.Width - 70);
                subLabel.Width = row.Width;
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
                Font = Theme.FontSmall,
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