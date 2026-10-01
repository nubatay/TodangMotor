namespace TodangMotor.Models
{
    /// <summary>
    /// One row of the Sales table — the header of a single customer
    /// transaction. Line items live in SaleItem.
    /// Pure data container, no logic.
    /// </summary>
    public class Sale
    {
        /// <summary>Auto-number from the DB. 0 when not yet saved.</summary>
        public int SaleId { get; set; }

        /// <summary>
        /// System-generated invoice number.
        /// Format: INV-yyyyMMdd-0001 (per-day sequence).
        /// </summary>
        public string InvoiceNo { get; set; } = string.Empty;

        /// <summary>Which user (Cashier or Owner) processed the sale.</summary>
        public int UserId { get; set; }

        /// <summary>When the sale was completed.</summary>
        public System.DateTime SaleDate { get; set; }

        /// <summary>
        /// Optional customer name. Null for walk-in customers.
        /// </summary>
        public string? CustomerName { get; set; }

        /// <summary>Payment method used. Either "Cash" or "GCash".</summary>
        public string PaymentMethod { get; set; } = string.Empty;

        /// <summary>Sum of all line totals, before payment is applied.</summary>
        public decimal Subtotal { get; set; }

        /// <summary>Amount the customer handed over (Cash) or paid digitally (GCash).</summary>
        public decimal AmountTendered { get; set; }

        /// <summary>
        /// Change returned to the customer. For GCash, this is 0.
        /// </summary>
        public decimal ChangeAmount { get; set; }

        /// <summary>
        /// Transaction status. Either "Completed" or "Void".
        /// New sales always start as "Completed".
        /// </summary>
        public string Status { get; set; } = "Completed";

        /// <summary>
        /// Which user voided the sale. Null when Status = "Completed".
        /// </summary>
        public int? VoidedByUserId { get; set; }

        /// <summary>
        /// When the sale was voided. Null when Status = "Completed".
        /// </summary>
        public System.DateTime? VoidedAt { get; set; }

        /// <summary>
        /// Why the sale was voided. Required at app level when voiding.
        /// Null when Status = "Completed".
        /// </summary>
        public string? VoidReason { get; set; }
    }
}