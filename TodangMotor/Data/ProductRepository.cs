using Dapper;
using TodangMotor.Models;

namespace TodangMotor.Data
{
    public class ProductRepository
    {
        // Returns ALL products (active AND inactive), all columns including
        // CostPrice. This is only ever called from Owner-only code paths.
        public async Task<List<Product>> GetAllAsync()
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                SELECT ProductId, CategoryId, ProductName, Brand, Unit,
                       CostPrice, SellingPrice, QuantityOnHand, ReorderLevel,
                       IsActive, CreatedAt, UpdatedAt
                FROM Products
                ORDER BY ProductName ASC;";

            var products = await connection.QueryAsync<Product>(sql);
            return products.ToList();
        }

        // Fetch a single product by its Id (used when opening ProductForm
        // in Edit mode).
        public async Task<Product?> GetByIdAsync(int productId)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                SELECT ProductId, CategoryId, ProductName, Brand, Unit,
                       CostPrice, SellingPrice, QuantityOnHand, ReorderLevel,
                       IsActive, CreatedAt, UpdatedAt
                FROM Products
                WHERE ProductId = @ProductId;";

            return await connection.QuerySingleOrDefaultAsync<Product>(sql, new { ProductId = productId });
        }

        // Case-insensitive lookup on ProductName + Brand combination.
        // Used by the Service for the duplicate check (Option B).
        public async Task<Product?> GetByNameAndBrandAsync(string productName, string brand)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                SELECT ProductId, CategoryId, ProductName, Brand, Unit,
                       CostPrice, SellingPrice, QuantityOnHand, ReorderLevel,
                       IsActive, CreatedAt, UpdatedAt
                FROM Products
                WHERE LOWER(ProductName) = LOWER(@ProductName)
                  AND LOWER(Brand) = LOWER(@Brand);";

            return await connection.QuerySingleOrDefaultAsync<Product>(
                sql, new { ProductName = productName, Brand = brand });
        }

        // Inserts a brand-new product. QuantityOnHand always starts at 0 —
        // it is NEVER set here, it only changes via Stock-In/Sales later.
        // Returns the new ProductId (needed so the Form can refresh/select it).
        public async Task<int> InsertAsync(Product product)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                INSERT INTO Products
                    (CategoryId, ProductName, Brand, Unit, CostPrice,
                     SellingPrice, QuantityOnHand, ReorderLevel, IsActive,
                     CreatedAt, UpdatedAt)
                VALUES
                    (@CategoryId, @ProductName, @Brand, @Unit, @CostPrice,
                     @SellingPrice, 0, @ReorderLevel, 1,
                     GETDATE(), GETDATE());

                SELECT CAST(SCOPE_IDENTITY() AS int);";

            var newId = await connection.QuerySingleAsync<int>(sql, new
            {
                product.CategoryId,
                product.ProductName,
                product.Brand,
                product.Unit,
                product.CostPrice,
                product.SellingPrice,
                product.ReorderLevel
            });

            return newId;
        }

        // Updates the 6 editable fields only. Deliberately does NOT touch
        // QuantityOnHand or IsActive — those are handled elsewhere.
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
                product.ReorderLevel
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
    }
}