namespace TodangMotor.Models
{
    /// <summary>
    /// This is just a "box" that holds one supplier's information —
    /// matches the Suppliers table in the database exactly, column for column.
    /// No logic here, just data.
    /// </summary>
    public class Supplier
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public string? Address { get; set; }
        public bool IsActive { get; set; }
    }
}