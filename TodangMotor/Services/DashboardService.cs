using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TodangMotor.Common;
using TodangMotor.Data;
using TodangMotor.Models;

namespace TodangMotor.Services
{
    /// <summary>One bar of the dashboard sales chart.</summary>
    public class ChartBucket
    {
        public string Label { get; set; } = string.Empty;
        public decimal Value { get; set; }
    }

    /// <summary>Everything the dashboard needs for one render.</summary>
    public class DashboardSnapshot
    {
        // ---- Range-dependent stats ----
        public decimal SalesTotal { get; set; }
        public decimal CostTotal { get; set; }
        public decimal NetProfit { get; set; }
        public int TransactionCount { get; set; }
        public int ItemsSold { get; set; }
        public decimal AverageTransaction { get; set; }

        // ---- Trend percentages vs. previous equal-length period ----
        public decimal? SalesTrendPct { get; set; }
        public decimal? NetProfitTrendPct { get; set; }
        public decimal? TransactionsTrendPct { get; set; }

        // ---- Always-current product stats ----
        public int TotalProducts { get; set; }

        /// <summary>Products with 0 &lt; OnHand &lt;= ReorderLevel.</summary>
        public int LowStockCount { get; set; }

        /// <summary>Products with OnHand == 0.</summary>
        public int OutOfStockCount { get; set; }

        // ---- Chart ----
        public List<ChartBucket> ChartBuckets { get; set; } = new();
        public string ChartTitle { get; set; } = string.Empty;

        // ---- New charts (Phase 6B) ----
        public List<CategoryRevenueRow> SalesByCategory { get; set; } = new();
        public PaymentSplitRow PaymentSplit { get; set; } = new();

        // ---- Lists ----
        public List<TopProductRow> TopProducts { get; set; } = new();
        public List<Sale> RecentSales { get; set; } = new();
        public List<Product> LowStockAlerts { get; set; } = new();
    }

    /// <summary>
    /// Dashboard data provider. All range-dependent data is driven by
    /// explicit From/To dates (To is exclusive).
    /// </summary>
    public class DashboardService
    {
        private readonly ProductRepository _productRepository;
        private readonly SaleRepository _saleRepository;

        public DashboardService()
        {
            _productRepository = new ProductRepository();
            _saleRepository = new SaleRepository();
        }

        // ============================================================
        // SNAPSHOT
        // ============================================================

        /// <summary>
        /// Builds the full dashboard snapshot for the given date range.
        /// To is exclusive — pass To = end date + 1 day to include the whole end day.
        /// Never throws — returns an empty snapshot on failure.
        /// </summary>
        public async Task<DashboardSnapshot> GetSnapshotAsync(DateTime from, DateTime toExclusive)
        {
            var snapshot = new DashboardSnapshot();

            try
            {
                if (toExclusive <= from)
                    toExclusive = from.AddDays(1);

                snapshot.ChartTitle = BuildChartTitle(from, toExclusive);

                // ---- Range-dependent stats ----
                var currentLines = await _saleRepository.GetFlatSaleLinesAsync(from, toExclusive);

                snapshot.SalesTotal = currentLines.Sum(l => l.LineTotal);
                snapshot.TransactionCount = currentLines.Select(l => l.SaleId).Distinct().Count();
                snapshot.ItemsSold = currentLines.Sum(l => l.Quantity);
                snapshot.AverageTransaction = snapshot.TransactionCount > 0
                    ? Math.Round(snapshot.SalesTotal / snapshot.TransactionCount, 2)
                    : 0m;

                if (SessionManager.IsOwner)
                {
                    snapshot.CostTotal = currentLines
                        .Where(l => l.UnitCost.HasValue)
                        .Sum(l => l.UnitCost!.Value * l.Quantity);
                    snapshot.NetProfit = snapshot.SalesTotal - snapshot.CostTotal;
                }

                // ---- Chart buckets ----
                snapshot.ChartBuckets = BuildChartBuckets(from, toExclusive, currentLines);

                // ---- Trend vs. previous equal-length period ----
                var length = toExclusive - from;
                var prevFrom = from - length;
                var prevTo = from;

                var prevLines = await _saleRepository.GetFlatSaleLinesAsync(prevFrom, prevTo);

                decimal prevSales = prevLines.Sum(l => l.LineTotal);
                int prevTx = prevLines.Select(l => l.SaleId).Distinct().Count();

                snapshot.SalesTrendPct = ComputeTrend(snapshot.SalesTotal, prevSales);
                snapshot.TransactionsTrendPct = ComputeTrend(snapshot.TransactionCount, prevTx);

                if (SessionManager.IsOwner)
                {
                    decimal prevCost = prevLines
                        .Where(l => l.UnitCost.HasValue)
                        .Sum(l => l.UnitCost!.Value * l.Quantity);
                    decimal prevProfit = prevSales - prevCost;
                    snapshot.NetProfitTrendPct = ComputeTrend(snapshot.NetProfit, prevProfit);
                }

                // ---- Top products (existing horizontal list, still used by legacy view) ----
                snapshot.TopProducts = await _saleRepository.GetTopProductsAsync(from, toExclusive, 5);

                // ---- New chart data (Phase 6B) ----
                snapshot.SalesByCategory = await _saleRepository.GetSalesByCategoryAsync(from, toExclusive);
                snapshot.PaymentSplit = await _saleRepository.GetPaymentMethodSplitAsync(from, toExclusive);

                // ---- Recent sales in range ----
                var allSales = await _saleRepository.GetAllAsync();
                snapshot.RecentSales = allSales
                    .Where(s => s.SaleDate >= from && s.SaleDate < toExclusive)
                    .OrderByDescending(s => s.SaleDate)
                    .ThenByDescending(s => s.SaleId)
                    .Take(5)
                    .ToList();

                // ---- Always-current product stats ----
                var allProducts = await _productRepository.GetAllAsync();
                var activeProducts = allProducts.Where(p => p.IsActive).ToList();

                snapshot.TotalProducts = activeProducts.Count;
                snapshot.LowStockCount = activeProducts.Count(p => p.QuantityOnHand > 0
                                                                  && p.QuantityOnHand <= p.ReorderLevel);
                snapshot.OutOfStockCount = activeProducts.Count(p => p.QuantityOnHand == 0);

                // Low stock alerts — items below or at reorder (excludes 0-stock which are handled separately below).
                // We merge them here in the raw list and let the UI split them.
                snapshot.LowStockAlerts = activeProducts
                    .Where(p => p.QuantityOnHand <= p.ReorderLevel)
                    .OrderBy(p => p.QuantityOnHand - p.ReorderLevel)
                    .ThenBy(p => p.ProductName)
                    .Take(8)
                    .ToList();
            }
            catch
            {
                // Return whatever we have — UI shows zeros.
            }

            return snapshot;
        }

