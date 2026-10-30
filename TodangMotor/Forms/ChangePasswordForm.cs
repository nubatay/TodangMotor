using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using TodangMotor.Common;
using TodangMotor.Services;

namespace TodangMotor.Forms
{
    /// <summary>
    /// Small popup for the currently-logged-in user to change their password.
    /// Opens from the user chip in the dashboard header.
    /// </summary>
    public class ChangePasswordForm : ShellForm
    {
        private readonly UserService _userService = new();

        private RoundedTextBox _txtCurrent = null!;
        private RoundedTextBox _txtNew = null!;
        private RoundedTextBox _txtConfirm = null!;
        private Button _btnSave = null!;
        private Button _btnCancel = null!;
        private Label _lblStatus = null!;

        private const int PadX = 32;
        private const int LabelH = 22;
        private const int FieldH = 44;
        private const int RowGap = 16;

        public ChangePasswordForm()
        {
            HeaderTitle = "Change Password";
            ShowMaximizeButton = false;
            ShowMinimizeButton = true;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(580, 500);
            MinimumSize = new Size(560, 480);
            BackColor = Theme.Background;
            KeyPreview = true;

            BuildLayout();

            Load += (s, e) =>
            {
                Animator.SlideFadeInForm(this, 180, 12);
                _txtCurrent.FocusInput();
            };
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                }
            };
        }

        private void BuildLayout()
        {
            // ---------- Footer ----------
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 88,
                BackColor = Theme.Background
            };

            var divider = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = Theme.Divider
            };
            footer.Controls.Add(divider);

            int btnW = 140;
            int btnH = UiFactory.PrimaryButtonHeight;

            _lblStatus = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.Danger,
                AutoSize = false,
                Location = new Point(PadX, 22),
                Height = btnH,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            _btnSave = UiFactory.CreateButton("Save", UiFactory.ButtonStyle.Primary, btnW, btnH);
            _btnSave.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnSave.Click += async (s, e) => await SaveAsync();

            _btnCancel = UiFactory.CreateButton("Cancel", UiFactory.ButtonStyle.Ghost, btnW, btnH);
            _btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            footer.Controls.Add(_lblStatus);
            footer.Controls.Add(_btnSave);
            footer.Controls.Add(_btnCancel);

            footer.Resize += (s, e) =>
            {
                _btnSave.Location = new Point(footer.ClientSize.Width - PadX - btnW, 22);
                _btnCancel.Location = new Point(footer.ClientSize.Width - PadX - btnW - 8 - btnW, 22);
                _lblStatus.Width = Math.Max(100,
                    footer.ClientSize.Width - PadX - btnW * 2 - 8 - PadX - 16);
            };

            ContentPanel.Controls.Add(footer);

            // ---------- Body ----------
            int fieldW = ClientSize.Width - PadX * 2;
            int y = 28;

            // Current Password
            AddFieldLabel("Current Password", PadX, y, fieldW);
            _txtCurrent = UiFactory.CreateTextBox(fieldW, isPassword: true);
            _txtCurrent.Location = new Point(PadX, y + LabelH + 4);
            _txtCurrent.MaxLength = 100;
            _txtCurrent.ShowPasswordToggle = true;
            _txtCurrent.Placeholder = "Enter your current password";
            ContentPanel.Controls.Add(_txtCurrent);
            y += LabelH + 4 + FieldH + RowGap;

            // New Password
            AddFieldLabel("New Password", PadX, y, fieldW);
            _txtNew = UiFactory.CreateTextBox(fieldW, isPassword: true);
            _txtNew.Location = new Point(PadX, y + LabelH + 4);
            _txtNew.MaxLength = 100;
            _txtNew.ShowPasswordToggle = true;
            _txtNew.Placeholder = "Minimum 6 characters";
            ContentPanel.Controls.Add(_txtNew);

            var hint = new Label
            {
                Text = "Must be at least 6 characters. Use a mix of letters and numbers.",
                Font = Theme.FontTiny,
                ForeColor = Theme.TextMuted,
                AutoSize = false,
                Location = new Point(PadX, y + LabelH + 4 + FieldH + 2),
                Size = new Size(fieldW, 18),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            ContentPanel.Controls.Add(hint);
            y += LabelH + 4 + FieldH + 22 + RowGap;

            // Confirm New Password
            AddFieldLabel("Confirm New Password", PadX, y, fieldW);
            _txtConfirm = UiFactory.CreateTextBox(fieldW, isPassword: true);
            _txtConfirm.Location = new Point(PadX, y + LabelH + 4);
            _txtConfirm.MaxLength = 100;
            _txtConfirm.ShowPasswordToggle = true;
            _txtConfirm.Placeholder = "Re-type the new password";
            ContentPanel.Controls.Add(_txtConfirm);
        }

        private void AddFieldLabel(string text, int x, int y, int width)
        {
            var lbl = new Label
            {
                Text = text,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(x, y),
                Size = new Size(width, LabelH),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            ContentPanel.Controls.Add(lbl);
        }

        private async Task SaveAsync()
        {
            _lblStatus.Text = string.Empty;
            SetBusy(true);
            try
            {
                var (success, error) = await _userService.ChangeOwnPasswordAsync(
                    _txtCurrent.Text,
                    _txtNew.Text,
                    _txtConfirm.Text);

                if (!success)
                {
                    ShowError(error);
                    return;
                }

                MessageBox.Show(
                    "Password changed successfully.",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            _btnSave.Enabled = !busy;
            _btnCancel.Enabled = !busy;
            _txtCurrent.Enabled = !busy;
            _txtNew.Enabled = !busy;
            _txtConfirm.Enabled = !busy;
        }

        private void ShowError(string message)
        {
            _lblStatus.ForeColor = Theme.Danger;
            _lblStatus.Text = message ?? string.Empty;
        }
    }
}