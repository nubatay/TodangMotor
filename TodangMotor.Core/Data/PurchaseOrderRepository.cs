using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using Microsoft.Data.SqlClient;

namespace TodangMotor.Data
{
    /// <summary>
    /// Data access for PurchaseOrders and PurchaseOrderItems.
    /// All writes happen inside atomic transactions.
    /// PO numbers use the format PO-yyyyMMdd-NNNN (per-day sequence).
    /// </summary>
    public class PurchaseOrderRepository
    {
        // ============================================================
        // INSERT (atomic)
        // ============================================================

        /// <summary>
        /// Saves one complete Purchase Order (header + items) inside a
        /// single transaction. Generates the PO number per-day.
        /// Returns (Success, ErrorMessage, PONumber).
        /// </summary>
        public async Task<(bool Success, string ErrorMessage, string PONumber)> InsertWithItemsAsync(
            Models.PurchaseOrder header,
            List<Models.PurchaseOrderItem> items,
            int userId)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));
            if (items == null || items.Count == 0)
                throw new ArgumentException("A Purchase Order must have at least one item.", nameof(items));

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
                        // ---------- 1. Generate PO number ----------
                        string poNumber = await GeneratePONumberAsync(connection, transaction);

                        // ---------- 2. Insert PurchaseOrders header ----------
                        const string headerSql = @"
                            INSERT INTO PurchaseOrders
                                (PONumber, SupplierId, UserId, OrderDate, Notes, CreatedAt)
                            VALUES
                                (@PONumber, @SupplierId, @UserId, @OrderDate, @Notes, GETDATE());

                            SELECT CAST(SCOPE_IDENTITY() AS int);";

                        int purchaseOrderId = await connection.QuerySingleAsync<int>(headerSql, new
                        {
                            PONumber = poNumber,
                            SupplierId = header.SupplierId,
                            UserId = userId,
                            OrderDate = header.OrderDate,
                            Notes = header.Notes
                        }, transaction);

                        // ---------- 3. Insert items ----------
                        const string itemSql = @"
                            INSERT INTO PurchaseOrderItems
                                (PurchaseOrderId, ProductId, Quantity)
                            VALUES
                                (@PurchaseOrderId, @ProductId, @Quantity);";

                        foreach (var item in items)
                        {
                            await connection.ExecuteAsync(itemSql, new
                            {
                                PurchaseOrderId = purchaseOrderId,
                                item.ProductId,
                                item.Quantity
                            }, transaction);
                        }

                        transaction.Commit();
                        return (true, string.Empty, poNumber);
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
                        "Could not generate a unique PO number after multiple attempts. Please try again.",
                        string.Empty);
                }
            }

            return (false, "Could not save the Purchase Order.", string.Empty);
        }

        // ============================================================
        // READS
        // ============================================================

        /// <summary>
        /// All POs, newest first, with SupplierName and ItemCount.
        /// Optionally filtered by PO number or supplier name.
        /// </summary>
        public async Task<List<Models.PurchaseOrder>> GetAllAsync(string? search = null)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            string sql = @"
                SELECT po.PurchaseOrderId, po.PONumber, po.SupplierId, po.UserId,
                       po.OrderDate, po.Notes, po.CreatedAt,
                       s.SupplierName AS SupplierName,
                       (SELECT COUNT(*) FROM PurchaseOrderItems poi
                        WHERE poi.PurchaseOrderId = po.PurchaseOrderId) AS ItemCount
                FROM PurchaseOrders po
                LEFT JOIN Suppliers s ON s.SupplierId = po.SupplierId";

            string? cleanSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            if (cleanSearch != null)
            {
                sql += @"
                WHERE po.PONumber LIKE @Search
                   OR s.SupplierName LIKE @Search";
            }

            sql += " ORDER BY po.OrderDate DESC, po.PurchaseOrderId DESC;";

            var rows = await connection.QueryAsync<Models.PurchaseOrder>(sql, new
            {
                Search = cleanSearch != null ? "%" + cleanSearch + "%" : null
            });

            return rows.ToList();
        }

        /// <summary>One PO header by ID. Returns null if not found.</summary>
        public async Task<Models.PurchaseOrder?> GetByIdAsync(int purchaseOrderId)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                SELECT po.PurchaseOrderId, po.PONumber, po.SupplierId, po.UserId,
                       po.OrderDate, po.Notes, po.CreatedAt,
                       s.SupplierName AS SupplierName,
                       (SELECT COUNT(*) FROM PurchaseOrderItems poi
                        WHERE poi.PurchaseOrderId = po.PurchaseOrderId) AS ItemCount
                FROM PurchaseOrders po
                LEFT JOIN Suppliers s ON s.SupplierId = po.SupplierId
                WHERE po.PurchaseOrderId = @PurchaseOrderId;";

            return await connection.QuerySingleOrDefaultAsync<Models.PurchaseOrder>(
                sql, new { PurchaseOrderId = purchaseOrderId });
        }

        /// <summary>Line items for one PO, with product name/brand/unit joined in.</summary>
        public async Task<List<Models.PurchaseOrderItem>> GetItemsAsync(int purchaseOrderId)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                SELECT poi.PurchaseOrderItemId, poi.PurchaseOrderId,
                       poi.ProductId, poi.Quantity,
                       p.ProductName AS ProductName,
                       p.Brand       AS ProductBrand,
                       p.Unit        AS ProductUnit
                FROM PurchaseOrderItems poi
                LEFT JOIN Products p ON p.ProductId = poi.ProductId
                WHERE poi.PurchaseOrderId = @PurchaseOrderId
                ORDER BY poi.PurchaseOrderItemId ASC;";

            var rows = await connection.QueryAsync<Models.PurchaseOrderItem>(
                sql, new { PurchaseOrderId = purchaseOrderId });

            return rows.ToList();
        }

        // ============================================================
        // PRIVATE — PO NUMBER
        // ============================================================

        private static async Task<string> GeneratePONumberAsync(
            IDbConnection connection,
            IDbTransaction transaction)
        {
            string datePart = DateTime.Now.ToString("yyyyMMdd");
            string pattern = $"PO-{datePart}-%";

            const string sql = @"
                SELECT TOP 1 PONumber
                FROM PurchaseOrders
                WHERE PONumber LIKE @Pattern
                ORDER BY PurchaseOrderId DESC;";

            string? lastPO = await connection.QueryFirstOrDefaultAsync<string>(
                sql, new { Pattern = pattern }, transaction);

            int nextSeq = 1;

            if (!string.IsNullOrEmpty(lastPO))
            {
                int dashIndex = lastPO.LastIndexOf('-');
                if (dashIndex >= 0 && dashIndex < lastPO.Length - 1)
                {
                    string seqPart = lastPO.Substring(dashIndex + 1);
                    if (int.TryParse(seqPart, out int lastSeq))
                        nextSeq = lastSeq + 1;
                }
            }

            return $"PO-{datePart}-{nextSeq:D4}";
        }
    }
}