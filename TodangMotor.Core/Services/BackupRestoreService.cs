using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using TodangMotor.Common;

namespace TodangMotor.Services
{
    /// <summary>
    /// Owner-only Backup and Restore for the TodangMotorDB SQL Server database.
    /// Backup: writes the entire DB to a .bak file at an Owner-chosen path.
    /// Restore: replaces the current DB with a .bak file, then the app exits.
    /// Also records the last successful backup in a small text file.
    /// </summary>
    public class BackupRestoreService
    {
        private const string DatabaseName = "TodangMotorDB";

        // ============================================================
        // PATHS
        // ============================================================

        public static string DefaultBackupFolder
        {
            get
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return Path.Combine(docs, "TodangMotor", "Backups");
            }
        }

        private static string StateFilePath
        {
            get
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return Path.Combine(docs, "TodangMotor", "LastBackup.txt");
            }
        }

        // ============================================================
        // BACKUP
        // ============================================================

        public async Task<(bool Success, string ErrorMessage)> BackupAsync(string destinationPath)
        {
            if (!SessionManager.IsOwner)
                return (false, "Access denied. Only the Owner can back up the database.");

            if (string.IsNullOrWhiteSpace(destinationPath))
                return (false, "Please choose a file path for the backup.");

            try
            {
                string directory = Path.GetDirectoryName(destinationPath) ?? string.Empty;
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                const string sql = @"
                    BACKUP DATABASE [TodangMotorDB]
                    TO DISK = @Path
                    WITH INIT, FORMAT;";

                using var connection = new SqlConnection(BuildMasterConnectionString());
                await connection.OpenAsync();

                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@Path", destinationPath);
                command.CommandTimeout = 120;

                await command.ExecuteNonQueryAsync();

                TryWriteStateFile(destinationPath);
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Backup failed: {ex.Message}");
            }
        }

        // ============================================================
        // RESTORE
        // ============================================================

        public async Task<(bool Success, string ErrorMessage)> RestoreAsync(string backupFilePath)
        {
            if (!SessionManager.IsOwner)
                return (false, "Access denied. Only the Owner can restore the database.");

            if (string.IsNullOrWhiteSpace(backupFilePath))
                return (false, "Please choose a backup file.");

            if (!File.Exists(backupFilePath))
                return (false, "The selected backup file does not exist.");

            try
            {
                // Release every pooled connection so the single-user lock succeeds.
                SqlConnection.ClearAllPools();

                using var connection = new SqlConnection(BuildMasterConnectionString());
                await connection.OpenAsync();

                // ---------- 1. Read the logical file names inside the backup ----------
                var (dataLogicalName, logLogicalName) =
                    await ReadLogicalFileNamesAsync(connection, backupFilePath);

                if (string.IsNullOrEmpty(dataLogicalName) || string.IsNullOrEmpty(logLogicalName))
                    return (false,
                        "Could not read the file list from the backup. The file may be corrupt " +
                        "or not a SQL Server .bak file.");

                // ---------- 2. Determine the current server's default data folder ----------
                string defaultDataDir = await GetServerDefaultDataPathAsync(connection);

                // Fallback if the server didn't report one (rare).
                if (string.IsNullOrEmpty(defaultDataDir))
                {
                    string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    defaultDataDir = Path.Combine(docs, "TodangMotor", "DatabaseFiles");
                    if (!Directory.Exists(defaultDataDir))
                        Directory.CreateDirectory(defaultDataDir);
                }

                // SQL Server needs the paths to exist.
                if (!Directory.Exists(defaultDataDir))
                    Directory.CreateDirectory(defaultDataDir);

                // ---------- 3. Build new physical paths ----------
                string targetMdf = Path.Combine(defaultDataDir, $"{DatabaseName}.mdf");
                string targetLdf = Path.Combine(defaultDataDir, $"{DatabaseName}_log.ldf");

                // ---------- 4. Force single-user ----------
                const string singleUserSql = @"
                    IF EXISTS (SELECT 1 FROM sys.databases WHERE name = 'TodangMotorDB')
                    BEGIN
                        ALTER DATABASE [TodangMotorDB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    END";

                using (var cmd = new SqlCommand(singleUserSql, connection))
                {
                    cmd.CommandTimeout = 60;
                    await cmd.ExecuteNonQueryAsync();
                }

                // ---------- 5. RESTORE with MOVE ----------
                // RESTORE ... WITH REPLACE moves the files to the target paths
                // regardless of where the backup was originally taken.
                string restoreSql = $@"
                    RESTORE DATABASE [TodangMotorDB]
                    FROM DISK = @Path
                    WITH REPLACE,
                         MOVE @DataLogical TO @TargetMdf,
                         MOVE @LogLogical  TO @TargetLdf;";

                using (var cmd = new SqlCommand(restoreSql, connection))
                {
                    cmd.Parameters.AddWithValue("@Path", backupFilePath);
                    cmd.Parameters.AddWithValue("@DataLogical", dataLogicalName);
                    cmd.Parameters.AddWithValue("@LogLogical", logLogicalName);
                    cmd.Parameters.AddWithValue("@TargetMdf", targetMdf);
                    cmd.Parameters.AddWithValue("@TargetLdf", targetLdf);
                    cmd.CommandTimeout = 600;
                    await cmd.ExecuteNonQueryAsync();
                }

                // ---------- 6. Back to multi-user ----------
                const string multiUserSql = @"ALTER DATABASE [TodangMotorDB] SET MULTI_USER;";

                using (var cmd = new SqlCommand(multiUserSql, connection))
                {
                    cmd.CommandTimeout = 60;
                    await cmd.ExecuteNonQueryAsync();
                }

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                // Try to leave the DB in a usable state even if restore failed.
                try
                {
                    using var conn = new SqlConnection(BuildMasterConnectionString());
                    await conn.OpenAsync();
                    using var cmd = new SqlCommand(
                        "ALTER DATABASE [TodangMotorDB] SET MULTI_USER;", conn);
                    await cmd.ExecuteNonQueryAsync();
                }
                catch { /* ignore */ }

                return (false, $"Restore failed: {ex.Message}");
            }
        }

        // ============================================================
        // RESTORE HELPERS
        // ============================================================

        /// <summary>
        /// Runs RESTORE FILELISTONLY against the given backup and returns
        /// (logical data name, logical log name). These are the identifiers
        /// we need for the MOVE clauses.
        /// </summary>
        private static async Task<(string DataLogicalName, string LogLogicalName)>
            ReadLogicalFileNamesAsync(SqlConnection connection, string backupPath)
        {
            string dataLogical = string.Empty;
            string logLogical = string.Empty;

            const string sql = @"RESTORE FILELISTONLY FROM DISK = @Path;";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Path", backupPath);
            cmd.CommandTimeout = 120;

            using var reader = await cmd.ExecuteReaderAsync();

            // Columns we care about: LogicalName, Type ('D' = data, 'L' = log)
            int logicalNameOrdinal = reader.GetOrdinal("LogicalName");
            int typeOrdinal = reader.GetOrdinal("Type");

            while (await reader.ReadAsync())
            {
                string logicalName = reader.GetString(logicalNameOrdinal);
                string type = reader.GetString(typeOrdinal).Trim().ToUpperInvariant();

                if (type == "D" && string.IsNullOrEmpty(dataLogical))
                    dataLogical = logicalName;
                else if (type == "L" && string.IsNullOrEmpty(logLogical))
                    logLogical = logicalName;
            }

            return (dataLogical, logLogical);
        }

        /// <summary>
        /// Asks the current SQL Server instance for its default data directory
        /// (the folder where it puts .mdf and .ldf files by default).
        /// Different on every machine — that's the whole reason we query it.
        /// </summary>
        private static async Task<string> GetServerDefaultDataPathAsync(SqlConnection connection)
        {
            const string sql = @"
                SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS NVARCHAR(4000));";

            using var cmd = new SqlCommand(sql, connection);
            cmd.CommandTimeout = 30;

            var result = await cmd.ExecuteScalarAsync();

            if (result == null || result == DBNull.Value)
                return string.Empty;

            return result.ToString() ?? string.Empty;
        }

        // ============================================================
        // LAST BACKUP INFO
        // ============================================================

        public static (DateTime? LastBackupTime, string? LastBackupPath) GetLastBackupInfo()
        {
            try
            {
                if (!File.Exists(StateFilePath))
                    return (null, null);

                var lines = File.ReadAllLines(StateFilePath);
                if (lines.Length < 2)
                    return (null, null);

                if (DateTime.TryParse(lines[0], out var dt))
                    return (dt, lines[1]);

                return (null, null);
            }
            catch
            {
                return (null, null);
            }
        }

        public static string SuggestBackupFileName()
        {
            return $"TodangMotor_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
        }

        // ============================================================
        // HELPERS
        // ============================================================

        /// <summary>
        /// Builds a copy of AppSettings.ConnectionString that points at
        /// the master database instead of TodangMotorDB.
        /// </summary>
        private static string BuildMasterConnectionString()
        {
            var builder = new SqlConnectionStringBuilder(AppSettings.ConnectionString);
            builder.InitialCatalog = "master";
            return builder.ConnectionString;
        }

        private static void TryWriteStateFile(string backupPath)
        {
            try
            {
                string folder = Path.GetDirectoryName(StateFilePath) ?? string.Empty;
                if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                File.WriteAllLines(StateFilePath, new[]
                {
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    backupPath
                });
            }
            catch { /* non-critical */ }
        }
    }
}