using TodangMotor.Forms;
using TodangMotor.Services;

namespace TodangMotor
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            // Before showing any screen, make sure the Owner account exists.
            // If the Users table is completely empty (first time running the app),
            // this automatically creates the default Owner login.
            try
            {
                var authService = new AuthService();
                authService.SeedOwnerIfNeededAsync().GetAwaiter().GetResult();

                // TEMPORARY SCAFFOLDING: also make sure a test Cashier account
                // exists, so Cashier-side access restrictions can be tested
                // before Module 9 (User Management) is built. Remove this call
                // once there's a real "Add User" screen for creating Cashiers.
                authService.SeedTestCashierIfNeededAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Cannot connect to the database. Please check your database setup.\n\n" + ex.Message,
                    "Startup Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                return; // stop the app from continuing if we can't even reach the DB
            }

            Application.Run(new LoginForm());
        }
    }
}