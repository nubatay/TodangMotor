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
    /// and reads them back for reports and the Movement History view.
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
        public const string TypeVoid = "Void";

        private static readonly string[] ValidTypes =
        {
            TypeAdjustment, TypeStockIn, TypeSale, TypeInitial, TypeVoid
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

        public static StockMovement CreateVoid(
            int productId,
            int quantityRestored,
            int quantityBefore,
            int saleId,
            int userId,
            string? notes)
        {
            int after = quantityBefore + quantityRestored;
            return BuildMovement(
                productId, TypeVoid,
                quantityBefore, after,
                referenceId: saleId, userId, notes);
        }

        // ============================================================
        // SAVE
        // ============================================================

        public async Task<int> SaveAsync(StockMovement movement)
        {
            Validate(movement);
            return await _repo.InsertAsync(movement);
        }

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

        /// <summary>
        /// All movements between two dates. For the daily report.
        /// Inclusive of 'from', exclusive of 'to'.
        /// </summary>
        public async Task<List<StockMovement>> GetMovementsInRangeAsync(
            DateTime fromInclusive,
            DateTime toExclusive)
        {
            return await _repo.GetByDateRangeAsync(fromInclusive, toExclusive);
        }

        /// <summary>
        /// Filtered movements for the Movement History tab.
        /// Owner-only (CostPrice-adjacent data).
        /// </summary>
        public async Task<(bool Success, string ErrorMessage, List<StockMovement> Movements)>
            GetFilteredAsync(
                DateTime fromInclusive,
                DateTime toExclusive,
                string? productSearch,
                string? movementType)
        {
            if (!TodangMotor.Common.SessionManager.IsOwner)
                return (false, "Access denied. Only the Owner can view stock movements.", new List<StockMovement>());

            if (toExclusive <= fromInclusive)
                return (false, "The end date must come after the start date.", new List<StockMovement>());

            try
            {
                var rows = await _repo.GetFilteredAsync(
                    fromInclusive, toExclusive, productSearch, movementType);

                return (true, string.Empty, rows);
            }
            catch (Exception)
            {
                return (false, "Could not load stock movements. Please check your database connection.",
                    new List<StockMovement>());
            }
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

            if (movement.QuantityBefore + movement.QuantityChange != movement.QuantityAfter)
                throw new ArgumentException(
                    "Movement is inconsistent: QuantityBefore + QuantityChange != QuantityAfter.");
        }
    }
}