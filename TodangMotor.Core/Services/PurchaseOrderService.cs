using System;
using System.Collections.Generic;
using System.Linq;
using TodangMotor.Common;
using TodangMotor.Data;
using TodangMotor.Models;

namespace TodangMotor.Services
{
    // ================================================================
    // DTOs
    // ================================================================

    /// <summary>One line the PO builder sends to the service.</summary>
    public class PurchaseOrderRequestLine
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    /// <summary>Returned by GetDetailAsync — a PO header plus its line items.</summary>
    public class PurchaseOrderDetail
    {
        public PurchaseOrder Order { get; set; } = new();
        public List<PurchaseOrderItem> Items { get; set; } = new();
    }

    // ================================================================
    // SERVICE
    // ================================================================

    /// <summary>
    /// Owner-only Purchase Order workflow: supplier/product lookups,
    /// validation, and save orchestration. Uses SaleRepository-style
    /// atomic inserts via PurchaseOrderRepository.
    /// </summary>
    public class PurchaseOrderService
    {
        private readonly PurchaseOrderRepository _poRepository;
        private readonly SupplierRepository _supplierRepository;
        private readonly ProductRepository _productRepository;

        private const int NotesMaxLength = 500;

        private const string AccessDeniedMessage =
            "Access denied. Only the Owner can manage Purchase Orders.";
        private const string NotLoggedInMessage =
            "You must be logged in to perform this action.";
        private const string DbErrorMessage =
            "Cannot complete this action right now. Please check your database connection.";

        public PurchaseOrderService()
        {
            _poRepository = new PurchaseOrderRepository();
            _supplierRepository = new SupplierRepository();
            _productRepository = new ProductRepository();
        }

        // ============================================================
        // LOOKUPS FOR THE FORM
        // ============================================================

        /// <summary>Active suppliers for the PO supplier dropdown.</summary>
        public async Task<List<Supplier>> GetActiveSuppliersAsync()
        {
            if (!SessionManager.IsOwner) return new List<Supplier>();

            try
            {
                var list = await _supplierRepository.GetActiveAsync();
                return list.ToList();
            }
            catch
            {
                return new List<Supplier>();
            }
        }

        /// <summary>
        /// Active products supplied by the given supplier — as primary OR
        /// alternate. Pass 0 for all active products.
        /// </summary>
        public async Task<List<Product>> GetProductsBySupplierAsync(int supplierId)
        {
            if (!SessionManager.IsOwner) return new List<Product>();

            try
            {
                if (supplierId <= 0)
                {
                    var all = await _productRepository.GetAllAsync();
                    return all
                        .Where(p => p.IsActive)
                        .OrderBy(p => p.ProductName)
                        .ThenBy(p => p.Brand)
                        .ToList();
                }

                var filtered = await _productRepository.GetBySupplierIdAsync(supplierId);
                return filtered
                    .OrderBy(p => p.ProductName)
                    .ThenBy(p => p.Brand)
                    .ToList();
            }
            catch
            {
                return new List<Product>();
            }
        }

        // ============================================================
        // SAVE
        // ============================================================

