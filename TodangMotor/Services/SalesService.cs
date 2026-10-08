using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TodangMotor.Common;
using TodangMotor.Data;
using TodangMotor.Models;

namespace TodangMotor.Services
{
    // ================================================================
    // DTOs
    // ================================================================

    public class SaleRequestLine
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public class CashierProductView
    {
        public int ProductId { get; set; }
        public int CategoryId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal SellingPrice { get; set; }
        public int QuantityOnHand { get; set; }
        public int ReorderLevel { get; set; }
    }

    public class SaleLineDisplay
    {
        public string ProductName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class SaleDetailResult
    {
        public Sale Sale { get; set; } = new();
        public List<SaleLineDisplay> Lines { get; set; } = new();
        public string CashierName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Result envelope for the revenue report. Owner-only.
    /// </summary>
    public class RevenueReportResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }      // exclusive
        public string PeriodLabel { get; set; } = string.Empty;

        public decimal TotalRevenue { get; set; }
        public decimal TotalCost { get; set; }
        public decimal TotalNetIncome { get; set; }
        public int TotalTransactions { get; set; }

        /// <summary>True when every line in the range has a recorded cost.</summary>
        public bool IsCostComplete { get; set; }
        public int DaysWithMissingCost { get; set; }

        public List<DailyRevenueRow> DailyRows { get; set; } = new();
    }

    // ================================================================
    // SERVICE
    // ================================================================

    public class SalesService
    {
        private readonly SaleRepository _saleRepository;
        private readonly ProductRepository _productRepository;
        private readonly UserRepository _userRepository;

        private const int CustomerNameMaxLength = 100;

        private const string NotLoggedInMessage =
            "You must be logged in to perform this action.";
        private const string AccessDeniedMessage =
            "Access denied. Only the Owner can generate this report.";
        private const string DbErrorMessage =
            "Cannot complete this action right now. Please check your database connection.";

        public SalesService()
        {
            _saleRepository = new SaleRepository();
            _productRepository = new ProductRepository();
            _userRepository = new UserRepository();
        }

        // ============================================================
        // PRODUCT LOOKUP FOR POS
        // ============================================================

        public async Task<(bool Success, string ErrorMessage, List<CashierProductView> Products)>
            GetActiveProductsForCashierAsync()
        {
            if (!SessionManager.IsLoggedIn)
                return (false, NotLoggedInMessage, new List<CashierProductView>());

            try
            {
                var all = await _productRepository.GetAllAsync();
                var view = all
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.ProductName)
                    .ThenBy(p => p.Brand)
                    .Select(p => new CashierProductView
                    {
                        ProductId = p.ProductId,
                        CategoryId = p.CategoryId,
                        ProductName = p.ProductName ?? string.Empty,
                        Brand = p.Brand ?? string.Empty,
                        Unit = p.Unit ?? string.Empty,
                        SellingPrice = p.SellingPrice,
                        QuantityOnHand = p.QuantityOnHand,
                        ReorderLevel = p.ReorderLevel
                    })
                    .ToList();

                return (true, string.Empty, view);
            }
            catch (Exception)
            {
                return (false, DbErrorMessage, new List<CashierProductView>());
            }
        }

        // ============================================================
        // COMPLETE SALE
        // ============================================================

