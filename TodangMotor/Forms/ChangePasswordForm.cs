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

        private RoundedTextBox _txtCurrent;
        private RoundedTextBox _txtNew;
        private RoundedTextBox _txtConfirm;
        private Button _btnSave;
        private Button _btnCancel;
        private Label _lblStatus;

        public ChangePasswordForm()
        {
            HeaderTitle = "Change Password";
            ShowMaximizeButton = false;
            ShowMinimizeButton = true;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(520, 460);
            MinimumSize = new Size(500, 440);
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
            const int padX = 32;
            int fieldW = ClientSize.Width - padX * 2;

            // Footer
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 76,
                BackColor = Theme.Background
            };

            const int btnW = 120;
            const int btnH = 42;

            _btnSave = UiFactory.CreateButton("Save", UiFactory.ButtonStyle.Primary, btnW, btnH);
            _btnSave.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnSave.Click += async (s, e) => await SaveAsync();

            _btnCancel = UiFactory.CreateButton("Cancel", UiFactory.ButtonStyle.Ghost, btnW, btnH);
            _btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            footer.Controls.Add(_btnSave);
            footer.Controls.Add(_btnCancel);

            footer.Resize += (s, e) =>
            {
                _btnSave.Location = new Point(footer.ClientSize.Width - padX - btnW, 16);
                _btnCancel.Location = new Point(footer.ClientSize.Width - padX - btnW - 10 - btnW, 16);
            };

            ContentPanel.Controls.Add(footer);

            // Fields
            int y = 24;

            AddLabel("Current Password:", padX, y);
            _txtCurrent = UiFactory.CreateTextBox(fieldW, isPassword: true);
            _txtCurrent.Location = new Point(padX, y + 22);
            _txtCurrent.MaxLength = 100;
            _txtCurrent.ShowPasswordToggle = true;
            _txtCurrent.Placeholder = "Enter your current password";
            ContentPanel.Controls.Add(_txtCurrent);
            y += 76;

            AddLabel("New Password:", padX, y);
            _txtNew = UiFactory.CreateTextBox(fieldW, isPassword: true);
            _txtNew.Location = new Point(padX, y + 22);
            _txtNew.MaxLength = 100;
            _txtNew.ShowPasswordToggle = true;
            _txtNew.Placeholder = "Min 6 characters";
            ContentPanel.Controls.Add(_txtNew);
            y += 76;

            AddLabel("Confirm New Password:", padX, y);
            _txtConfirm = UiFactory.CreateTextBox(fieldW, isPassword: true);
            _txtConfirm.Location = new Point(padX, y + 22);
            _txtConfirm.MaxLength = 100;
            _txtConfirm.ShowPasswordToggle = true;
            _txtConfirm.Placeholder = "Re-type the new password";
            ContentPanel.Controls.Add(_txtConfirm);
            y += 76;

            _lblStatus = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.Danger,
                AutoSize = false,
                Location = new Point(padX, y),
                Size = new Size(fieldW, 40),
                TextAlign = ContentAlignment.TopLeft,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            ContentPanel.Controls.Add(_lblStatus);
        }

        private void AddLabel(string text, int x, int y)
        {
            var lbl = new Label
            {
                Text = text,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(300, 18),
                Location = new Point(x, y),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            ContentPanel.Controls.Add(lbl);
        }

        private async Task SaveAsync()
        {
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