        /// <summary>
        /// Validates and saves a new PO. Returns (Success, ErrorMessage, PONumber).
        /// </summary>
        public async Task<(bool Success, string ErrorMessage, string PONumber)>
            SavePurchaseOrderAsync(
                int supplierId,
                DateTime orderDate,
                string? notes,
                List<PurchaseOrderRequestLine> lines)
        {
            if (!SessionManager.IsOwner)
                return (false, AccessDeniedMessage, string.Empty);

            int userId = SessionManager.CurrentUser?.UserId ?? 0;
            if (userId <= 0)
                return (false, NotLoggedInMessage, string.Empty);

            // ---------- Header validation ----------
            if (supplierId <= 0)
                return (false, "Please select a supplier.", string.Empty);

            if (orderDate == default)
                return (false, "Order date is required.", string.Empty);

            string? cleanNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            if (cleanNotes != null && cleanNotes.Length > NotesMaxLength)
                return (false, $"Notes cannot exceed {NotesMaxLength} characters.", string.Empty);

            // ---------- Lines validation ----------
            if (lines == null || lines.Count == 0)
                return (false, "A Purchase Order must have at least one item.", string.Empty);

            foreach (var l in lines)
            {
                if (l.ProductId <= 0)
                    return (false, "A PO item has an invalid product.", string.Empty);
                if (l.Quantity <= 0)
                    return (false, "Every PO item must have a quantity greater than zero.", string.Empty);
            }

            if (lines.GroupBy(l => l.ProductId).Any(g => g.Count() > 1))
                return (false, "The same product appears more than once in the PO.", string.Empty);

            // ---------- Supplier lookup ----------
            try
            {
                var supplier = await _supplierRepository.GetByIdAsync(supplierId);
                if (supplier == null)
                    return (false, "The selected supplier no longer exists.", string.Empty);
                if (!supplier.IsActive)
                    return (false, "The selected supplier is inactive.", string.Empty);
            }
            catch
            {
                return (false, DbErrorMessage, string.Empty);
            }

            // ---------- Product lookup ----------
            try
            {
                foreach (var line in lines)
                {
                    var product = await _productRepository.GetByIdAsync(line.ProductId);
                    if (product == null)
                        return (false, "A product in this PO no longer exists. Please refresh and try again.", string.Empty);
                    if (!product.IsActive)
                        return (false, $"Product \"{product.ProductName}\" is inactive.", string.Empty);
                }
            }
            catch
            {
                return (false, DbErrorMessage, string.Empty);
            }

            // ---------- Build models ----------
            var header = new PurchaseOrder
            {
                SupplierId = supplierId,
                UserId = userId,
                OrderDate = orderDate.Date,
                Notes = cleanNotes
            };

            var items = lines
                .Select(l => new PurchaseOrderItem
                {
                    ProductId = l.ProductId,
                    Quantity = l.Quantity
                })
                .ToList();

            // ---------- Save ----------
            try
            {
                var (success, error, poNumber) =
                    await _poRepository.InsertWithItemsAsync(header, items, userId);

                if (!success)
                    return (false, error, string.Empty);

                return (true, string.Empty, poNumber);
            }
            catch
            {
                return (false, DbErrorMessage, string.Empty);
            }
        }

        // ============================================================
        // READS
        // ============================================================

        /// <summary>All POs (optionally filtered by PO number or supplier name).</summary>
        public async Task<(bool Success, string ErrorMessage, List<PurchaseOrder> Orders)>
            GetAllAsync(string? search = null)
        {
            if (!SessionManager.IsOwner)
                return (false, AccessDeniedMessage, new List<PurchaseOrder>());

            try
            {
                var orders = await _poRepository.GetAllAsync(search);
                return (true, string.Empty, orders);
            }
            catch
            {
                return (false, DbErrorMessage, new List<PurchaseOrder>());
            }
        }

        /// <summary>
        /// Loads a PO and its line items for the detail popup.
        /// Returns (Success, ErrorMessage, Detail).
        /// </summary>
        public async Task<(bool Success, string ErrorMessage, PurchaseOrderDetail? Detail)>
            GetDetailAsync(int purchaseOrderId)
        {
            if (!SessionManager.IsOwner)
                return (false, AccessDeniedMessage, null);

            if (purchaseOrderId <= 0)
                return (false, "Invalid Purchase Order.", null);

            try
            {
                var order = await _poRepository.GetByIdAsync(purchaseOrderId);
                if (order == null)
                    return (false, "Purchase Order not found.", null);

                var items = await _poRepository.GetItemsAsync(purchaseOrderId);

                var detail = new PurchaseOrderDetail
                {
                    Order = order,
                    Items = items
                };

                return (true, string.Empty, detail);
            }
            catch
            {
                return (false, DbErrorMessage, null);
            }
        }
    }
}