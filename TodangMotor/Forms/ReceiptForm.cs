using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TodangMotor.Common;
using TodangMotor.Services;

namespace TodangMotor.Forms
{
    public class ReceiptForm : ShellForm
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

        public ReceiptForm(int saleId)
        {
            _saleId = saleId;

            HeaderTitle = "Sale Receipt";
            ShowMaximizeButton = false;
            ShowMinimizeButton = true;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(780, 760);
            MinimumSize = new Size(700, 660);
            BackColor = Theme.Background;
            KeyPreview = true;

            BuildLayout();

            Load += ReceiptForm_Load;
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
            Size = new Size(660, 30),
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

            const int btnH = UiFactory.PrimaryButtonHeight;

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
                MessageBox.Show(error ?? "Could not load the receipt.", "Error",
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
                PrintReceipt(_detail, this);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not print the receipt.\n\n" + ex.Message,
                    "Print Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _isBusy = false;
                _btnPrint.Enabled = true;
                _btnClose.Enabled = true;
            }
        }

        /// <summary>
        /// Opens the standard print dialog for the given sale and sends the
        /// document to the selected printer. Returns true if the user pressed
        /// Print (the job was submitted), false if they cancelled.
        /// </summary>
        public static bool PrintReceipt(SaleDetailResult detail, IWin32Window? owner)
        {
            using var doc = BuildPrintDocument(detail);
            using var dialog = new PrintDialog
            {
                Document = doc,
                UseEXDialog = true
            };

            var result = owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog();
            if (result != DialogResult.OK) return false;

            doc.Print();
            return true;
        }

        private static PrintDocument BuildPrintDocument(SaleDetailResult detail)
        {
            var doc = new PrintDocument
            {
                DocumentName = string.IsNullOrWhiteSpace(detail.Sale.InvoiceNo)
                    ? $"Receipt-{detail.Sale.SaleId}"
                    : $"Receipt-{detail.Sale.InvoiceNo}"
            };
            doc.DefaultPageSettings.Margins = new Margins(50, 50, 50, 50);

            var state = new PrintState();
            doc.PrintPage += (s, e) => DrawReceiptPage(detail, e, state);
            return doc;
        }

        private sealed class PrintState
        {
            public int RowIndex;
            public bool HeaderDrawn;
        }

