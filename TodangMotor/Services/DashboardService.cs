using TodangMotor.Data;
using TodangMotor.Models;

namespace TodangMotor.Services
{
    /// <summary>
    /// Feeds the Owner Dashboard Home screen with the small set of numbers
    /// and lists it needs to display.
    ///
    /// Sales-related methods are intentionally stubbed for now. Once Module 6
    /// (POS/Sales) is built, flip IsSalesModuleAvailable to true and replace
    /// the bodies of GetTodaySalesTotalAsync / GetTodayTransactionsAsync
    /// with real queries against a future SaleRepository. No UI change needed.
    /// </summary>
    public class DashboardService
    {
        private readonly ProductRepository _productRepository;

        public DashboardService()
        {
            _productRepository = new ProductRepository();
        }

        // ============================================================
        // PRODUCTS
        // ============================================================

        /// <summary>How many active (non-deactivated) products exist.</summary>
        public async Task<int> GetTotalActiveProductsAsync()
        {
            var all = await _productRepository.GetAllAsync();
            return all.Count(p => p.IsActive);
        }

        /// <summary>
        /// How many active products are currently at or below their reorder level.
        /// Locked rule: Low stock = QuantityOnHand &lt;= ReorderLevel.
        /// </summary>
        public async Task<int> GetLowStockCountAsync()
        {
            var all = await _productRepository.GetAllAsync();
            return all.Count(p => p.IsActive && p.QuantityOnHand <= p.ReorderLevel);
        }

        /// <summary>
        /// The N most-depleted active products (QuantityOnHand - ReorderLevel ascending).
        /// Ties broken alphabetically. Used by the "Low Stock Alerts" panel.
        /// </summary>
        public async Task<List<Product>> GetTopLowStockAsync(int take = 5)
        {
            if (take <= 0) return new List<Product>();

            var all = await _productRepository.GetAllAsync();

            return all
                .Where(p => p.IsActive && p.QuantityOnHand <= p.ReorderLevel)
                .OrderBy(p => p.QuantityOnHand - p.ReorderLevel)
                .ThenBy(p => p.ProductName)
                .Take(take)
                .ToList();
        }

        // ============================================================
        // SALES (stubs until Module 6 / POS is built)
        // ============================================================

        /// <summary>
        /// When false, the UI shows "—" for sales-related values instead of
        /// misleading zeroes. Set to true once the POS module is live.
        /// </summary>
        public bool IsSalesModuleAvailable => false;

        /// <summary>
        /// Total sales amount for today (local date). Stub: returns 0m for now.
        /// </summary>
        public Task<decimal> GetTodaySalesTotalAsync()
        {
            // TODO: replace with SaleRepository.GetTodayTotalAsync() in Module 6.
            return Task.FromResult(0m);
        }

        /// <summary>
        /// Count of today's completed transactions. Stub: returns 0 for now.
        /// </summary>
        public Task<int> GetTodayTransactionsAsync()
        {
            // TODO: replace with SaleRepository.GetTodayCountAsync() in Module 6.
            return Task.FromResult(0);
        }
    }
}