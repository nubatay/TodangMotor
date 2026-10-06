using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using TodangMotor.Common;
using TodangMotor.Data;
using TodangMotor.Models;

namespace TodangMotor.Services
{
    public class ProductService
    {
        private readonly ProductRepository _productRepository;
        private readonly CategoryRepository _categoryRepository;
        private readonly SupplierRepository _supplierRepository;

        private const int ProductNameMaxLength = 150;
        private const int BrandMaxLength = 100;
        private const int UnitMaxLength = 20;
        private const int NotesMaxLength = 500;
        private const int SkuMaxLength = 50;
        private const int DescriptionMaxLength = 500;

        // SKU allowed characters: letters, digits, dash, underscore, dot.
        private static readonly Regex SkuPattern = new(
            @"^[A-Za-z0-9\-_.]+$", RegexOptions.Compiled);

        public ProductService()
        {
            _productRepository = new ProductRepository();
            _categoryRepository = new CategoryRepository();
            _supplierRepository = new SupplierRepository();
        }

        // ==================== READ ====================

        public async Task<(bool Success, string ErrorMessage, List<Product> Products)> GetAllAsync()
        {
            if (!SessionManager.IsOwner)
                return (false, "Access denied. Only the Owner can view product details.", new List<Product>());

            try
            {
                var products = await _productRepository.GetAllAsync();
                return (true, string.Empty, products);
            }
            catch (Exception)
            {
                return (false, "Could not load products. Please check your database connection.", new List<Product>());
            }
        }

        public async Task<(bool Success, string ErrorMessage, Product? Product)> GetByIdAsync(int productId)
        {
            if (!SessionManager.IsOwner)
                return (false, "Access denied. Only the Owner can view product details.", null);

            try
            {
                var product = await _productRepository.GetByIdAsync(productId);
                if (product == null)
                    return (false, "Product not found.", null);

                return (true, string.Empty, product);
            }
            catch (Exception)
            {
                return (false, "Could not load the product. Please check your database connection.", null);
            }
        }

        /// <summary>
        /// Active products supplied by the given supplier — used by Stock-In.
        /// </summary>
        public async Task<(bool Success, string ErrorMessage, List<Product> Products)>
            GetBySupplierAsync(int supplierId)
        {
            if (!SessionManager.IsOwner)
                return (false, "Access denied.", new List<Product>());

            if (supplierId <= 0)
                return (false, "Please select a supplier.", new List<Product>());

            try
            {
                var products = await _productRepository.GetBySupplierIdAsync(supplierId);
                return (true, string.Empty, products);
            }
            catch (Exception)
            {
                return (false, "Could not load products for this supplier.", new List<Product>());
            }
        }

        // ==================== ADD ====================

        public async Task<(bool Success, string ErrorMessage, int NewProductId)> AddAsync(
            int categoryId, string productName, string brand, string unit,
            string costPriceText, string sellingPriceText, string reorderLevelText,
            string notes,
            string? sku = null,
            DateTime? expirationDate = null,
            string? description = null,
            int? supplierId = null,
            List<int>? alternateSupplierIds = null)
        {
            if (!SessionManager.IsOwner)
                return (false, "Access denied. Only the Owner can manage products.", 0);

            var (valid, errorMessage, cleanProduct) = await ValidateAndBuildAsync(
                categoryId, productName, brand, unit,
                costPriceText, sellingPriceText, reorderLevelText, notes,
                sku, expirationDate, description, supplierId,
                excludingProductId: null);

            if (!valid)
                return (false, errorMessage, 0);

            try
            {
                var newId = await _productRepository.InsertAsync(cleanProduct!);

                // Save alternate suppliers (Phase 2 will pass these; Phase 1 callers
                // leave them null).
                if (alternateSupplierIds != null && alternateSupplierIds.Count > 0)
                {
                    // Validate alternates before saving.
                    var (altsValid, altsError) = await ValidateAlternateSuppliersAsync(alternateSupplierIds);
                    if (!altsValid)
                        return (false, altsError, newId);

                    await _productRepository.SetAlternateSuppliersAsync(newId, alternateSupplierIds);
                }

                return (true, string.Empty, newId);
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                return (false, "A product with this name and brand already exists, or the SKU is already used.", 0);
            }
            catch (Exception)
            {
                return (false, "Could not add the product. Please check your database connection.", 0);
            }
        }

