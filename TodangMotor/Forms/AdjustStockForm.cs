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
    /// Small popup for adjusting a product's on-hand stock.
    /// Owner-only. Writes an Adjustment movement to StockMovements.
    /// Opened from the Inventory → Stock Levels tab.
    /// </summary>
    public class AdjustStockForm : ShellForm
    {
        private readonly ProductService _productService = new();
        private readonly Product _product;

        // ============================================================
        // UI
        // ============================================================

        private Label _lblItemName;
        private Label _lblCurrentQty;
        private RoundedTextBox _txtNewQty;
        private RoundedTextBox _txtNotes;
        private Button _btnSave;
        private Button _btnCancel;
        private Label _lblStatus;

        // ============================================================
        // CONSTRUCTION
        // ============================================================

        public AdjustStockForm(Product product)
        {
            _product = product ?? throw new ArgumentNullException(nameof(product));

            HeaderTitle = "Adjust Stock";
            ShowMaximizeButton = false;
            ShowMinimizeButton = true;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(520, 400);
            MinimumSize = new Size(500, 380);
            BackColor = Theme.Background;
            KeyPreview = true;

            BuildLayout();

            Load += AdjustStockForm_Load;
            KeyDown += AdjustStockForm_KeyDown;
        }

        // ============================================================
        // LAYOUT
        // ============================================================

        private void BuildLayout()
        {
            const int padX = 32;
            int fieldW = ClientSize.Width - padX * 2;

            // ---- Footer (Bottom) ----
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

            // ---- Content ----
            int y = 24;

            // Item (read-only)
            AddLabel("Item:", padX, y);
            _lblItemName = new Label
            {
                Text = string.Empty,
                Font = Theme.FontBodyBold,
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(padX + 130, y),
                Size = new Size(fieldW - 130, 30),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };
            ContentPanel.Controls.Add(_lblItemName);
            y += 46;

            // Current Quantity (read-only)
            AddLabel("Current Qty:", padX, y);
            _lblCurrentQty = new Label
            {
                Text = "0",
                Font = new Font(Theme.UiFontFamily, 14F, FontStyle.Bold),
                ForeColor = Theme.Primary,
                AutoSize = false,
                Location = new Point(padX + 130, y - 4),
                Size = new Size(fieldW - 130, 32),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            ContentPanel.Controls.Add(_lblCurrentQty);
            y += 52;

            // New Quantity (editable)
            AddLabel("New Qty:", padX, y);
            _txtNewQty = UiFactory.CreateTextBox(fieldW - 130);
            _txtNewQty.Location = new Point(padX + 130, y);
            _txtNewQty.MaxLength = 9;
            _txtNewQty.Placeholder = "Enter new on-hand quantity";
            ContentPanel.Controls.Add(_txtNewQty);
            y += 62;

            // Notes (optional)
            AddLabel("Notes:", padX, y);
            _txtNotes = UiFactory.CreateTextBox(fieldW - 130);
            _txtNotes.Location = new Point(padX + 130, y);
            _txtNotes.MaxLength = 200;
            _txtNotes.Placeholder = "Optional — e.g. Found extra units during recount";
            ContentPanel.Controls.Add(_txtNotes);
            y += 62;

            // Status
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
                Size = new Size(120, 40),
                Location = new Point(x, y),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            ContentPanel.Controls.Add(lbl);
        }

        // ============================================================
        // LOAD
        // ============================================================

        private void AdjustStockForm_Load(object? sender, EventArgs e)
        {
            Animator.SlideFadeInForm(this, 180, 12);

            string display = string.IsNullOrWhiteSpace(_product.Brand)
                ? (_product.ProductName ?? string.Empty)
                : $"{_product.ProductName} — {_product.Brand}";

            _lblItemName.Text = display;
            _lblCurrentQty.Text = _product.QuantityOnHand.ToString();
            _txtNewQty.Text = _product.QuantityOnHand.ToString();

            _txtNewQty.FocusInput();
            _txtNewQty.SelectAll();
        }

        // ============================================================
        // SAVE
        // ============================================================

        private async Task SaveAsync()
        {
            string raw = (_txtNewQty.Text ?? string.Empty).Trim();

            if (!int.TryParse(raw, out int newQty))
            {
                ShowError("New quantity must be a whole number.");
                _txtNewQty.FocusInput();
                return;
            }

            if (newQty < 0)
            {
                ShowError("Quantity cannot be negative.");
                _txtNewQty.FocusInput();
                return;
            }

            if (newQty == _product.QuantityOnHand)
            {
                ShowError("New quantity is the same as the current quantity — nothing to record.");
                _txtNewQty.FocusInput();
                return;
            }

            SetBusy(true);
            try
            {
                var (success, error) = await _productService.UpdateStockAsync(
                    _product.ProductId,
                    newQty,
                    _txtNotes.Text);

                if (!success)
                {
                    ShowError(error);
                    return;
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            finally
            {
                SetBusy(false);
            }
        }

        // ============================================================
        // EVENTS
        // ============================================================

        private void AdjustStockForm_KeyDown(object? sender, KeyEventArgs e)
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
            _txtNewQty.Enabled = !busy;
            _txtNotes.Enabled = !busy;
        }

        private void ShowError(string message)
        {
            _lblStatus.ForeColor = Theme.Danger;
            _lblStatus.Text = message ?? string.Empty;
        }
    }
}