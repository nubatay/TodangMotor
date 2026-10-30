using Dapper;
using TodangMotor.Models;

namespace TodangMotor.Data
{
    public class CategoryRepository
    {
        // Get every category, active or not (Owner needs to see and reactivate hidden ones).
        // Sorted by CategoryId ASC so newly added categories always appear at the BOTTOM
        // of the grid, in a predictable spot — not scattered alphabetically.
        public async Task<IEnumerable<Category>> GetAllAsync()
        {
            using var connection = DbConnectionFactory.CreateConnection();
            const string sql = "SELECT CategoryId, CategoryName, IsActive FROM Categories ORDER BY CategoryId ASC";
            return await connection.QueryAsync<Category>(sql);
        }

        // Get only active categories (used for dropdowns, like Product form).
        // Kept alphabetical here on purpose — easier for a user to FIND a category
        // by name when picking from a dropdown list.
        public async Task<IEnumerable<Category>> GetActiveAsync()
        {
            using var connection = DbConnectionFactory.CreateConnection();
            const string sql = "SELECT CategoryId, CategoryName, IsActive FROM Categories WHERE IsActive = 1 ORDER BY CategoryName";
            return await connection.QueryAsync<Category>(sql);
        }

        // Get one category by its ID
        public async Task<Category?> GetByIdAsync(int categoryId)
        {
            using var connection = DbConnectionFactory.CreateConnection();
            const string sql = "SELECT CategoryId, CategoryName, IsActive FROM Categories WHERE CategoryId = @CategoryId";
            return await connection.QueryFirstOrDefaultAsync<Category>(sql, new { CategoryId = categoryId });
        }

        // Get a category by its exact name (used to check for duplicates)
        public async Task<Category?> GetByNameAsync(string categoryName)
        {
            using var connection = DbConnectionFactory.CreateConnection();
            const string sql = "SELECT CategoryId, CategoryName, IsActive FROM Categories WHERE LOWER(CategoryName) = LOWER(@CategoryName)";
            return await connection.QueryFirstOrDefaultAsync<Category>(sql, new { CategoryName = categoryName });
        }

        // Add a new category, returns the new CategoryId that SQL Server generated
        public async Task<int> InsertAsync(Category category)
        {
            using var connection = DbConnectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO Categories (CategoryName, IsActive)
                VALUES (@CategoryName, @IsActive);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            return await connection.ExecuteScalarAsync<int>(sql, new
            {
                category.CategoryName,
                category.IsActive
            });
        }

        // Update an existing category's name
        public async Task<int> UpdateAsync(Category category)
        {
            using var connection = DbConnectionFactory.CreateConnection();
            const string sql = "UPDATE Categories SET CategoryName = @CategoryName WHERE CategoryId = @CategoryId";
            return await connection.ExecuteAsync(sql, new
            {
                category.CategoryName,
                category.CategoryId
            });
        }

        // Flip IsActive on/off (soft delete or reactivate)
        public async Task<int> SetActiveStatusAsync(int categoryId, bool isActive)
        {
            using var connection = DbConnectionFactory.CreateConnection();
            const string sql = "UPDATE Categories SET IsActive = @IsActive WHERE CategoryId = @CategoryId";
            return await connection.ExecuteAsync(sql, new
            {
                CategoryId = categoryId,
                IsActive = isActive
            });
        }

        // Count how many active products still use this category (needed before deactivating)
        public async Task<int> CountActiveProductsUsingCategoryAsync(int categoryId)
        {
            using var connection = DbConnectionFactory.CreateConnection();
            const string sql = "SELECT COUNT(*) FROM Products WHERE CategoryId = @CategoryId AND IsActive = 1";
            return await connection.ExecuteScalarAsync<int>(sql, new { CategoryId = categoryId });
        }
    }
}