        public async Task<(bool Success, string ErrorMessage, int SaleId, string InvoiceNo)>
            CompleteSaleAsync(
                List<SaleRequestLine> lines,
                string? paymentMethod,
                decimal amountTendered,
                string? customerName)
        {
            if (!SessionManager.IsLoggedIn)
                return (false, NotLoggedInMessage, 0, string.Empty);

            int userId = SessionManager.CurrentUser?.UserId ?? 0;
            if (userId <= 0)
                return (false, "Could not identify the current user.", 0, string.Empty);

            if (lines == null || lines.Count == 0)
                return (false, "Add at least one item before completing the sale.", 0, string.Empty);

            foreach (var l in lines)
            {
                if (l.ProductId <= 0)
                    return (false, "A cart item has an invalid product.", 0, string.Empty);
                if (l.Quantity <= 0)
                    return (false, "Every cart item must have a quantity greater than zero.", 0, string.Empty);
            }

            if (lines.GroupBy(l => l.ProductId).Any(g => g.Count() > 1))
                return (false, "The same product appears more than once in the cart.", 0, string.Empty);

            string cleanPayment = (paymentMethod ?? string.Empty).Trim();
            if (cleanPayment != "Cash" && cleanPayment != "GCash")
                return (false, "Payment method must be Cash or GCash.", 0, string.Empty);

            string? cleanCustomerName = string.IsNullOrWhiteSpace(customerName)
                ? null
                : customerName.Trim();

            if (cleanCustomerName != null && cleanCustomerName.Length > CustomerNameMaxLength)
                return (false,
                    $"Customer name cannot exceed {CustomerNameMaxLength} characters.",
                    0, string.Empty);

            var productMap = new Dictionary<int, Product>();

            try
            {
                foreach (var line in lines)
                {
                    var product = await _productRepository.GetByIdAsync(line.ProductId);

                    if (product == null)
                        return (false,
                            "A product in the cart no longer exists. Please remove it and try again.",
                            0, string.Empty);

                    if (!product.IsActive)
                        return (false,
                            $"\"{product.ProductName}\" is no longer available for sale.",
                            0, string.Empty);

                    if (line.Quantity > product.QuantityOnHand)
                        return (false,
                            $"Not enough stock for \"{product.ProductName}\". " +
                            $"Only {product.QuantityOnHand} left.",
                            0, string.Empty);

                    productMap[line.ProductId] = product;
                }
            }
            catch (Exception)
            {
                return (false, DbErrorMessage, 0, string.Empty);
            }

            var items = new List<SaleItem>();
            decimal subtotal = 0m;

            foreach (var line in lines)
            {
                var p = productMap[line.ProductId];
                decimal lineTotal = p.SellingPrice * line.Quantity;

                items.Add(new SaleItem
                {
                    ProductId = p.ProductId,
                    Quantity = line.Quantity,
                    UnitPrice = p.SellingPrice,
                    LineTotal = lineTotal,
                    UnitCost = p.CostPrice
                });

                subtotal += lineTotal;
            }

            decimal change = 0m;

            if (cleanPayment == "Cash")
            {
                if (amountTendered < subtotal)
                    return (false,
                        $"Amount tendered (₱{amountTendered:N2}) is less than the subtotal (₱{subtotal:N2}).",
                        0, string.Empty);

                change = amountTendered - subtotal;
            }
            else
            {
                amountTendered = subtotal;
                change = 0m;
            }

            var header = new Sale
            {
                InvoiceNo = string.Empty,
                UserId = userId,
                SaleDate = DateTime.Now,
                CustomerName = cleanCustomerName,
                PaymentMethod = cleanPayment,
                Subtotal = subtotal,
                AmountTendered = amountTendered,
                ChangeAmount = change,
                Status = "Completed"
            };

            try
            {
                var (success, error, saleId, invoiceNo) =
                    await _saleRepository.InsertSaleAsync(header, items, userId);

                if (!success)
                    return (false, error, 0, string.Empty);

                return (true, string.Empty, saleId, invoiceNo);
            }
            catch (Exception)
            {
                return (false, DbErrorMessage, 0, string.Empty);
            }
        }

        // ============================================================
        // READS — history + dashboard
        // ============================================================

        public async Task<(bool Success, string ErrorMessage, List<Sale> Sales)> GetAllSalesAsync()
        {
            if (!SessionManager.IsLoggedIn)
                return (false, NotLoggedInMessage, new List<Sale>());

            try
            {
                var sales = await _saleRepository.GetAllAsync();
                return (true, string.Empty, sales);
            }
            catch (Exception)
            {
                return (false, DbErrorMessage, new List<Sale>());
            }
        }

