using System.Data;
using Dapper;
using TodangMotor.Models;

namespace TodangMotor.Data
{
    /// <summary>
    /// Data access for the StockMovements table.
    /// Inserts single movements or many movements inside a caller's
    /// transaction, and reads movements back for reports.
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
        /// that the caller already opened. Used by Stock-In (4B) and
        /// Sales (Module 6) so all writes commit or roll back together.
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
        /// All movements between two dates. Inclusive of 'from',
        /// exclusive of 'to'. Ordered oldest first.
        /// </summary>
        public async Task<List<StockMovement>> GetByDateRangeAsync(
            System.DateTime fromInclusive,
            System.DateTime toExclusive)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                SELECT MovementId, ProductId, MovementType, QuantityChange,
                       QuantityBefore, QuantityAfter, ReferenceId, UserId,
                       MovementDate, Notes
                FROM StockMovements
                WHERE MovementDate >= @From
                  AND MovementDate <  @To
                ORDER BY MovementDate ASC, MovementId ASC;";

            var rows = await connection.QueryAsync<StockMovement>(sql, new
            {
                From = fromInclusive,
                To = toExclusive
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