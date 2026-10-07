using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TodangMotor.Common;
using TodangMotor.Services;

namespace TodangMotor.Forms
{
    /// <summary>
    /// Digital receipt shown after a successful POS sale.
    /// View-only. Loads sale details by SaleId and displays them
    /// as a receipt-style summary.
    /// </summary>
    public class ReceiptForm : ShellForm
    {
        private readonly SalesService _salesService = new();
        private readonly int _saleId;

        private SaleDetailResult? _detail;

        // ---- Header info ----
        private Label _lblInvoiceNumber;
        private Label _metaDateValue;
        private Label _metaCashierValue;
        private Label _metaCustomerValue;
        private Label _metaPaymentValue;

        // ---- Line items ----
        private DataGridView _linesGrid;

        // ---- Totals ----
        private Label _lblSubtotal;
        private Label _lblTendered;
        private Label _lblChange;

        // ---- Footer ----
        private Button _btnClose;

        // ============================================================
        // CONSTRUCTION
        // ============================================================

        public ReceiptForm(int saleId)
        {
            _saleId = saleId;

            HeaderTitle = "Sale Receipt";
            ShowMaximizeButton = false;
            ShowMinimizeButton = true;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(760, 720);
            MinimumSize = new Size(700, 640);
            BackColor = Theme.Background;
            KeyPreview = true;

            BuildLayout();

            Load += ReceiptForm_Load;
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
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Theme.Background,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));

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
                Margin = Padding.Empty
            };

            const int padX = 32;
            const int contentW = 680;

            // ---- "INVOICE" caption ----
            var caption = new Label
            {
                Text = "INVOICE",
                Font = new Font(Theme.UiFontFamily, 9F, FontStyle.Bold),
                ForeColor = Theme.TextMuted,
                AutoSize = false,
                Location = new Point(padX, 14),
                Size = new Size(contentW, 16),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            // ---- Invoice number ----
            _lblInvoiceNumber = new Label
            {
                Text = "—",
                Font = new Font(Theme.UiFontFamily, 18F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(padX, 32),
                Size = new Size(contentW, 34),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            // ---- Divider under invoice ----
            var divider = new Panel
            {
                Location = new Point(padX, 76),
                Size = new Size(contentW, 1),
                BackColor = Theme.Divider
            };

            // ---- 4-column × 2-row meta grid ----
            var meta = new TableLayoutPanel
            {
                ColumnCount = 4,
                RowCount = 2,
                Location = new Point(padX, 86),
                Size = new Size(contentW, 60),
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            meta.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            meta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            meta.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            meta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            meta.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            meta.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

            _metaDateValue = MakeMetaValue();
            _metaCashierValue = MakeMetaValue();
            _metaCustomerValue = MakeMetaValue();
            _metaPaymentValue = MakeMetaValue();

            meta.Controls.Add(MakeMetaLabel("Date"), 0, 0);
            meta.Controls.Add(_metaDateValue, 1, 0);
            meta.Controls.Add(MakeMetaLabel("Cashier"), 2, 0);
            meta.Controls.Add(_metaCashierValue, 3, 0);

            meta.Controls.Add(MakeMetaLabel("Customer"), 0, 1);
            meta.Controls.Add(_metaCustomerValue, 1, 1);
            meta.Controls.Add(MakeMetaLabel("Payment"), 2, 1);
            meta.Controls.Add(_metaPaymentValue, 3, 1);

            block.Controls.Add(caption);
            block.Controls.Add(_lblInvoiceNumber);
            block.Controls.Add(divider);
            block.Controls.Add(meta);

            return block;
        }

        private static Label MakeMetaLabel(string text)
        {
            return new Label
            {
                Text = text,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
        }

        private static Label MakeMetaValue()
        {
            return new Label
            {
                Text = string.Empty,
                Font = Theme.FontBody,
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
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

            _lblSubtotal = MakeTotalLabel("Subtotal", 8, true);
            _lblTendered = MakeTotalLabel("Tendered", 42, false);
            _lblChange = MakeTotalLabel("Change", 72, false);

            block.Controls.Add(_lblSubtotal);
            block.Controls.Add(_lblTendered);
            block.Controls.Add(_lblChange);

            return block;
        }

        private Label MakeTotalLabel(string prefix, int y, bool bold)
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
                Size = new Size(660, 28),
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

            _btnClose = UiFactory.CreateButton("Close", UiFactory.ButtonStyle.Primary, btnW, btnH);
            _btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnClose.Click += (s, e) => Close();

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

            AddCol("Item", "Item", 320);
            AddCol("Quantity", "Qty", 60, true);
            AddCol("UnitPrice", "Unit Price", 110, true);
            AddCol("LineTotal", "Line Total", 110, true);

            _linesGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }

        // ============================================================
        // LOAD
        // ============================================================

        private async void ReceiptForm_Load(object? sender, EventArgs e)
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
                    error ?? "Could not load the receipt.",
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

            _lblInvoiceNumber.Text = sale.InvoiceNo;

            _metaDateValue.Text = sale.SaleDate.ToString("MMM d, yyyy  h:mm tt");
            _metaCashierValue.Text = string.IsNullOrEmpty(_detail.CashierName)
                ? "—"
                : _detail.CashierName;
            _metaCustomerValue.Text = string.IsNullOrEmpty(sale.CustomerName)
                ? "Walk-in"
                : sale.CustomerName;
            _metaPaymentValue.Text = sale.PaymentMethod ?? string.Empty;

            var lineRows = _detail.Lines
                .Select(l => new
                {
                    Item = BuildItemText(l),
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice.ToString("N2"),
                    LineTotal = l.LineTotal.ToString("N2")
                })
                .ToList();

            _linesGrid.DataSource = lineRows;

            _lblSubtotal.Text = $"Subtotal:  ₱ {sale.Subtotal:N2}";
            _lblTendered.Text = $"Tendered:  ₱ {sale.AmountTendered:N2}  ({sale.PaymentMethod})";
            _lblChange.Text = $"Change:    ₱ {sale.ChangeAmount:N2}";
        }

        private static string BuildItemText(SaleLineDisplay l)
        {
            string name = l.ProductName ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(l.Brand))
                name = $"{name} — {l.Brand}";

            if (!string.IsNullOrWhiteSpace(l.Unit))
                name = $"{name}  ·  {l.Unit}";

            return name;
        }
    }
}