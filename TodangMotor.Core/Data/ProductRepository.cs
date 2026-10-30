using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using TodangMotor.Models;

namespace TodangMotor.Data
{
    /// <summary>
    /// All database access for the Products table and its related
    /// alternate-suppliers junction table.
    /// </summary>
    public class ProductRepository
    {
        // ============================================================
        // READS
        // ============================================================

        /// <summary>
        /// Returns ALL products (active and inactive), with supplier name
        /// and alternate supplier IDs populated.
        /// Owner-only code paths only.
        /// </summary>
        public async Task<List<Product>> GetAllAsync()
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                SELECT p.ProductId, p.CategoryId, p.ProductName, p.Brand, p.Unit,
                       p.CostPrice, p.SellingPrice, p.QuantityOnHand, p.ReorderLevel,
                       p.IsActive, p.CreatedAt, p.UpdatedAt, p.Notes,
                       p.SKU, p.ExpirationDate, p.Description, p.SupplierId,
                       s.SupplierName AS PrimarySupplierName
                FROM Products p
                LEFT JOIN Suppliers s ON s.SupplierId = p.SupplierId
                ORDER BY p.ProductName ASC, p.Brand ASC;";

            var products = (await connection.QueryAsync<Product>(sql)).ToList();

            // Load all alternate supplier links in one shot, then map to products.
            var alts = await LoadAllAlternateSuppliersAsync(connection);
            foreach (var p in products)
            {
                if (alts.TryGetValue(p.ProductId, out var ids))
                    p.AlternateSupplierIds = ids;
            }

            return products;
        }

        /// <summary>
        /// Single product by ID, with supplier name and alternate supplier IDs.
        /// Returns null if not found.
        /// </summary>
        public async Task<Product?> GetByIdAsync(int productId)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                SELECT p.ProductId, p.CategoryId, p.ProductName, p.Brand, p.Unit,
                       p.CostPrice, p.SellingPrice, p.QuantityOnHand, p.ReorderLevel,
                       p.IsActive, p.CreatedAt, p.UpdatedAt, p.Notes,
                       p.SKU, p.ExpirationDate, p.Description, p.SupplierId,
                       s.SupplierName AS PrimarySupplierName
                FROM Products p
                LEFT JOIN Suppliers s ON s.SupplierId = p.SupplierId
                WHERE p.ProductId = @ProductId;";

            var product = await connection.QuerySingleOrDefaultAsync<Product>(
                sql, new { ProductId = productId });

            if (product != null)
                product.AlternateSupplierIds = await LoadAlternateSupplierIdsAsync(connection, productId);

