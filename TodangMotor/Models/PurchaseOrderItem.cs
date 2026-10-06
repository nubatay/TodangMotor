namespace TodangMotor.Models
{
    /// <summary>
    /// One line of a purchase order.
    /// Multiple PurchaseOrderItem rows belong to a single PurchaseOrder header.
    /// Pure data container, no logic.
    /// </summary>
    public class PurchaseOrderItem
    {
        /// <summary>Auto-number from the DB. 0 when not yet saved.</summary>
        public int PurchaseOrderItemId { get; set; }

        /// <summary>Which PurchaseOrder header this line belongs to.</summary>
        public int PurchaseOrderId { get; set; }

        /// <summary>Which product is being ordered.</summary>
        public int ProductId { get; set; }

        /// <summary>How many units to order. Always greater than zero.</summary>
        public int Quantity { get; set; }

        // ============================================================
        // READ-ONLY HELPERS (populated by queries, not stored)
        // ============================================================

        /// <summary>
        /// Product name — filled by the repository on read via a JOIN.
        /// </summary>
        public string? ProductName { get; set; }

        /// <summary>
        /// Product brand — filled by the repository on read via a JOIN.
        /// </summary>
        public string? ProductBrand { get; set; }

        /// <summary>
        /// Product unit — filled by the repository on read via a JOIN.
        /// </summary>
        public string? ProductUnit { get; set; }
    }
}