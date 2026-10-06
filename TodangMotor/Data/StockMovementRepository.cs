using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using TodangMotor.Models;

namespace TodangMotor.Data
{
    /// <summary>
    /// Data access for the StockMovements table.
    /// Inserts single movements or many movements inside a caller's
    /// transaction, and reads movements back for reports and the
    /// Movement History view (with product name/brand via JOIN).
    /// </summary>
    public class StockMovementRepository
    {
        // ============================================================
        // WRITES
        // ============================================================

        /// <summary>
        /// Inserts one movement row using its own connection.
        /// Returns the new MovementId.
        /// </summary>
        public async Task<int> InsertAsync(StockMovement movement)
        {
            using var connection = DbConnectionFactory.CreateConnection();
            return await InsertCoreAsync(connection, null, movement);
        }

        /// <summary>
        /// Inserts many movement rows on a connection and transaction
        /// that the caller already opened.
        /// Returns the number of rows inserted.
        /// </summary>
        public async Task<int> InsertManyAsync(
            IEnumerable<StockMovement> movements,
            IDbConnection connection,
            IDbTransaction? transaction)
        {
            if (movements == null) return 0;

            int count = 0;
            foreach (var m in movements)
            {
                await InsertCoreAsync(connection, transaction, m);
                count++;
            }
            return count;
        }

        // ============================================================
        // READS
        // ============================================================

        /// <summary>
        /// All movements between two dates with product info joined in.
        /// Inclusive of 'from', exclusive of 'to'.
        /// Ordered oldest first.
        /// </summary>
        public async Task<List<StockMovement>> GetByDateRangeAsync(
            DateTime fromInclusive,
            DateTime toExclusive)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                SELECT sm.MovementId, sm.ProductId, sm.MovementType, sm.QuantityChange,
                       sm.QuantityBefore, sm.QuantityAfter, sm.ReferenceId, sm.UserId,
                       sm.MovementDate, sm.Notes,
                       p.ProductName AS ProductName,
                       p.Brand       AS ProductBrand
                FROM StockMovements sm
                LEFT JOIN Products p ON p.ProductId = sm.ProductId
                WHERE sm.MovementDate >= @From
                  AND sm.MovementDate <  @To
                ORDER BY sm.MovementDate ASC, sm.MovementId ASC;";

            var rows = await connection.QueryAsync<StockMovement>(sql, new
            {
                From = fromInclusive,
                To = toExclusive
            });

            return rows.ToList();
        }

        /// <summary>
        /// Filtered movements for the Movement History tab.
        /// - Date range (inclusive of From, exclusive of To)
        /// - Optional product search (name or brand, case-insensitive)
        /// - Optional movement type filter (null/empty/"All" = no filter)
        /// Ordered newest first.
        /// </summary>
        public async Task<List<StockMovement>> GetFilteredAsync(
            DateTime fromInclusive,
            DateTime toExclusive,
            string? productSearch,
            string? movementType)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            // Base query + dynamic WHERE conditions.
            string sql = @"
                SELECT sm.MovementId, sm.ProductId, sm.MovementType, sm.QuantityChange,
                       sm.QuantityBefore, sm.QuantityAfter, sm.ReferenceId, sm.UserId,
                       sm.MovementDate, sm.Notes,
                       p.ProductName AS ProductName,
                       p.Brand       AS ProductBrand
                FROM StockMovements sm
                LEFT JOIN Products p ON p.ProductId = sm.ProductId
                WHERE sm.MovementDate >= @From
                  AND sm.MovementDate <  @To";

            string? search = string.IsNullOrWhiteSpace(productSearch) ? null : productSearch.Trim();
            if (search != null)
            {
                sql += @"
                  AND (
                      p.ProductName LIKE @Search
                      OR p.Brand LIKE @Search
                  )";
            }

            string? type = string.IsNullOrWhiteSpace(movementType) ? null : movementType.Trim();
            if (type != null && type != "All")
            {
                sql += " AND sm.MovementType = @Type";
            }

            sql += " ORDER BY sm.MovementDate DESC, sm.MovementId DESC;";

            var rows = await connection.QueryAsync<StockMovement>(sql, new
            {
                From = fromInclusive,
                To = toExclusive,
                Search = search != null ? "%" + search + "%" : null,
                Type = type
            });

            return rows.ToList();
        }

        // ============================================================
        // SHARED INSERT CORE
        // ============================================================

        private static async Task<int> InsertCoreAsync(
            IDbConnection connection,
            IDbTransaction? transaction,
            StockMovement movement)
        {
            const string sql = @"
                INSERT INTO StockMovements
                    (ProductId, MovementType, QuantityChange,
                     QuantityBefore, QuantityAfter, ReferenceId, UserId,
                     MovementDate, Notes)
                VALUES
                    (@ProductId, @MovementType, @QuantityChange,
                     @QuantityBefore, @QuantityAfter, @ReferenceId, @UserId,
                     @MovementDate, @Notes);

                SELECT CAST(SCOPE_IDENTITY() AS int);";

            var newId = await connection.QuerySingleAsync<int>(sql, new
            {
                movement.ProductId,
                movement.MovementType,
                movement.QuantityChange,
                movement.QuantityBefore,
                movement.QuantityAfter,
                movement.ReferenceId,
                movement.UserId,
                movement.MovementDate,
                movement.Notes
            }, transaction);

            movement.MovementId = newId;
            return newId;
        }
    }
}