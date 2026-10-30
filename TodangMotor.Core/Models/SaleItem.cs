namespace TodangMotor.Models
{
    /// <summary>
    /// One line of a sales transaction.
    /// Multiple SaleItem rows belong to a single Sale header.
    /// Pure data container, no logic.
    /// </summary>
    public class SaleItem
    {
        /// <summary>Auto-number from the DB. 0 when not yet saved.</summary>
        public int SaleItemId { get; set; }

        /// <summary>Which Sale header this line belongs to.</summary>
        public int SaleId { get; set; }

        /// <summary>Which product was sold.</summary>
        public int ProductId { get; set; }

        /// <summary>How many units sold. Always greater than zero.</summary>
        public int Quantity { get; set; }

        /// <summary>
        /// Selling price per unit at the moment of sale.
        /// Snapshotted here so historical accuracy is preserved
        /// even if the product's SellingPrice changes later.
        /// </summary>
        public decimal UnitPrice { get; set; }

        /// <summary>
        /// Quantity × UnitPrice, stored so the DB does not need to
        /// recompute it on every read. Calculated in code at insert time.
        /// </summary>
        public decimal LineTotal { get; set; }

        /// <summary>
        /// Product cost per unit at the moment of sale.
        /// Snapshotted so the revenue report can compute accurate profit
        /// even if the product's CostPrice changes later.
        ///
        /// Nullable because sales recorded before this column existed
        /// have no cost history. New sales will always set it.
        /// </summary>
        public decimal? UnitCost { get; set; }
    }
}