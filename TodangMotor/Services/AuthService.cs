using BCrypt.Net;
using TodangMotor.Data;
using TodangMotor.Models;

namespace TodangMotor.Services
{
    /// <summary>
    /// This is a small "traffic light" label that tells the LoginForm
    /// WHY a login attempt succeeded or failed, so it can show the
    /// right message to the user.
    /// </summary>
    public enum LoginResult
    {
        Success,
        InvalidCredentials,
        AccountDisabled
    }

    /// <summary>
    /// This class is the "brain" of login logic.
    /// It decides what counts as a valid login, and handles creating
    /// the very first Owner account automatically.
    /// </summary>
    public class AuthService
    {
        private readonly UserRepository _userRepository;

        public AuthService()
        {
            _userRepository = new UserRepository();
        }

        /// <summary>
        /// Checks if the Users table is completely empty.
        /// If it is, creates the default Owner account automatically.
        /// This should run once, when the app starts up.
        /// </summary>
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

        /// <summary>
        /// TEMPORARY SCAFFOLDING (remove once Module 9 / User Management exists).
        /// Checks if a user named "cashier" already exists. If not, creates a
        /// default test Cashier account so Cashier-side access restrictions can
        /// be tested before the real "Add User" screen is built.
        /// Password is BCrypt-hashed, never stored as plaintext.
        /// </summary>
        public async Task SeedTestCashierIfNeededAsync()
        {
            User? existingCashier = await _userRepository.GetByUsernameAsync("cashier");

            if (existingCashier == null)
            {
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword("cashier123");

                await _userRepository.InsertAsync(
                    username: "cashier",
                    passwordHash: hashedPassword,
                    fullName: "Test Cashier",
                    role: "Cashier"
                );
            }
        }

        /// <summary>
        /// Tries to log a user in.
        /// Returns a LoginResult (Success / InvalidCredentials / AccountDisabled)
        /// and the User object if login succeeded (null otherwise).
        /// </summary>
        public async Task<(LoginResult Result, User? User)> LoginAsync(string username, string password)
        {
            string trimmedUsername = username.Trim();

            User? user = await _userRepository.GetByUsernameAsync(trimmedUsername);

            if (user == null)
            {
                return (LoginResult.InvalidCredentials, null);
            }

            bool passwordMatches = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);

            if (!passwordMatches)
            {
                return (LoginResult.InvalidCredentials, null);
            }

            if (!user.IsActive)
            {
                return (LoginResult.AccountDisabled, null);
            }

            return (LoginResult.Success, user);
        }
    }
}