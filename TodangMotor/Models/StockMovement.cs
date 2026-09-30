namespace TodangMotor.Models
{
    /// <summary>
    /// One row of the StockMovements table.
    /// Records a single change to a product's QuantityOnHand.
    /// MovementType is one of: "Sale", "StockIn", "Adjustment", "Initial".
    /// Pure data container — no logic, no validation here.
    /// </summary>
    public class StockMovement
    {
        /// <summary>Auto-number from the database. 0 when not yet saved.</summary>
        public int MovementId { get; set; }

        /// <summary>Which product this movement belongs to.</summary>
        public int ProductId { get; set; }

        /// <summary>
        /// Why the stock changed. One of:
        /// "Sale", "StockIn", "Adjustment", "Initial".
        /// </summary>
        public string MovementType { get; set; } = string.Empty;

        /// <summary>
        /// Signed change. Positive means added, negative means removed.
        /// Never zero — a movement with no change would be meaningless.
        /// </summary>
        public int QuantityChange { get; set; }

        /// <summary>Quantity on hand just before this change.</summary>
        public int QuantityBefore { get; set; }

        /// <summary>Quantity on hand just after this change.</summary>
        public int QuantityAfter { get; set; }

        /// <summary>
        /// Optional link to the SaleId or StockInId that caused this movement.
        /// Null for manual adjustments.
        /// </summary>
        public int? ReferenceId { get; set; }

        /// <summary>Which user caused the change.</summary>
        public int UserId { get; set; }

        /// <summary>When the change happened.</summary>
        public System.DateTime MovementDate { get; set; }

        /// <summary>
        /// Optional free-text reason. Populated for manual adjustments,
        /// typically null for automatic movements.
        /// </summary>
        public string? Notes { get; set; }
    }
}