        // ==================== UPDATE ====================

        public async Task<(bool Success, string ErrorMessage)> UpdateAsync(
            int productId, int categoryId, string productName, string brand, string unit,
            string costPriceText, string sellingPriceText, string reorderLevelText,
            string notes,
            string? sku = null,
            DateTime? expirationDate = null,
            string? description = null,
            int? supplierId = null,
            List<int>? alternateSupplierIds = null)
        {
            if (!SessionManager.IsOwner)
                return (false, "Access denied. Only the Owner can manage products.");

            var existing = await _productRepository.GetByIdAsync(productId);
            if (existing == null)
                return (false, "Product not found.");

            var (valid, errorMessage, cleanProduct) = await ValidateAndBuildAsync(
                categoryId, productName, brand, unit,
                costPriceText, sellingPriceText, reorderLevelText, notes,
                sku, expirationDate, description, supplierId,
                excludingProductId: productId);

            if (!valid)
                return (false, errorMessage);

            cleanProduct!.ProductId = productId;

            try
            {
                var updated = await _productRepository.UpdateAsync(cleanProduct);
                if (!updated)
                    return (false, "Product not found.");

                // Replace alternate suppliers if provided.
                if (alternateSupplierIds != null)
                {
                    var (altsValid, altsError) = await ValidateAlternateSuppliersAsync(alternateSupplierIds);
                    if (!altsValid)
                        return (false, altsError);

                    await _productRepository.SetAlternateSuppliersAsync(productId, alternateSupplierIds);
                }

                return (true, string.Empty);
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                return (false, "A product with this name and brand already exists, or the SKU is already used.");
            }
            catch (Exception)
            {
                return (false, "Could not update the product. Please check your database connection.");
            }
        }

        // ==================== UPDATE STOCK (ADJUSTMENT) ====================

        public async Task<(bool Success, string ErrorMessage)> UpdateStockAsync(
            int productId, int newQuantity, string? notes)
        {
            if (!SessionManager.IsOwner)
                return (false, "Access denied. Only the Owner can adjust stock.");

            if (productId <= 0)
                return (false, "Invalid product selected.");

            if (newQuantity < 0)
                return (false, "Stock quantity cannot be negative.");

            Product? existing;
            try
            {
                existing = await _productRepository.GetByIdAsync(productId);
            }
            catch (Exception)
            {
                return (false, "Could not load the product. Please check your database connection.");
            }

            if (existing == null)
                return (false, "Product not found.");

            int before = existing.QuantityOnHand;

            if (newQuantity == before)
                return (false, "New stock quantity is the same as the current quantity — nothing to record.");

            int userId = SessionManager.CurrentUser?.UserId ?? 0;
            if (userId <= 0)
                return (false, "Could not identify the current user. Please log in again.");

            try
            {
                var success = await _productRepository.UpdateStockWithMovementAsync(
                    productId, before, newQuantity, userId, notes);

                if (!success)
                    return (false, "Product not found.");

                return (true, string.Empty);
            }
            catch (Exception)
            {
                return (false, "Could not update stock. Please check your database connection.");
            }
        }

        // ==================== UPDATE COST PRICE (Stock-In prompt) ====================

        public async Task<(bool Success, string ErrorMessage, int UpdatedCount)> UpdateCostPricesAsync(
            List<(int ProductId, decimal NewCost)> updates)
        {
            if (!SessionManager.IsOwner)
                return (false, "Access denied. Only the Owner can update cost prices.", 0);

            if (updates == null || updates.Count == 0)
                return (true, string.Empty, 0);

            int count = 0;
            try
            {
                foreach (var (productId, newCost) in updates)
                {
                    if (productId <= 0) continue;
                    if (newCost < 0) continue;

                    var product = await _productRepository.GetByIdAsync(productId);
                    if (product == null) continue;

                    if (newCost > product.SellingPrice) continue;
                    if (newCost == product.CostPrice) continue;

                    await _productRepository.UpdateCostPriceAsync(productId, newCost);
                    count++;
                }

                return (true, string.Empty, count);
            }
            catch (Exception)
            {
                return (false, "Some cost prices could not be updated. Please check your database connection.", count);
            }
        }

