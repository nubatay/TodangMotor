using System;
using System.Windows.Forms;
using TodangMotor.Data;
using TodangMotor.Forms;
using TodangMotor.Services;

namespace TodangMotor
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            // ---- 1. Ensure the database and schema exist ----
            try
            {
                var (ok, error) = DatabaseInitializer
                    .EnsureDatabaseAsync()
                    .GetAwaiter()
                    .GetResult();

                if (!ok)
                {
                    MessageBox.Show(
                        "Could not prepare the database.\n\n" + error +
                        "\n\nIf this is the first run on this PC, please make sure " +
                        "SQL Server Express LocalDB is installed.",
                        "Database Setup Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not prepare the database.\n\n" + ex.Message +
                    "\n\nIf this is the first run on this PC, please make sure " +
                    "SQL Server Express LocalDB is installed.",
                    "Database Setup Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            // ---- 2. Ensure the default Owner account exists ----
            try
            {
                var authService = new AuthService();
                authService.SeedOwnerIfNeededAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Cannot connect to the database. Please check your database setup.\n\n" + ex.Message,
                    "Startup Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            // ---- 3. Show login ----
            Application.Run(new LoginForm());
        }
    }
}