            return product;
        }

        /// <summary>
        /// Case-sensitive lookup on ProductName + Brand combination.
        /// Used by the service for the duplicate check.
        /// </summary>
        public async Task<Product?> GetByNameAndBrandAsync(string productName, string brand)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                SELECT TOP 1 p.ProductId, p.CategoryId, p.ProductName, p.Brand, p.Unit,
                       p.CostPrice, p.SellingPrice, p.QuantityOnHand, p.ReorderLevel,
                       p.IsActive, p.CreatedAt, p.UpdatedAt, p.Notes,
                       p.SKU, p.ExpirationDate, p.Description, p.SupplierId,
                       s.SupplierName AS PrimarySupplierName
                FROM Products p
                LEFT JOIN Suppliers s ON s.SupplierId = p.SupplierId
                WHERE p.ProductName COLLATE SQL_Latin1_General_CP1_CS_AS = @ProductName
                  AND p.Brand       COLLATE SQL_Latin1_General_CP1_CS_AS = @Brand;";

            return await connection.QuerySingleOrDefaultAsync<Product>(
                sql, new { ProductName = productName, Brand = brand });
        }

        /// <summary>
        /// Looks up a product by SKU (case-insensitive). Returns null if not found.
        /// Used for the SKU uniqueness check.
        /// </summary>
        public async Task<Product?> GetBySkuAsync(string sku)
        {
            if (string.IsNullOrWhiteSpace(sku)) return null;

            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                SELECT TOP 1 p.ProductId, p.CategoryId, p.ProductName, p.Brand, p.Unit,
                       p.CostPrice, p.SellingPrice, p.QuantityOnHand, p.ReorderLevel,
                       p.IsActive, p.CreatedAt, p.UpdatedAt, p.Notes,
                       p.SKU, p.ExpirationDate, p.Description, p.SupplierId,
                       s.SupplierName AS PrimarySupplierName
                FROM Products p
                LEFT JOIN Suppliers s ON s.SupplierId = p.SupplierId
                WHERE LOWER(p.SKU) = LOWER(@SKU);";

            return await connection.QuerySingleOrDefaultAsync<Product>(sql, new { SKU = sku });
        }

        /// <summary>
        /// Active products supplied by the given supplier — either as the
        /// primary supplier or as an alternate.
        /// Used by the Stock-In product picker.
        /// </summary>
        public async Task<List<Product>> GetBySupplierIdAsync(int supplierId)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                SELECT p.ProductId, p.CategoryId, p.ProductName, p.Brand, p.Unit,
                       p.CostPrice, p.SellingPrice, p.QuantityOnHand, p.ReorderLevel,
                       p.IsActive, p.CreatedAt, p.UpdatedAt, p.Notes,
                       p.SKU, p.ExpirationDate, p.Description, p.SupplierId,
                       s.SupplierName AS PrimarySupplierName
                FROM Products p
                LEFT JOIN Suppliers s ON s.SupplierId = p.SupplierId
                WHERE p.IsActive = 1
                  AND (
                      p.SupplierId = @SupplierId
                      OR EXISTS (
                          SELECT 1 FROM ProductAlternateSuppliers pas
                          WHERE pas.ProductId = p.ProductId
                            AND pas.SupplierId = @SupplierId
                      )
                  )
                ORDER BY p.ProductName ASC, p.Brand ASC;";

            var products = (await connection.QueryAsync<Product>(sql, new { SupplierId = supplierId })).ToList();

            var alts = await LoadAllAlternateSuppliersAsync(connection);
            foreach (var p in products)
            {
                if (alts.TryGetValue(p.ProductId, out var ids))
                    p.AlternateSupplierIds = ids;
            }

            return products;
        }

        // ============================================================
        // WRITES
        // ============================================================

        /// <summary>
        /// Inserts a brand-new product. QuantityOnHand always starts at 0.
        /// Returns the new ProductId.
        /// </summary>
        public async Task<int> InsertAsync(Product product)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                INSERT INTO Products
                    (CategoryId, ProductName, Brand, Unit, CostPrice,
                     SellingPrice, QuantityOnHand, ReorderLevel, IsActive,
                     CreatedAt, UpdatedAt, Notes,
                     SKU, ExpirationDate, Description, SupplierId)
                VALUES
                    (@CategoryId, @ProductName, @Brand, @Unit, @CostPrice,
                     @SellingPrice, 0, @ReorderLevel, 1,
                     GETDATE(), GETDATE(), @Notes,
                     @SKU, @ExpirationDate, @Description, @SupplierId);

                SELECT CAST(SCOPE_IDENTITY() AS int);";

            var newId = await connection.QuerySingleAsync<int>(sql, new
            {
                product.CategoryId,
                product.ProductName,
                product.Brand,
                product.Unit,
                product.CostPrice,
                product.SellingPrice,
                product.ReorderLevel,
                product.Notes,
                product.SKU,
                product.ExpirationDate,
                product.Description,
                product.SupplierId
            });

            return newId;
        }

        /// <summary>
        /// Updates editable fields (including Notes, SKU, ExpirationDate,
        /// Description, SupplierId). Does NOT touch QuantityOnHand or IsActive.
        /// </summary>
        public async Task<bool> UpdateAsync(Product product)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                UPDATE Products
                SET CategoryId     = @CategoryId,
                    ProductName    = @ProductName,
                    Brand          = @Brand,
                    Unit           = @Unit,
                    CostPrice      = @CostPrice,
                    SellingPrice   = @SellingPrice,
                    ReorderLevel   = @ReorderLevel,
                    Notes          = @Notes,
                    SKU            = @SKU,
                    ExpirationDate = @ExpirationDate,
                    Description    = @Description,
                    SupplierId     = @SupplierId,
                    UpdatedAt      = GETDATE()
                WHERE ProductId = @ProductId;";

            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                product.ProductId,
                product.CategoryId,
                product.ProductName,
                product.Brand,
                product.Unit,
                product.CostPrice,
                product.SellingPrice,
                product.ReorderLevel,
                product.Notes,
                product.SKU,
                product.ExpirationDate,
                product.Description,
                product.SupplierId
            });

            return rowsAffected > 0;
        }

        /// <summary>
        /// Flips IsActive on/off (Deactivate/Reactivate), stamping UpdatedAt.
        /// </summary>
        public async Task<bool> SetActiveStatusAsync(int productId, bool isActive)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                UPDATE Products
                SET IsActive = @IsActive,
                    UpdatedAt = GETDATE()
                WHERE ProductId = @ProductId;";

            var rowsAffected = await connection.ExecuteAsync(
                sql, new { ProductId = productId, IsActive = isActive });

            return rowsAffected > 0;
        }

        /// <summary>
        /// Updates QuantityOnHand and logs a StockMovements row in one
        /// atomic transaction.
        /// </summary>
        public async Task<bool> UpdateStockWithMovementAsync(
            int productId,
            int quantityBefore,
            int quantityAfter,
            int userId,
            string? notes)
        {
            using var connection = DbConnectionFactory.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                const string updateSql = @"
                    UPDATE Products
                    SET QuantityOnHand = @QuantityAfter,
                        UpdatedAt      = GETDATE()
                    WHERE ProductId = @ProductId;";

                var rows = await connection.ExecuteAsync(updateSql, new
                {
                    ProductId = productId,
                    QuantityAfter = quantityAfter
                }, transaction);

                if (rows == 0)
                {
                    transaction.Rollback();
                    return false;
                }

                const string insertSql = @"
                    INSERT INTO StockMovements
                        (ProductId, MovementType, QuantityChange,
                         QuantityBefore, QuantityAfter, ReferenceId, UserId,
                         MovementDate, Notes)
                    VALUES
                        (@ProductId, 'Adjustment', @QuantityChange,
                         @QuantityBefore, @QuantityAfter, NULL, @UserId,
                         GETDATE(), @Notes);";

                await connection.ExecuteAsync(insertSql, new
                {
                    ProductId = productId,
                    QuantityChange = quantityAfter - quantityBefore,
                    QuantityBefore = quantityBefore,
                    QuantityAfter = quantityAfter,
                    UserId = userId,
                    Notes = notes
                }, transaction);

                transaction.Commit();
                return true;
            }
            catch
            {
                try { transaction.Rollback(); } catch { /* ignore */ }
                throw;
            }
        }

        /// <summary>
        /// Updates only CostPrice. Used by Stock-In's prompt-after-save flow.
        /// </summary>
        public async Task<bool> UpdateCostPriceAsync(int productId, decimal costPrice)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                UPDATE Products
                SET CostPrice = @CostPrice,
                    UpdatedAt = GETDATE()
                WHERE ProductId = @ProductId;";

            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                ProductId = productId,
                CostPrice = costPrice
            });

            return rowsAffected > 0;
        }

        // ============================================================
        // ALTERNATE SUPPLIERS
        // ============================================================

        /// <summary>
        /// Loads the alternate supplier IDs for one product.
        /// </summary>
        public async Task<List<int>> GetAlternateSupplierIdsAsync(int productId)
        {
            using var connection = DbConnectionFactory.CreateConnection();
            return await LoadAlternateSupplierIdsAsync(connection, productId);
        }

        /// <summary>
        /// Replaces the entire alternate supplier list for a product.
        /// Deletes existing rows and inserts the new ones in one transaction.
        /// </summary>
        public async Task<bool> SetAlternateSuppliersAsync(int productId, List<int> supplierIds)
        {
            using var connection = DbConnectionFactory.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                // 1. Delete existing
                const string deleteSql = @"
                    DELETE FROM ProductAlternateSuppliers
                    WHERE ProductId = @ProductId;";

                await connection.ExecuteAsync(deleteSql,
                    new { ProductId = productId }, transaction);

                // 2. Insert new (deduplicated)
                if (supplierIds != null && supplierIds.Count > 0)
                {
                    const string insertSql = @"
                        INSERT INTO ProductAlternateSuppliers (ProductId, SupplierId)
                        VALUES (@ProductId, @SupplierId);";

                    foreach (var sid in supplierIds.Distinct())
                    {
                        await connection.ExecuteAsync(insertSql,
                            new { ProductId = productId, SupplierId = sid },
                            transaction);
                    }
                }

                transaction.Commit();
                return true;
            }
            catch
            {
                try { transaction.Rollback(); } catch { /* ignore */ }
                throw;
            }
        }

        // ============================================================
        // PRIVATE HELPERS
        // ============================================================

        private static async Task<List<int>> LoadAlternateSupplierIdsAsync(
            IDbConnection connection, int productId)
        {
            const string sql = @"
                SELECT SupplierId
                FROM ProductAlternateSuppliers
                WHERE ProductId = @ProductId;";

            var rows = await connection.QueryAsync<int>(sql, new { ProductId = productId });
            return rows.ToList();
        }

        private static async Task<Dictionary<int, List<int>>> LoadAllAlternateSuppliersAsync(
            IDbConnection connection)
        {
            const string sql = @"
                SELECT ProductId, SupplierId
                FROM ProductAlternateSuppliers;";

            var rows = await connection.QueryAsync<AlternateSupplierRow>(sql);

            return rows
                .GroupBy(r => r.ProductId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(r => r.SupplierId).ToList());
        }

        private class AlternateSupplierRow
        {
            public int ProductId { get; set; }
            public int SupplierId { get; set; }
        }
    }
}