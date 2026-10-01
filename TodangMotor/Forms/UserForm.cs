using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using TodangMotor.Common;
using TodangMotor.Models;
using TodangMotor.Services;

namespace TodangMotor.Forms
{
    /// <summary>
    /// Single-record User editor popup.
    /// Add mode: UserForm()
    /// Edit mode: UserForm(existingUser)
    /// List view and Deactivate/Reactivate live in UserManagementControl.
    /// </summary>
    public class UserForm : ShellForm
    {
        private readonly UserService _userService = new();

        private readonly User? _userToEdit;
        private bool IsEditMode => _userToEdit != null;

        private RoundedTextBox _txtUsername;
        private RoundedTextBox _txtFullName;
        private RoundedComboBox _cmbRole;
        private RoundedTextBox _txtPassword;
        private RoundedTextBox _txtConfirmPassword;
        private Label _lblPasswordHint;
        private Button _btnSave;
        private Button _btnCancel;
        private Label _lblStatus;

        // ============================================================
        // CONSTRUCTION
        // ============================================================

        public UserForm(User? userToEdit = null)
        {
            _userToEdit = userToEdit;

            HeaderTitle = IsEditMode ? "Edit User" : "Add User";
            ShowMaximizeButton = false;
            ShowMinimizeButton = true;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(620, 680);
            MinimumSize = new Size(580, 640);
            BackColor = Theme.Background;
            KeyPreview = true;

            BuildLayout();

            Load += UserForm_Load;
            KeyDown += UserForm_KeyDown;
        }

        // ============================================================
        // LAYOUT
        // ============================================================

        private void BuildLayout()
        {
            // ---- Footer (docked bottom) ----
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
                int rightPad = 32;
                _btnSave.Location = new Point(footer.ClientSize.Width - rightPad - btnW, 18);
                _btnCancel.Location = new Point(footer.ClientSize.Width - rightPad - btnW - 8 - btnW, 18);
            };

            // ---- Content area ----
            const int padX = 32;
            int fieldW = ClientSize.Width - padX * 2;

            int y = 26;
            const int rowStep = 78;

            // ---- Username ----
            AddFieldLabel("Username:", padX, y);
            _txtUsername = UiFactory.CreateTextBox(fieldW);
            _txtUsername.Location = new Point(padX, y + 22);
            _txtUsername.MaxLength = 50;
            _txtUsername.Placeholder = "letters, digits, _ and . only";
            ContentPanel.Controls.Add(_txtUsername);
            y += rowStep;

            // ---- Full Name ----
            AddFieldLabel("Full Name:", padX, y);
            _txtFullName = UiFactory.CreateTextBox(fieldW);
            _txtFullName.Location = new Point(padX, y + 22);
            _txtFullName.MaxLength = 100;
            _txtFullName.Placeholder = "e.g. Juan Dela Cruz";
            ContentPanel.Controls.Add(_txtFullName);
            y += rowStep;

            // ---- Role ----
            AddFieldLabel("Role:", padX, y);
            _cmbRole = new RoundedComboBox
            {
                Width = fieldW,
                Location = new Point(padX, y + 22)
            };
            _cmbRole.Items.Add("Cashier");
            _cmbRole.Items.Add("Owner");
            _cmbRole.SelectedIndex = 0;
            ContentPanel.Controls.Add(_cmbRole);
            y += rowStep;

            // ---- Password ----
            AddFieldLabel("Password:", padX, y);
            _txtPassword = UiFactory.CreateTextBox(fieldW, isPassword: true);
            _txtPassword.Location = new Point(padX, y + 22);
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
                Location = new Point(padX, y + 68),
                Size = new Size(fieldW, 18),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            ContentPanel.Controls.Add(_lblPasswordHint);
            y += rowStep + 18;

            // ---- Confirm Password ----
            AddFieldLabel("Confirm Password:", padX, y);
            _txtConfirmPassword = UiFactory.CreateTextBox(fieldW, isPassword: true);
            _txtConfirmPassword.Location = new Point(padX, y + 22);
            _txtConfirmPassword.MaxLength = 100;
            _txtConfirmPassword.ShowPasswordToggle = true;
            ContentPanel.Controls.Add(_txtConfirmPassword);
            y += rowStep;

            // ---- Status ----
            _lblStatus = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.Danger,
                AutoSize = false,
                Location = new Point(padX, y),
                Size = new Size(fieldW, 60),
                TextAlign = ContentAlignment.TopLeft,
                BackColor = Color.Transparent
            };
            ContentPanel.Controls.Add(_lblStatus);

            // Footer added last so it docks first and wins the bottom edge.
            ContentPanel.Controls.Add(footer);
        }

        private void AddFieldLabel(string text, int x, int y)
        {
            var lbl = new Label
            {
                Text = text,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(400, 18),
                Location = new Point(x, y),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            ContentPanel.Controls.Add(lbl);
        }

        // ============================================================
        // LOAD
        // ============================================================

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

        // ============================================================
        // SAVE
        // ============================================================

        private async Task SaveAsync()
        {
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

        // ============================================================
        // EVENTS
        // ============================================================

        private void UserForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }

        // ============================================================
        // HELPERS
        // ============================================================

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