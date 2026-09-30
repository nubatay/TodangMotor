using System;
using System.Collections.Generic;
using System.Data;
using TodangMotor.Data;
using TodangMotor.Models;

namespace TodangMotor.Services
{
    /// <summary>
    /// Orchestrates stock movement logging. Validates and constructs
    /// movement rows, saves them (single or inside a caller's transaction),
    /// and reads them back for reports.
    /// Does NOT touch Products.QuantityOnHand — that belongs to ProductService.
    /// </summary>
    public class StockMovementService
    {
        // ============================================================
        // MOVEMENT TYPE CONSTANTS
        // ============================================================

        public const string TypeAdjustment = "Adjustment";
        public const string TypeStockIn = "StockIn";
        public const string TypeSale = "Sale";
        public const string TypeInitial = "Initial";

        private static readonly string[] ValidTypes =
        {
            TypeAdjustment, TypeStockIn, TypeSale, TypeInitial
        };

        private readonly StockMovementRepository _repo;

        public StockMovementService()
        {
            _repo = new StockMovementRepository();
        }

        public StockMovementService(StockMovementRepository repository)
        {
            _repo = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        // ============================================================
        // FACTORIES
        // ============================================================

        /// <summary>
        /// Manual stock adjustment by the Owner.
        /// Example: Owner recounts and corrects 30 to 25.
        /// </summary>
        public static StockMovement CreateAdjustment(
            int productId,
            int quantityBefore,
            int quantityAfter,
            int userId,
            string? notes)
        {
            return BuildMovement(
                productId, TypeAdjustment,
                quantityBefore, quantityAfter,
                referenceId: null, userId, notes);
        }

        /// <summary>
        /// Initial stock load. Reserved for future use — not called anywhere yet.
        /// </summary>
        public static StockMovement CreateInitial(
            int productId,
            int quantityAfter,
            int userId,
            string? notes)
        {
            return BuildMovement(
                productId, TypeInitial,
                quantityBefore: 0, quantityAfter,
                referenceId: null, userId, notes);
        }

        /// <summary>
        /// Stock added via a completed Stock-In delivery (4B).
        /// </summary>
        public static StockMovement CreateStockIn(
            int productId,
            int quantityAdded,
            int quantityBefore,
            int stockInId,
            int userId)
        {
            int after = quantityBefore + quantityAdded;
            return BuildMovement(
                productId, TypeStockIn,
                quantityBefore, after,
                referenceId: stockInId, userId, notes: null);
        }

        /// <summary>
        /// Stock removed by a completed Sale (Module 6).
        /// </summary>
        public static StockMovement CreateSale(
            int productId,
            int quantitySold,
            int quantityBefore,
            int saleId,
            int userId)
        {
            int after = quantityBefore - quantitySold;
            return BuildMovement(
                productId, TypeSale,
                quantityBefore, after,
                referenceId: saleId, userId, notes: null);
        }

        // ============================================================
        // SAVE
        // ============================================================

        /// <summary>
        /// Save one movement using its own connection.
        /// Use this only for standalone adjustments (ProductForm Save).
        /// </summary>
        public async Task<int> SaveAsync(StockMovement movement)
        {
            Validate(movement);
            return await _repo.InsertAsync(movement);
        }

        /// <summary>
        /// Save many movements on a connection and transaction the caller
        /// already opened. Used by 4B Stock-In and Module 6 Sales so all
        /// writes commit or roll back together.
        /// </summary>
        public async Task<int> SaveManyAsync(
            IEnumerable<StockMovement> movements,
            IDbConnection connection,
            IDbTransaction? transaction)
        {
            if (movements == null) return 0;

            foreach (var m in movements)
                Validate(m);

            return await _repo.InsertManyAsync(movements, connection, transaction);
        }

        // ============================================================
        // READS
        // ============================================================

        /// <summary>All movements between two dates. For the daily report.</summary>
        public async Task<List<StockMovement>> GetMovementsInRangeAsync(
            DateTime fromInclusive,
            DateTime toExclusive)
        {
            return await _repo.GetByDateRangeAsync(fromInclusive, toExclusive);
        }

        // ============================================================
        // INTERNAL HELPERS
        // ============================================================

        private static StockMovement BuildMovement(
            int productId,
            string type,
            int quantityBefore,
            int quantityAfter,
            int? referenceId,
            int userId,
            string? notes)
        {
            return new StockMovement
            {
                ProductId = productId,
                MovementType = type,
                QuantityBefore = quantityBefore,
                QuantityAfter = quantityAfter,
                QuantityChange = quantityAfter - quantityBefore,
                ReferenceId = referenceId,
                UserId = userId,
                MovementDate = DateTime.Now,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
            };
        }

        private static void Validate(StockMovement movement)
        {
            if (movement == null)
                throw new ArgumentNullException(nameof(movement));

            if (movement.ProductId <= 0)
                throw new ArgumentException("Movement must reference a valid ProductId.");

            if (movement.UserId <= 0)
                throw new ArgumentException("Movement must reference a valid UserId.");

            if (string.IsNullOrWhiteSpace(movement.MovementType))
                throw new ArgumentException("Movement type is required.");

            bool typeIsKnown = false;
            foreach (var t in ValidTypes)
            {
                if (string.Equals(t, movement.MovementType, StringComparison.Ordinal))
                {
                    typeIsKnown = true;
                    break;
                }
            }
            if (!typeIsKnown)
                throw new ArgumentException(
                    $"Unknown movement type '{movement.MovementType}'. " +
                    $"Must be one of: {string.Join(", ", ValidTypes)}.");

            if (movement.QuantityChange == 0)
                throw new ArgumentException("Quantity change must not be zero.");

            if (movement.QuantityAfter < 0)
                throw new ArgumentException("Quantity after would be negative. Stock cannot go below zero.");

            // Before + change must equal after. This catches factory mistakes.
            if (movement.QuantityBefore + movement.QuantityChange != movement.QuantityAfter)
                throw new ArgumentException(
                    "Movement is inconsistent: QuantityBefore + QuantityChange != QuantityAfter.");
        }
    }
}