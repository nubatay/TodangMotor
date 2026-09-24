using Dapper;
using TodangMotor.Common;
using TodangMotor.Models;

namespace TodangMotor.Data
{
    /// <summary>
    /// This file talks directly to the Suppliers table in the database.
    /// Think of it as the "librarian" — it knows how to fetch, add, and
    /// update supplier records. It does NOT decide if something is valid;
    /// that's SupplierService's job. This just does the raw database work.
    /// </summary>
    public class SupplierRepository
    {
        // Gets EVERY supplier, active and inactive — used to fill the grid.
        // Sorted by SupplierId ASC so newly added suppliers always appear at
        // the BOTTOM of the grid, in a predictable spot — matching the same
        // behavior as CategoryRepository's GetAllAsync().
        public async Task<IEnumerable<Supplier>> GetAllAsync()
        {
            using var connection = DbConnectionFactory.CreateConnection();
            const string sql = @"
                SELECT SupplierId, SupplierName, ContactNumber, Address, IsActive
                FROM Suppliers
                ORDER BY SupplierId ASC;";

            return await connection.QueryAsync<Supplier>(sql);
        }

        // Gets only ACTIVE suppliers — used later for dropdowns (e.g., Stock-In form).
        // Kept alphabetical here on purpose — easier for a user to FIND a supplier
        // by name when picking from a dropdown list.
        public async Task<IEnumerable<Supplier>> GetActiveAsync()
        {
            using var connection = DbConnectionFactory.CreateConnection();
            const string sql = @"
                SELECT SupplierId, SupplierName, ContactNumber, Address, IsActive
                FROM Suppliers
                WHERE IsActive = 1
                ORDER BY SupplierName ASC;";

            return await connection.QueryAsync<Supplier>(sql);
        }

        // Gets ONE supplier by its ID — used when loading a row for editing.
        public async Task<Supplier?> GetByIdAsync(int supplierId)
        {
            using var connection = DbConnectionFactory.CreateConnection();
            const string sql = @"
                SELECT SupplierId, SupplierName, ContactNumber, Address, IsActive
                FROM Suppliers
                WHERE SupplierId = @SupplierId;";

            return await connection.QuerySingleOrDefaultAsync<Supplier>(sql, new { SupplierId = supplierId });
        }

        // Gets ONE supplier by its name (case-insensitive) — used for duplicate checks.
        public async Task<Supplier?> GetByNameAsync(string supplierName)
        {
            using var connection = DbConnectionFactory.CreateConnection();
            const string sql = @"
                SELECT SupplierId, SupplierName, ContactNumber, Address, IsActive
                FROM Suppliers
                WHERE LOWER(SupplierName) = LOWER(@SupplierName);";

            return await connection.QuerySingleOrDefaultAsync<Supplier>(sql, new { SupplierName = supplierName });
        }

        // Adds a brand new supplier row. Returns the new SupplierId.
        public async Task<int> InsertAsync(Supplier supplier)
        {
            using var connection = DbConnectionFactory.CreateConnection();
            const string sql = @"
                INSERT INTO Suppliers (SupplierName, ContactNumber, Address, IsActive)
                VALUES (@SupplierName, @ContactNumber, @Address, 1);
                SELECT CAST(SCOPE_IDENTITY() AS int);";

            return await connection.QuerySingleAsync<int>(sql, new
            {
                supplier.SupplierName,
                supplier.ContactNumber,
                supplier.Address
            });
        }

        // Updates an existing supplier's details (name, contact, address).
        // Does NOT touch IsActive — that's handled separately by SetActiveStatusAsync.
        public async Task UpdateAsync(Supplier supplier)
        {
            using var connection = DbConnectionFactory.CreateConnection();
            const string sql = @"
                UPDATE Suppliers
                SET SupplierName = @SupplierName,
                    ContactNumber = @ContactNumber,
                    Address = @Address
                WHERE SupplierId = @SupplierId;";

            await connection.ExecuteAsync(sql, new
            {
                supplier.SupplierId,
                supplier.SupplierName,
                supplier.ContactNumber,
                supplier.Address
            });
        }

        // Flips a supplier between active/inactive (soft delete / restore).
        public async Task SetActiveStatusAsync(int supplierId, bool isActive)
        {
            using var connection = DbConnectionFactory.CreateConnection();
            const string sql = @"
                UPDATE Suppliers
                SET IsActive = @IsActive
                WHERE SupplierId = @SupplierId;";

            await connection.ExecuteAsync(sql, new { SupplierId = supplierId, IsActive = isActive });
        }
    }
}