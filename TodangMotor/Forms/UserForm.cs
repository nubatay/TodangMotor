using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using TodangMotor.Common;
using TodangMotor.Models;
using TodangMotor.Services;

namespace TodangMotor.Forms
{
    public class UserForm : ShellForm
    {
        private readonly UserService _userService = new();

        private readonly User? _userToEdit;
        private bool IsEditMode => _userToEdit != null;

        private RoundedTextBox _txtUsername = null!;
        private RoundedTextBox _txtFullName = null!;
        private RoundedComboBox _cmbRole = null!;
        private RoundedTextBox _txtPassword = null!;
        private RoundedTextBox _txtConfirmPassword = null!;
        private Label _lblPasswordHint = null!;
        private Button _btnSave = null!;
        private Button _btnCancel = null!;
        private Label _lblStatus = null!;

        private const int PadX = 32;
        private const int LabelH = 22;
        private const int FieldH = 44;
        private const int RowGap = 16;

        public UserForm(User? userToEdit = null)
        {
            _userToEdit = userToEdit;

            HeaderTitle = IsEditMode ? "Edit User" : "Add User";
            ShowMaximizeButton = false;
            ShowMinimizeButton = true;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(700, 700);
            MinimumSize = new Size(660, 680);
            BackColor = Theme.Background;
            KeyPreview = true;

            BuildLayout();

            Load += UserForm_Load;
            KeyDown += UserForm_KeyDown;
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

            // Username
            AddFieldLabel("Username", PadX, y, fieldW);
            _txtUsername = UiFactory.CreateTextBox(fieldW);
            _txtUsername.Location = new Point(PadX, y + LabelH + 4);
            _txtUsername.MaxLength = 50;
            _txtUsername.Placeholder = "letters, digits, _ and . only";
            ContentPanel.Controls.Add(_txtUsername);
            y += LabelH + 4 + FieldH + RowGap;

            // Full Name
            AddFieldLabel("Full Name", PadX, y, fieldW);
            _txtFullName = UiFactory.CreateTextBox(fieldW);
            _txtFullName.Location = new Point(PadX, y + LabelH + 4);
            _txtFullName.MaxLength = 100;
            _txtFullName.Placeholder = "e.g. Juan Dela Cruz";
            ContentPanel.Controls.Add(_txtFullName);
            y += LabelH + 4 + FieldH + RowGap;

            // Role
            AddFieldLabel("Role", PadX, y, fieldW);
            _cmbRole = new RoundedComboBox
            {
                Width = fieldW,
                Location = new Point(PadX, y + LabelH + 4)
            };
            _cmbRole.Items.Add("Cashier");
            _cmbRole.Items.Add("Owner");
            _cmbRole.SelectedIndex = 0;
            ContentPanel.Controls.Add(_cmbRole);
            y += LabelH + 4 + FieldH + RowGap;

            // Password
            AddFieldLabel("Password", PadX, y, fieldW);
            _txtPassword = UiFactory.CreateTextBox(fieldW, isPassword: true);
            _txtPassword.Location = new Point(PadX, y + LabelH + 4);
            _txtPassword.MaxLength = 100;
            _txtPassword.ShowPasswordToggle = true;
            ContentPanel.Controls.Add(_txtPassword);

            _lblPasswordHint = new Label
            {
                Text = IsEditMode
                    ? "Leave blank to keep the current password. Min 6 characters if changing."
                    : "Minimum 6 characters.",
                Font = Theme.FontTiny,
                ForeColor = Theme.TextMuted,
                AutoSize = false,
                Location = new Point(PadX, y + LabelH + 4 + FieldH + 2),
                Size = new Size(fieldW, 18),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            ContentPanel.Controls.Add(_lblPasswordHint);
            y += LabelH + 4 + FieldH + 22 + RowGap;

            // Confirm Password
            AddFieldLabel("Confirm Password", PadX, y, fieldW);
            _txtConfirmPassword = UiFactory.CreateTextBox(fieldW, isPassword: true);
            _txtConfirmPassword.Location = new Point(PadX, y + LabelH + 4);
            _txtConfirmPassword.MaxLength = 100;
            _txtConfirmPassword.ShowPasswordToggle = true;
            ContentPanel.Controls.Add(_txtConfirmPassword);
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

        private void UserForm_Load(object? sender, EventArgs e)
        {
            Animator.SlideFadeInForm(this, 180, 12);

            if (IsEditMode)
            {
                var u = _userToEdit!;
                _txtUsername.Text = u.Username ?? string.Empty;
                _txtFullName.Text = u.FullName ?? string.Empty;

                int idx = _cmbRole.Items.IndexOf(u.Role);
                if (idx >= 0) _cmbRole.SelectedIndex = idx;

                _txtUsername.FocusInput();
                _txtUsername.SelectAll();
            }
            else
            {
                _txtUsername.FocusInput();
            }
        }

        private async Task SaveAsync()
        {
            _lblStatus.Text = string.Empty;

            string pw = _txtPassword.Text ?? string.Empty;
            string confirm = _txtConfirmPassword.Text ?? string.Empty;

            if (pw.Length > 0 && pw != confirm)
            {
                ShowError("Password and Confirm Password do not match.");
                _txtConfirmPassword.FocusInput();
                return;
            }

            string role = (_cmbRole.SelectedItem as string) ?? "Cashier";

            SetBusy(true);
            try
            {
                (bool Success, string ErrorMessage) result;

                if (IsEditMode)
                {
                    result = await _userService.UpdateAsync(
                        _userToEdit!.UserId,
                        _txtUsername.Text,
                        _txtFullName.Text,
                        role,
                        _txtPassword.Text);
                }
                else
                {
                    result = await _userService.AddAsync(
                        _txtUsername.Text,
                        _txtFullName.Text,
                        role,
                        _txtPassword.Text);
                }

                if (result.Success)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    ShowError(result.ErrorMessage);
                }
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void UserForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }

        private void SetBusy(bool busy)
        {
            _btnSave.Enabled = !busy;
            _btnCancel.Enabled = !busy;
            _txtUsername.Enabled = !busy;
            _txtFullName.Enabled = !busy;
            _cmbRole.Enabled = !busy;
            _txtPassword.Enabled = !busy;
            _txtConfirmPassword.Enabled = !busy;
        }

        private void ShowError(string message)
        {
            _lblStatus.ForeColor = Theme.Danger;
            _lblStatus.Text = message ?? string.Empty;
        }
    }
}