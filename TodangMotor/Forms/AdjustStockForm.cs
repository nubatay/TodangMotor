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

        private Label _lblItemName = null!;
        private Label _lblItemSub = null!;
        private Label _lblCurrentQty = null!;
        private RoundedTextBox _txtNewQty = null!;
        private RoundedTextBox _txtNotes = null!;
        private Button _btnSave = null!;
        private Button _btnCancel = null!;
        private Label _lblStatus = null!;

        private const int PadX = 32;
        private const int LabelH = 22;
        private const int FieldH = 44;
        private const int RowGap = 16;

        public AdjustStockForm(Product product)
        {
            _product = product ?? throw new ArgumentNullException(nameof(product));

            HeaderTitle = "Adjust Stock";
            ShowMaximizeButton = false;
            ShowMinimizeButton = true;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(560, 580);
            MinimumSize = new Size(540, 560);
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

            // ---------- Body Panel ----------
            var bodyPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Background
            };

            int fieldW = ClientSize.Width - PadX * 2;
            int y = 28; // Top padding

            // ---- Item info (read-only card) ----
            var infoCard = new RoundedPanel
            {
                Location = new Point(PadX, y),
                Size = new Size(fieldW, 88),
                BackColor = Theme.Surface,
                Radius = Theme.RadiusSmall,
                BorderColor = Theme.PanelBorder,
                BorderSize = 1,
                ShadowEnabled = false,
                Padding = new Padding(16, 12, 16, 12)
            };

            var caption = new Label
            {
                Text = "ADJUSTING",
                Font = new Font(Theme.UiFontFamily, 8F, FontStyle.Bold),
                ForeColor = Theme.TextMuted,
                AutoSize = false,
                Location = new Point(16, 10),
                Size = new Size(fieldW - 32, 14),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _lblItemName = new Label
            {
                Text = "—",
                Font = new Font(Theme.UiFontFamily, 13F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(16, 26),
                Size = new Size(fieldW - 32, 24),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };

            _lblItemSub = new Label
            {
                Text = "—",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(16, 52),
                Size = new Size(fieldW - 32, 20),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };

            infoCard.Controls.Add(caption);
            infoCard.Controls.Add(_lblItemName);
            infoCard.Controls.Add(_lblItemSub);
            bodyPanel.Controls.Add(infoCard);

            y += 88 + RowGap;

            // ---- Current quantity (read-only stat) ----
            var qtyLabel = new Label
            {
                Text = "Current Quantity",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(PadX, y),
                Size = new Size(fieldW, LabelH),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            bodyPanel.Controls.Add(qtyLabel);

            _lblCurrentQty = new Label
            {
                Text = "0",
                Font = new Font(Theme.UiFontFamily, 24F, FontStyle.Bold),
                ForeColor = Theme.Primary,
                AutoSize = false,
                Location = new Point(PadX, y + LabelH + 2),
                Size = new Size(fieldW, 44),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            bodyPanel.Controls.Add(_lblCurrentQty);

            var unitsLabel = new Label
            {
                Text = "units on hand",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextMuted,
                AutoSize = false,
                Location = new Point(PadX + 60, y + LabelH + 16),
                Size = new Size(fieldW - 60, 22),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            bodyPanel.Controls.Add(unitsLabel);

            y += LabelH + 2 + 44 + RowGap;

            // ---- New quantity ----
            AddFieldLabel(bodyPanel, "New Quantity", PadX, y, fieldW);
            _txtNewQty = UiFactory.CreateTextBox(fieldW);
            _txtNewQty.Location = new Point(PadX, y + LabelH + 4);
            _txtNewQty.MaxLength = 9;
            _txtNewQty.Placeholder = "Enter new on-hand quantity";
            bodyPanel.Controls.Add(_txtNewQty);
            y += LabelH + 4 + FieldH + RowGap;

            // ---- Notes ----
            AddFieldLabel(bodyPanel, "Notes (optional)", PadX, y, fieldW);
            _txtNotes = UiFactory.CreateTextBox(fieldW);
            _txtNotes.Location = new Point(PadX, y + LabelH + 4);
            _txtNotes.MaxLength = 200;
            _txtNotes.Placeholder = "e.g. Found extra units during recount";
            bodyPanel.Controls.Add(_txtNotes);

            // ---------- Add to ContentPanel ----------
            ContentPanel.Controls.Add(bodyPanel);
            ContentPanel.Controls.Add(footer);
        }

        private void AddFieldLabel(Control parent, string text, int x, int y, int width)
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
            parent.Controls.Add(lbl);
        }

        // ============================================================
        // LOAD
        // ============================================================

        private void AdjustStockForm_Load(object? sender, EventArgs e)
        {
            Animator.SlideFadeInForm(this, 180, 12);

            _lblItemName.Text = _product.ProductName ?? string.Empty;
            _lblItemSub.Text = string.IsNullOrWhiteSpace(_product.Brand)
                ? "No brand"
                : _product.Brand;

            _lblCurrentQty.Text = _product.QuantityOnHand.ToString("N0");
            _txtNewQty.Text = _product.QuantityOnHand.ToString();

            _txtNewQty.FocusInput();
            _txtNewQty.SelectAll();
        }

        // ============================================================
        // SAVE
        // ============================================================

        private async Task SaveAsync()
        {
            _lblStatus.Text = string.Empty;

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