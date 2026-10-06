using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TodangMotor.Common;
using TodangMotor.Data;
using TodangMotor.Models;

namespace TodangMotor.Services
{
    /// <summary>
    /// Owner-only user management. Validates and coordinates all
    /// Add / Edit / Deactivate / Reactivate actions for users.
    /// Password hashing uses BCrypt.
    /// </summary>
    public class UserService
    {
        private readonly UserRepository _userRepository;

        private const int UsernameMinLength = 3;
        private const int UsernameMaxLength = 50;
        private const int FullNameMaxLength = 100;
        private const int PasswordMinLength = 6;

        private static readonly Regex UsernamePattern = new(
            @"^[A-Za-z0-9_.]+$", RegexOptions.Compiled);

        private const string AccessDeniedMessage =
            "Access denied. Only the Owner can manage users.";
        private const string DbErrorMessage =
            "Cannot complete this action right now. Please check your database connection.";

        public UserService()
        {
            _userRepository = new UserRepository();
        }

        // ============================================================
        // READS
        // ============================================================

        public async Task<(bool Success, string ErrorMessage, List<User> Users)> GetAllAsync()
        {
            if (!SessionManager.IsOwner)
                return (false, AccessDeniedMessage, new List<User>());

            try
            {
                var users = await _userRepository.GetAllAsync();
                return (true, string.Empty, users);
            }
            catch (Exception)
            {
                return (false, DbErrorMessage, new List<User>());
            }
        }

        public async Task<(bool Success, string ErrorMessage, User? User)> GetByIdAsync(int userId)
        {
            if (!SessionManager.IsOwner)
                return (false, AccessDeniedMessage, null);

            if (userId <= 0)
                return (false, "Invalid user ID.", null);

            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                    return (false, "User not found.", null);

                return (true, string.Empty, user);
            }
            catch (Exception)
            {
                return (false, DbErrorMessage, null);
            }
        }

        // ============================================================
        // ADD
        // ============================================================

        public async Task<(bool Success, string ErrorMessage)> AddAsync(
            string? username, string? fullName, string? role, string? password)
        {
            if (!SessionManager.IsOwner)
                return (false, AccessDeniedMessage);

            string cleanUsername = (username ?? string.Empty).Trim();
            string cleanFullName = (fullName ?? string.Empty).Trim();
            string cleanRole = (role ?? string.Empty).Trim();
            string cleanPassword = password ?? string.Empty;

            var (valid, error) = ValidateCommon(cleanUsername, cleanFullName, cleanRole);
            if (!valid) return (false, error);

            if (string.IsNullOrEmpty(cleanPassword))
                return (false, "Password is required.");

            if (cleanPassword.Length < PasswordMinLength)
                return (false, $"Password must be at least {PasswordMinLength} characters.");

            try
            {
                var existing = await _userRepository.GetByUsernameAsync(cleanUsername);
                if (existing != null)
                    return (false, $"A user with the username \"{existing.Username}\" already exists.");
            }
            catch (Exception)
            {
                return (false, DbErrorMessage);
            }

            try
            {
                string hash = BCrypt.Net.BCrypt.HashPassword(cleanPassword);
                await _userRepository.InsertAsync(cleanUsername, hash, cleanFullName, cleanRole);
                return (true, string.Empty);
            }
            catch (Exception)
            {
                return (false, DbErrorMessage);
            }
        }

        // ============================================================
        // UPDATE
        // ============================================================

        public async Task<(bool Success, string ErrorMessage)> UpdateAsync(
            int userId, string? username, string? fullName, string? role, string? newPassword)
        {
            if (!SessionManager.IsOwner)
                return (false, AccessDeniedMessage);

            if (userId <= 0)
                return (false, "Invalid user selected.");

            string cleanUsername = (username ?? string.Empty).Trim();
            string cleanFullName = (fullName ?? string.Empty).Trim();
            string cleanRole = (role ?? string.Empty).Trim();
            string cleanPassword = (newPassword ?? string.Empty).Trim();

            var (valid, error) = ValidateCommon(cleanUsername, cleanFullName, cleanRole);
            if (!valid) return (false, error);

            if (cleanPassword.Length > 0 && cleanPassword.Length < PasswordMinLength)
                return (false, $"Password must be at least {PasswordMinLength} characters.");

            User? current;
            try
            {
                current = await _userRepository.GetByIdAsync(userId);
            }
            catch (Exception)
            {
                return (false, DbErrorMessage);
            }

            if (current == null)
                return (false, "User not found.");

            int currentUserId = SessionManager.CurrentUser?.UserId ?? 0;
            if (userId == currentUserId
                && current.Role == "Owner"
                && cleanRole != "Owner")
            {
                return (false, "You cannot change your own role from Owner.");
            }

            if (current.Role == "Owner" && cleanRole != "Owner")
            {
                try
                {
                    int activeOwners = await _userRepository.CountActiveByRoleAsync("Owner");
                    if (activeOwners <= 1)
                        return (false, "There must always be at least one active Owner account.");
                }
                catch (Exception)
                {
                    return (false, DbErrorMessage);
                }
            }

            try
            {
                var existing = await _userRepository.GetByUsernameAsync(cleanUsername);
                if (existing != null && existing.UserId != userId)
                    return (false, $"Another user with the username \"{existing.Username}\" already exists.");
            }
            catch (Exception)
            {
                return (false, DbErrorMessage);
            }

            try
            {
                var updated = await _userRepository.UpdateAsync(userId, cleanUsername, cleanFullName, cleanRole);
                if (!updated)
                    return (false, "User not found.");

                if (cleanPassword.Length > 0)
                {
                    string hash = BCrypt.Net.BCrypt.HashPassword(cleanPassword);
                    await _userRepository.UpdatePasswordHashAsync(userId, hash);
                }

                return (true, string.Empty);
            }
            catch (Exception)
            {
                return (false, DbErrorMessage);
            }
        }

