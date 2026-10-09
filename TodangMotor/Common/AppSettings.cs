namespace TodangMotor.Common
{
    /// <summary>
    /// This class holds app-wide settings.
    /// Right now, it only holds the database connection strings.
    /// </summary>
    public static class AppSettings
    {
        /// <summary>
        /// The "address" the app uses to find and talk to the TodangMotorDB database.
        /// </summary>
        public static string ConnectionString =
            @"Server=(localdb)\mssqllocaldb;Database=TodangMotorDB;Integrated Security=True;TrustServerCertificate=True;";

        /// <summary>
        /// Same server, but connects to the "master" database.
        /// Used only during first-run setup to check whether TodangMotorDB
        /// exists, and to create it if it doesn't.
        /// </summary>
        public static string MasterConnectionString =
            @"Server=(localdb)\mssqllocaldb;Database=master;Integrated Security=True;TrustServerCertificate=True;";
    }
}