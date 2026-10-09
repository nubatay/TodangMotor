using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using TodangMotor.Common;

namespace TodangMotor.Data
{
    /// <summary>
    /// Prepares TodangMotorDB on first launch.
    /// Steps:
    ///   1. If the database doesn't exist → create it via master.
    ///   2. If the core tables are missing → run the embedded Schema.sql.
    /// Everything is idempotent: safe to run on every startup.
    /// </summary>
    public static class DatabaseInitializer
    {
        private const string EmbeddedResourceName = "TodangMotor.Data.Schema.sql";

        /// <summary>
        /// Ensures the database and schema are ready.
        /// Returns (true, "") on success; (false, errorMessage) on failure.
        /// </summary>
        public static async Task<(bool Success, string ErrorMessage)> EnsureDatabaseAsync()
        {
            try
            {
                // ---------- 1. Does the database exist? ----------
                bool dbExists;
                using (var masterConn = new SqlConnection(AppSettings.MasterConnectionString))
                {
                    await masterConn.OpenAsync();

                    int count = await masterConn.ExecuteScalarAsync<int>(
                        "SELECT COUNT(*) FROM sys.databases WHERE name = 'TodangMotorDB';");

                    dbExists = count > 0;
                }

                // ---------- 2. Create it if missing ----------
                if (!dbExists)
                {
                    using var masterConn = new SqlConnection(AppSettings.MasterConnectionString);
                    await masterConn.OpenAsync();
                    await masterConn.ExecuteAsync("CREATE DATABASE TodangMotorDB;");
                }

                // ---------- 3. Are the core tables present? ----------
                bool tablesExist;
                using (var dbConn = new SqlConnection(AppSettings.ConnectionString))
                {
                    await dbConn.OpenAsync();

                    int count = await dbConn.ExecuteScalarAsync<int>(@"
                        SELECT COUNT(*)
                        FROM INFORMATION_SCHEMA.TABLES
                        WHERE TABLE_TYPE = 'BASE TABLE'
                          AND TABLE_NAME IN ('Categories', 'Products', 'Sales');");

                    tablesExist = count == 3;
                }

                if (tablesExist)
                    return (true, string.Empty);

                // ---------- 4. Run the schema script ----------
                string rawSql = LoadEmbeddedSchema();
                var batches = SplitBatches(rawSql);

                using (var dbConn = new SqlConnection(AppSettings.ConnectionString))
                {
                    await dbConn.OpenAsync();

                    foreach (var batch in batches)
                    {
                        if (string.IsNullOrWhiteSpace(batch)) continue;
                        await dbConn.ExecuteAsync(batch);
                    }
                }

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private static string LoadEmbeddedSchema()
        {
            var assembly = Assembly.GetExecutingAssembly();

            using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName);
            if (stream == null)
                throw new InvalidOperationException(
                    $"Embedded resource '{EmbeddedResourceName}' not found. " +
                    "Check the LogicalName in TodangMotor.csproj.");

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        private static List<string> SplitBatches(string sql)
        {
            var batches = new List<string>();
            var current = new StringBuilder();

            foreach (var line in sql.Split('\n'))
            {
                if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
                {
                    batches.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.AppendLine(line);
                }
            }

            if (current.Length > 0)
                batches.Add(current.ToString());

            return batches;
        }
    }
}