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
    /// Shows one PO with its line items and offers a "Generate PDF" action.
    /// </summary>
    public class PurchaseOrderDetailForm : ShellForm
    {
        private readonly PurchaseOrderService _poService = new();
        private readonly int _purchaseOrderId;

        private PurchaseOrderDetail? _detail;
        private bool _isBusy;

        // ---- Header ----
        private Label _lblPoNumber = null!;
        private Label _metaDateValue = null!;
        private Label _metaSupplierValue = null!;
        private Label _lblNotes = null!;

        // ---- Grid ----
        private DataGridView _linesGrid = null!;

        // ---- Totals ----
        private Label _lblItemCount = null!;

        // ---- Footer ----
        private Button _btnExportPdf = null!;
        private Button _btnClose = null!;
        private Label _lblStatus = null!;

        private const int ColBrandW = 160;
        private const int ColUnitW = 80;
        private const int ColQtyW = 90;

        public PurchaseOrderDetailForm(int purchaseOrderId)
        {
            _purchaseOrderId = purchaseOrderId;

            HeaderTitle = "Purchase Order Detail";
            ShowMaximizeButton = false;
            ShowMinimizeButton = true;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(820, 760);
            MinimumSize = new Size(760, 700);
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
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Theme.Background,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 200)); // header info
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // grid
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));  // totals
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

            const int contentW = 740;

            var caption = new Label
            {
                Text = "PURCHASE ORDER",
                Font = new Font(Theme.UiFontFamily, 9.5F, FontStyle.Bold),
                ForeColor = Theme.TextMuted,
                AutoSize = false,
                Location = new Point(32, 16),
                Size = new Size(contentW, 18),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _lblPoNumber = new Label
            {
                Text = "—",
                Font = new Font(Theme.UiFontFamily, 20F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(32, 36),
                Size = new Size(contentW, 40),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            var divider = new Panel
            {
                Location = new Point(32, 88),
                Size = new Size(contentW, 1),
                BackColor = Theme.Divider
            };

            var meta = new TableLayoutPanel
            {
                ColumnCount = 4,
                RowCount = 1,
                Location = new Point(32, 100),
                Size = new Size(contentW, 32),
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            meta.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            meta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            meta.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            meta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            meta.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

            _metaDateValue = MakeMetaValue();
            _metaSupplierValue = MakeMetaValue();

            meta.Controls.Add(MakeMetaLabel("Order Date"), 0, 0);
            meta.Controls.Add(_metaDateValue, 1, 0);
            meta.Controls.Add(MakeMetaLabel("Supplier"), 2, 0);
            meta.Controls.Add(_metaSupplierValue, 3, 0);

            _lblNotes = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(32, 140),
                Size = new Size(contentW, 40),
                TextAlign = ContentAlignment.TopLeft,
                BackColor = Color.Transparent
            };

            block.Controls.Add(caption);
            block.Controls.Add(_lblPoNumber);
            block.Controls.Add(divider);
            block.Controls.Add(meta);
            block.Controls.Add(_lblNotes);

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
            Margin = new Padding(0)
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
            _linesGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            PrimeLinesColumns();

            _linesGrid.Resize += (s, e) => FitProductColumn();

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
                Font = new Font(Theme.UiFontFamily, 13F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(32, 8),
                Size = new Size(740, 40),
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

            int btnW = 160;
            int btnH = UiFactory.PrimaryButtonHeight;

            _lblStatus = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(32, 26),
                Height = btnH,
                Width = 300,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _btnExportPdf = UiFactory.CreateButton(
                "Generate PDF",
                UiFactory.ButtonStyle.Primary,
                btnW, btnH);
            _btnExportPdf.Location = new Point(360, 26);
            _btnExportPdf.Click += async (s, e) => await ExportPdfAsync();

            _btnClose = UiFactory.CreateButton(
                "Close",
                UiFactory.ButtonStyle.Ghost,
                120, btnH);
            _btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnClose.Click += (s, e) => Close();

            footer.Controls.Add(_lblStatus);
            footer.Controls.Add(_btnExportPdf);
            footer.Controls.Add(_btnClose);

            footer.Resize += (s, e) =>
            {
                _btnClose.Location = new Point(footer.ClientSize.Width - 32 - _btnClose.Width, 26);
                _btnExportPdf.Location = new Point(_btnClose.Left - 8 - _btnExportPdf.Width, 26);
                _lblStatus.Width = Math.Max(100, _btnExportPdf.Left - _lblStatus.Left - 16);
            };

            return footer;
        }

        private void PrimeLinesColumns()
        {
            _linesGrid.AutoGenerateColumns = false;
            _linesGrid.Columns.Clear();

            void AddCol(string prop, string header, int width,
                DataGridViewContentAlignment align = DataGridViewContentAlignment.MiddleLeft)
            {
                var col = new DataGridViewTextBoxColumn
                {
                    Name = prop,
                    DataPropertyName = prop,
                    HeaderText = header,
                    Width = width,
                    ReadOnly = true,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                    Resizable = DataGridViewTriState.False
                };
                if (align != DataGridViewContentAlignment.MiddleLeft)
                    col.DefaultCellStyle.Alignment = align;
                _linesGrid.Columns.Add(col);
            }

            AddCol("ProductName", "Product", 400);
            AddCol("ProductBrand", "Brand", ColBrandW);
            AddCol("ProductUnit", "Unit", ColUnitW, DataGridViewContentAlignment.MiddleCenter);
            AddCol("Quantity", "Qty", ColQtyW, DataGridViewContentAlignment.MiddleRight);
        }

        private void FitProductColumn()
        {
            if (_linesGrid == null || !_linesGrid.Columns.Contains("ProductName")) return;

            int others = ColBrandW + ColUnitW + ColQtyW;
            int available = _linesGrid.ClientSize.Width - others - 24;
            if (available < 220) available = 220;

            _linesGrid.Columns["ProductName"].Width = available;
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
            _metaDateValue.Text = order.OrderDate.ToString("MMMM d, yyyy");
            _metaSupplierValue.Text = order.SupplierName ?? "—";

            _lblNotes.Text = string.IsNullOrWhiteSpace(order.Notes)
                ? string.Empty
                : $"Notes:  {order.Notes}";

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
            _linesGrid.ClearSelection();

            int totalQty = _detail.Items.Sum(i => i.Quantity);
            _lblItemCount.Text =
                $"{_detail.Items.Count} item(s)   ·   {totalQty} total unit(s)";

            FitProductColumn();
        }

        // ============================================================
        // EXPORT PDF
        // ============================================================

        private async Task ExportPdfAsync()
        {
            if (_detail == null)
            {
                _lblStatus.ForeColor = Theme.Danger;
                _lblStatus.Text = "Still loading. Please wait a moment.";
                return;
            }

            if (_isBusy) return;

            string suggestedName;
            try
            {
                string poNo = _detail.Order.PONumber ?? string.Empty;
                if (string.IsNullOrWhiteSpace(poNo))
                    poNo = $"PO-{_detail.Order.PurchaseOrderId}";
                suggestedName = $"{poNo}.pdf";
            }
            catch
            {
                suggestedName = "PurchaseOrder.pdf";
            }

            using var sfd = new SaveFileDialog
            {
                Title = "Save Purchase Order PDF",
                Filter = "PDF files (*.pdf)|*.pdf",
                DefaultExt = ".pdf",
                AddExtension = true,
                OverwritePrompt = true,
                FileName = suggestedName,
                InitialDirectory = GetDefaultReportFolder()
            };

            var parent = FindForm();
            var result = parent != null ? sfd.ShowDialog(parent) : sfd.ShowDialog();
            if (result != DialogResult.OK) return;

            string path = sfd.FileName;

            SetBusy(true);
            _lblStatus.ForeColor = Theme.TextSecondary;
            _lblStatus.Text = "Generating PDF…";
            try
            {
                var table = BuildReportTable(_detail);
                await Task.Run(() => ReportService.ExportPdf(table, path));

                if (!File.Exists(path))
                {
                    _lblStatus.ForeColor = Theme.Danger;
                    _lblStatus.Text = "File was not created.";
                    return;
                }

                _lblStatus.ForeColor = Theme.Success;
                _lblStatus.Text = $"Saved: {Path.GetFileName(path)}";

                var openIt = MessageBox.Show(
                    $"PDF saved successfully.\n\nLocation: {path}\n\nOpen the folder?",
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
                _lblStatus.ForeColor = Theme.Danger;
                _lblStatus.Text = "PDF export failed.";
                MessageBox.Show(
                    "Could not generate PDF.\n\n" + ex.Message,
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

        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            _btnExportPdf.Enabled = !busy;
            _btnClose.Enabled = !busy;
        }
    }
}