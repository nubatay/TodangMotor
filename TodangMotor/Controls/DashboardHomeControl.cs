using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using TodangMotor.Common;
using TodangMotor.Models;
using TodangMotor.Services;

namespace TodangMotor.Controls
{
    /// <summary>
    /// Default content shown on the Owner dashboard.
    /// Four stat cards on top, Recent Sales + Low Stock Alerts panels below.
    /// All data comes from DashboardService; no database code lives here.
    /// </summary>
    public class DashboardHomeControl : UserControl
    {
        private readonly DashboardService _dashboardService = new();

        // Stat card value labels (updated after async load).
        private Label _totalProductsValue;
        private Label _lowStockValue;
        private Label _todaySalesValue;
        private Label _todayTxValue;

        // Stat card containers (for entrance animation).
        private RoundedPanel _cardProducts;
        private RoundedPanel _cardLowStock;
        private RoundedPanel _cardTodaySales;
        private RoundedPanel _cardTodayTx;

        // List bodies.
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

            Load += DashboardHomeControl_Load;
        }

        // ============================================================
        // LOAD
        // ============================================================

        private async void DashboardHomeControl_Load(object sender, EventArgs e)
        {
            // Animate after the first layout pass so RiseInControl captures final bounds.
            BeginInvoke(new Action(AnimateEntrance));
            await LoadDataAsync();
        }