        // ==================== DEACTIVATE / REACTIVATE ====================

        public async Task<(bool Success, string ErrorMessage)> DeactivateAsync(int productId)
        {
            if (!SessionManager.IsOwner)
                return (false, "Access denied. Only the Owner can manage products.");

            try
            {
                var existing = await _productRepository.GetByIdAsync(productId);
                if (existing == null)
                    return (false, "Product not found.");

                if (!existing.IsActive)
                    return (false, "This product is already inactive.");

                var success = await _productRepository.SetActiveStatusAsync(productId, false);
                if (!success)
                    return (false, "Product not found.");

                return (true, string.Empty);
            }
            catch (Exception)
            {
                return (false, "Could not deactivate the product. Please check your database connection.");
            }
        }

        public async Task<(bool Success, string ErrorMessage)> ReactivateAsync(int productId)
        {
            if (!SessionManager.IsOwner)
                return (false, "Access denied. Only the Owner can manage products.");

            try
            {
                var existing = await _productRepository.GetByIdAsync(productId);
                if (existing == null)
                    return (false, "Product not found.");

                if (existing.IsActive)
                    return (false, "This product is already active.");

                var category = await _categoryRepository.GetByIdAsync(existing.CategoryId);
                if (category == null || !category.IsActive)
                    return (false, "This product's category is inactive. Reactivate its category first.");

                var success = await _productRepository.SetActiveStatusAsync(productId, true);
                if (!success)
                    return (false, "Product not found.");

                return (true, string.Empty);
            }
            catch (Exception)
            {
                return (false, "Could not reactivate the product. Please check your database connection.");
            }
        }

        // ==================== SHARED VALIDATION ====================