        // ============================================================
        // DEACTIVATE / REACTIVATE
        // ============================================================

        public async Task<(bool Success, string ErrorMessage)> DeactivateAsync(int userId)
        {
            if (!SessionManager.IsOwner)
                return (false, AccessDeniedMessage);

            if (userId <= 0)
                return (false, "Invalid user selected.");

            User? current;
            try
            {
                current = await _userRepository.GetByIdAsync(userId);
            }
            catch (Exception)
            {
                return (false, DbErrorMessage);
            }

            if (current == null)
                return (false, "User not found.");

            if (!current.IsActive)
                return (false, "This user is already inactive.");

            int currentUserId = SessionManager.CurrentUser?.UserId ?? 0;
            if (userId == currentUserId)
                return (false, "You cannot deactivate your own account.");

            if (current.Role == "Owner")
            {
                try
                {
                    int activeOwners = await _userRepository.CountActiveByRoleAsync("Owner");
                    if (activeOwners <= 1)
                        return (false, "There must always be at least one active Owner account.");
                }
                catch (Exception)
                {
                    return (false, DbErrorMessage);
                }
            }

            try
            {
                var updated = await _userRepository.SetActiveStatusAsync(userId, false);
                if (!updated)
                    return (false, "User not found.");

                return (true, string.Empty);
            }
            catch (Exception)
            {
                return (false, DbErrorMessage);
            }
        }

        public async Task<(bool Success, string ErrorMessage)> ReactivateAsync(int userId)
        {
            if (!SessionManager.IsOwner)
                return (false, AccessDeniedMessage);

            if (userId <= 0)
                return (false, "Invalid user selected.");

            User? current;
            try
            {
                current = await _userRepository.GetByIdAsync(userId);
            }
            catch (Exception)
            {
                return (false, DbErrorMessage);
            }

            if (current == null)
                return (false, "User not found.");

            if (current.IsActive)
                return (false, "This user is already active.");

            try
            {
                var updated = await _userRepository.SetActiveStatusAsync(userId, true);
                if (!updated)
                    return (false, "User not found.");

                return (true, string.Empty);
            }
            catch (Exception)
            {
                return (false, DbErrorMessage);
            }
        }

        // ============================================================
        // CHANGE OWN PASSWORD
        // ============================================================

        /// <summary>
        /// Lets the currently-logged-in user change their own password.
        /// Verifies the old password with BCrypt before hashing the new one.
        /// Also clears any lockout state as a bonus.
        /// </summary>
        public async Task<(bool Success, string ErrorMessage)> ChangeOwnPasswordAsync(
            string? oldPassword,
            string? newPassword,
            string? confirmPassword)
        {
            if (!SessionManager.IsLoggedIn)
                return (false, "You must be logged in to change your password.");

            int userId = SessionManager.CurrentUser?.UserId ?? 0;
            if (userId <= 0)
                return (false, "Could not identify the current user.");

            string oldPw = oldPassword ?? string.Empty;
            string newPw = newPassword ?? string.Empty;
            string confirmPw = confirmPassword ?? string.Empty;

            if (oldPw.Length == 0)
                return (false, "Please enter your current password.");

            if (newPw.Length == 0)
                return (false, "Please enter a new password.");

            if (newPw.Length < PasswordMinLength)
                return (false, $"New password must be at least {PasswordMinLength} characters.");

            if (newPw != confirmPw)
                return (false, "New password and confirmation do not match.");

            if (newPw == oldPw)
                return (false, "New password must be different from the current password.");

            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                    return (false, "Your account could not be found.");

                bool oldMatches = BCrypt.Net.BCrypt.Verify(oldPw, user.PasswordHash);
                if (!oldMatches)
                    return (false, "Current password is incorrect.");

                string newHash = BCrypt.Net.BCrypt.HashPassword(newPw);
                await _userRepository.UpdatePasswordHashAsync(userId, newHash);

                return (true, string.Empty);
            }
            catch (Exception)
            {
                return (false, DbErrorMessage);
            }
        }

        // ============================================================
        // SHARED VALIDATION
        // ============================================================

        private static (bool Valid, string ErrorMessage) ValidateCommon(
            string username, string fullName, string role)
        {
            if (string.IsNullOrWhiteSpace(username))
                return (false, "Username is required.");

            if (username.Length < UsernameMinLength)
                return (false, $"Username must be at least {UsernameMinLength} characters.");

            if (username.Length > UsernameMaxLength)
                return (false, $"Username cannot exceed {UsernameMaxLength} characters.");

            if (!UsernamePattern.IsMatch(username))
                return (false, "Username may only contain letters, digits, underscore (_), and dot (.).");

            if (string.IsNullOrWhiteSpace(fullName))
                return (false, "Full name is required.");

            if (fullName.Length > FullNameMaxLength)
                return (false, $"Full name cannot exceed {FullNameMaxLength} characters.");

            if (string.IsNullOrWhiteSpace(role))
                return (false, "Role is required.");

            if (role != "Owner" && role != "Cashier")
                return (false, "Role must be either Owner or Cashier.");

            return (true, string.Empty);
        }
    }
}