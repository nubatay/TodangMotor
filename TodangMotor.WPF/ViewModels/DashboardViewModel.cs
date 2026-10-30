using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using TodangMotor.Common;
using TodangMotor.Models;
using TodangMotor.Services;

namespace TodangMotor.WPF.ViewModels
{
    // Wrapper for Recent Sales to make XAML binding easy
    public class RecentSaleItem
    {
        public string ItemsSummary { get; set; } = string.Empty;
        public string SubtotalFormatted { get; set; } = string.Empty;
        public string MetaData { get; set; } = string.Empty;
    }

    // Wrapper for Stock Alerts
    public class StockAlertItem
    {
        public string Name { get; set; } = string.Empty;
        public string QuantityText { get; set; } = string.Empty;
        public bool IsOutOfStock { get; set; }
        public string ColorHex => IsOutOfStock ? "#EF4444" : "#F59E0B"; // Red or Orange
    }

    public partial class DashboardViewModel : ObservableObject
    {
        private readonly DashboardService _dashboardService;

        // Navigation Event to tell MainWindowViewModel to switch screens
        public event Action<string>? NavigateRequested;

        public bool IsOwner => SessionManager.IsOwner;

        [ObservableProperty] private bool _isLoading;

        // ---- Filters ----
        [ObservableProperty] private string _activeFilter = "Today"; // Today, 7d, 30d, Month, Custom
        [ObservableProperty] private bool _isCustomDateVisible;
        [ObservableProperty] private DateTime _fromDate = DateTime.Today;
        [ObservableProperty] private DateTime _toDate = DateTime.Today;

        // ---- Stat Cards ----
        [ObservableProperty] private string _salesValue = "₱0.00";
        [ObservableProperty] private string _salesTrend = "";
        [ObservableProperty] private string _salesTrendColor = "#64748B"; // Default muted

        [ObservableProperty] private string _netProfitValue = "₱0.00";
        [ObservableProperty] private string _netProfitSub = "";

        [ObservableProperty] private string _transactionsValue = "0";
        [ObservableProperty] private string _transactionsTrend = "";
        [ObservableProperty] private string _transactionsTrendColor = "#64748B";

        [ObservableProperty] private string _itemsSoldValue = "0 units";
        [ObservableProperty] private string _itemsSoldSub = "";

