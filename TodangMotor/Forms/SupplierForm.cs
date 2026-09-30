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
    /// Single-record Supplier editor popup.
    /// Add mode: SupplierForm()
    /// Edit mode: SupplierForm(existingSupplier)
    /// </summary>
    public class SupplierForm : ShellForm
    {
        private readonly SupplierService _supplierService = new();

        private readonly Supplier? _supplierToEdit;
        private bool IsEditMode => _supplierToEdit != null;

        private RoundedTextBox _txtSupplierName;
        private RoundedTextBox _txtContactNumber;
        private RoundedTextBox _txtAddress;
        private Button _btnSave;
        private Button _btnCancel;
        private Label _lblStatus;

        // ============================================================
        // CONSTRUCTION
        // ============================================================

        public SupplierForm(Supplier? supplierToEdit = null)
        {
            _supplierToEdit = supplierToEdit;

            HeaderTitle = IsEditMode ? "Edit Supplier" : "Add Supplier";
            ShowMaximizeButton = false;
            ShowMinimizeButton = true;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(560, 420);
            MinimumSize = new Size(540, 400);
            BackColor = Theme.Background;
            KeyPreview = true;

            BuildLayout();

            Load += SupplierForm_Load;
            KeyDown += SupplierForm_KeyDown;
        }

        // ============================================================
        // LAYOUT
        // ============================================================

        private void BuildLayout()
        {
            // ---- Footer panel (docked bottom, holds buttons) ----
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 68,
                BackColor = Theme.Background,
                Padding = new Padding(0)
            };

            const int btnW = 110;
            const int btnH = 40;

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
                _btnSave.Location = new Point(
                    footer.ClientSize.Width - rightPad - btnW, 14);
                _btnCancel.Location = new Point(
                    footer.ClientSize.Width - rightPad - btnW - 8 - btnW, 14);
            };

            // ---- Content area above the footer ----
            const int padX = 32;
            int fieldW = ClientSize.Width - padX * 2;

            int y = 24;

            // ---- Supplier Name ----
            AddLabel("Supplier Name:", padX, y);
            _txtSupplierName = UiFactory.CreateTextBox(fieldW);
            _txtSupplierName.Location = new Point(padX, y + 22);
            _txtSupplierName.MaxLength = 150;
            ContentPanel.Controls.Add(_txtSupplierName);
            y += 76;

            // ---- Contact Number ----
            AddLabel("Contact Number:", padX, y);
            _txtContactNumber = UiFactory.CreateTextBox(fieldW);
            _txtContactNumber.Location = new Point(padX, y + 22);
            _txtContactNumber.MaxLength = 15;
            ContentPanel.Controls.Add(_txtContactNumber);

            // Hint label below the field. Height 20 gives 8pt text room for
            // its descenders (g, p, y) and a little padding.
            var hint = new Label
            {
                Text = "Format: 09XXXXXXXXX (11 digits, e.g. 09171234567)",
                Font = Theme.FontTiny,
                ForeColor = Theme.TextMuted,
                AutoSize = false,
                Location = new Point(padX, y + 68),
                Size = new Size(fieldW, 20),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            ContentPanel.Controls.Add(hint);
            y += 96; // field (64) + hint (20) + breathing room (12)

            // ---- Address (optional) ----
            AddLabel("Address (optional):", padX, y);
            _txtAddress = UiFactory.CreateTextBox(fieldW);
            _txtAddress.Location = new Point(padX, y + 22);
            _txtAddress.MaxLength = 250;
            ContentPanel.Controls.Add(_txtAddress);
            y += 76;

            // ---- Status ----
            _lblStatus = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.Danger,
                AutoSize = false,
                Location = new Point(padX, y),
                Size = new Size(fieldW, 40),
                TextAlign = ContentAlignment.TopLeft,
                BackColor = Color.Transparent
            };
            ContentPanel.Controls.Add(_lblStatus);

            // Add footer LAST so it docks first and always wins the bottom edge.
            ContentPanel.Controls.Add(footer);
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

        // ============================================================
        // LOAD
        // ============================================================

        private void SupplierForm_Load(object? sender, EventArgs e)
        {
            Animator.SlideFadeInForm(this, 180, 12);

            if (IsEditMode)
            {
                var s = _supplierToEdit!;
                _txtSupplierName.Text = s.SupplierName ?? string.Empty;
                _txtContactNumber.Text = s.ContactNumber ?? string.Empty;
                _txtAddress.Text = s.Address ?? string.Empty;
                _txtSupplierName.FocusInput();
                _txtSupplierName.SelectAll();
            }
            else
            {
                _txtSupplierName.FocusInput();
            }
        }

        // ============================================================
        // SAVE
        // ============================================================

        private async Task SaveAsync()
        {
            SetBusy(true);
            try
            {
                (bool Success, string ErrorMessage) result;

                if (IsEditMode)
                {
                    result = await _supplierService.UpdateAsync(
                        _supplierToEdit!.SupplierId,
                        _txtSupplierName.Text,
                        _txtContactNumber.Text,
                        _txtAddress.Text);
                }
                else
                {
                    result = await _supplierService.AddAsync(
                        _txtSupplierName.Text,
                        _txtContactNumber.Text,
                        _txtAddress.Text);
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

        private void SupplierForm_KeyDown(object? sender, KeyEventArgs e)
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
            _txtSupplierName.Enabled = !busy;
            _txtContactNumber.Enabled = !busy;
            _txtAddress.Enabled = !busy;
        }

        private void ShowError(string message)
        {
            _lblStatus.ForeColor = Theme.Danger;
            _lblStatus.Text = message ?? string.Empty;
        }
    }
}