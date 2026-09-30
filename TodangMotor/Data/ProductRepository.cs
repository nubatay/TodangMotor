using System.Data;
using Dapper;
using TodangMotor.Models;

namespace TodangMotor.Data
{
    public class ProductRepository
    {
        // Returns ALL products (active AND inactive), all columns including
        // CostPrice and Notes. Owner-only code paths only.
        public async Task<List<Product>> GetAllAsync()
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                SELECT ProductId, CategoryId, ProductName, Brand, Unit,
                       CostPrice, SellingPrice, QuantityOnHand, ReorderLevel,
                       IsActive, CreatedAt, UpdatedAt, Notes
                FROM Products
                ORDER BY ProductName ASC;";

            var products = await connection.QueryAsync<Product>(sql);
            return products.ToList();
        }

        // Fetch a single product by its Id.
        public async Task<Product?> GetByIdAsync(int productId)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                SELECT ProductId, CategoryId, ProductName, Brand, Unit,
                       CostPrice, SellingPrice, QuantityOnHand, ReorderLevel,
                       IsActive, CreatedAt, UpdatedAt, Notes
                FROM Products
                WHERE ProductId = @ProductId;";

            return await connection.QuerySingleOrDefaultAsync<Product>(sql, new { ProductId = productId });
        }

        // CASE-SENSITIVE lookup on ProductName + Brand combination.
        public async Task<Product?> GetByNameAndBrandAsync(string productName, string brand)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                SELECT TOP 1 ProductId, CategoryId, ProductName, Brand, Unit,
                       CostPrice, SellingPrice, QuantityOnHand, ReorderLevel,
                       IsActive, CreatedAt, UpdatedAt, Notes
                FROM Products
                WHERE ProductName COLLATE SQL_Latin1_General_CP1_CS_AS = @ProductName
                  AND Brand       COLLATE SQL_Latin1_General_CP1_CS_AS = @Brand;";

            return await connection.QuerySingleOrDefaultAsync<Product>(
                sql, new { ProductName = productName, Brand = brand });
        }

        // Inserts a brand-new product. QuantityOnHand always starts at 0.
        public async Task<int> InsertAsync(Product product)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                INSERT INTO Products
                    (CategoryId, ProductName, Brand, Unit, CostPrice,
                     SellingPrice, QuantityOnHand, ReorderLevel, IsActive,
                     CreatedAt, UpdatedAt, Notes)
                VALUES
                    (@CategoryId, @ProductName, @Brand, @Unit, @CostPrice,
                     @SellingPrice, 0, @ReorderLevel, 1,
                     GETDATE(), GETDATE(), @Notes);

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
                product.Notes
            });

            return newId;
        }

        // Updates the editable fields (including Notes).
        public async Task<bool> UpdateAsync(Product product)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                UPDATE Products
                SET CategoryId    = @CategoryId,
                    ProductName   = @ProductName,
                    Brand         = @Brand,
                    Unit          = @Unit,
                    CostPrice     = @CostPrice,
                    SellingPrice  = @SellingPrice,
                    ReorderLevel  = @ReorderLevel,
                    Notes         = @Notes,
                    UpdatedAt     = GETDATE()
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
                product.Notes
            });

            return rowsAffected > 0;
        }

        // Flips IsActive on/off (Deactivate/Reactivate), stamping UpdatedAt.
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

        // Updates QuantityOnHand and logs a StockMovements row in one
        // atomic transaction.
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

        // Updates only CostPrice. Used by Stock-In's "prompt after save"
        // flow when the Owner accepts a delivery's new cost as the product's
        // new baseline cost. UpdatedAt is stamped.
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
    }
}