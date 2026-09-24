using Microsoft.Data.SqlClient;
using System.Data;
using TodangMotor.Common;

namespace TodangMotor.Data
{
    /// <summary>
    /// This class's only job is to create a connection to the database.
    /// Every Repository (Products, Sales, Users, etc.) will ask this class
    /// for a fresh connection whenever they need to talk to the database.
    /// </summary>
    public static class DbConnectionFactory
    {
        /// <summary>
        /// Creates and returns a brand new database connection,
        /// using the address from AppSettings.
        /// </summary>
        public static IDbConnection CreateConnection()
        {
            return new SqlConnection(AppSettings.ConnectionString);
        }
    }
}