        public async Task<(bool Success, string ErrorMessage, List<Sale> Sales)> GetSalesInRangeAsync(
            DateTime fromInclusive, DateTime toExclusive)
        {
            if (!SessionManager.IsLoggedIn)
                return (false, NotLoggedInMessage, new List<Sale>());

            try
            {
                var sales = await _saleRepository.GetByDateRangeAsync(fromInclusive, toExclusive);
                return (true, string.Empty, sales);
            }
            catch (Exception)
            {
                return (false, DbErrorMessage, new List<Sale>());
            }
        }

        public async Task<(bool Success, string ErrorMessage, SaleDetailResult? Detail)>
            GetSaleDetailAsync(int saleId)
        {
            if (!SessionManager.IsLoggedIn)
                return (false, NotLoggedInMessage, null);

            if (saleId <= 0)
                return (false, "Invalid sale.", null);

            try
            {
                var sale = await _saleRepository.GetByIdAsync(saleId);
                if (sale == null)
                    return (false, "Sale not found.", null);

                var items = await _saleRepository.GetItemsAsync(saleId);

                var displayLines = new List<SaleLineDisplay>();
                foreach (var item in items)
                {
                    var p = await _productRepository.GetByIdAsync(item.ProductId);

                    displayLines.Add(new SaleLineDisplay
                    {
                        ProductName = p?.ProductName ?? "(unknown product)",
                        Brand = p?.Brand ?? string.Empty,
                        Unit = p?.Unit ?? string.Empty,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        LineTotal = item.LineTotal
                    });
                }

                string cashierName = string.Empty;
                var cashier = await _userRepository.GetByIdAsync(sale.UserId);
                if (cashier != null)
                    cashierName = cashier.FullName;

                var result = new SaleDetailResult
                {
                    Sale = sale,
                    Lines = displayLines,
                    CashierName = cashierName
                };

                return (true, string.Empty, result);
            }
            catch (Exception)
            {
                return (false, DbErrorMessage, null);
            }
        }

        public async Task<(bool Success, string ErrorMessage, decimal Total, int Count)>
            GetTodayStatsAsync()
        {
            if (!SessionManager.IsLoggedIn)
                return (false, NotLoggedInMessage, 0m, 0);

            try
            {
                var (total, count) = await _saleRepository.GetTodayStatsAsync();
                return (true, string.Empty, total, count);
            }
            catch (Exception)
            {
                return (false, DbErrorMessage, 0m, 0);
            }
        }

        // ============================================================
        // REVENUE REPORT (Owner-only)
        // ============================================================

        /// <summary>
        /// Aggregates revenue and profit for the given date range. Owner-only.
        /// </summary>
        public async Task<RevenueReportResult> GenerateRevenueReportAsync(
            DateTime fromDate,
            DateTime toDateExclusive,
            string periodLabel)
        {
            var result = new RevenueReportResult
            {
                FromDate = fromDate,
                ToDate = toDateExclusive,
                PeriodLabel = periodLabel
            };

            if (!SessionManager.IsOwner)
            {
                result.ErrorMessage = AccessDeniedMessage;
                return result;
            }

            if (toDateExclusive <= fromDate)
            {
                result.ErrorMessage = "The end of the report range must come after the start.";
                return result;
            }

            try
            {
                var daily = await _saleRepository.GetDailyRevenueReportAsync(
                    fromDate, toDateExclusive);

                result.DailyRows = daily ?? new List<DailyRevenueRow>();

                result.TotalRevenue = result.DailyRows.Sum(r => r.Revenue);
                result.TotalCost = result.DailyRows.Sum(r => r.KnownCost);
                result.TotalNetIncome = result.TotalRevenue - result.TotalCost;
                result.TotalTransactions = result.DailyRows.Sum(r => r.TransactionCount);

                int incompleteDays = result.DailyRows.Count(r => !r.HasCompleteCost);
                result.DaysWithMissingCost = incompleteDays;
                result.IsCostComplete = incompleteDays == 0;

                result.Success = true;
                return result;
            }
            catch (Exception)
            {
                result.ErrorMessage = DbErrorMessage;
                return result;
            }
        }
    }
}