        private async Task<(bool Valid, string ErrorMessage, Product? Product)> ValidateAndBuildAsync(
            int categoryId, string productName, string brand, string unit,
            string costPriceText, string sellingPriceText, string reorderLevelText,
            string notes,
            string? sku, DateTime? expirationDate, string? description, int? supplierId,
            int? excludingProductId)
        {
            // ---- ProductName ----
            if (string.IsNullOrWhiteSpace(productName))
                return (false, "Product name is required.", null);

            productName = productName.Trim();
            if (productName.Length > ProductNameMaxLength)
                return (false, $"Product name cannot exceed {ProductNameMaxLength} characters.", null);

            // ---- Brand ----
            if (string.IsNullOrWhiteSpace(brand))
                return (false, "Brand is required.", null);

            brand = brand.Trim();
            if (brand.Length > BrandMaxLength)
                return (false, $"Brand cannot exceed {BrandMaxLength} characters.", null);

            // ---- Unit ----
            if (string.IsNullOrWhiteSpace(unit))
                return (false, "Unit is required.", null);

            unit = unit.Trim();
            if (unit.Length > UnitMaxLength)
                return (false, $"Unit cannot exceed {UnitMaxLength} characters.", null);

            // ---- CategoryId ----
            if (categoryId <= 0)
                return (false, "Please select a category.", null);

            var category = await _categoryRepository.GetByIdAsync(categoryId);
            if (category == null)
                return (false, "The selected category no longer exists.", null);

            if (!category.IsActive)
                return (false, "The selected category is inactive. Please choose an active category.", null);

            // ---- CostPrice ----
            if (!decimal.TryParse(costPriceText, NumberStyles.Number, CultureInfo.InvariantCulture, out var costPrice))
                return (false, "Cost price must be a valid number.", null);

            if (costPrice < 0)
                return (false, "Cost price cannot be negative.", null);

            // ---- SellingPrice ----
            if (!decimal.TryParse(sellingPriceText, NumberStyles.Number, CultureInfo.InvariantCulture, out var sellingPrice))
                return (false, "Selling price must be a valid number.", null);

            if (sellingPrice <= 0)
                return (false, "Selling price must be greater than zero.", null);

            if (sellingPrice < costPrice)
                return (false, "Selling price cannot be lower than cost price.", null);

            // ---- ReorderLevel ----
            if (!int.TryParse(reorderLevelText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var reorderLevel))
                return (false, "Reorder level must be a whole number.", null);

            if (reorderLevel < 0)
                return (false, "Reorder level cannot be negative.", null);

            // ---- Notes (optional) ----
            if (!string.IsNullOrEmpty(notes))
            {
                notes = notes.Trim();
                if (notes.Length > NotesMaxLength)
                    return (false, $"Notes cannot exceed {NotesMaxLength} characters.", null);
            }

            // ---- SKU (optional, unique when provided) ----
            string? cleanSku = string.IsNullOrWhiteSpace(sku) ? null : sku.Trim();

            if (cleanSku != null)
            {
                if (cleanSku.Length > SkuMaxLength)
                    return (false, $"SKU cannot exceed {SkuMaxLength} characters.", null);

                if (!SkuPattern.IsMatch(cleanSku))
                    return (false, "SKU may only contain letters, digits, dash, underscore, and dot.", null);

                var existingSku = await _productRepository.GetBySkuAsync(cleanSku);
                if (existingSku != null && existingSku.ProductId != excludingProductId)
                    return (false, $"SKU \"{cleanSku}\" is already in use.", null);
            }

            // ---- ExpirationDate (optional, must be in the future) ----
            if (expirationDate.HasValue && expirationDate.Value.Date < DateTime.Now.Date)
                return (false, "Expiration date cannot be in the past.", null);

            // ---- Description (optional) ----
            string? cleanDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            if (cleanDescription != null && cleanDescription.Length > DescriptionMaxLength)
                return (false, $"Description cannot exceed {DescriptionMaxLength} characters.", null);

            // ---- SupplierId (Phase 1: optional. Phase 2: required when ProductForm is updated.) ----
            if (supplierId.HasValue && supplierId.Value > 0)
            {
                var supplier = await _supplierRepository.GetByIdAsync(supplierId.Value);
                if (supplier == null)
                    return (false, "The selected supplier no longer exists.", null);

                if (!supplier.IsActive)
                    return (false, "The selected supplier is inactive.", null);
            }

            // ---- Duplicate check: ProductName + Brand (case-sensitive) ----
            var existingMatch = await _productRepository.GetByNameAndBrandAsync(productName, brand);
            if (existingMatch != null && existingMatch.ProductId != excludingProductId)
                return (false, "A product with this name and brand already exists.", null);

            var product = new Product
            {
                CategoryId = categoryId,
                ProductName = productName,
                Brand = brand,
                Unit = unit,
                CostPrice = costPrice,
                SellingPrice = sellingPrice,
                ReorderLevel = reorderLevel,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes,
                SKU = cleanSku,
                ExpirationDate = expirationDate,
                Description = cleanDescription,
                SupplierId = (supplierId.HasValue && supplierId.Value > 0) ? supplierId.Value : null
            };

            return (true, string.Empty, product);
        }

        private async Task<(bool Valid, string ErrorMessage)> ValidateAlternateSuppliersAsync(List<int> supplierIds)
        {
            if (supplierIds == null || supplierIds.Count == 0)
                return (true, string.Empty);

            foreach (var id in supplierIds.Distinct())
            {
                if (id <= 0)
                    return (false, "One of the alternate suppliers is invalid.");

                var supplier = await _supplierRepository.GetByIdAsync(id);
                if (supplier == null)
                    return (false, $"An alternate supplier (ID {id}) no longer exists.");

                if (!supplier.IsActive)
                    return (false, $"Alternate supplier \"{supplier.SupplierName}\" is inactive.");
            }

            return (true, string.Empty);
        }
    }
}