        private static void DrawReceiptPage(SaleDetailResult detail, PrintPageEventArgs e, PrintState state)
        {
            var g = e.Graphics!;
            var bounds = e.MarginBounds;

            float x = bounds.Left;
            float y = bounds.Top;
            float width = bounds.Width;
            float bottom = bounds.Bottom - 20;

            string family = Theme.UiFontFamily;

            using var titleFont = new Font(family, 18F, FontStyle.Bold);
            using var sectionFont = new Font(family, 11F, FontStyle.Bold);
            using var bodyFont = new Font(family, 10F, FontStyle.Regular);
            using var smallFont = new Font(family, 9F, FontStyle.Regular);
            using var totalFont = new Font(family, 13F, FontStyle.Bold);
            using var pen = new Pen(Color.FromArgb(170, 170, 170), 1f);
            using var blackBrush = new SolidBrush(Color.Black);
            using var grayBrush = new SolidBrush(Color.FromArgb(110, 110, 110));
            using var centerFmt = new StringFormat { Alignment = StringAlignment.Center };
            using var rightFmt = new StringFormat { Alignment = StringAlignment.Far };
            using var truncFmt = new StringFormat
            {
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };

            var sale = detail.Sale;

            if (!state.HeaderDrawn)
            {
                // Title
                g.DrawString("SALE RECEIPT", titleFont, blackBrush,
                    new RectangleF(x, y, width, 32), centerFmt);
                y += 40;

                // Meta block
                string cashier = string.IsNullOrEmpty(detail.CashierName) ? "—" : detail.CashierName;
                string customer = string.IsNullOrEmpty(sale.CustomerName) ? "Walk-in" : sale.CustomerName;
                string payment = string.IsNullOrEmpty(sale.PaymentMethod) ? "—" : sale.PaymentMethod;
                string reference = string.IsNullOrWhiteSpace(sale.ReferenceNo) ? "—" : sale.ReferenceNo;

                float c1Label = x;
                float c1Value = x + 75;
                float c2Label = x + width * 0.5f;
                float c2Value = x + width * 0.5f + 75;

                void MetaLine(int row, string l1, string v1, string l2, string v2)
                {
                    float ry = y + row * 18;
                    g.DrawString(l1, smallFont, grayBrush, c1Label, ry);
                    g.DrawString(v1, bodyFont, blackBrush, c1Value, ry - 1);
                    if (l2 != null)
                    {
                        g.DrawString(l2, smallFont, grayBrush, c2Label, ry);
                        g.DrawString(v2, bodyFont, blackBrush, c2Value, ry - 1);
                    }
                }

                MetaLine(0, "Invoice", sale.InvoiceNo, "Date",
                    sale.SaleDate.ToString("MMM d, yyyy  h:mm tt"));
                MetaLine(1, "Cashier", cashier, "Customer", customer);
                MetaLine(2, "Payment", payment, "Ref No", reference);

                y += 3 * 18 + 10;

                g.DrawLine(pen, x, y, x + width, y);
                y += 8;

                // Table header
                g.DrawString("Item", sectionFont, blackBrush, x, y);
                g.DrawString("Qty", sectionFont, blackBrush, x + width * 0.55f, y);
                g.DrawString("Unit Price", sectionFont, blackBrush, x + width * 0.68f, y);
                g.DrawString("Line Total", sectionFont, blackBrush,
                    new RectangleF(x + width * 0.82f, y, width * 0.18f, 20), rightFmt);
                y += 20;
                g.DrawLine(pen, x, y, x + width, y);
                y += 6;

                state.HeaderDrawn = true;
            }

            // Column geometry
            float itemColX = x;
            float itemColW = width * 0.55f - 10;
            float qtyColX = x + width * 0.55f;
            float unitColX = x + width * 0.68f;
            float totalColX = x + width * 0.82f;
            float totalColW = width * 0.18f;

            // Rows
            while (state.RowIndex < detail.Lines.Count)
            {
                if (y + 18 > bottom)
                {
                    e.HasMorePages = true;
                    return;
                }

                var line = detail.Lines[state.RowIndex];
                string itemText = BuildItemText(line);

                g.DrawString(itemText, bodyFont, blackBrush,
                    new RectangleF(itemColX, y, itemColW, 18), truncFmt);
                g.DrawString(line.Quantity.ToString(), bodyFont, blackBrush, qtyColX, y);
                g.DrawString(line.UnitPrice.ToString("N2"), bodyFont, blackBrush, unitColX, y);
                g.DrawString(line.LineTotal.ToString("N2"), bodyFont, blackBrush,
                    new RectangleF(totalColX, y, totalColW, 18), rightFmt);

                y += 18;
                state.RowIndex++;
            }

            // Divider
            g.DrawLine(pen, x, y + 2, x + width, y + 2);
            y += 14;

            // Summary (right-aligned)
            float sumLabelX = x + width * 0.55f;
            float sumValueX = x + width * 0.82f;
            float sumValueW = width * 0.18f;

            void SummaryLine(string label, string value, Font f)
            {
                g.DrawString(label, f, blackBrush, sumLabelX, y);
                g.DrawString(value, f, blackBrush,
                    new RectangleF(sumValueX, y, sumValueW, 24), rightFmt);
                y += 24;
            }

            SummaryLine("Subtotal", "₱ " + sale.Subtotal.ToString("N2"), totalFont);
            y += 4;
            SummaryLine("Tendered", "₱ " + sale.AmountTendered.ToString("N2"), bodyFont);
            SummaryLine("Change", "₱ " + sale.ChangeAmount.ToString("N2"), bodyFont);

            y += 12;
            g.DrawLine(pen, x, y, x + width, y);
            y += 12;

            g.DrawString("Thank you for your purchase.", smallFont, grayBrush,
                new RectangleF(x, y, width, 18), centerFmt);

            e.HasMorePages = false;
        }

        // -----------------------------------------------------------------
        //  Shared table helper (kept for other callers / exporters)
        // -----------------------------------------------------------------

        public static ReportTable BuildReceiptTable(SaleDetailResult detail)
        {
            var sale = detail.Sale;

            string customer = string.IsNullOrEmpty(sale.CustomerName) ? "Walk-in" : sale.CustomerName;
            string cashier = string.IsNullOrEmpty(detail.CashierName) ? "—" : detail.CashierName;

            string subtitle = $"Invoice {sale.InvoiceNo}   ·   {sale.SaleDate:MMMM d, yyyy  h:mm tt}   ·   " +
                              $"Cashier: {cashier}   ·   Customer: {customer}   ·   {sale.PaymentMethod}";

            if (!string.IsNullOrWhiteSpace(sale.ReferenceNo))
            {
                subtitle += $"   ·   Ref: {sale.ReferenceNo}";
            }

            var table = new ReportTable
            {
                Title = "SALE RECEIPT",
                Subtitle = subtitle,
                FooterNote = "Thank you for your purchase.",
                SummaryItems = new List<(string Label, string Value)>
                {
                    ("Subtotal", "₱ " + sale.Subtotal.ToString("N2")),
                    ("Tendered", "₱ " + sale.AmountTendered.ToString("N2")),
                    ("Change",   "₱ " + sale.ChangeAmount.ToString("N2"))
                },
                Headers = new List<string> { "Item", "Qty", "Unit Price", "Line Total" }
            };

            foreach (var line in detail.Lines)
            {
                table.Rows.Add(new List<string>
                {
                    BuildItemText(line),
                    line.Quantity.ToString(),
                    line.UnitPrice.ToString("N2"),
                    line.LineTotal.ToString("N2")
                });
            }

            return table;
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