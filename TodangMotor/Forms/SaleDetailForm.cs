using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TodangMotor.Common;
using TodangMotor.Services;

namespace TodangMotor.Forms
{
    /// <summary>
    /// Sale detail popup. Shows one sale's full information and
    /// provides a Void Sale button (same-day only, reason required).
    /// </summary>
    public class SaleDetailForm : ShellForm
    {
        private readonly SalesService _salesService = new();
        private readonly int _saleId;

        private SaleDetailResult? _detail;

        // ---- Controls ----
        private Label _lblInvoice;
        private Label _lblMeta;
        private Label _lblVoidBanner;
        private DataGridView _linesGrid;
        private Label _lblSubtotal;
        private Label _lblTendered;
        private Label _lblChange;
        private Button _btnVoid;
        private Button _btnClose;

        // ============================================================
        // CONSTRUCTION
        // ============================================================

        public SaleDetailForm(int saleId)
        {
            _saleId = saleId;

            HeaderTitle = "Sale Detail";
            ShowMaximizeButton = false;
            ShowMinimizeButton = true;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(820, 780);
            MinimumSize = new Size(760, 700);
            BackColor = Theme.Background;
            KeyPreview = true;

            BuildLayout();

            Load += SaleDetailForm_Load;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) Close();
            };
        }

        // ============================================================
        // LAYOUT
        // ============================================================

        private void BuildLayout()
        {
            // Root uses a TableLayoutPanel so the vertical stack is
            // deterministic regardless of docking-order quirks.
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Theme.Background,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 176)); // header info
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // grid
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 130)); // totals
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));  // footer

            root.Controls.Add(BuildInfoBlock(), 0, 0);
            root.Controls.Add(BuildGridBlock(), 0, 1);
            root.Controls.Add(BuildTotalsBlock(), 0, 2);
            root.Controls.Add(BuildFooter(), 0, 3);

            ContentPanel.Controls.Add(root);
        }

        private Panel BuildInfoBlock()
        {
            var block = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Background,
                Padding = new Padding(32, 16, 32, 0)
            };

            _lblInvoice = new Label
            {
                Text = "Loading…",
                Font = new Font(Theme.UiFontFamily, 20F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(32, 16),
                Size = new Size(740, 36),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _lblMeta = new Label
            {
                Text = string.Empty,
                Font = Theme.FontBody,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(32, 58),
                Size = new Size(740, 76),
                TextAlign = ContentAlignment.TopLeft,
                BackColor = Color.Transparent
            };

            _lblVoidBanner = new Label
            {
                Text = string.Empty,
                Font = Theme.FontBodyBold,
                ForeColor = Theme.Danger,
                AutoSize = false,
                Location = new Point(32, 138),
                Size = new Size(740, 32),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            block.Controls.Add(_lblInvoice);
            block.Controls.Add(_lblMeta);
            block.Controls.Add(_lblVoidBanner);

            return block;
        }

        private Panel BuildGridBlock()
        {
            var block = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Background,
                Padding = new Padding(32, 0, 32, 12)
            };

            _linesGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 0, 4)
            };
            UiFactory.StyleGrid(_linesGrid);
            _linesGrid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 251);
            PrimeLinesColumns();

            block.Controls.Add(_linesGrid);
            return block;
        }

        private Panel BuildTotalsBlock()
        {
            var block = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Background,
                Padding = new Padding(32, 0, 32, 0)
            };

            _lblSubtotal = MakeTotalLabel("Subtotal", 0, 8, true);
            _lblTendered = MakeTotalLabel("Tendered", 0, 42, false);
            _lblChange = MakeTotalLabel("Change", 0, 72, false);

            block.Controls.Add(_lblSubtotal);
            block.Controls.Add(_lblTendered);
            block.Controls.Add(_lblChange);

            return block;
        }

        private Label MakeTotalLabel(string prefix, int x, int y, bool bold)
        {
            return new Label
            {
                Text = prefix + ": ₱ 0.00",
                Font = bold
                    ? new Font(Theme.UiFontFamily, 13F, FontStyle.Bold)
                    : Theme.FontBody,
                ForeColor = bold ? Theme.TextPrimary : Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(32, y),
                Size = new Size(740, 28),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
        }

        private Panel BuildFooter()
        {
            var footer = new Panel
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
            footer.Controls.Add(divider);

            const int btnW = 140;
            const int btnH = 44;

            _btnVoid = UiFactory.CreateButton("Void Sale", UiFactory.ButtonStyle.Danger, btnW, btnH);
            _btnVoid.Location = new Point(32, 22);
            _btnVoid.Click += async (s, e) => await VoidSaleAsync();

            _btnClose = UiFactory.CreateButton("Close", UiFactory.ButtonStyle.Ghost, btnW, btnH);
            _btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnClose.Click += (s, e) => Close();

            footer.Controls.Add(_btnVoid);
            footer.Controls.Add(_btnClose);

            footer.Resize += (s, e) =>
            {
                _btnClose.Location = new Point(footer.ClientSize.Width - 32 - btnW, 22);
            };

            return footer;
        }

        private void PrimeLinesColumns()
        {
            _linesGrid.AutoGenerateColumns = false;
            _linesGrid.Columns.Clear();

            void AddCol(string prop, string header, int weight, bool rightAlign = false)
            {
                var col = new DataGridViewTextBoxColumn
                {
                    Name = prop,
                    DataPropertyName = prop,
                    HeaderText = header,
                    FillWeight = weight,
                    ReadOnly = true
                };
                if (rightAlign)
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                _linesGrid.Columns.Add(col);
            }

            AddCol("ProductName", "Product", 240);
            AddCol("Brand", "Brand", 140);
            AddCol("Unit", "Unit", 60);
            AddCol("Quantity", "Qty", 60, true);
            AddCol("UnitPrice", "Unit Price", 120, true);
            AddCol("LineTotal", "Line Total", 120, true);
        }

        // ============================================================
        // LOAD
        // ============================================================

        private async void SaleDetailForm_Load(object? sender, EventArgs e)
        {
            Animator.SlideFadeInForm(this, 180, 12);
            await LoadDetailAsync();
        }

        private async Task LoadDetailAsync()
        {
            var (success, error, detail) = await _salesService.GetSaleDetailAsync(_saleId);

            if (!success || detail == null)
            {
                MessageBox.Show(
                    error ?? "Could not load the sale.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Close();
                return;
            }

            _detail = detail;
            RenderDetail();
        }

        private void RenderDetail()
        {
            if (_detail == null) return;
            var sale = _detail.Sale;

            _lblInvoice.Text = $"Invoice {sale.InvoiceNo}";

            string customer = string.IsNullOrEmpty(sale.CustomerName)
                ? "Walk-in"
                : sale.CustomerName;

            string statusIcon = sale.Status == "Void" ? "  ·  VOID" : "";

            _lblMeta.Text =
                $"Date:          {sale.SaleDate:MMMM d, yyyy  h:mm tt}\n" +
                $"Cashier:       {_detail.CashierName}\n" +
                $"Customer:      {customer}   ·   " +
                $"Payment: {sale.PaymentMethod}{statusIcon}";

            if (sale.Status == "Void")
            {
                _lblVoidBanner.Text =
                    $"VOID — by {_detail.VoidedByName} on " +
                    $"{sale.VoidedAt:MMM d, yyyy h:mm tt}. Reason: {sale.VoidReason}";
            }
            else
            {
                _lblVoidBanner.Text = string.Empty;
            }

            var lineRows = _detail.Lines
                .Select(l => new
                {
                    ProductName = l.ProductName,
                    Brand = l.Brand,
                    Unit = l.Unit,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice.ToString("N2"),
                    LineTotal = l.LineTotal.ToString("N2")
                })
                .ToList();

            _linesGrid.DataSource = lineRows;

            _lblSubtotal.Text = $"Subtotal:  ₱ {sale.Subtotal:N2}";
            _lblTendered.Text = $"Tendered:  ₱ {sale.AmountTendered:N2}  ({sale.PaymentMethod})";
            _lblChange.Text = $"Change:    ₱ {sale.ChangeAmount:N2}";

            bool canVoid = sale.Status != "Void"
                           && sale.SaleDate.Date == DateTime.Now.Date;
            _btnVoid.Enabled = canVoid;

            if (sale.Status == "Void")
                _btnVoid.Text = "Already Void";
            else if (sale.SaleDate.Date != DateTime.Now.Date)
                _btnVoid.Text = "Cannot Void";
            else
                _btnVoid.Text = "Void Sale";
        }

        // ============================================================
        // VOID SALE
        // ============================================================

        private async Task VoidSaleAsync()
        {
            if (_detail == null) return;

            using var reasonForm = new VoidReasonForm();
            var choice = reasonForm.ShowDialog(this);

            if (choice != DialogResult.OK) return;

            string reason = reasonForm.Reason;
            if (string.IsNullOrWhiteSpace(reason)) return;

            var confirm = MessageBox.Show(
                $"Void invoice {_detail.Sale.InvoiceNo}?\n\n" +
                "Stock will be restored for all items in this sale.\n" +
                "This cannot be undone.",
                "Confirm Void",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            var (success, error) = await _salesService.VoidSaleAsync(_saleId, reason);

            if (!success)
            {
                MessageBox.Show(
                    error ?? "Could not void the sale.",
                    "Void Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            MessageBox.Show(
                "Sale voided. Stock has been restored.",
                "Void Successful",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            DialogResult = DialogResult.OK;
            Close();
        }
    }

    // ================================================================
    // VOID REASON DIALOG
    // ================================================================

    public class VoidReasonForm : ShellForm
    {
        private RoundedTextBox _reasonBox;
        private Label _errorLabel;

        public string Reason => _reasonBox?.Text?.Trim() ?? string.Empty;

        public VoidReasonForm()
        {
            HeaderTitle = "Void Sale — Reason";
            ShowMaximizeButton = false;
            ShowMinimizeButton = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(520, 300);
            MinimumSize = new Size(500, 280);
            BackColor = Theme.Background;
            KeyPreview = true;

            BuildLayout();

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

            var lbl = new Label
            {
                Text = "Why is this sale being voided?",
                Font = Theme.FontBody,
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(padX, 24),
                Size = new Size(fieldW, 22),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            ContentPanel.Controls.Add(lbl);

            _reasonBox = UiFactory.CreateTextBox(fieldW);
            _reasonBox.Location = new Point(padX, 54);
            _reasonBox.MaxLength = 250;
            _reasonBox.Placeholder = "e.g. Wrong item, Customer changed mind";
            ContentPanel.Controls.Add(_reasonBox);

            _errorLabel = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.Danger,
                AutoSize = false,
                Location = new Point(padX, 104),
                Size = new Size(fieldW, 22),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            ContentPanel.Controls.Add(_errorLabel);

            int btnW = 130;
            int btnH = 42;
            int btnY = ClientSize.Height - 68;

            var btnCancel = UiFactory.CreateButton("Cancel", UiFactory.ButtonStyle.Ghost, btnW, btnH);
            btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnCancel.Location = new Point(ClientSize.Width - padX - btnW - 8 - btnW, btnY);
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            var btnOk = UiFactory.CreateButton("Continue", UiFactory.ButtonStyle.Danger, btnW, btnH);
            btnOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnOk.Location = new Point(ClientSize.Width - padX - btnW, btnY);
            btnOk.Click += (s, e) =>
            {
                string r = _reasonBox.Text?.Trim() ?? string.Empty;
                if (r.Length == 0)
                {
                    _errorLabel.Text = "Please type a reason.";
                    _reasonBox.FocusInput();
                    return;
                }
                if (r.Length > 250)
                {
                    _errorLabel.Text = "Reason cannot exceed 250 characters.";
                    return;
                }
                DialogResult = DialogResult.OK;
                Close();
            };

            ContentPanel.Controls.Add(btnCancel);
            ContentPanel.Controls.Add(btnOk);
        }
    }
}