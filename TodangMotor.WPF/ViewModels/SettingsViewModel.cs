using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TodangMotor.Services;
using System.Windows;

namespace TodangMotor.WPF.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly BackupRestoreService _backupService;

        [ObservableProperty] private string _lastBackupDate = "No backup yet";
        [ObservableProperty] private string _lastBackupSize = "—";
        [ObservableProperty] private string _lastBackupAge = "—";
        [ObservableProperty] private string _lastBackupAgeColor = "#64748B"; // Muted
        [ObservableProperty] private string _lastBackupLocation = "—";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotBusy))]
        private bool _isBusy;

        public bool IsNotBusy => !IsBusy;

        // Event to show Message Boxes from the View (keeps ViewModel clean)
        public event Action<string, string, bool>? ShowMessage;

        public SettingsViewModel(BackupRestoreService backupService)
        {
            _backupService = backupService;
            RefreshLastBackupInfo();
        }

        [RelayCommand]
        private void RefreshBackupInfo() => RefreshLastBackupInfo();

        private void RefreshLastBackupInfo()
        {
            var (time, path) = BackupRestoreService.GetLastBackupInfo();

            if (time == null)
            {
                LastBackupDate = "No backup yet";
                LastBackupSize = "—";
                LastBackupAge = "—";
                LastBackupAgeColor = "#64748B";
                LastBackupLocation = "—";
                return;
            }

            LastBackupDate = time.Value.ToString("MMM d, yyyy  h:mm tt");
            LastBackupSize = TryGetFileSize(path);
            LastBackupAge = BuildAgeHint(time.Value);
            LastBackupAgeColor = GetAgeColor(time.Value);
            LastBackupLocation = string.IsNullOrEmpty(path) ? "—" : path;
        }

        [RelayCommand]
        private async Task BackupAsync()
        {
            if (IsBusy) return;

            var dialog = new SaveFileDialog
            {
                Title = "Save Database Backup",
                Filter = "Backup files (*.bak)|*.bak|All files (*.*)|*.*",
                DefaultExt = "bak",
                AddExtension = true,
                FileName = BackupRestoreService.SuggestBackupFileName(),
                InitialDirectory = BackupRestoreService.DefaultBackupFolder,
                OverwritePrompt = true
            };

            if (dialog.ShowDialog() != true) return;

            IsBusy = true;
            try
            {
                var (success, error) = await _backupService.BackupAsync(dialog.FileName);

                if (!success)
                {
                    ShowMessage?.Invoke("Backup Failed", error, true);
                    return;
                }

                RefreshLastBackupInfo();
                ShowMessage?.Invoke("Backup Successful", $"Backup completed successfully.\n\nSaved to:\n{dialog.FileName}", false);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task RestoreAsync()
        {
            if (IsBusy) return;

            var result = MessageBox.Show(
                "WARNING\n\nRestoring will REPLACE all current data — products, sales, inventory, and users — with the contents of the backup file.\n\nMake sure you have a fresh backup of the current data first.\n\nContinue?",
                "Confirm Restore", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

            if (result != MessageBoxResult.Yes) return;

            var dialog = new OpenFileDialog
            {
                Title = "Select a Backup File",
                Filter = "Backup files (*.bak)|*.bak|All files (*.*)|*.*",
                InitialDirectory = BackupRestoreService.DefaultBackupFolder,
                CheckFileExists = true
            };

            if (dialog.ShowDialog() != true) return;

            var finalResult = MessageBox.Show(
                $"Restore from:\n\n{dialog.FileName}\n\nAfter restore, the application will close. Reopen it to use the restored data.\n\nProceed?",
                "Final Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

            if (finalResult != MessageBoxResult.Yes) return;

            IsBusy = true;
            try
            {
                var (success, error) = await _backupService.RestoreAsync(dialog.FileName);

                if (!success)
                {
                    ShowMessage?.Invoke("Restore Failed", error, true);
                    return;
                }

                MessageBox.Show("Restore completed successfully.\n\nThe application will now close. Please reopen it to use the restored data.", "Restore Successful", MessageBoxButton.OK, MessageBoxImage.Information);

                Application.Current.Shutdown();
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void OpenBackupFolder()
        {
            try
            {
                string folder = BackupRestoreService.DefaultBackupFolder;
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                Process.Start("explorer.exe", folder);
            }
            catch (Exception ex)
            {
                ShowMessage?.Invoke("Error", $"Could not open backup folder: {ex.Message}", true);
            }
        }

        // --- Helpers ---
        private static string TryGetFileSize(string? path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return "—";
                long bytes = new FileInfo(path).Length;
                double kb = bytes / 1024.0;
                if (kb < 1024) return $"{kb:0.0} KB";
                return $"{(kb / 1024.0):0.00} MB";
            }
            catch { return "—"; }
        }

        private static string BuildAgeHint(DateTime time)
        {
            var span = DateTime.Now - time;
            if (span.TotalMinutes < 1) return "just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours} hr ago";
            if (span.TotalDays < 30) return $"{(int)span.TotalDays} days ago";
            return $"{(int)(span.TotalDays / 30)} mo ago";
        }

        private static string GetAgeColor(DateTime time)
        {
            var span = DateTime.Now - time;
            if (span.TotalDays <= 7) return "#22C55E"; // Green
            if (span.TotalDays <= 30) return "#F59E0B"; // Orange
            return "#EF4444"; // Red
        }
    }
}