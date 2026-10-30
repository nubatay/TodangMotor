using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using TodangMotor.Common;
using TodangMotor.Models;
using TodangMotor.Services;

namespace TodangMotor.Forms
{
    public class SupplierForm : ShellForm
    {
        private readonly SupplierService _supplierService = new();

        private readonly Supplier? _supplierToEdit;
        private bool IsEditMode => _supplierToEdit != null;

        private RoundedTextBox _txtSupplierName = null!;
        private RoundedTextBox _txtContactNumber = null!;
        private RoundedTextBox _txtAddress = null!;
        private Button _btnSave = null!;
        private Button _btnCancel = null!;
        private Label _lblStatus = null!;

        private const int PadX = 32;
        private const int LabelH = 22;
        private const int FieldH = 44;
        private const int RowGap = 16;

        public SupplierForm(Supplier? supplierToEdit = null)
        {
            _supplierToEdit = supplierToEdit;

            HeaderTitle = IsEditMode ? "Edit Supplier" : "Add Supplier";
            ShowMaximizeButton = false;
            ShowMinimizeButton = true;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(640, 500);
            MinimumSize = new Size(600, 480);
            BackColor = Theme.Background;
            KeyPreview = true;

            BuildLayout();

            Load += SupplierForm_Load;
            KeyDown += SupplierForm_KeyDown;
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

            // Supplier Name
            AddFieldLabel("Supplier Name", PadX, y, fieldW);
            _txtSupplierName = UiFactory.CreateTextBox(fieldW);
            _txtSupplierName.Location = new Point(PadX, y + LabelH + 4);
            _txtSupplierName.MaxLength = 150;
            _txtSupplierName.Placeholder = "e.g. Motohub";
            ContentPanel.Controls.Add(_txtSupplierName);
            y += LabelH + 4 + FieldH + RowGap;

            // Contact Number
            AddFieldLabel("Contact Number", PadX, y, fieldW);
            _txtContactNumber = UiFactory.CreateTextBox(fieldW);
            _txtContactNumber.Location = new Point(PadX, y + LabelH + 4);
            _txtContactNumber.MaxLength = 15;
            _txtContactNumber.Placeholder = "09XXXXXXXXX";
            ContentPanel.Controls.Add(_txtContactNumber);

            var hint = new Label
            {
                Text = "Format: 09XXXXXXXXX (11 digits, e.g. 09171234567)",
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

            // Address
            AddFieldLabel("Address (optional)", PadX, y, fieldW);
            _txtAddress = UiFactory.CreateTextBox(fieldW);
            _txtAddress.Location = new Point(PadX, y + LabelH + 4);
            _txtAddress.MaxLength = 250;
            _txtAddress.Placeholder = "e.g. 123 Rizal St., Davao City";
            ContentPanel.Controls.Add(_txtAddress);
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

        private async Task SaveAsync()
        {
            _lblStatus.Text = string.Empty;
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

        private void SupplierForm_KeyDown(object? sender, KeyEventArgs e)
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