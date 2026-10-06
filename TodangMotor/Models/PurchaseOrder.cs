namespace TodangMotor.Models
{
    /// <summary>
    /// One row of the PurchaseOrders table — the header of a purchase order
    /// sent to a supplier. Line items live in PurchaseOrderItem.
    /// Pure data container, no logic.
    /// </summary>
    public class PurchaseOrder
    {
        /// <summary>Auto-number from the DB. 0 when not yet saved.</summary>
        public int PurchaseOrderId { get; set; }

        /// <summary>
        /// System-generated PO number.
        /// Format: PO-yyyyMMdd-NNNN (per-day sequence).
        /// </summary>
        public string PONumber { get; set; } = string.Empty;

        /// <summary>Which supplier this order is for.</summary>
        public int SupplierId { get; set; }

        /// <summary>Which user (Owner) created the PO.</summary>
        public int UserId { get; set; }

        /// <summary>When the PO was created (editable — can be set to a different date).</summary>
        public System.DateTime OrderDate { get; set; }

        /// <summary>Optional free-text note about the order.</summary>
        public string? Notes { get; set; }

        /// <summary>When this record was created in the system.</summary>
        public System.DateTime CreatedAt { get; set; }

        // ============================================================
        // READ-ONLY HELPERS (populated by queries, not stored)
        // ============================================================

        /// <summary>
        /// Supplier name — filled by the repository on read via a JOIN.
        /// Never written back to the DB.
        /// </summary>
        public string? SupplierName { get; set; }

        /// <summary>
        /// How many line items this PO has — filled by the repository on read.
        /// Never written back to the DB.
        /// </summary>
        public int ItemCount { get; set; }
    }
}