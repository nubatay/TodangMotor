namespace TodangMotor.Models
{
    /// <summary>
    /// One row of the StockIns table — the header of a delivery.
    /// Lines live in StockInItem (next file).
    /// Pure data container, no logic.
    /// </summary>
    public class StockIn
    {
        /// <summary>Auto-number from the DB. 0 when not yet saved.</summary>
        public int StockInId { get; set; }

        /// <summary>Which supplier delivered this stock.</summary>
        public int SupplierId { get; set; }

        /// <summary>Which user recorded this delivery. Always the Owner.</summary>
        public int UserId { get; set; }

        /// <summary>The day the delivery arrived.</summary>
        public System.DateTime DeliveryDate { get; set; }

        /// <summary>
        /// Optional supplier invoice or DR number.
        /// Max 50 chars per the DB column.
        /// </summary>
        public string? ReferenceNo { get; set; }

        /// <summary>
        /// Optional free-text note about the delivery.
        /// Max 250 chars per the DB column.
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>When this record was created in the system.</summary>
        public System.DateTime CreatedAt { get; set; }
    }
}