using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using Microsoft.Data.SqlClient;
using TodangMotor.Models;

namespace TodangMotor.Data
{
    /// <summary>
    /// Data access for Sales and SaleItems.
    /// All writes happen inside atomic transactions.
    /// Includes revenue report aggregation and dashboard queries.
    /// </summary>
    public class SaleRepository
    {
        // ============================================================
        // INSERT SALE (atomic)
        // ============================================================

        public async Task<(bool Success, string ErrorMessage, string InvoiceNo)> InsertSaleAsync(
            Sale header,
            List<SaleItem> items,
            int userId)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));
            if (items == null || items.Count == 0)
                throw new ArgumentException("A sale must have at least one item.", nameof(items));

            const int maxRetries = 3;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    using var connection = DbConnectionFactory.CreateConnection();
                    connection.Open();
                    using var transaction = connection.BeginTransaction();

                    try
                    {
                        string invoiceNo = await GenerateInvoiceNoAsync(connection, transaction);

                        const string headerSql = @"
                            INSERT INTO Sales
                                (InvoiceNo, UserId, SaleDate, CustomerName, PaymentMethod,
                                 Subtotal, AmountTendered, ChangeAmount, Status)
                            VALUES
                                (@InvoiceNo, @UserId, @SaleDate, @CustomerName, @PaymentMethod,
                                 @Subtotal, @AmountTendered, @ChangeAmount, 'Completed');

                            SELECT CAST(SCOPE_IDENTITY() AS int);";

                        int saleId = await connection.QuerySingleAsync<int>(headerSql, new
                        {
                            InvoiceNo = invoiceNo,
                            UserId = userId,
                            SaleDate = header.SaleDate,
                            CustomerName = header.CustomerName,
                            PaymentMethod = header.PaymentMethod,
                            Subtotal = header.Subtotal,
                            AmountTendered = header.AmountTendered,
                            ChangeAmount = header.ChangeAmount
                        }, transaction);

                        foreach (var item in items)
                        {
                            const string itemSql = @"
                                INSERT INTO SaleItems
                                    (SaleId, ProductId, Quantity, UnitPrice, LineTotal, UnitCost)
                                VALUES
                                    (@SaleId, @ProductId, @Quantity, @UnitPrice, @LineTotal, @UnitCost);";

                            await connection.ExecuteAsync(itemSql, new
                            {
                                SaleId = saleId,
                                item.ProductId,
                                item.Quantity,
                                item.UnitPrice,
                                item.LineTotal,
                                item.UnitCost
                            }, transaction);

                            const string readQtySql = @"
                                SELECT QuantityOnHand
                                FROM Products
                                WHERE ProductId = @ProductId;";

                            int beforeQty = await connection.QuerySingleAsync<int>(
                                readQtySql, new { item.ProductId }, transaction);

                            if (beforeQty < item.Quantity)
                            {
                                throw new InvalidOperationException(
                                    $"Not enough stock for ProductId {item.ProductId}. " +
                                    $"On hand: {beforeQty}, requested: {item.Quantity}.");
                            }

                            int afterQty = beforeQty - item.Quantity;

                            const string updateQtySql = @"
                                UPDATE Products
                                SET QuantityOnHand = @QuantityAfter,
                                    UpdatedAt      = GETDATE()
                                WHERE ProductId = @ProductId;";

                            await connection.ExecuteAsync(updateQtySql,
                                new { item.ProductId, QuantityAfter = afterQty },
                                transaction);

                            const string movementSql = @"
                                INSERT INTO StockMovements
                                    (ProductId, MovementType, QuantityChange,
                                     QuantityBefore, QuantityAfter, ReferenceId, UserId,
                                     MovementDate, Notes)
                                VALUES
                                    (@ProductId, 'Sale', @QuantityChange,
                                     @QuantityBefore, @QuantityAfter, @SaleId, @UserId,
                                     GETDATE(), NULL);";

                            await connection.ExecuteAsync(movementSql, new
                            {
                                item.ProductId,
                                QuantityChange = -item.Quantity,
                                QuantityBefore = beforeQty,
                                QuantityAfter = afterQty,
                                SaleId = saleId,
                                UserId = userId
                            }, transaction);
                        }

                        transaction.Commit();
                        return (true, string.Empty, invoiceNo);
                    }
                    catch
                    {
                        try { transaction.Rollback(); } catch { /* ignore */ }
                        throw;
                    }
                }
                catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
                {
                    if (attempt < maxRetries - 1)
                        continue;

                    return (false,
                        "Could not generate a unique invoice number after multiple attempts. Please try again.",
                        string.Empty);
                }
            }

            return (false, "Could not save the sale.", string.Empty);
        }

        // ============================================================
        // VOID SALE (atomic)
        // ============================================================

        public async Task<(bool Success, string ErrorMessage)> VoidSaleAsync(
            int saleId,
            int voidedByUserId,
            string voidReason)
        {
            if (saleId <= 0) throw new ArgumentException("Invalid sale ID.", nameof(saleId));

            using var connection = DbConnectionFactory.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                const string statusSql = @"
                    SELECT Status
                    FROM Sales
                    WHERE SaleId = @SaleId;";

                string? currentStatus = await connection.QuerySingleOrDefaultAsync<string>(
                    statusSql, new { SaleId = saleId }, transaction);

                if (currentStatus == null)
                {
                    transaction.Rollback();
                    return (false, "Sale not found.");
                }
                if (currentStatus == "Void")
                {
                    transaction.Rollback();
                    return (false, "This sale has already been voided.");
                }

                const string itemsSql = @"
                    SELECT SaleItemId, SaleId, ProductId, Quantity, UnitPrice, LineTotal, UnitCost
                    FROM SaleItems
                    WHERE SaleId = @SaleId;";

                var items = (await connection.QueryAsync<SaleItem>(
                    itemsSql, new { SaleId = saleId }, transaction)).ToList();

                if (items.Count == 0)
                {
                    transaction.Rollback();
                    return (false, "This sale has no items to void.");
                }

                const string updateSaleSql = @"
                    UPDATE Sales
                    SET Status         = 'Void',
                        VoidedByUserId = @VoidedByUserId,
                        VoidedAt       = GETDATE(),
                        VoidReason     = @VoidReason
                    WHERE SaleId = @SaleId;";

                await connection.ExecuteAsync(updateSaleSql, new
                {
                    SaleId = saleId,
                    VoidedByUserId = voidedByUserId,
                    VoidReason = voidReason
                }, transaction);

                foreach (var item in items)
                {
                    const string readQtySql = @"
                        SELECT QuantityOnHand
                        FROM Products
                        WHERE ProductId = @ProductId;";

                    int beforeQty = await connection.QuerySingleAsync<int>(
                        readQtySql, new { item.ProductId }, transaction);

                    int afterQty = beforeQty + item.Quantity;

                    const string updateQtySql = @"
                        UPDATE Products
                        SET QuantityOnHand = @QuantityAfter,
                            UpdatedAt      = GETDATE()
                        WHERE ProductId = @ProductId;";

                    await connection.ExecuteAsync(updateQtySql,
                        new { item.ProductId, QuantityAfter = afterQty },
                        transaction);

                    const string movementSql = @"
                        INSERT INTO StockMovements
                            (ProductId, MovementType, QuantityChange,
                             QuantityBefore, QuantityAfter, ReferenceId, UserId,
                             MovementDate, Notes)
                        VALUES
                            (@ProductId, 'Void', @QuantityChange,
                             @QuantityBefore, @QuantityAfter, @SaleId, @UserId,
                             GETDATE(), @Notes);";

                    await connection.ExecuteAsync(movementSql, new
                    {
                        item.ProductId,
                        QuantityChange = item.Quantity,
                        QuantityBefore = beforeQty,
                        QuantityAfter = afterQty,
                        SaleId = saleId,
                        UserId = voidedByUserId,
                        Notes = voidReason
                    }, transaction);
                }

                transaction.Commit();
                return (true, string.Empty);
            }
            catch
            {
                try { transaction.Rollback(); } catch { /* ignore */ }
                throw;
            }
        }

        // ============================================================
        // READS
        // ============================================================

        public async Task<List<Sale>> GetAllAsync()
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                SELECT SaleId, InvoiceNo, UserId, SaleDate, CustomerName,
                       PaymentMethod, Subtotal, AmountTendered, ChangeAmount,
                       Status, VoidedByUserId, VoidedAt, VoidReason
                FROM Sales
                ORDER BY SaleId DESC;";

            var rows = await connection.QueryAsync<Sale>(sql);
            return rows.ToList();
        }

        public async Task<List<Sale>> GetByDateRangeAsync(DateTime fromInclusive, DateTime toExclusive)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                SELECT SaleId, InvoiceNo, UserId, SaleDate, CustomerName,
                       PaymentMethod, Subtotal, AmountTendered, ChangeAmount,
                       Status, VoidedByUserId, VoidedAt, VoidReason
                FROM Sales
                WHERE SaleDate >= @From AND SaleDate < @To
                ORDER BY SaleId DESC;";

            var rows = await connection.QueryAsync<Sale>(sql, new
            {
                From = fromInclusive,
                To = toExclusive
            });
            return rows.ToList();
        }

        public async Task<Sale?> GetByIdAsync(int saleId)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                SELECT SaleId, InvoiceNo, UserId, SaleDate, CustomerName,
                       PaymentMethod, Subtotal, AmountTendered, ChangeAmount,
                       Status, VoidedByUserId, VoidedAt, VoidReason
                FROM Sales
                WHERE SaleId = @SaleId;";

            return await connection.QuerySingleOrDefaultAsync<Sale>(
                sql, new { SaleId = saleId });
        }

        public async Task<List<SaleItem>> GetItemsAsync(int saleId)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                SELECT SaleItemId, SaleId, ProductId, Quantity, UnitPrice, LineTotal, UnitCost
                FROM SaleItems
                WHERE SaleId = @SaleId
                ORDER BY SaleItemId;";

            var rows = await connection.QueryAsync<SaleItem>(sql, new { SaleId = saleId });
            return rows.ToList();
        }

        public async Task<(decimal TodayTotal, int TodayCount)> GetTodayStatsAsync()
        {
            using var connection = DbConnectionFactory.CreateConnection();

            DateTime today = DateTime.Now.Date;
            DateTime tomorrow = today.AddDays(1);

            const string sql = @"
                SELECT
                    ISNULL(SUM(Subtotal), 0) AS TotalAmount,
                    COUNT(*) AS TransactionCount
                FROM Sales
                WHERE SaleDate >= @From
                  AND SaleDate <  @To
                  AND Status = 'Completed';";

            var row = await connection.QuerySingleAsync<(decimal TotalAmount, int TransactionCount)>(
                sql, new { From = today, To = tomorrow });

            return (row.TotalAmount, row.TransactionCount);
        }

        // ============================================================
        // REVENUE REPORT
        // ============================================================

        public async Task<List<DailyRevenueRow>> GetDailyRevenueReportAsync(
            DateTime fromInclusive,
            DateTime toExclusive)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                SELECT
                    CAST(s.SaleDate AS DATE)                                        AS SaleDay,
                    COUNT(DISTINCT s.SaleId)                                        AS TransactionCount,
                    ISNULL(SUM(si.LineTotal), 0)                                    AS Revenue,
                    ISNULL(SUM(CASE WHEN si.UnitCost IS NOT NULL
                                    THEN si.UnitCost * si.Quantity
                                    ELSE 0 END), 0)                                 AS KnownCost,
                    SUM(CASE WHEN si.UnitCost IS NULL THEN 1 ELSE 0 END)            AS UnknownCostLineCount,
                    COUNT(*)                                                        AS TotalLineCount
                FROM Sales s
                INNER JOIN SaleItems si ON si.SaleId = s.SaleId
                WHERE s.SaleDate >= @From
                  AND s.SaleDate <  @To
                  AND s.Status = 'Completed'
                GROUP BY CAST(s.SaleDate AS DATE)
                ORDER BY SaleDay DESC;";

            var rows = await connection.QueryAsync<DailyRevenueRow>(sql, new
            {
                From = fromInclusive,
                To = toExclusive
            });

            return rows.ToList();
        }

        public async Task<int> GetVoidCountInRangeAsync(
            DateTime fromInclusive,
            DateTime toExclusive)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                SELECT COUNT(*)
                FROM Sales
                WHERE SaleDate >= @From
                  AND SaleDate <  @To
                  AND Status = 'Void';";

            return await connection.ExecuteScalarAsync<int>(sql, new
            {
                From = fromInclusive,
                To = toExclusive
            });
        }

        // ============================================================
        // TOP PRODUCTS
        // ============================================================

        public async Task<List<TopProductRow>> GetTopProductsAsync(
            DateTime fromInclusive,
            DateTime toExclusive,
            int take)
        {
            if (take <= 0) take = 5;

            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                SELECT TOP (@Take)
                    p.ProductId,
                    p.ProductName,
                    p.Brand,
                    SUM(si.Quantity)   AS QuantitySold,
                    SUM(si.LineTotal)  AS Revenue
                FROM SaleItems si
                INNER JOIN Sales s    ON s.SaleId = si.SaleId
                INNER JOIN Products p ON p.ProductId = si.ProductId
                WHERE s.SaleDate >= @From
                  AND s.SaleDate <  @To
                  AND s.Status = 'Completed'
                GROUP BY p.ProductId, p.ProductName, p.Brand
                ORDER BY Revenue DESC;";

            var rows = await connection.QueryAsync<TopProductRow>(sql, new
            {
                From = fromInclusive,
                To = toExclusive,
                Take = take
            });

            return rows.ToList();
        }

        // ============================================================
        // FLAT SALE LINES (used by dashboard for in-memory aggregation)
        // ============================================================

        public async Task<List<SaleLineFlat>> GetFlatSaleLinesAsync(
            DateTime fromInclusive,
            DateTime toExclusive)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                SELECT si.SaleId, s.SaleDate, si.ProductId, si.Quantity,
                       si.LineTotal, si.UnitCost
                FROM SaleItems si
                INNER JOIN Sales s ON s.SaleId = si.SaleId
                WHERE s.SaleDate >= @From
                  AND s.SaleDate <  @To
                  AND s.Status = 'Completed';";

            var rows = await connection.QueryAsync<SaleLineFlat>(sql, new
            {
                From = fromInclusive,
                To = toExclusive
            });

            return rows.ToList();
        }

        // ============================================================
        // DASHBOARD QUERIES (Phase 6B)
        // ============================================================

        /// <summary>
        /// Revenue grouped by category within the range.
        /// Excludes void sales. Returns descending by revenue.
        /// </summary>
        public async Task<List<CategoryRevenueRow>> GetSalesByCategoryAsync(
            DateTime fromInclusive,
            DateTime toExclusive)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                SELECT
                    c.CategoryId,
                    c.CategoryName,
                    ISNULL(SUM(si.LineTotal), 0) AS Revenue,
                    ISNULL(SUM(si.Quantity), 0)  AS UnitsSold
                FROM SaleItems si
                INNER JOIN Sales s       ON s.SaleId = si.SaleId
                INNER JOIN Products p    ON p.ProductId = si.ProductId
                INNER JOIN Categories c  ON c.CategoryId = p.CategoryId
                WHERE s.SaleDate >= @From
                  AND s.SaleDate <  @To
                  AND s.Status = 'Completed'
                GROUP BY c.CategoryId, c.CategoryName
                ORDER BY Revenue DESC;";

            var rows = await connection.QueryAsync<CategoryRevenueRow>(sql, new
            {
                From = fromInclusive,
                To = toExclusive
            });

            return rows.ToList();
        }

        /// <summary>
        /// Total amount tendered split by payment method within the range.
        /// Excludes void sales.
        /// </summary>
        public async Task<PaymentSplitRow> GetPaymentMethodSplitAsync(
            DateTime fromInclusive,
            DateTime toExclusive)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                SELECT
                    ISNULL(SUM(CASE WHEN PaymentMethod = 'Cash'  THEN Subtotal ELSE 0 END), 0) AS CashAmount,
                    ISNULL(SUM(CASE WHEN PaymentMethod = 'GCash' THEN Subtotal ELSE 0 END), 0) AS GCashAmount,
                    COUNT(*) AS TransactionCount
                FROM Sales
                WHERE SaleDate >= @From
                  AND SaleDate <  @To
                  AND Status = 'Completed';";

            var row = await connection.QuerySingleOrDefaultAsync<PaymentSplitRow>(sql, new
            {
                From = fromInclusive,
                To = toExclusive
            });

            return row ?? new PaymentSplitRow();
        }

        /// <summary>
        /// Total units sold (sum of SaleItems.Quantity) in the range.
        /// Excludes void sales.
        /// </summary>
        public async Task<int> GetItemsSoldCountAsync(
            DateTime fromInclusive,
            DateTime toExclusive)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                SELECT ISNULL(SUM(si.Quantity), 0)
                FROM SaleItems si
                INNER JOIN Sales s ON s.SaleId = si.SaleId
                WHERE s.SaleDate >= @From
                  AND s.SaleDate <  @To
                  AND s.Status = 'Completed';";

            return await connection.ExecuteScalarAsync<int>(sql, new
            {
                From = fromInclusive,
                To = toExclusive
            });
        }

        /// <summary>
        /// Average transaction amount in the range (Subtotal sum / count).
        /// Returns 0 if no transactions. Excludes void sales.
        /// </summary>
        public async Task<decimal> GetAverageTransactionAsync(
            DateTime fromInclusive,
            DateTime toExclusive)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                SELECT
                    CASE WHEN COUNT(*) = 0 THEN 0
                         ELSE CAST(SUM(Subtotal) AS DECIMAL(10,2)) / COUNT(*)
                    END
                FROM Sales
                WHERE SaleDate >= @From
                  AND SaleDate <  @To
                  AND Status = 'Completed';";

            return await connection.ExecuteScalarAsync<decimal>(sql, new
            {
                From = fromInclusive,
                To = toExclusive
            });
        }

        // ============================================================
        // PRIVATE — INVOICE NUMBER
        // ============================================================

        private static async Task<string> GenerateInvoiceNoAsync(
            IDbConnection connection,
            IDbTransaction transaction)
        {
            string datePart = DateTime.Now.ToString("yyyyMMdd");
            string pattern = $"INV-{datePart}-%";

            const string sql = @"
                SELECT TOP 1 InvoiceNo
                FROM Sales
                WHERE InvoiceNo LIKE @Pattern
                ORDER BY SaleId DESC;";

            string? lastInvoice = await connection.QueryFirstOrDefaultAsync<string>(
                sql, new { Pattern = pattern }, transaction);

            int nextSeq = 1;

            if (!string.IsNullOrEmpty(lastInvoice))
            {
                int dashIndex = lastInvoice.LastIndexOf('-');
                if (dashIndex >= 0 && dashIndex < lastInvoice.Length - 1)
                {
                    string seqPart = lastInvoice.Substring(dashIndex + 1);
                    if (int.TryParse(seqPart, out int lastSeq))
                        nextSeq = lastSeq + 1;
                }
            }

            return $"INV-{datePart}-{nextSeq:D4}";
        }
    }

    // ================================================================
    // DTOs
    // ================================================================

    public class DailyRevenueRow
    {
        public DateTime SaleDay { get; set; }
        public int TransactionCount { get; set; }
        public decimal Revenue { get; set; }
        public decimal KnownCost { get; set; }
        public int UnknownCostLineCount { get; set; }
        public int TotalLineCount { get; set; }
        public bool HasCompleteCost => UnknownCostLineCount == 0;
        public decimal? NetIncome => HasCompleteCost ? Revenue - KnownCost : (decimal?)null;
    }

    public class TopProductRow
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
    }

    public class SaleLineFlat
    {
        public int SaleId { get; set; }
        public DateTime SaleDate { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal LineTotal { get; set; }
        public decimal? UnitCost { get; set; }
    }

    /// <summary>One row of "sales by category" aggregation.</summary>
    public class CategoryRevenueRow
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int UnitsSold { get; set; }
    }

    /// <summary>Cash vs GCash split for the period.</summary>
    public class PaymentSplitRow
    {
        public decimal CashAmount { get; set; }
        public decimal GCashAmount { get; set; }
        public int TransactionCount { get; set; }
    }
}