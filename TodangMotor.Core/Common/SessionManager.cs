using TodangMotor.Models;

namespace TodangMotor.Common
{
    /// <summary>
    /// This class remembers WHO is currently logged into the app.
    /// Think of it like a "name tag" the app wears after you log in.
    /// Every form can peek at this to know who's using the app right now.
    /// </summary>
    public static class SessionManager
    {
        /// <summary>
        /// The user who is currently logged in.
        /// This is null (empty) if nobody has logged in yet.
        /// </summary>
        public static User? CurrentUser { get; private set; }

        /// <summary>
        /// Quick check: is anyone logged in right now?
        /// </summary>
        public static bool IsLoggedIn => CurrentUser != null;

        /// <summary>
        /// Quick check: is the logged-in person the Owner?
        /// </summary>
        public static bool IsOwner => CurrentUser?.Role == "Owner";

        /// <summary>
        /// Quick check: is the logged-in person the Cashier?
        /// </summary>
        public static bool IsCashier => CurrentUser?.Role == "Cashier";

        /// <summary>
        /// Call this right after a successful login.
        /// It "puts on the name tag" for the rest of the app to see.
        /// </summary>
        public static void Login(User user)
        {
            CurrentUser = user;
        }

        /// <summary>
        /// Call this to log out.
        /// It "takes off the name tag" — nobody is logged in anymore.
        /// </summary>
        public static void Logout()
        {
            CurrentUser = null;
        }
    }
}