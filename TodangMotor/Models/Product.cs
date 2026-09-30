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
        /// Optional long-lived memo the Owner can keep per product.
        /// Max 500 chars per the DB column.
        /// </summary>
        public string? Notes { get; set; }
    }
}