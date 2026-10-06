namespace TodangMotor.Models
{
    /// <summary>
    /// One product row from the Products table.
    /// Pure data container. Business rules live in ProductService.
    /// </summary>
    public class Product
    {
        public int ProductId { get; set; }

        public int CategoryId { get; set; }

        public string ProductName { get; set; } = string.Empty;

        /// <summary>Required at app level even though the DB allows NULL.</summary>
        public string Brand { get; set; } = string.Empty;

        /// <summary>Free-text label like "pcs", "L", "set", "bottle". No conversion math.</summary>
        public string Unit { get; set; } = "pcs";

        public decimal CostPrice { get; set; }

        public decimal SellingPrice { get; set; }

        /// <summary>
        /// Read-only in most places. Only changes via Stock-In, Sales,
        /// or Owner manual adjustment (logged as a StockMovements row).
        /// </summary>
        public int QuantityOnHand { get; set; }

        public int ReorderLevel { get; set; } = 5;

        public bool IsActive { get; set; } = true;

        public System.DateTime CreatedAt { get; set; }

        public System.DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Optional long-lived memo per product.
        /// Max 500 chars per the DB column.
        /// </summary>
        public string? Notes { get; set; }

        // ============================================================
        // NEW FIELDS (Phase 1)
        // ============================================================

        /// <summary>
        /// Stock Keeping Unit — a short internal code for the product.
        /// Optional but unique when provided. Max 50 chars.
        /// Example: "OIL-CAS-4T-1L".
        /// </summary>
        public string? SKU { get; set; }

        /// <summary>
        /// Expiration date for consumables (lubricants, chemicals).
        /// Nullable — many parts never expire.
        /// </summary>
        public System.DateTime? ExpirationDate { get; set; }

        /// <summary>
        /// Longer descriptive text for the product.
        /// Max 500 chars per the DB column.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Primary supplier for this product.
        /// Nullable in the DB for legacy data, but required at app level.
        /// </summary>
        public int? SupplierId { get; set; }

        // ============================================================
        // READ-ONLY HELPERS (populated by queries, not stored)
        // ============================================================

        /// <summary>
        /// Primary supplier's name — filled by the repository on read
        /// via a JOIN. Never written back to the DB.
        /// </summary>
        public string? PrimarySupplierName { get; set; }

        /// <summary>
        /// Alternate supplier IDs — filled by the repository on read.
        /// Not a DB column on Products; comes from ProductAlternateSuppliers.
        /// </summary>
        public System.Collections.Generic.List<int> AlternateSupplierIds { get; set; } = new();
    }
}