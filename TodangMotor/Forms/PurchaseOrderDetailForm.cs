using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TodangMotor.Common;
using TodangMotor.Services;

namespace TodangMotor.Forms
{
    /// <summary>
    /// Purchase Order detail popup. View-only.
    /// Shows one PO with its line items and offers a "Generate PDF" action
    /// so the Owner can re-export an old PO if needed.
    /// </summary>
    public class PurchaseOrderDetailForm : ShellForm
    {
        // ============================================================
        // SERVICES
        // ============================================================

        private readonly PurchaseOrderService _poService = new();
        private readonly int _purchaseOrderId;

        // ============================================================
        // STATE
        // ============================================================

        private PurchaseOrderDetail? _detail;
        private bool _isBusy;

        // ============================================================
        // UI CONTROLS
        // ============================================================

        private Label _lblPoNumber;
        private Label _lblMeta;
        private Label _lblNotes;
        private DataGridView _linesGrid;
        private Label _lblItemCount;
        private Button _btnExportPdf;
        private Button _btnClose;

        // ============================================================
        // CONSTRUCTION
        // ============================================================

        public PurchaseOrderDetailForm(int purchaseOrderId)
        {
            _purchaseOrderId = purchaseOrderId;

            HeaderTitle = "Purchase Order Detail";
            ShowMaximizeButton = false;
            ShowMinimizeButton = true;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(720, 720);
            MinimumSize = new Size(680, 640);
            BackColor = Theme.Background;
            KeyPreview = true;

            BuildLayout();

            Load += PurchaseOrderDetailForm_Load;
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
            // Root: 4 rows — info / grid / totals / footer
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Theme.Background,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 168)); // header info
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // grid
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));  // item count
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

            _lblPoNumber = new Label
            {
                Text = "Loading…",
                Font = new Font(Theme.UiFontFamily, 20F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(32, 16),
                Size = new Size(640, 36),
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
                Size = new Size(640, 44),
                TextAlign = ContentAlignment.TopLeft,
                BackColor = Color.Transparent
            };

            _lblNotes = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextMuted,
                AutoSize = false,
                Location = new Point(32, 106),
                Size = new Size(640, 44),
                TextAlign = ContentAlignment.TopLeft,
                BackColor = Color.Transparent
            };

            block.Controls.Add(_lblPoNumber);
            block.Controls.Add(_lblMeta);
            block.Controls.Add(_lblNotes);

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

            _lblItemCount = new Label
            {
                Text = "—",
                Font = new Font(Theme.UiFontFamily, 12F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(32, 4),
                Size = new Size(640, 40),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            block.Controls.Add(_lblItemCount);

            return block;
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

            const int btnW = 160;
            const int btnH = 44;

            _btnExportPdf = UiFactory.CreateButton(
                "Generate PDF",
                UiFactory.ButtonStyle.Primary,
                btnW, btnH);
            _btnExportPdf.Location = new Point(32, 22);
            _btnExportPdf.Click += async (s, e) => await ExportPdfAsync();

            _btnClose = UiFactory.CreateButton(
                "Close",
                UiFactory.ButtonStyle.Ghost,
                btnW, btnH);
            _btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnClose.Click += (s, e) => Close();

            footer.Controls.Add(_btnExportPdf);
            footer.Controls.Add(_btnClose);

            footer.Resize += (s, e) =>
            {
                _btnClose.Location = new Point(
                    footer.ClientSize.Width - 32 - btnW, 22);
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

            AddCol("ProductName", "Product", 300);
            AddCol("ProductBrand", "Brand", 160);
            AddCol("ProductUnit", "Unit", 80);
            AddCol("Quantity", "Qty", 90, true);
        }

        // ============================================================
        // LOAD
        // ============================================================

        private async void PurchaseOrderDetailForm_Load(object? sender, EventArgs e)
        {
            Animator.SlideFadeInForm(this, 180, 12);
            await LoadDetailAsync();
        }

        private async Task LoadDetailAsync()
        {
            var (success, error, detail) = await _poService.GetDetailAsync(_purchaseOrderId);

            if (!success || detail == null)
            {
                MessageBox.Show(
                    error ?? "Could not load the Purchase Order.",
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
            var order = _detail.Order;

            _lblPoNumber.Text = order.PONumber ?? "—";

            _lblMeta.Text =
                $"Order Date:    {order.OrderDate:MMMM d, yyyy}\n" +
                $"Supplier:      {order.SupplierName ?? "—"}";

            _lblNotes.Text = string.IsNullOrWhiteSpace(order.Notes)
                ? string.Empty
                : $"Notes:  {order.Notes}";

            // Line items
            var rows = _detail.Items
                .Select(i => new
                {
                    ProductName = i.ProductName ?? "(unknown)",
                    ProductBrand = i.ProductBrand ?? string.Empty,
                    ProductUnit = i.ProductUnit ?? string.Empty,
                    Quantity = i.Quantity
                })
                .ToList();

            _linesGrid.DataSource = rows;

            int totalQty = _detail.Items.Sum(i => i.Quantity);
            _lblItemCount.Text =
                $"{_detail.Items.Count} item(s)   ·   {totalQty} total unit(s)";
        }

        // ============================================================
        // EXPORT PDF
        // ============================================================

        private async Task ExportPdfAsync()
        {
            if (_isBusy || _detail == null) return;

            using var sfd = new SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                DefaultExt = ".pdf",
                AddExtension = true,
                FileName = $"{_detail.Order.PONumber}.pdf",
                InitialDirectory = GetDefaultReportFolder()
            };

            if (sfd.ShowDialog(FindForm()) != DialogResult.OK) return;

            SetBusy(true);
            try
            {
                string path = sfd.FileName;
                var table = BuildReportTable(_detail);

                await Task.Run(() => ReportService.ExportPdf(table, path));

                var openIt = MessageBox.Show(
                    $"PDF saved to:\n{path}\n\nOpen the folder?",
                    "PDF Saved",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (openIt == DialogResult.Yes)
                {
                    try
                    {
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
                    }
                    catch { /* ignore */ }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not generate PDF:\n\n{ex.Message}",
                    "PDF Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        // ============================================================
        // SHARED REPORT TABLE BUILDER
        // ============================================================

        /// <summary>
        /// Builds the ReportTable for a PO — used both here and by
        /// PurchaseOrdersControl after saving a new PO.
        /// </summary>
        public static ReportTable BuildReportTable(PurchaseOrderDetail detail)
        {
            var order = detail.Order;

            var table = new ReportTable
            {
                Title = "PURCHASE ORDER",
                Subtitle = $"{order.PONumber}   ·   " +
                           $"Order Date: {order.OrderDate:MMMM d, yyyy}   ·   " +
                           $"Generated {DateTime.Now:MMM d, yyyy h:mm tt}",
                FooterNote = string.IsNullOrWhiteSpace(order.Notes)
                    ? string.Empty
                    : $"Notes: {order.Notes}",
                SummaryItems = new List<(string Label, string Value)>
                {
                    ("Supplier", order.SupplierName ?? "—"),
                    ("Items",    detail.Items.Count.ToString("N0")),
                    ("Total Qty", detail.Items.Sum(i => i.Quantity).ToString("N0"))
                },
                Headers = new List<string> { "Product", "Brand", "Unit", "Qty" }
            };

            foreach (var item in detail.Items)
            {
                table.Rows.Add(new List<string>
                {
                    item.ProductName ?? string.Empty,
                    item.ProductBrand ?? string.Empty,
                    item.ProductUnit ?? string.Empty,
                    item.Quantity.ToString()
                });
            }

            return table;
        }

        private static string GetDefaultReportFolder()
        {
            try
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string folder = Path.Combine(docs, "TodangMotor", "Reports");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);
                return folder;
            }
            catch
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            _btnExportPdf.Enabled = !busy;
            _btnClose.Enabled = !busy;
        }
    }
}