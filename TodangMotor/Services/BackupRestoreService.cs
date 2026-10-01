using System;
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

                // BACKUP DATABASE — must run against master (or any other DB),
                // not against the DB we're backing up (SQL actually allows both,
                // but master is the standard).
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
                // Release every pooled connection to TodangMotorDB so the
                // single-user lock can succeed.
                SqlConnection.ClearAllPools();

                using var connection = new SqlConnection(BuildMasterConnectionString());
                await connection.OpenAsync();

                // 1. Force the DB into single-user mode (kills other connections).
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

                // 2. RESTORE with REPLACE. Same-PC restore → paths still valid.
                const string restoreSql = @"
                    RESTORE DATABASE [TodangMotorDB]
                    FROM DISK = @Path
                    WITH REPLACE;";

                using (var cmd = new SqlCommand(restoreSql, connection))
                {
                    cmd.Parameters.AddWithValue("@Path", backupFilePath);
                    cmd.CommandTimeout = 600;
                    await cmd.ExecuteNonQueryAsync();
                }

                // 3. Return to multi-user.
                const string multiUserSql = @"
                    ALTER DATABASE [TodangMotorDB] SET MULTI_USER;";

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
        /// the master database instead of TodangMotorDB. Required for
        /// BACKUP and RESTORE, since you can't safely talk to a database
        /// while you're replacing it.
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