        [ObservableProperty] private string _stockAlertsValue = "0";
        [ObservableProperty] private string _stockAlertsSub = "";
        [ObservableProperty] private string _stockAlertsColor = "#22C55E"; // Default Green
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasNoStockAlerts))]
        private bool _hasStockAlerts;

        public bool HasNoStockAlerts => !HasStockAlerts;

        // ---- Charts (LiveCharts2) ----
        [ObservableProperty] private string _salesChartTitle = "Sales Trend";
        [ObservableProperty] private ISeries[] _salesTrendSeries = Array.Empty<ISeries>();
        [ObservableProperty] private Axis[] _salesTrendXAxes = Array.Empty<Axis>();
        [ObservableProperty] private Axis[] _salesTrendYAxes = Array.Empty<Axis>();
        [ObservableProperty] private ISeries[] _topProductsSeries = Array.Empty<ISeries>();
        [ObservableProperty] private ISeries[] _categorySeries = Array.Empty<ISeries>();
        [ObservableProperty] private Axis[] _topProductsYAxes = Array.Empty<Axis>();
        [ObservableProperty] private Axis[] _topProductsXAxes = Array.Empty<Axis>();

        // ---- Lists ----
        public ObservableCollection<RecentSaleItem> RecentSales { get; } = new();
        public ObservableCollection<StockAlertItem> StockAlerts { get; } = new();

        public DashboardViewModel(DashboardService dashboardService)
        {
            _dashboardService = dashboardService;

            // Load initial data on startup
            LoadDataCommand.Execute(null);
        }

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            if (IsLoading) return;
            IsLoading = true;

            try
            {
                var (from, toExclusive) = GetSelectedRange();
                var snap = await _dashboardService.GetSnapshotAsync(from, toExclusive);

                RenderStats(snap, from, toExclusive);
                RenderCharts(snap);
                RenderLists(snap);

                // Default Stock Alert Card Color
                int totalAlerts = snap.LowStockCount + snap.OutOfStockCount;
                StockAlertsColor = totalAlerts == 0 ? "#22C55E" : (snap.OutOfStockCount > 0 ? "#EF4444" : "#F59E0B");
            }
            catch (Exception ex)
            {
                // In a real app, log this error
                System.Diagnostics.Debug.WriteLine($"Dashboard Error: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ============================================================
        // FILTER COMMANDS
        // ============================================================

        [RelayCommand]
        private async Task SetFilterAsync(string filter)
        {
            ActiveFilter = filter;
            DateTime today = DateTime.Today;

            IsCustomDateVisible = filter == "Custom";

            switch (filter)
            {
                case "Today":
                    FromDate = today; ToDate = today; break;
                case "7d":
                    FromDate = today.AddDays(-6); ToDate = today; break;
                case "30d":
                    FromDate = today.AddDays(-29); ToDate = today; break;
                case "Month":
                    FromDate = new DateTime(today.Year, today.Month, 1); ToDate = today; break;
                case "Custom":
                    return; // Wait for user to pick dates
            }

            await LoadDataAsync();
        }

        // ============================================================
        // NAVIGATION COMMANDS
        // ============================================================

        [RelayCommand]
        private void NavigateToInventory() => NavigateRequested?.Invoke("inventory");

        [RelayCommand]
        private void NavigateToSales() => NavigateRequested?.Invoke("sales");

        // ============================================================
        // PRIVATE HELPERS (The "Brain" logic)
        // ============================================================

        private (DateTime From, DateTime ToExclusive) GetSelectedRange()
        {
            if (ToDate < FromDate) ToDate = FromDate;
            return (FromDate.Date, ToDate.Date.AddDays(1));
        }

        private void RenderStats(DashboardSnapshot snap, DateTime from, DateTime toExclusive)
        {
            string vsLabel = from == toExclusive.AddDays(-1)
                ? "vs prior day"
                : $"vs prior {(int)(toExclusive - from).TotalDays}d";

            // Sales Card
            SalesValue = "₱" + snap.SalesTotal.ToString("N2");
            ApplyTrend(snap.SalesTrendPct, vsLabel, out string sText, out string sColor);
            SalesTrend = sText; SalesTrendColor = sColor;

            // Net Profit Card
            if (IsOwner)
            {
                NetProfitValue = "₱" + snap.NetProfit.ToString("N2");
                decimal margin = snap.SalesTotal > 0 ? Math.Round(snap.NetProfit / snap.SalesTotal * 100m, 1) : 0m;
                NetProfitSub = $"{margin:0.#}% margin";
            }

            // Transactions Card
            TransactionsValue = snap.TransactionCount.ToString("N0");
            ApplyTrend(snap.TransactionsTrendPct, vsLabel, out string tText, out string tColor);
            TransactionsTrend = tText; TransactionsTrendColor = tColor;

            // Items Sold Card
            ItemsSoldValue = snap.ItemsSold.ToString("N0") + " units";
            ItemsSoldSub = snap.TransactionCount > 0
                ? $"avg {snap.ItemsSold / Math.Max(1, snap.TransactionCount):0.#} per sale"
                : "no items sold";

            // Stock Alerts Card
            int totalAlerts = snap.LowStockCount + snap.OutOfStockCount;
            StockAlertsValue = totalAlerts.ToString("N0");
            StockAlertsSub = totalAlerts == 0
                ? "All products healthy"
                : $"{snap.LowStockCount} low  ·  {snap.OutOfStockCount} out";
        }

        private void ApplyTrend(decimal? pct, string vsLabel, out string text, out string color)
        {
            color = "#64748B"; // Muted Gray
            if (pct == null) { text = vsLabel; return; }

            decimal value = pct.Value;
            if (value == 0) { text = $"— no change {vsLabel}"; return; }

            if (value > 0)
            {
                color = "#22C55E"; // Green
                text = $"▲ {value:0.#}% {vsLabel}";
            }
            else
            {
                color = "#EF4444"; // Red
                text = $"▼ {Math.Abs(value):0.#}% {vsLabel}";
            }
        }

        private void RenderCharts(DashboardSnapshot snap)
        {
            SalesChartTitle = snap.ChartTitle;

            var brand = SKColor.Parse("#4D6BFE");
            var muted = SKColor.Parse("#64748B");
            var ink = SKColor.Parse("#0F172A");
            var gridLine = SKColor.Parse("#EEF1F6");

            // 1. Sales Trend: smooth area chart
            var buckets = snap.ChartBuckets.ToList();

            SalesTrendSeries = new ISeries[]
            {
                new LineSeries<decimal>
                {
                    Values = buckets.Select(b => b.Value).ToArray(),
                    LineSmoothness = 0.25,
                    Stroke = new SolidColorPaint(brand) { StrokeThickness = 3 },
                    Fill = new LinearGradientPaint(
                        brand.WithAlpha(90), brand.WithAlpha(0),
                        new SKPoint(0.5f, 0), new SKPoint(0.5f, 1)),
                    // Show dots only when there are few points (e.g. "Today"); hide them on long ranges
                    GeometrySize = buckets.Count <= 12 ? 8 : 0,
                    GeometryFill = new SolidColorPaint(SKColors.White),
                    GeometryStroke = new SolidColorPaint(brand) { StrokeThickness = 3 },
                    YToolTipLabelFormatter = (point) => "₱" + point.Coordinate.PrimaryValue.ToString("N2")
                }
            };

            SalesTrendXAxes = new Axis[]
            {
                new Axis
                {
                    Labels = buckets.Select(b => b.Label).ToArray(),
                    LabelsPaint = new SolidColorPaint(muted),
                    TextSize = 12,
                    SeparatorsPaint = null,
                    LabelsRotation = 0,
                    Padding = new LiveChartsCore.Drawing.Padding(0, 10, 0, 0)
                }
            };

            SalesTrendYAxes = new Axis[]
            {
                new Axis
                {
                    LabelsPaint = new SolidColorPaint(muted),
                    TextSize = 12,
                    MinLimit = 0,
                    SeparatorsPaint = new SolidColorPaint(gridLine) { StrokeThickness = 1 },
                    Labeler = (value) => "₱" + value.ToString("N0")
                }
            };

            // 2. Top Products: ranked horizontal bars, highest on top
            var top = snap.TopProducts.OrderByDescending(p => p.Revenue).ToList();
            double maxRevenue = top.Count > 0 ? (double)top.Max(p => p.Revenue) : 0;

            TopProductsSeries = new ISeries[]
            {
                new RowSeries<decimal>
                {
                    Values = top.Select(p => p.Revenue).ToArray(),
                    Fill = new SolidColorPaint(brand),
                    MaxBarWidth = 18,
                    Padding = 6,
                    Rx = 6,
                    Ry = 6,
                    DataLabelsPaint = new SolidColorPaint(ink),
                    DataLabelsSize = 12,
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.End,
                    DataLabelsFormatter = (point) => "₱" + point.Coordinate.PrimaryValue.ToString("N0")
                }
            };

            TopProductsYAxes = new Axis[]
            {
                new Axis
                {
                    Labels = top.Select(p => p.ProductName != null && p.ProductName.Length > 15 ? p.ProductName.Substring(0, 15) + "..." : p.ProductName).ToArray(),
                    LabelsPaint = new SolidColorPaint(muted),
                    SeparatorsPaint = null,
                    TextSize = 12,
                    IsInverted = true // first item (best seller) at the top
                }
            };

            // X axis hidden: the value labels on the bars already show revenue.
            // MaxLimit leaves ~1/3 of the width empty so the label on the longest bar isn't clipped.
            TopProductsXAxes = new Axis[]
            {
                new Axis
                {
                    IsVisible = false,
                    SeparatorsPaint = null,
                    MinLimit = 0,
                    MaxLimit = maxRevenue > 0 ? maxRevenue * 1.5 : null
                }
            };
        }

        private void RenderLists(DashboardSnapshot snap)
        {
            RecentSales.Clear();
            foreach (var sale in snap.RecentSales)
            {
                RecentSales.Add(new RecentSaleItem
                {
                    ItemsSummary = sale.ItemsSummary,
                    SubtotalFormatted = "₱" + sale.Sale.Subtotal.ToString("N2"),
                    MetaData = $"{sale.Sale.InvoiceNo}  ·  {sale.Sale.SaleDate:MMM d, h:mm tt}  ·  {sale.Sale.PaymentMethod}"
                });
            }

            StockAlerts.Clear();
            foreach (var p in snap.LowStockAlerts)
            {
                StockAlerts.Add(new StockAlertItem
                {
                    Name = $"{p.ProductName} — {p.Brand}",
                    QuantityText = $"{p.QuantityOnHand} / {p.ReorderLevel}",
                    IsOutOfStock = p.QuantityOnHand == 0
                });
            }

            // Toggle the Empty State
            HasStockAlerts = StockAlerts.Count > 0;
        }
    }
}