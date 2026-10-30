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
    /// Owner-only Settings screen. Compact action cards for Backup / Restore
    /// plus a compact info card showing the last backup at a glance.
    /// </summary>
    public class SettingsControl : UserControl
    {
        private readonly BackupRestoreService _backupService = new();

        private Button _btnBackup = null!;
        private Button _btnRestore = null!;

        private Label _valDate = null!;
        private Label _valSize = null!;
        private Label _valAge = null!;
        private Label _valLocation = null!;

        private Button _btnOpenFolder = null!;
        private Label _statusLabel = null!;
        private bool _isBusy;

        public SettingsControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(UiFactory.PagePadding);

            BuildLayout();

            Load += (s, e) => RefreshLastBackupInfo();
        }

        // ============================================================
        // LAYOUT
        // ============================================================

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                BackColor = Theme.Background,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));   // header
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 200));  // action cards
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 240));  // info card
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // empty filler
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));   // status

            root.Controls.Add(BuildHeaderBar(), 0, 0);
            root.Controls.Add(BuildActionsRow(), 0, 1);
            root.Controls.Add(BuildInfoCard(), 0, 2);
            root.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background }, 0, 3);
            root.Controls.Add(BuildStatusBar(), 0, 4);

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
                Location = new Point(0, 8),
                Height = 24,
                Width = 800,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            var subtitle = new Label
            {
                Text = "Protect your shop's data. Back up regularly, restore when needed.",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(0, 34),
                Height = 22,
                Width = 900,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            bar.Controls.Add(title);
            bar.Controls.Add(subtitle);
            return bar;
        }

        private TableLayoutPanel BuildActionsRow()
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Theme.Background,
                Margin = Padding.Empty,
                Padding = new Padding(0, 8, 0, 16)
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var backupCard = BuildActionCard(
                icon: "\uE74E",
                title: "Backup",
                body: "Save a complete copy of the current database to a .bak file.",
                buttonTx: "Backup Now",
                buttonStyle: UiFactory.ButtonStyle.Primary,
                onClick: async (s, e) => await DoBackupAsync(),
                out _btnBackup);

            var restoreCard = BuildActionCard(
                icon: "\uE777",
                title: "Restore",
                body: "Replace all current data with a previously saved backup.",
                buttonTx: "Restore Now",
                buttonStyle: UiFactory.ButtonStyle.Secondary,
                onClick: async (s, e) => await DoRestoreAsync(),
                out _btnRestore);

            backupCard.Margin = new Padding(0, 0, 8, 0);
            restoreCard.Margin = new Padding(8, 0, 0, 0);

            row.Controls.Add(backupCard, 0, 0);
            row.Controls.Add(restoreCard, 1, 0);
            return row;
        }

        private RoundedPanel BuildActionCard(
    string icon,
    string title,
    string body,
    string buttonTx,
    UiFactory.ButtonStyle buttonStyle,
    EventHandler onClick,
    out Button button)
        {
            var card = UiFactory.CreateCard();
            card.Dock = DockStyle.Fill;

            // ---- Bottom bar (button) docked so it never overlaps the content ----
            var bottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 56,
                BackColor = Color.Transparent
            };

            var btn = UiFactory.CreateButton(buttonTx, buttonStyle, 140, UiFactory.PrimaryButtonHeight);
            btn.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btn.Click += onClick;
            bottomBar.Controls.Add(btn);

            bottomBar.Resize += (s, e) =>
            {
                btn.Location = new Point(
                    bottomBar.ClientSize.Width - btn.Width,
                    bottomBar.ClientSize.Height - btn.Height);
            };

            // ---- Content area above the bottom bar ----
            var content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };

            // 44px tall row holds both the icon and the title so their
            // vertical centers line up perfectly.
            const int headerRowH = 44;

            var iconLabel = new Label
            {
                Text = icon,
                Font = new Font(Theme.IconFontFamily, 24F, FontStyle.Regular),
                ForeColor = Theme.Primary,
                AutoSize = false,
                Size = new Size(56, headerRowH),
                Location = new Point(0, 4),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            var titleLabel = new Label
            {
                Text = title,
                Font = new Font(Theme.UiFontFamily, 16F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(72, 4),
                Height = headerRowH,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var bodyLabel = new Label
            {
                Text = body,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(72, 52),
                Height = 44,
                TextAlign = ContentAlignment.TopLeft,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            content.Controls.Add(iconLabel);
            content.Controls.Add(titleLabel);
            content.Controls.Add(bodyLabel);

            content.Resize += (s, e) =>
            {
                int w = Math.Max(100, content.ClientSize.Width - 72);
                titleLabel.Width = w;
                bodyLabel.Width = w;
            };

            card.Controls.Add(content);
            card.Controls.Add(bottomBar);

            button = btn;
            return card;
        }

        private RoundedPanel BuildInfoCard()
        {
            var card = UiFactory.CreateCard();
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(0);

            // ---- Top row: title + "Open Backup Folder" button ----
            var topRow = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = Color.Transparent
            };

            var titleLabel = new Label
            {
                Text = "Last Backup",
                Font = new Font(Theme.UiFontFamily, 13F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(0, 12),
                Height = 32,
                Width = 300,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _btnOpenFolder = UiFactory.CreateButton(
                "Open Backup Folder",
                UiFactory.ButtonStyle.Secondary,
                190,
                UiFactory.SecondaryButtonHeight);
            _btnOpenFolder.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnOpenFolder.Click += (s, e) => OpenBackupFolder();

            topRow.Controls.Add(titleLabel);
            topRow.Controls.Add(_btnOpenFolder);

            topRow.Resize += (s, e) =>
            {
                _btnOpenFolder.Location = new Point(
                    topRow.ClientSize.Width - _btnOpenFolder.Width,
                    (topRow.ClientSize.Height - _btnOpenFolder.Height) / 2);
            };

            // ---- Stat row: Date | Size | Age ----
            var statRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 90,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            statRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            statRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            statRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            statRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            statRow.Controls.Add(BuildStatBlock("Date", out _valDate), 0, 0);
            statRow.Controls.Add(BuildStatBlock("Size", out _valSize), 1, 0);
            statRow.Controls.Add(BuildStatBlock("Age", out _valAge), 2, 0);

            // ---- Location row ----
            var locRow = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = Color.Transparent
            };

            var lblLocKey = new Label
            {
                Text = "Location",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(0, 0),
                Height = 20,
                Width = 200,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _valLocation = new Label
            {
                Text = "—",
                Font = Theme.FontBody,
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(0, 22),
                Height = 24,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                AutoEllipsis = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            locRow.Controls.Add(lblLocKey);
            locRow.Controls.Add(_valLocation);

            locRow.Resize += (s, e) =>
            {
                _valLocation.Width = locRow.ClientSize.Width;
            };

            // Add order: bottom-most row first
            card.Controls.Add(locRow);
            card.Controls.Add(statRow);
            card.Controls.Add(topRow);

            return card;
        }

        private Panel BuildStatBlock(string labelText, out Label valueLabel)
        {
            var block = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 0, 16, 0)
            };

            var label = new Label
            {
                Text = labelText,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(0, 0),
                Height = 20,
                Width = 200,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            var value = new Label
            {
                Text = "—",
                Font = new Font(Theme.UiFontFamily, 14F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(0, 24),
                Height = 32,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                AutoEllipsis = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            valueLabel = value;

            block.Controls.Add(label);
            block.Controls.Add(value);

            block.Resize += (s, e) =>
            {
                value.Width = Math.Max(80, block.ClientSize.Width - 16);
                label.Width = value.Width;
            };

            return block;
        }

        private Panel BuildStatusBar()
        {
            var bar = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Background
            };

            _statusLabel = new Label
            {
                Text = "Ready.",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            bar.Controls.Add(_statusLabel);
            return bar;
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
                    $"Backup completed successfully.\n\nSaved to:\n{sfd.FileName}",
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
        // LAST BACKUP INFO
        // ============================================================

        private void RefreshLastBackupInfo()
        {
            var (time, path) = BackupRestoreService.GetLastBackupInfo();

            if (time == null)
            {
                _valDate.Text = "No backup yet";
                _valDate.ForeColor = Theme.TextMuted;
                _valSize.Text = "—";
                _valSize.ForeColor = Theme.TextMuted;
                _valAge.Text = "—";
                _valAge.ForeColor = Theme.TextMuted;
                _valLocation.Text = "—";
                _valLocation.ForeColor = Theme.TextMuted;
                return;
            }

            _valDate.Text = time.Value.ToString("MMM d, yyyy  h:mm tt");
            _valDate.ForeColor = Theme.TextPrimary;

            _valSize.Text = TryGetFileSize(path);
            _valSize.ForeColor = Theme.TextPrimary;

            _valAge.Text = BuildAgeHint(time.Value);
            _valAge.ForeColor = GetAgeColor(time.Value);

            _valLocation.Text = string.IsNullOrEmpty(path) ? "—" : path;
            _valLocation.ForeColor = Theme.TextPrimary;
        }

        private static string TryGetFileSize(string? path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) return "—";
                if (!File.Exists(path)) return "File not found";

                long bytes = new FileInfo(path).Length;
                double kb = bytes / 1024.0;
                if (kb < 1024) return $"{kb:0.0} KB";

                double mb = kb / 1024.0;
                return $"{mb:0.00} MB";
            }
            catch
            {
                return "—";
            }
        }

        private static string BuildAgeHint(DateTime time)
        {
            var span = DateTime.Now - time;
            if (span.TotalMinutes < 1) return "just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours} hr ago";
            if (span.TotalDays < 30) return $"{(int)span.TotalDays} days ago";

            int months = (int)(span.TotalDays / 30);
            return $"{months} mo ago";
        }

        private static Color GetAgeColor(DateTime time)
        {
            var span = DateTime.Now - time;
            if (span.TotalDays <= 7) return Theme.Success;
            if (span.TotalDays <= 30) return Color.FromArgb(200, 130, 40);
            return Theme.Danger;
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
            _btnBackup.Enabled = !busy;
            _btnRestore.Enabled = !busy;
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