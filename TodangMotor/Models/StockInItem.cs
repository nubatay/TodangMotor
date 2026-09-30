namespace TodangMotor.Models
{
    /// <summary>
    /// One line of a Stock-In delivery.
    /// Multiple StockInItem rows belong to a single StockIn header.
    /// Pure data container, no logic.
    /// </summary>
    public class StockInItem
    {
        /// <summary>Auto-number from the DB. 0 when not yet saved.</summary>
        public int StockInItemId { get; set; }

        /// <summary>Which StockIn header this line belongs to.</summary>
        public int StockInId { get; set; }

        /// <summary>Which product was delivered.</summary>
        public int ProductId { get; set; }

        /// <summary>How many units arrived. Always greater than zero.</summary>
        public int Quantity { get; set; }

        /// <summary>
        /// Cost per unit as paid on this delivery.
        /// Snapshotted here so historical accuracy is preserved even if
        /// the product's CostPrice changes later.
        /// </summary>
        public decimal UnitCost { get; set; }

        /// <summary>
        /// Quantity x UnitCost, stored so the DB does not need to recompute
        /// it on every read. Calculated in code at insert time.
        /// </summary>
        public decimal LineTotal { get; set; }
    }
}