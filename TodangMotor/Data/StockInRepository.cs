using System.Data;
using Dapper;
using TodangMotor.Models;

namespace TodangMotor.Data
{
    /// <summary>
    /// Writes for the StockIns and StockInItems tables.
    /// All writes happen inside one atomic transaction: header, items,
    /// product quantity updates, and StockMovements rows commit together
    /// or not at all.
    /// </summary>
    public class StockInRepository
    {
        // ============================================================
        // PUBLIC ENTRY POINT
        // ============================================================

        /// <summary>
        /// Saves one complete Stock-In (header + items + stock updates +
        /// movement log) inside a single transaction.
        /// Returns the new StockInId on success.
        /// Throws on any failure after rolling back — the caller
        /// (StockInService) is expected to catch and translate.
        /// </summary>
        public async Task<int> InsertWithItemsAsync(
            StockIn header,
            List<StockInItem> items,
            int userId)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));
            if (items == null || items.Count == 0)
                throw new ArgumentException("At least one line item is required.", nameof(items));

            using var connection = DbConnectionFactory.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                // ---------- 1. Insert StockIns header ----------
                const string headerSql = @"
                    INSERT INTO StockIns
                        (SupplierId, UserId, DeliveryDate, ReferenceNo, Notes, CreatedAt)
                    VALUES
                        (@SupplierId, @UserId, @DeliveryDate, @ReferenceNo, @Notes, GETDATE());

                    SELECT CAST(SCOPE_IDENTITY() AS int);";

                int stockInId = await connection.QuerySingleAsync<int>(headerSql, new
                {
                    header.SupplierId,
                    UserId = userId,
                    header.DeliveryDate,
                    header.ReferenceNo,
                    header.Notes
                }, transaction);

                // ---------- 2. Loop through line items ----------
                foreach (var item in items)
                {
                    // 2a. Insert StockInItems row.
                    const string itemSql = @"
                        INSERT INTO StockInItems
                            (StockInId, ProductId, Quantity, UnitCost, LineTotal)
                        VALUES
                            (@StockInId, @ProductId, @Quantity, @UnitCost, @LineTotal);";

                    await connection.ExecuteAsync(itemSql, new
                    {
                        StockInId = stockInId,
                        item.ProductId,
                        item.Quantity,
                        item.UnitCost,
                        item.LineTotal
                    }, transaction);

                    // 2b. Read current quantity (this is the "before" value).
                    // QuerySingleAsync throws if the product vanished, which
                    // correctly triggers the rollback.
                    const string readQtySql = @"
                        SELECT QuantityOnHand
                        FROM Products
                        WHERE ProductId = @ProductId;";

                    int beforeQty = await connection.QuerySingleAsync<int>(
                        readQtySql, new { item.ProductId }, transaction);

                    // 2c. Increase the product's on-hand quantity.
                    const string updateQtySql = @"
                        UPDATE Products
                        SET QuantityOnHand = QuantityOnHand + @Quantity,
                            UpdatedAt      = GETDATE()
                        WHERE ProductId = @ProductId;";

                    await connection.ExecuteAsync(updateQtySql,
                        new { item.ProductId, item.Quantity }, transaction);

                    int afterQty = beforeQty + item.Quantity;

                    // 2d. Log the movement (type = 'StockIn',
                    // ReferenceId points back to the StockIns header).
                    const string movementSql = @"
                        INSERT INTO StockMovements
                            (ProductId, MovementType, QuantityChange,
                             QuantityBefore, QuantityAfter, ReferenceId, UserId,
                             MovementDate, Notes)
                        VALUES
                            (@ProductId, 'StockIn', @QuantityChange,
                             @QuantityBefore, @QuantityAfter, @StockInId, @UserId,
                             GETDATE(), NULL);";

                    await connection.ExecuteAsync(movementSql, new
                    {
                        item.ProductId,
                        QuantityChange = item.Quantity,
                        QuantityBefore = beforeQty,
                        QuantityAfter = afterQty,
                        StockInId = stockInId,
                        UserId = userId
                    }, transaction);
                }

                // ---------- 3. Commit ----------
                transaction.Commit();
                return stockInId;
            }
            catch
            {
                try { transaction.Rollback(); } catch { /* ignore */ }
                throw;
            }
        }
    }
}