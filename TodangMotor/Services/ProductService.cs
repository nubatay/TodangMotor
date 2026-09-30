using System.Globalization;
using TodangMotor.Common;
using TodangMotor.Data;
using TodangMotor.Models;

namespace TodangMotor.Services
{
    public class ProductService
    {
        private readonly ProductRepository _productRepository;
        private readonly CategoryRepository _categoryRepository;

        private const int ProductNameMaxLength = 150;
        private const int BrandMaxLength = 100;
        private const int UnitMaxLength = 20;

        public ProductService()
        {
            _productRepository = new ProductRepository();
            _categoryRepository = new CategoryRepository();
        }

        // ==================== READ ====================

        // Returns ALL products (active + inactive), including CostPrice.
        // Owner-only — Cashier must never even fetch this (Rule 5).
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

        // ==================== ADD ====================

        public async Task<(bool Success, string ErrorMessage)> AddAsync(
            int categoryId, string productName, string brand, string unit,
            string costPriceText, string sellingPriceText, string reorderLevelText)
        {
            if (!SessionManager.IsOwner)
                return (false, "Access denied. Only the Owner can manage products.");

            var (valid, errorMessage, cleanProduct) = await ValidateAndBuildAsync(
                categoryId, productName, brand, unit, costPriceText, sellingPriceText, reorderLevelText,
                excludingProductId: null);

            if (!valid)
                return (false, errorMessage);

            try
            {
                await _productRepository.InsertAsync(cleanProduct!);
                return (true, string.Empty);
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                return (false, "A product with this name and brand already exists.");
            }
            catch (Exception)
            {
                return (false, "Could not add the product. Please check your database connection.");
            }
        }

        // ==================== UPDATE ====================

        public async Task<(bool Success, string ErrorMessage)> UpdateAsync(
            int productId, int categoryId, string productName, string brand, string unit,
            string costPriceText, string sellingPriceText, string reorderLevelText)
        {
            if (!SessionManager.IsOwner)
                return (false, "Access denied. Only the Owner can manage products.");

            var existing = await _productRepository.GetByIdAsync(productId);
            if (existing == null)
                return (false, "Product not found.");

            var (valid, errorMessage, cleanProduct) = await ValidateAndBuildAsync(
                categoryId, productName, brand, unit, costPriceText, sellingPriceText, reorderLevelText,
                excludingProductId: productId);

            if (!valid)
                return (false, errorMessage);

            cleanProduct!.ProductId = productId;

            try
            {
                var updated = await _productRepository.UpdateAsync(cleanProduct);
                if (!updated)
                    return (false, "Product not found.");

                return (true, string.Empty);
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                return (false, "A product with this name and brand already exists.");
            }
            catch (Exception)
            {
                return (false, "Could not update the product. Please check your database connection.");
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

        // Validates everything and, if all good, returns a ready-to-save
        // Product object (QuantityOnHand/IsActive/timestamps are NOT set
        // here — the Repository handles those).
        private async Task<(bool Valid, string ErrorMessage, Product? Product)> ValidateAndBuildAsync(
            int categoryId, string productName, string brand, string unit,
            string costPriceText, string sellingPriceText, string reorderLevelText,
            int? excludingProductId)
        {
            // ---- ProductName ----
            if (string.IsNullOrWhiteSpace(productName))
                return (false, "Product name is required.", null);

            productName = productName.Trim();
            if (productName.Length > ProductNameMaxLength)
                return (false, $"Product name cannot exceed {ProductNameMaxLength} characters.", null);

            // ---- Brand (required per business decision) ----
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

            // ---- Duplicate check: ProductName + Brand (case-insensitive) ----
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
                ReorderLevel = reorderLevel
            };

            return (true, string.Empty, product);
        }
    }
}