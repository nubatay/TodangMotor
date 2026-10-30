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
    public class SaleDetailForm : ShellForm
    {
        private readonly SalesService _salesService = new();
        private readonly int _saleId;

        private SaleDetailResult? _detail;
        private bool _isBusy;

        private Label _lblInvoiceNumber = null!;
        private Label _metaDateValue = null!;
        private Label _metaCashierValue = null!;
        private Label _metaCustomerValue = null!;
        private Label _metaPaymentValue = null!;
        private Label _metaReferenceValue = null!;

        private DataGridView _linesGrid = null!;

        private Label _lblSubtotal = null!;
        private Label _lblTendered = null!;
        private Label _lblChange = null!;

        private Button _btnPrint = null!;
        private Button _btnClose = null!;

        public SaleDetailForm(int saleId)
        {
            _saleId = saleId;

            HeaderTitle = "Sale Detail";
            ShowMaximizeButton = false;
            ShowMinimizeButton = true;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(820, 780);
            MinimumSize = new Size(760, 720);
            BackColor = Theme.Background;
            KeyPreview = true;

            BuildLayout();

            Load += SaleDetailForm_Load;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) Close();
            };
        }

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
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 128));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));

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
            int contentW = ClientSize.Width - (padX * 2);

            var caption = new Label
            {
                Text = "INVOICE",
                Font = new Font(Theme.UiFontFamily, 9.5F, FontStyle.Bold),
                ForeColor = Theme.TextMuted,
                AutoSize = false,
                Location = new Point(padX, 16),
                Size = new Size(contentW, 18),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _lblInvoiceNumber = new Label
            {
                Text = "—",
                Font = new Font(Theme.UiFontFamily, 20F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(padX, 36),
                Size = new Size(contentW, 40),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            var divider = new Panel
            {
                Location = new Point(padX, 88),
                Size = new Size(contentW, 1),
                BackColor = Theme.Divider
            };

            var metaBox = new RoundedPanel
            {
                Location = new Point(padX, 100),
                Size = new Size(contentW, 100),
                Radius = Theme.RadiusSmall,
                BorderColor = Theme.Divider,
                BorderSize = 1,
                ShadowEnabled = false,
                BackColor = Theme.Surface,
                Padding = new Padding(16, 12, 16, 12),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            var meta = new TableLayoutPanel
            {
                ColumnCount = 4,
                RowCount = 3,
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            meta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15f));
            meta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35f));
            meta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15f));
            meta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35f));

            meta.RowStyles.Add(new RowStyle(SizeType.Percent, 33f));
            meta.RowStyles.Add(new RowStyle(SizeType.Percent, 33f));
            meta.RowStyles.Add(new RowStyle(SizeType.Percent, 34f));

            _metaDateValue = MakeMetaValue();
            _metaCashierValue = MakeMetaValue();
            _metaCustomerValue = MakeMetaValue();
            _metaPaymentValue = MakeMetaValue();
            _metaReferenceValue = MakeMetaValue();

            meta.Controls.Add(MakeMetaLabel("Date"), 0, 0);
            meta.Controls.Add(_metaDateValue, 1, 0);
            meta.Controls.Add(MakeMetaLabel("Cashier"), 2, 0);
            meta.Controls.Add(_metaCashierValue, 3, 0);

            meta.Controls.Add(MakeMetaLabel("Customer"), 0, 1);
            meta.Controls.Add(_metaCustomerValue, 1, 1);
            meta.Controls.Add(MakeMetaLabel("Payment"), 2, 1);
            meta.Controls.Add(_metaPaymentValue, 3, 1);

            meta.Controls.Add(MakeMetaLabel("Ref No"), 0, 2);
            meta.Controls.Add(_metaReferenceValue, 1, 2);

            metaBox.Controls.Add(meta);

            block.Controls.Add(caption);
            block.Controls.Add(_lblInvoiceNumber);
            block.Controls.Add(divider);
            block.Controls.Add(metaBox);

            void AlignToBlockWidth()
            {
                int w = Math.Max(0, block.ClientSize.Width - (padX * 2));
                caption.Width = w;
                _lblInvoiceNumber.Width = w;
                divider.Width = w;
                metaBox.Width = w;
            }
            block.Resize += (s, e) => AlignToBlockWidth();
            AlignToBlockWidth();

            return block;
        }

        private static Label MakeMetaLabel(string text) => new Label
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

        private static Label MakeMetaValue() => new Label
        {
            Text = string.Empty,
            Font = Theme.FontBody,
            ForeColor = Theme.TextPrimary,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            AutoEllipsis = true
        };

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
                Margin = new Padding(0)
            };
            UiFactory.StyleGridModern(_linesGrid);
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

            _lblSubtotal = MakeTotalLabel("Subtotal", 12, true);
            _lblTendered = MakeTotalLabel("Tendered", 48, false);
            _lblChange = MakeTotalLabel("Change", 78, false);

            block.Controls.Add(_lblSubtotal);
            block.Controls.Add(_lblTendered);
            block.Controls.Add(_lblChange);

            return block;
        }

        private Label MakeTotalLabel(string prefix, int y, bool bold) => new Label
        {
            Text = prefix + ": ₱ 0.00",
            Font = bold
                ? new Font(Theme.UiFontFamily, 14F, FontStyle.Bold)
                : Theme.FontBody,
            ForeColor = bold ? Theme.TextPrimary : Theme.TextSecondary,
            AutoSize = false,
            Location = new Point(32, y),
            Size = new Size(700, 30),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent
        };

        private Panel BuildFooter()
        {
            var footer = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };

            var divider = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = Theme.Divider
            };
            footer.Controls.Add(divider);

            int btnH = UiFactory.PrimaryButtonHeight;

            _btnPrint = UiFactory.CreateButton("Print", UiFactory.ButtonStyle.Primary, 120, btnH);
            _btnPrint.Location = new Point(32, 26);
            _btnPrint.Click += (s, e) => OnPrintClick();

            _btnClose = UiFactory.CreateButton("Close", UiFactory.ButtonStyle.Ghost, 120, btnH);
            _btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnClose.Click += (s, e) => Close();

            footer.Controls.Add(_btnPrint);
            footer.Controls.Add(_btnClose);

            footer.Resize += (s, e) =>
            {
                _btnClose.Location = new Point(footer.ClientSize.Width - 32 - _btnClose.Width, 26);
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
        }

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
                MessageBox.Show(error ?? "Could not load the sale.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            _metaCashierValue.Text = string.IsNullOrEmpty(_detail.CashierName) ? "—" : _detail.CashierName;
            _metaCustomerValue.Text = string.IsNullOrEmpty(sale.CustomerName) ? "Walk-in" : sale.CustomerName;
            _metaPaymentValue.Text = sale.PaymentMethod ?? string.Empty;
            _metaReferenceValue.Text = string.IsNullOrWhiteSpace(sale.ReferenceNo) ? "—" : sale.ReferenceNo;

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

        // -----------------------------------------------------------------
        //  Printing
        // -----------------------------------------------------------------

        private void OnPrintClick()
        {
            if (_isBusy || _detail == null) return;

            _isBusy = true;
            _btnPrint.Enabled = false;
            _btnClose.Enabled = false;
            try
            {
                // Reuse the same print pipeline as ReceiptForm so the output
                // is identical whether you print from a receipt or a sale.
                ReceiptForm.PrintReceipt(_detail, this);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not print the sale.\n\n" + ex.Message,
                    "Print Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _isBusy = false;
                _btnPrint.Enabled = true;
                _btnClose.Enabled = true;
            }
        }

        private static string BuildItemText(SaleLineDisplay l)
        {
            string name = l.ProductName ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(l.Brand)) name = $"{name} — {l.Brand}";
            if (!string.IsNullOrWhiteSpace(l.Unit)) name = $"{name}  ·  {l.Unit}";
            return name;
        }
    }
}