        // ============================================================
        // CHART TITLE
        // ============================================================

        private static string BuildChartTitle(DateTime from, DateTime toExclusive)
        {
            var span = toExclusive - from;

            string label = from.Date == toExclusive.AddDays(-1).Date
                ? from.ToString("MMMM d, yyyy")
                : $"{from:MMM d, yyyy} to {toExclusive.AddDays(-1):MMM d, yyyy}";

            if (span.TotalDays <= 1.5)
                return $"Sales — {label} (by hour)";

            return $"Sales — {label}";
        }

        // ============================================================
        // CHART BUCKETS (span-driven)
        // ============================================================

        private static List<ChartBucket> BuildChartBuckets(
            DateTime from,
            DateTime toExclusive,
            List<SaleLineFlat> lines)
        {
            var buckets = new List<ChartBucket>();
            var span = toExclusive - from;

            if (span.TotalDays <= 1.5)
            {
                // Hourly — 8 AM to 8 PM (13 buckets)
                var day = from.Date;
                for (int h = 8; h <= 20; h++)
                {
                    var start = day.AddHours(h);
                    var end = start.AddHours(1);
                    decimal sum = lines
                        .Where(l => l.SaleDate >= start && l.SaleDate < end)
                        .Sum(l => l.LineTotal);
                    buckets.Add(new ChartBucket { Label = $"{h:00}", Value = sum });
                }
            }
            else if (span.TotalDays <= 31)
            {
                int days = (int)Math.Ceiling(span.TotalDays);
                for (int i = 0; i < days; i++)
                {
                    var start = from.Date.AddDays(i);
                    var end = start.AddDays(1);
                    if (end > toExclusive) end = toExclusive;

                    decimal sum = lines
                        .Where(l => l.SaleDate >= start && l.SaleDate < end)
                        .Sum(l => l.LineTotal);

                    buckets.Add(new ChartBucket
                    {
                        Label = start.ToString("MMM d"),
                        Value = sum
                    });
                }
            }
            else if (span.TotalDays <= 180)
            {
                var cursor = from.Date;
                int weekNum = 1;
                while (cursor < toExclusive)
                {
                    var end = cursor.AddDays(7);
                    if (end > toExclusive) end = toExclusive;

                    decimal sum = lines
                        .Where(l => l.SaleDate >= cursor && l.SaleDate < end)
                        .Sum(l => l.LineTotal);

                    buckets.Add(new ChartBucket { Label = $"W{weekNum}", Value = sum });
                    cursor = end;
                    weekNum++;
                }
            }
            else
            {
                var cursor = new DateTime(from.Year, from.Month, 1);
                while (cursor < toExclusive)
                {
                    var end = cursor.AddMonths(1);
                    if (end > toExclusive) end = toExclusive;

                    decimal sum = lines
                        .Where(l => l.SaleDate >= cursor && l.SaleDate < end)
                        .Sum(l => l.LineTotal);

                    buckets.Add(new ChartBucket
                    {
                        Label = cursor.ToString("MMM yy"),
                        Value = sum
                    });
                    cursor = cursor.AddMonths(1);
                }
            }

            return buckets;
        }

        // ============================================================
        // TREND
        // ============================================================

        private static decimal? ComputeTrend(decimal current, decimal previous)
        {
            if (previous <= 0) return null;
            return Math.Round((current - previous) / previous * 100m, 1);
        }

        private static decimal? ComputeTrend(int current, int previous)
        {
            if (previous <= 0) return null;
            return Math.Round((current - previous) / (decimal)previous * 100m, 1);
        }
    }
}