using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using TodangMotor.Common;
using TodangMotor.Services;

namespace TodangMotor.Controls
{
    /// <summary>
    /// Owner-only Settings screen. Hosts the Backup and Restore feature:
    /// two large action tiles plus a status area showing the last backup.
    /// </summary>
    public class SettingsControl : UserControl
    {
        private readonly BackupRestoreService _backupService = new();

        private RoundedPanel _backupTile;
        private RoundedPanel _restoreTile;
        private Label _lastBackupTimeLabel;
        private Label _lastBackupPathLabel;
        private Button _btnOpenFolder;
        private Label _statusLabel;
        private bool _isBusy;

        public SettingsControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(Theme.SpacingLg);

            BuildLayout();

            Load += (s, e) => RefreshLastBackupInfo();
        }

        // ============================================================
        // LAYOUT
        // ============================================================

        private void BuildLayout()
        {
            // Root: 3 rows — header (fixed), tiles (fill), bottom (fixed).
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Theme.Background,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));

            root.Controls.Add(BuildHeaderBar(), 0, 0);
            root.Controls.Add(BuildTilesArea(), 0, 1);
            root.Controls.Add(BuildBottomPanel(), 0, 2);

            Controls.Add(root);
        }

        private Panel BuildHeaderBar()
        {
            var bar = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Background
            };

            var title = new Label
            {
                Text = "BACKUP AND RESTORE",
                Font = new Font(Theme.UiFontFamily, 10F, FontStyle.Bold),
                ForeColor = Theme.Primary,
                AutoSize = false,
                Location = new Point(0, 4),
                Size = new Size(800, 22),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            var subtitle = new Label
            {
                Text = "Protect your shop's data. Back up regularly, restore when needed.",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(0, 28),
                Size = new Size(900, 20),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            bar.Controls.Add(title);
            bar.Controls.Add(subtitle);

            return bar;
        }

        private Panel BuildTilesArea()
        {
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Theme.Background,
                Margin = new Padding(0, 4, 0, 4),
                Padding = Padding.Empty
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _backupTile = BuildActionTile(
                icon: "\uE74E",
                title: "Backup",
                body: "Save a complete copy of the current database to a .bak file. " +
                          "Choose a USB drive or any folder for extra safety.",
                buttonTx: "Backup Now",
                buttonStyle: UiFactory.ButtonStyle.Primary,
                onClick: async (s, e) => await DoBackupAsync());

            _restoreTile = BuildActionTile(
                icon: "\uE777",
                title: "Restore",
                body: "Replace all current data with a previously saved backup. " +
                          "The app will close after restore so you can reopen it with the recovered data.",
                buttonTx: "Restore Now",
                buttonStyle: UiFactory.ButtonStyle.Secondary,
                onClick: async (s, e) => await DoRestoreAsync());

            _backupTile.Margin = new Padding(0, 0, Theme.SpacingSm, 0);
            _restoreTile.Margin = new Padding(Theme.SpacingSm, 0, 0, 0);

            grid.Controls.Add(_backupTile, 0, 0);
            grid.Controls.Add(_restoreTile, 1, 0);

            return grid;
        }

        private RoundedPanel BuildActionTile(
            string icon,
            string title,
            string body,
            string buttonTx,
            UiFactory.ButtonStyle buttonStyle,
            EventHandler onClick)
        {
            var tile = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Radius = Theme.RadiusCard,
                BorderColor = Theme.Divider,
                BorderSize = 1,
                BackColor = Theme.Surface,
                Padding = new Padding(28)
            };

            var iconLabel = new Label
            {
                Text = icon,
                Font = new Font(Theme.IconFontFamily, 52F, FontStyle.Regular),
                ForeColor = Theme.Primary,
                AutoSize = false,
                Size = new Size(120, 120),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            var titleLabel = new Label
            {
                Text = title,
                Font = new Font(Theme.UiFontFamily, 22F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Size = new Size(320, 40),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            var bodyLabel = new Label
            {
                Text = body,
                Font = Theme.FontBody,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(340, 80),
                TextAlign = ContentAlignment.TopCenter,
                BackColor = Color.Transparent
            };

            var btn = UiFactory.CreateButton(buttonTx, buttonStyle, 200, 46);
            btn.Click += onClick;

            tile.Controls.Add(iconLabel);
            tile.Controls.Add(titleLabel);
            tile.Controls.Add(bodyLabel);
            tile.Controls.Add(btn);

            tile.Resize += (s, e) => CenterTileContent(tile, iconLabel, titleLabel, bodyLabel, btn);
            CenterTileContent(tile, iconLabel, titleLabel, bodyLabel, btn);

            return tile;
        }

        private void CenterTileContent(
            RoundedPanel tile,
            Control icon,
            Control title,
            Control body,
            Control button)
        {
            if (tile.Width <= 0 || tile.Height <= 0) return;

            int cx = tile.ClientSize.Width / 2;

            const int iconH = 120;
            const int titleH = 40;
            const int bodyH = 80;
            const int buttonH = 46;
            const int gap = 14;

            int totalH = iconH + titleH + bodyH + buttonH + gap * 3;
            int startY = Math.Max(24, (tile.ClientSize.Height - totalH) / 2);

            icon.Location = new Point(cx - icon.Width / 2, startY);
            title.Location = new Point(cx - title.Width / 2, startY + iconH + gap);
            body.Location = new Point(cx - body.Width / 2, startY + iconH + titleH + gap * 2);
            button.Location = new Point(cx - button.Width / 2, startY + iconH + titleH + bodyH + gap * 3);
        }

        private Panel BuildBottomPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Background
            };

            var divider = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = Theme.Divider
            };
            panel.Controls.Add(divider);

            _lastBackupTimeLabel = new Label
            {
                Text = "Last backup:  —",
                Font = Theme.FontBodyBold,
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(0, 16),
                Size = new Size(900, 22),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            panel.Controls.Add(_lastBackupTimeLabel);

            _lastBackupPathLabel = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(0, 40),
                Size = new Size(900, 18),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };
            panel.Controls.Add(_lastBackupPathLabel);

            _btnOpenFolder = UiFactory.CreateButton(
                "Open Backup Folder",
                UiFactory.ButtonStyle.Secondary,
                200, 38);
            _btnOpenFolder.Location = new Point(0, 66);
            _btnOpenFolder.Click += (s, e) => OpenBackupFolder();
            panel.Controls.Add(_btnOpenFolder);

            _statusLabel = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(0, 116),
                Size = new Size(900, 22),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            panel.Controls.Add(_statusLabel);

            // Keep labels stretched to full width on resize.
            panel.Resize += (s, e) =>
            {
                int w = panel.ClientSize.Width;
                _lastBackupTimeLabel.Width = Math.Max(200, w);
                _lastBackupPathLabel.Width = Math.Max(200, w);
                _statusLabel.Width = Math.Max(200, w);
            };

            return panel;
        }

        // ============================================================
        // BACKUP
        // ============================================================

        private async System.Threading.Tasks.Task DoBackupAsync()
        {
            if (_isBusy) return;

            try
            {
                Directory.CreateDirectory(BackupRestoreService.DefaultBackupFolder);
            }
            catch { /* ignore */ }

            using var sfd = new SaveFileDialog
            {
                Title = "Save Database Backup",
                Filter = "Backup files (*.bak)|*.bak|All files (*.*)|*.*",
                DefaultExt = "bak",
                AddExtension = true,
                FileName = BackupRestoreService.SuggestBackupFileName(),
                InitialDirectory = BackupRestoreService.DefaultBackupFolder,
                OverwritePrompt = true
            };

            if (sfd.ShowDialog(FindForm()) != DialogResult.OK) return;

            SetBusy(true);
            try
            {
                var (success, error) = await _backupService.BackupAsync(sfd.FileName);

                if (!success)
                {
                    MessageBox.Show(
                        error ?? "Backup failed.",
                        "Backup Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    ShowStatus("Backup failed.", isError: true);
                    return;
                }

                RefreshLastBackupInfo();

                MessageBox.Show(
                    $"Backup completed successfully.\n\n" +
                    $"Saved to:\n{sfd.FileName}",
                    "Backup Successful",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                ShowStatus("Backup completed.", isError: false);
            }
            finally
            {
                SetBusy(false);
            }
        }

        // ============================================================
        // RESTORE
        // ============================================================

        private async System.Threading.Tasks.Task DoRestoreAsync()
        {
            if (_isBusy) return;

            var warn1 = MessageBox.Show(
                "WARNING\n\n" +
                "Restoring will REPLACE all current data — products, sales, " +
                "inventory, and users — with the contents of the backup file.\n\n" +
                "Make sure you have a fresh backup of the current data first.\n\n" +
                "Continue?",
                "Confirm Restore",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (warn1 != DialogResult.Yes) return;

            using var ofd = new OpenFileDialog
            {
                Title = "Select a Backup File",
                Filter = "Backup files (*.bak)|*.bak|All files (*.*)|*.*",
                InitialDirectory = BackupRestoreService.DefaultBackupFolder,
                CheckFileExists = true
            };

            if (ofd.ShowDialog(FindForm()) != DialogResult.OK) return;

            var warn2 = MessageBox.Show(
                $"Restore from:\n\n{ofd.FileName}\n\n" +
                "After restore, the application will close. " +
                "Reopen it to use the restored data.\n\n" +
                "Proceed?",
                "Final Confirmation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (warn2 != DialogResult.Yes) return;

            SetBusy(true);
            try
            {
                var (success, error) = await _backupService.RestoreAsync(ofd.FileName);

                if (!success)
                {
                    MessageBox.Show(
                        error ?? "Restore failed.",
                        "Restore Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    ShowStatus("Restore failed.", isError: true);
                    return;
                }

                MessageBox.Show(
                    "Restore completed successfully.\n\n" +
                    "The application will now close. Please reopen it to use " +
                    "the restored data.",
                    "Restore Successful",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                Application.Exit();
            }
            finally
            {
                SetBusy(false);
            }
        }

        // ============================================================
        // INFO ROW
        // ============================================================

        private void RefreshLastBackupInfo()
        {
            if (_lastBackupTimeLabel == null) return;

            var (time, path) = BackupRestoreService.GetLastBackupInfo();

            if (time == null)
            {
                _lastBackupTimeLabel.Text = "No backup has been made yet.";
                _lastBackupTimeLabel.ForeColor = Theme.TextMuted;
                _lastBackupPathLabel.Text = string.Empty;
                return;
            }

            _lastBackupTimeLabel.Text =
                $"Last backup:  {time.Value:MMMM d, yyyy  h:mm tt}";
            _lastBackupTimeLabel.ForeColor = Theme.TextPrimary;

            _lastBackupPathLabel.Text = string.IsNullOrEmpty(path)
                ? string.Empty
                : $"Location:  {path}";
        }

        private void OpenBackupFolder()
        {
            try
            {
                string folder = BackupRestoreService.DefaultBackupFolder;

                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                Process.Start("explorer.exe", folder);
            }
            catch (Exception ex)
            {
                ShowStatus($"Could not open backup folder: {ex.Message}", isError: true);
            }
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private void SetBusy(bool busy)
        {
            _isBusy = busy;

            foreach (Control c in _backupTile.Controls)
                c.Enabled = !busy;
            foreach (Control c in _restoreTile.Controls)
                c.Enabled = !busy;

            _btnOpenFolder.Enabled = !busy;
        }

        private void ShowStatus(string message, bool isError)
        {
            if (_statusLabel == null) return;
            _statusLabel.ForeColor = isError ? Theme.Danger : Theme.Success;
            _statusLabel.Text = message ?? string.Empty;
        }
    }
}