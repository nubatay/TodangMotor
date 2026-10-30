using System;
using Dapper;
using TodangMotor.Models;

namespace TodangMotor.Data
{
    /// <summary>
    /// The ONLY place in the app that directly talks to the Users table.
    /// Handles lookups, inserts, updates, password resets, and lockout
    /// tracking for failed login attempts.
    /// </summary>
    public class UserRepository
    {
        // ============================================================
        // READS
        // ============================================================

        /// <summary>
        /// Finds one user by username (case-insensitive).
        /// Returns null if no user with that username exists.
        /// Includes FailedLoginAttempts and LockoutUntil for login checks.
        /// </summary>
        public async Task<User?> GetByUsernameAsync(string username)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                SELECT UserId, Username, PasswordHash, FullName, Role,
                       IsActive, CreatedAt, FailedLoginAttempts, LockoutUntil
                FROM Users
                WHERE LOWER(Username) = LOWER(@Username);";

            return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Username = username });
        }

        /// <summary>
        /// Finds one user by their UserId.
        /// Returns null if not found.
        /// </summary>
        public async Task<User?> GetByIdAsync(int userId)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                SELECT UserId, Username, PasswordHash, FullName, Role,
                       IsActive, CreatedAt, FailedLoginAttempts, LockoutUntil
                FROM Users
                WHERE UserId = @UserId;";

            return await connection.QueryFirstOrDefaultAsync<User>(sql, new { UserId = userId });
        }

        /// <summary>
        /// Returns every user (active and inactive), sorted by role then username.
        /// Used by the User Management list view.
        /// </summary>
        public async Task<List<User>> GetAllAsync()
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                SELECT UserId, Username, PasswordHash, FullName, Role,
                       IsActive, CreatedAt, FailedLoginAttempts, LockoutUntil
                FROM Users
                ORDER BY 
                    CASE WHEN Role = 'Owner' THEN 0 ELSE 1 END,
                    Username ASC;";

            var rows = await connection.QueryAsync<User>(sql);
            return rows.ToList();
        }

        /// <summary>
        /// Checks if the Users table has ANY rows at all.
        /// Used to decide whether we need to auto-create the first Owner.
        /// </summary>
        public async Task<bool> AnyUsersExistAsync()
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = "SELECT COUNT(1) FROM Users";

            int count = await connection.ExecuteScalarAsync<int>(sql);
            return count > 0;
        }

        /// <summary>
        /// Counts the number of ACTIVE users with the given role.
        /// Used to prevent deactivating the last active Owner.
        /// </summary>
        public async Task<int> CountActiveByRoleAsync(string role)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                SELECT COUNT(1) FROM Users
                WHERE Role = @Role AND IsActive = 1;";

            return await connection.ExecuteScalarAsync<int>(sql, new { Role = role });
        }

        // ============================================================
        // WRITES
        // ============================================================

        /// <summary>
        /// Adds a brand new user. Password must already be BCrypt-hashed.
        /// Returns the new UserId.
        /// </summary>
        public async Task<int> InsertAsync(string username, string passwordHash, string fullName, string role)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                INSERT INTO Users 
                    (Username, PasswordHash, FullName, Role, IsActive, CreatedAt,
                     FailedLoginAttempts, LockoutUntil)
                VALUES 
                    (@Username, @PasswordHash, @FullName, @Role, 1, GETDATE(),
                     0, NULL);

                SELECT CAST(SCOPE_IDENTITY() AS int);";

            return await connection.QuerySingleAsync<int>(sql, new
            {
                Username = username,
                PasswordHash = passwordHash,
                FullName = fullName,
                Role = role
            });
        }

        /// <summary>
        /// Updates username, full name, and role.
        /// Does NOT touch PasswordHash or IsActive — those have their own methods.
        /// Returns true if a row was updated.
        /// </summary>
        public async Task<bool> UpdateAsync(int userId, string username, string fullName, string role)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                UPDATE Users
                SET Username = @Username,
                    FullName = @FullName,
                    Role     = @Role
                WHERE UserId = @UserId;";

            var rows = await connection.ExecuteAsync(sql, new
            {
                UserId = userId,
                Username = username,
                FullName = fullName,
                Role = role
            });

            return rows > 0;
        }

        /// <summary>
        /// Flips IsActive on/off (deactivate / reactivate).
        /// Also clears any active lockout when deactivating, since a
        /// deactivated account can't log in anyway.
        /// </summary>
        public async Task<bool> SetActiveStatusAsync(int userId, bool isActive)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                UPDATE Users
                SET IsActive = @IsActive,
                    FailedLoginAttempts = 0,
                    LockoutUntil = NULL
                WHERE UserId = @UserId;";

            var rows = await connection.ExecuteAsync(sql, new
            {
                UserId = userId,
                IsActive = isActive
            });

            return rows > 0;
        }

        /// <summary>
        /// Replaces the user's PasswordHash with a new BCrypt hash.
        /// Also resets any lockout state (Owner just reset the password,
        /// so any prior failed attempts shouldn't carry over).
        /// </summary>
        public async Task<bool> UpdatePasswordHashAsync(int userId, string newPasswordHash)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                UPDATE Users
                SET PasswordHash = @PasswordHash,
                    FailedLoginAttempts = 0,
                    LockoutUntil = NULL
                WHERE UserId = @UserId;";

            var rows = await connection.ExecuteAsync(sql, new
            {
                UserId = userId,
                PasswordHash = newPasswordHash
            });

            return rows > 0;
        }

        // ============================================================
        // LOCKOUT TRACKING
        // ============================================================

        /// <summary>
        /// Increments FailedLoginAttempts by 1 for the given user
        /// and returns the new count. One round-trip via OUTPUT clause.
        /// </summary>
        public async Task<int> IncrementFailedAttemptsAsync(int userId)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                UPDATE Users
                SET FailedLoginAttempts = FailedLoginAttempts + 1
                OUTPUT INSERTED.FailedLoginAttempts
                WHERE UserId = @UserId;";

            return await connection.ExecuteScalarAsync<int>(sql, new { UserId = userId });
        }

        /// <summary>
        /// Sets LockoutUntil to a future timestamp and resets the
        /// failed-attempts counter to 0. Called once the counter hits
        /// the threshold. When the lockout expires, the user gets a
        /// fresh set of attempts.
        /// </summary>
        public async Task SetLockoutUntilAsync(int userId, DateTime until)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                UPDATE Users
                SET LockoutUntil = @Until,
                    FailedLoginAttempts = 0
                WHERE UserId = @UserId;";

            await connection.ExecuteAsync(sql, new
            {
                UserId = userId,
                Until = until
            });
        }

        /// <summary>
        /// Clears both FailedLoginAttempts and LockoutUntil.
        /// Called after a successful login.
        /// </summary>
        public async Task ResetLoginAttemptsAsync(int userId)
        {
            using var connection = DbConnectionFactory.CreateConnection();

            const string sql = @"
                UPDATE Users
                SET FailedLoginAttempts = 0,
                    LockoutUntil = NULL
                WHERE UserId = @UserId;";

            await connection.ExecuteAsync(sql, new { UserId = userId });
        }
    }
}