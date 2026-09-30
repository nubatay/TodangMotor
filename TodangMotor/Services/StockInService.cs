using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TodangMotor.Common;
using TodangMotor.Data;
using TodangMotor.Models;

namespace TodangMotor.Services
{
    // ================================================================
    // DTOs — small "envelope" classes used between form and service
    // ================================================================

    /// <summary>One line submitted from the StockInForm (before validation).</summary>
    public class StockInRequestLine
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitCost { get; set; }
    }

    /// <summary>
    /// One product's cost situation, used by the prompt-after-save dialog.
    /// CanUpdate is false when NewCost > CurrentSellingPrice (rule violation).
    /// </summary>
    public class CostComparisonItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public decimal CurrentCost { get; set; }
        public decimal NewCost { get; set; }
        public decimal CurrentSellingPrice { get; set; }
        public bool CanUpdate { get; set; }

        /// <summary>True when NewCost differs from CurrentCost (and update is possible).</summary>
        public bool HasChange => CanUpdate && NewCost != CurrentCost;
    }

    /// <summary>Return value of CompleteStockInAsync.</summary>
    public class StockInResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public int StockInId { get; set; }
        public List<CostComparisonItem> CostComparisons { get; set; } = new();
    }

    // ================================================================
    // SERVICE
    // ================================================================

    public class StockInService
    {
        private readonly StockInRepository _stockInRepository = new();
        private readonly SupplierRepository _supplierRepository = new();
        private readonly ProductRepository _productRepository = new();

        private const int ReferenceNoMaxLength = 50;
        private const int NotesMaxLength = 250;

        private const string AccessDeniedMessage =
            "Access denied. Only the Owner can record Stock-In.";
        private const string DbErrorMessage =
            "Cannot save Stock-In right now. Please check your database connection and try again.";

        // ============================================================
        // LOOKUPS FOR THE FORM
        // ============================================================

        /// <summary>Active suppliers for the dropdown. Alphabetical.</summary>
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

        /// <summary>Active products for the line-item picker. Alphabetical.</summary>
        public async Task<List<Product>> GetActiveProductsAsync()
        {
            if (!SessionManager.IsOwner) return new List<Product>();

            try
            {
                var list = await _productRepository.GetAllAsync();
                return list.Where(p => p.IsActive)
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
        // COMPLETE STOCK-IN
        // ============================================================

        /// <summary>
        /// Validates and saves a complete Stock-In.
        /// Returns Success=false with a message on any validation or DB error.
        /// On success, fills CostComparisons for the prompt-after-save dialog.
        /// </summary>
        public async Task<StockInResult> CompleteStockInAsync(
            int supplierId,
            DateTime deliveryDate,
            string? referenceNo,
            string? notes,
            List<StockInRequestLine> lines)
        {
            var result = new StockInResult();

            // ---------- Owner-only ----------
            if (!SessionManager.IsOwner)
            {
                result.ErrorMessage = AccessDeniedMessage;
                return result;
            }

            // ---------- Header validation ----------
            if (supplierId <= 0)
            {
                result.ErrorMessage = "Please select a supplier.";
                return result;
            }

            if (deliveryDate == default)
            {
                result.ErrorMessage = "Delivery date is required.";
                return result;
            }

            if (deliveryDate.Date > DateTime.Now.Date)
            {
                result.ErrorMessage = "Delivery date cannot be in the future.";
                return result;
            }

            string? cleanRef = string.IsNullOrWhiteSpace(referenceNo) ? null : referenceNo.Trim();
            if (cleanRef != null && cleanRef.Length > ReferenceNoMaxLength)
            {
                result.ErrorMessage = $"Reference No cannot exceed {ReferenceNoMaxLength} characters.";
                return result;
            }

            string? cleanNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            if (cleanNotes != null && cleanNotes.Length > NotesMaxLength)
            {
                result.ErrorMessage = $"Notes cannot exceed {NotesMaxLength} characters.";
                return result;
            }

            // ---------- Lines validation ----------
            if (lines == null || lines.Count == 0)
            {
                result.ErrorMessage = "A Stock-In must have at least one item.";
                return result;
            }

            // Duplicate product check.
            var duplicate = lines.GroupBy(l => l.ProductId).FirstOrDefault(g => g.Count() > 1);
            if (duplicate != null)
            {
                result.ErrorMessage = "The same product cannot appear twice in one Stock-In.";
                return result;
            }

            foreach (var line in lines)
            {
                if (line.ProductId <= 0)
                {
                    result.ErrorMessage = "Every line must have a valid product.";
                    return result;
                }
                if (line.Quantity <= 0)
                {
                    result.ErrorMessage = "Every line must have a quantity greater than zero.";
                    return result;
                }
                if (line.UnitCost < 0)
                {
                    result.ErrorMessage = "Unit cost cannot be negative.";
                    return result;
                }
            }

            // ---------- Lookups: supplier ----------
            Supplier? supplier;
            try
            {
                supplier = await _supplierRepository.GetByIdAsync(supplierId);
            }
            catch
            {
                result.ErrorMessage = DbErrorMessage;
                return result;
            }

            if (supplier == null)
            {
                result.ErrorMessage = "The selected supplier no longer exists.";
                return result;
            }
            if (!supplier.IsActive)
            {
                result.ErrorMessage = "The selected supplier is inactive.";
                return result;
            }

            // ---------- Lookups: products ----------
            var productsById = new Dictionary<int, Product>();
            try
            {
                foreach (var line in lines)
                {
                    var product = await _productRepository.GetByIdAsync(line.ProductId);
                    if (product == null)
                    {
                        result.ErrorMessage =
                            "A product in this Stock-In no longer exists. Please refresh and try again.";
                        return result;
                    }
                    if (!product.IsActive)
                    {
                        result.ErrorMessage =
                            $"Product \"{product.ProductName}\" is inactive. Reactivate it first.";
                        return result;
                    }
                    productsById[line.ProductId] = product;
                }
            }
            catch
            {
                result.ErrorMessage = DbErrorMessage;
                return result;
            }

            // ---------- Build models ----------
            int userId = SessionManager.CurrentUser?.UserId ?? 0;
            if (userId <= 0)
            {
                result.ErrorMessage = "Could not identify the current user. Please log in again.";
                return result;
            }

            var header = new StockIn
            {
                SupplierId = supplierId,
                UserId = userId,
                DeliveryDate = deliveryDate.Date,
                ReferenceNo = cleanRef,
                Notes = cleanNotes
            };

            var items = lines.Select(l => new StockInItem
            {
                ProductId = l.ProductId,
                Quantity = l.Quantity,
                UnitCost = l.UnitCost,
                LineTotal = l.Quantity * l.UnitCost
            }).ToList();

            // ---------- Save via repository (atomic) ----------
            int newId;
            try
            {
                newId = await _stockInRepository.InsertWithItemsAsync(header, items, userId);
            }
            catch
            {
                result.ErrorMessage = DbErrorMessage;
                return result;
            }

            // ---------- Build cost comparisons for the prompt dialog ----------
            foreach (var line in lines)
            {
                var product = productsById[line.ProductId];
                bool canUpdate = line.UnitCost <= product.SellingPrice;

                result.CostComparisons.Add(new CostComparisonItem
                {
                    ProductId = product.ProductId,
                    ProductName = product.ProductName ?? string.Empty,
                    Brand = product.Brand ?? string.Empty,
                    CurrentCost = product.CostPrice,
                    NewCost = line.UnitCost,
                    CurrentSellingPrice = product.SellingPrice,
                    CanUpdate = canUpdate
                });
            }

            result.Success = true;
            result.StockInId = newId;
            return result;
        }
    }
}