        private void AnimateEntrance()
        {
            if (IsDisposed) return;
            Animator.RiseInControl(_cardProducts, 14, 220);
            Animator.RiseInControl(_cardLowStock, 14, 220);
            Animator.RiseInControl(_cardTodaySales, 14, 220);
            Animator.RiseInControl(_cardTodayTx, 14, 220);
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            try
            {
                int totalProducts = await _dashboardService.GetTotalActiveProductsAsync();
                int lowStockCount = await _dashboardService.GetLowStockCountAsync();
                var topLowStock = await _dashboardService.GetTopLowStockAsync(5);

                if (IsDisposed) return;

                _totalProductsValue.Text = totalProducts.ToString("N0");
                _lowStockValue.Text = lowStockCount.ToString("N0");

                if (_dashboardService.IsSalesModuleAvailable)
                {
                    decimal todaySales = await _dashboardService.GetTodaySalesTotalAsync();
                    int todayTx = await _dashboardService.GetTodayTransactionsAsync();
                    _todaySalesValue.Text = "₱" + todaySales.ToString("N2");
                    _todayTxValue.Text = todayTx.ToString("N0");
                }
                else
                {
                    _todaySalesValue.Text = "—";
                    _todayTxValue.Text = "—";
                }

                PopulateLowStockList(topLowStock);
                PopulateRecentSalesPlaceholder();
            }
            catch
            {
                if (IsDisposed) return;
                _totalProductsValue.Text = "!";
                _lowStockValue.Text = "!";
                _todaySalesValue.Text = "!";
                _todayTxValue.Text = "!";
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
                RowCount = 2,
                BackColor = Theme.Background,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 168));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // ---------- Row 1: stat cards ----------
            var cardsRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Theme.Background,
                Margin = new Padding(0, 0, 0, Theme.SpacingMd),
                Padding = Padding.Empty
            };
            for (int i = 0; i < 4; i++)
                cardsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            cardsRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _cardProducts = BuildStatCard("\uE7B8", "Total Products", out _totalProductsValue);
            _cardLowStock = BuildStatCard("\uE7BA", "Low Stock Items", out _lowStockValue);
            _cardTodaySales = BuildStatCard("\uE719", "Today's Sales", out _todaySalesValue);
            _cardTodayTx = BuildStatCard("\uE9D9", "Today's Transactions", out _todayTxValue);

            _cardProducts.Margin = new Padding(Theme.SpacingSm);
            _cardLowStock.Margin = new Padding(Theme.SpacingSm);
            _cardTodaySales.Margin = new Padding(Theme.SpacingSm);
            _cardTodayTx.Margin = new Padding(Theme.SpacingSm);

            cardsRow.Controls.Add(_cardProducts, 0, 0);
            cardsRow.Controls.Add(_cardLowStock, 1, 0);
            cardsRow.Controls.Add(_cardTodaySales, 2, 0);
            cardsRow.Controls.Add(_cardTodayTx, 3, 0);

            root.Controls.Add(cardsRow, 0, 0);

            // ---------- Row 2: content panels ----------
            var contentRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Theme.Background,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            contentRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60f));
            contentRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f));
            contentRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var recentSalesCard = BuildListCard("Recent Sales", "\uE9D9", out _recentSalesList);
            recentSalesCard.Margin = new Padding(Theme.SpacingSm);

            var lowStockCard = BuildListCard("Low Stock Alerts", "\uE7BA", out _lowStockList);
            lowStockCard.Margin = new Padding(Theme.SpacingSm);

            contentRow.Controls.Add(recentSalesCard, 0, 0);
            contentRow.Controls.Add(lowStockCard, 1, 0);

            root.Controls.Add(contentRow, 0, 1);

            Controls.Add(root);
        }

        // ============================================================
        // CARD BUILDERS
        // ============================================================

        private RoundedPanel BuildStatCard(string iconGlyph, string labelText, out Label valueLabel)
        {
            var card = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Radius = Theme.RadiusCard,
                BorderColor = Theme.Divider,
                BorderSize = 1,
                BackColor = Theme.Surface,
                Padding = new Padding(20, 16, 20, 16)
            };

            // Icon (top-left).
            var icon = new Label
            {
                Text = iconGlyph,
                Font = new Font(Theme.IconFontFamily, 26F, FontStyle.Regular),
                ForeColor = Theme.Primary,
                AutoSize = false,
                Size = new Size(40, 36),
                Location = new Point(16, 6),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            // Big value (below the icon).
            var val = new Label
            {
                Text = "—",
                Font = new Font(Theme.UiFontFamily, 24F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(16, 44),
                Size = new Size(220, 42),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };
            valueLabel = val;

            // Small caption (bottom).
            var lab = new Label
            {
                Text = labelText,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(16, 88),
                Size = new Size(220, 22),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            card.Controls.Add(icon);
            card.Controls.Add(val);
            card.Controls.Add(lab);

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

            // Header row inside the card.
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 34,
                BackColor = Color.Transparent
            };

            var icon = new Label
            {
                Text = iconGlyph,
                Font = new Font(Theme.IconFontFamily, 14F),
                ForeColor = Theme.Primary,
                AutoSize = false,
                Size = new Size(26, 26),
                Location = new Point(0, 4),
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

            // List body (scrollable).
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

            // Keep child rows stretched to the list width as the panel resizes.
            list.Resize += (s, e) => StretchRows(list);

            card.Controls.Add(list);
            card.Controls.Add(headerPanel);

            return card;
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

        // ============================================================
        // LIST POPULATION
        // ============================================================

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
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
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

        private void PopulateRecentSalesPlaceholder()
        {
            _recentSalesList.Controls.Clear();

            if (!_dashboardService.IsSalesModuleAvailable)
            {
                _recentSalesList.Controls.Add(BuildEmptyRow(
                    _recentSalesList,
                    "Sales module coming soon.",
                    Theme.TextMuted));
                return;
            }

            // Real population happens in Module 6.
        }

        private Control BuildEmptyRow(FlowLayoutPanel parent, string text, Color dotColor)
        {
            int initialWidth = parent != null && parent.ClientSize.Width > 0
                ? Math.Max(100, parent.ClientSize.Width - 4)
                : 300;

            var row = new Panel
            {
                Width = initialWidth,
                Height = 60,
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };

            var dot = new Label
            {
                Text = "●",
                Font = new Font(Theme.UiFontFamily, 12F),
                ForeColor = dotColor,
                AutoSize = false,
                Size = new Size(20, 60),
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
                Height = 60,
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
    }
}