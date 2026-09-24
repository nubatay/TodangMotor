using Dapper;
using TodangMotor.Models;

namespace TodangMotor.Data
{
    /// <summary>
    /// This class is the ONLY place in the app that directly talks to
    /// the Users table in the database. Think of it as the librarian
    /// who knows exactly where the "Users" filing cabinet is and how
    /// to pull out or put in paperwork.
    /// </summary>
    public class UserRepository
    {
        /// <summary>
        /// Finds one user by their username.
        /// Returns null if no user with that username exists.
        /// </summary>
        public async Task<User?> GetByUsernameAsync(string username)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                SELECT UserId, Username, PasswordHash, FullName, Role, IsActive, CreatedAt
                FROM Users
                WHERE Username = @Username";

            return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Username = username });
        }

        /// <summary>
        /// Checks if the Users table has ANY rows at all.
        /// Used to decide whether we need to auto-create the Owner account
        /// the very first time the app runs.
        /// </summary>
        public async Task<bool> AnyUsersExistAsync()
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = "SELECT COUNT(1) FROM Users";

            int count = await connection.ExecuteScalarAsync<int>(sql);
            return count > 0;
        }

        /// <summary>
        /// Adds a brand new user row into the Users table.
        /// The password passed in here must ALREADY be hashed by BCrypt
        /// before it reaches this method — this method never sees the
        /// real plain-text password.
        /// </summary>
        public async Task InsertAsync(string username, string passwordHash, string fullName, string role)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                INSERT INTO Users (Username, PasswordHash, FullName, Role, IsActive, CreatedAt)
                VALUES (@Username, @PasswordHash, @FullName, @Role, 1, GETDATE())";

            await connection.ExecuteAsync(sql, new
            {
                Username = username,
                PasswordHash = passwordHash,
                FullName = fullName,
                Role = role
            });
        }
    }
}