using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TodangMotor.Data;
using TodangMotor.Models;

namespace TodangMotor.Services
{
    // ================================================================
    // PUBLIC TYPES
    // ================================================================

    /// <summary>Which period the dashboard is showing.</summary>
    public enum DashboardRange
    {
        Today,
        ThisWeek,
        ThisMonth,
        AllTime
    }

    /// <summary>One bar of the dashboard chart.</summary>
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

        // Trend percentages (null when AllTime or previous = 0)
        public decimal? SalesTrendPct { get; set; }
        public decimal? NetProfitTrendPct { get; set; }
        public decimal? TransactionsTrendPct { get; set; }

        // ---- Always-current stats ----
        public int TotalProducts { get; set; }
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }

        // ---- Chart ----
        public List<ChartBucket> ChartBuckets { get; set; } = new();
        public string ChartTitle { get; set; } = string.Empty;

        // ---- Lists ----
        public List<TopProductRow> TopProducts { get; set; } = new();
        public List<Sale> RecentSales { get; set; } = new();
        public List<Product> LowStockAlerts { get; set; } = new();
    }

    // ================================================================
    // SERVICE
    // ================================================================

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
        /// Builds a full dashboard snapshot for the given range.
        /// Never throws — returns an empty snapshot on failure.
        /// </summary>
        public async Task<DashboardSnapshot> GetSnapshotAsync(DashboardRange range)
        {
            var snapshot = new DashboardSnapshot();

            try
            {
                var (from, to) = GetRange(range);
                snapshot.ChartTitle = GetChartTitle(range);

                // ---- Range-dependent stats (from sale lines) ----
                var currentLines = await _saleRepository.GetFlatSaleLinesAsync(from, to);

                                snapshot.SalesTotal = currentLines.Sum(l => l.LineTotal);
                snapshot.TransactionCount = currentLines.Select(l => l.SaleId).Distinct().Count();

                // Cost and profit are Owner-only. Cashier never sees cost data —
                // don't even compute it.
                if (TodangMotor.Common.SessionManager.IsOwner)
                {
                    snapshot.CostTotal = currentLines
                        .Where(l => l.UnitCost.HasValue)
                        .Sum(l => l.UnitCost!.Value * l.Quantity);
                    snapshot.NetProfit = snapshot.SalesTotal - snapshot.CostTotal;
                }

                // ---- Chart buckets ----
                snapshot.ChartBuckets = BuildChartBuckets(range, from, to, currentLines);

                // ---- Trend vs. previous equivalent period ----
                if (range != DashboardRange.AllTime)
                {
                    var (prevFrom, prevTo) = GetPreviousRange(range);
                    var prevLines = await _saleRepository.GetFlatSaleLinesAsync(prevFrom, prevTo);

                    decimal prevSales = prevLines.Sum(l => l.LineTotal);
                    int prevTx = prevLines.Select(l => l.SaleId).Distinct().Count();

                    snapshot.SalesTrendPct = ComputeTrend(snapshot.SalesTotal, prevSales);
                    snapshot.TransactionsTrendPct = ComputeTrend(snapshot.TransactionCount, prevTx);

                    if (TodangMotor.Common.SessionManager.IsOwner)
                    {
                        decimal prevCost = prevLines
                            .Where(l => l.UnitCost.HasValue)
                            .Sum(l => l.UnitCost!.Value * l.Quantity);
                        decimal prevProfit = prevSales - prevCost;
                        snapshot.NetProfitTrendPct = ComputeTrend(snapshot.NetProfit, prevProfit);
                    }
                }

                // ---- Top products (range-dependent) ----
                snapshot.TopProducts = await _saleRepository.GetTopProductsAsync(from, to, 5);

                // ---- Recent sales (range-dependent) ----
                var allSales = await _saleRepository.GetAllAsync();
                snapshot.RecentSales = allSales
                    .Where(s => s.SaleDate >= from && s.SaleDate < to)
                    .OrderByDescending(s => s.SaleDate)
                    .ThenByDescending(s => s.SaleId)
                    .Take(5)
                    .ToList();

                // ---- Always-current product stats ----
                var allProducts = await _productRepository.GetAllAsync();
                var activeProducts = allProducts.Where(p => p.IsActive).ToList();

                snapshot.TotalProducts = activeProducts.Count;
                snapshot.LowStockCount = activeProducts.Count(p => p.QuantityOnHand <= p.ReorderLevel);
                snapshot.OutOfStockCount = activeProducts.Count(p => p.QuantityOnHand == 0);

                snapshot.LowStockAlerts = activeProducts
                    .Where(p => p.QuantityOnHand <= p.ReorderLevel)
                    .OrderBy(p => p.QuantityOnHand - p.ReorderLevel)
                    .ThenBy(p => p.ProductName)
                    .Take(5)
                    .ToList();
            }
            catch
            {
                // Return empty snapshot on any DB error — the UI shows zeros.
            }

            return snapshot;
        }

        // ============================================================
        // RANGE HELPERS
        // ============================================================

        private static (DateTime From, DateTime ToExclusive) GetRange(DashboardRange range)
        {
            var now = DateTime.Now;
            var today = now.Date;

            switch (range)
            {
                case DashboardRange.Today:
                    return (today, today.AddDays(1));

                case DashboardRange.ThisWeek:
                    {
                        // Monday 00:00 → next Monday 00:00
                        int diff = ((int)today.DayOfWeek + 6) % 7; // Mon=0 ... Sun=6
                        var monday = today.AddDays(-diff);
                        return (monday, monday.AddDays(7));
                    }

                case DashboardRange.ThisMonth:
                    {
                        var firstOfMonth = new DateTime(today.Year, today.Month, 1);
                        return (firstOfMonth, firstOfMonth.AddMonths(1));
                    }

                case DashboardRange.AllTime:
                default:
                    return (new DateTime(2000, 1, 1), today.AddDays(1));
            }
        }

        private static (DateTime From, DateTime ToExclusive) GetPreviousRange(DashboardRange range)
        {
            var (from, to) = GetRange(range);
            var length = to - from;
            return (from - length, from);
        }

        private static string GetChartTitle(DashboardRange range)
        {
            return range switch
            {
                DashboardRange.Today => "Sales — Today (by hour)",
                DashboardRange.ThisWeek => "Sales — This Week",
                DashboardRange.ThisMonth => "Sales — This Month",
                DashboardRange.AllTime => "Sales — Last 12 Months",
                _ => "Sales"
            };
        }

        // ============================================================
        // CHART BUCKETS
        // ============================================================

        private static List<ChartBucket> BuildChartBuckets(
            DashboardRange range,
            DateTime from,
            DateTime to,
            List<SaleLineFlat> lines)
        {
            var buckets = new List<ChartBucket>();

            switch (range)
            {
                case DashboardRange.Today:
                    {
                        // 13 hourly buckets: 8 AM through 8 PM.
                        for (int h = 8; h <= 20; h++)
                        {
                            var start = from.Date.AddHours(h);
                            var end = start.AddHours(1);
                            decimal sum = lines
                                .Where(l => l.SaleDate >= start && l.SaleDate < end)
                                .Sum(l => l.LineTotal);
                            buckets.Add(new ChartBucket
                            {
                                Label = $"{h:00}",
                                Value = sum
                            });
                        }
                        return buckets;
                    }

                case DashboardRange.ThisWeek:
                    {
                        // 7 daily buckets: Mon..Sun.
                        for (int i = 0; i < 7; i++)
                        {
                            var start = from.Date.AddDays(i);
                            var end = start.AddDays(1);
                            decimal sum = lines
                                .Where(l => l.SaleDate >= start && l.SaleDate < end)
                                .Sum(l => l.LineTotal);
                            buckets.Add(new ChartBucket
                            {
                                Label = start.ToString("ddd"),
                                Value = sum
                            });
                        }
                        return buckets;
                    }

                case DashboardRange.ThisMonth:
                    {
                        // 7-day buckets starting from day 1 (W1..W5).
                        var firstOfMonth = from.Date;
                        int daysInMonth = DateTime.DaysInMonth(firstOfMonth.Year, firstOfMonth.Month);

                        int weekNum = 1;
                        for (int dayOfMonth = 1; dayOfMonth <= daysInMonth; dayOfMonth += 7, weekNum++)
                        {
                            var start = firstOfMonth.AddDays(dayOfMonth - 1);
                            var end = start.AddDays(7);
                            if (end > to) end = to;

                            decimal sum = lines
                                .Where(l => l.SaleDate >= start && l.SaleDate < end)
                                .Sum(l => l.LineTotal);
                            buckets.Add(new ChartBucket
                            {
                                Label = $"W{weekNum}",
                                Value = sum
                            });
                        }
                        return buckets;
                    }

                case DashboardRange.AllTime:
                default:
                    {
                        // 12 monthly buckets ending with the current month.
                        var now = DateTime.Now.Date;
                        var firstOfThisMonth = new DateTime(now.Year, now.Month, 1);

                        for (int i = 11; i >= 0; i--)
                        {
                            var mStart = firstOfThisMonth.AddMonths(-i);
                            var mEnd = mStart.AddMonths(1);
                            decimal sum = lines
                                .Where(l => l.SaleDate >= mStart && l.SaleDate < mEnd)
                                .Sum(l => l.LineTotal);
                            buckets.Add(new ChartBucket
                            {
                                Label = mStart.ToString("MMM"),
                                Value = sum
                            });
                        }
                        return buckets;
                    }
            }
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