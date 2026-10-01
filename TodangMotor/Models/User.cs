using System;

namespace TodangMotor.Models
{
    /// <summary>
    /// This class is a "blueprint" that matches the Users table in the database.
    /// One User object = one row in the Users table.
    /// Dapper uses this to know how to package data coming from/going to SQL.
    /// </summary>
    public class User
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Number of consecutive failed login attempts.
        /// Resets to 0 on successful login or when a lockout starts.
        /// </summary>
        public int FailedLoginAttempts { get; set; }

        /// <summary>
        /// When the account is currently locked out, this holds the
        /// timestamp when the lockout expires. Null = not locked.
        /// </summary>
        public DateTime? LockoutUntil { get; set; }
    }
}