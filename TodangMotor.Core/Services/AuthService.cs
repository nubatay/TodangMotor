using System;
using BCrypt.Net;
using TodangMotor.Data;
using TodangMotor.Models;

namespace TodangMotor.Services
{
    /// <summary>
    /// The "traffic light" that tells the LoginForm WHY a login attempt
    /// succeeded or failed, so it can show the right message.
    /// </summary>
    public enum LoginResult
    {
        Success,
        InvalidCredentials,
        AccountDisabled,
        LockedOut
    }

    /// <summary>
    /// The "brain" of login logic. Decides what counts as a valid login,
    /// seeds the initial Owner on first run, and enforces the lockout
    /// policy for repeated failed attempts.
    /// </summary>
    public class AuthService
    {
        private readonly UserRepository _userRepository;

        // Lockout policy — locked decisions from Module 9 planning.
        private const int MaxFailedAttempts = 3;
        private const int LockoutDurationSecs = 500;

        public AuthService()
        {
            _userRepository = new UserRepository();
        }

        // ============================================================
        // FIRST-RUN OWNER SEED
        // ============================================================

        public async Task SeedOwnerIfNeededAsync()
        {
            bool usersExist = await _userRepository.AnyUsersExistAsync();

            if (!usersExist)
            {
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword("owner123");

                await _userRepository.InsertAsync(
                    username: "owner",
                    passwordHash: hashedPassword,
                    fullName: "Todang",
                    role: "Owner"
                );
            }
        }

        // ============================================================
        // LOGIN
        // ============================================================

        public async Task<(LoginResult Result, User? UserRecord, int LockoutSecondsRemaining)> LoginAsync(
            string username, string password)
        {
            string trimmedUsername = username.Trim();

            User? user = await _userRepository.GetByUsernameAsync(trimmedUsername);

            // ---- Unknown username: no counter, no lockout (L3 = B) ----
            if (user == null)
            {
                return (LoginResult.InvalidCredentials, null, 0);
            }

            // ---- Already locked out? ----
            if (user.LockoutUntil.HasValue && user.LockoutUntil.Value > DateTime.Now)
            {
                int secondsLeft = (int)Math.Ceiling(
                    (user.LockoutUntil.Value - DateTime.Now).TotalSeconds);

                if (secondsLeft < 0) secondsLeft = 0;

                return (LoginResult.LockedOut, null, secondsLeft);
            }

            // ---- Verify password ----
            bool passwordMatches = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);

            if (!passwordMatches)
            {
                int newCount = await _userRepository.IncrementFailedAttemptsAsync(user.UserId);

                if (newCount >= MaxFailedAttempts)
                {
                    var until = DateTime.Now.AddSeconds(LockoutDurationSecs);
                    await _userRepository.SetLockoutUntilAsync(user.UserId, until);

                    return (LoginResult.LockedOut, null, LockoutDurationSecs);
                }

                return (LoginResult.InvalidCredentials, null, 0);
            }

            // ---- Account disabled? ----
            if (!user.IsActive)
            {
                return (LoginResult.AccountDisabled, null, 0);
            }

            // ---- Success: clear any accumulated attempts ----
            await _userRepository.ResetLoginAttemptsAsync(user.UserId);

            return (LoginResult.Success, user, 0);
        }
    }
}