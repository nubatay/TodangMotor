namespace TodangMotor.Common
{
    /// <summary>
    /// This class holds app-wide settings.
    /// Right now, it only holds the database connection string.
    /// </summary>
    public static class AppSettings
    {
        /// <summary>
        /// This is the "address" the app uses to find and talk to the database.
        /// It tells the app: which server, which database, and how to log in.
        /// </summary>
        public static string ConnectionString =
            @"Server=(localdb)\mssqllocaldb;Database=TodangMotorDB;Integrated Security=True;TrustServerCertificate=True;";
    }
}