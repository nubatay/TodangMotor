using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TodangMotor.Common;
using TodangMotor.Data;
using TodangMotor.Models;
using TodangMotor.Services;

namespace TodangMotor.Controls
{
    /// <summary>
    /// Reports Hub — tile-based. Owner-only.
    /// From/To date pickers at the top drive all range-dependent reports.
    /// All exports are PDF only.
    /// </summary>
    public class ReportsControl : UserControl
    {
        // ============================================================
        // SERVICES
        // ============================================================

        private readonly SalesService _salesService = new();
        private readonly ProductService _productService = new();
        private readonly CategoryService _categoryService = new(new CategoryRepository());
        private readonly StockMovementService _stockMovementService = new();

        // ============================================================
        // STATE
        // ============================================================

        private bool _isBusy;

        // ============================================================
        // UI CONTROLS
        // ============================================================

        private DateTimePicker _dtpFrom;
        private DateTimePicker _dtpTo;
        private Label _lblStatus;

        // ============================================================
        // CONSTRUCTION
        // ============================================================

        public ReportsControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(Theme.SpacingLg);

            BuildLayout();
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
                RowCount = 3,
                BackColor = Theme.Background,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            root.Controls.Add(BuildDateBar(), 0, 0);
            root.Controls.Add(BuildTilesRow(), 0, 1);
            root.Controls.Add(BuildStatusBar(), 0, 2);

            Controls.Add(root);
        }

        private Panel BuildDateBar()
        {
            var bar = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Background
            };

            var lblFrom = new Label
            {
                Text = "From:",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(46, 20),
                Location = new Point(0, 14),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _dtpFrom = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "MMM d, yyyy",
                Font = Theme.FontBody,
                Location = new Point(48, 6),
                Size = new Size(170, 32),
                MaxDate = DateTime.Today,
                Value = DateTime.Today
            };

            var lblTo = new Label
            {
                Text = "To:",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(28, 20),
                Location = new Point(232, 14),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            _dtpTo = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "MMM d, yyyy",
                Font = Theme.FontBody,
                Location = new Point(262, 6),
                Size = new Size(170, 32),
                MaxDate = DateTime.Today,
                Value = DateTime.Today
            };

            bar.Controls.Add(lblFrom);
            bar.Controls.Add(_dtpFrom);
            bar.Controls.Add(lblTo);
            bar.Controls.Add(_dtpTo);

            return bar;
        }

        private TableLayoutPanel BuildTilesRow()
        {
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Theme.Background,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

            var salesTile = BuildTile(
                "\uE9D9",
                "Sales Report",
                "Revenue, cost, and net income per day for the selected range.",
                async (s, e) => await GenerateSalesReportAsync());

            var movementTile = BuildTile(
                "\uE81C",
                "Movement Report",
                "Every stock movement in the range — sales, deliveries, adjustments, voids.",
                async (s, e) => await GenerateMovementReportAsync());

            var inventoryTile = BuildTile(
                "\uE7B8",
                "Inventory Snapshot",
                "Current stock levels for all active products. Ignores the date range.",
                async (s, e) => await GenerateInventorySnapshotAsync());

            var lowStockTile = BuildTile(
                "\uE7BA",
                "Low Stock Report",
                "All items at or below their reorder level. Ignores the date range.",
                async (s, e) => await GenerateLowStockReportAsync());

            salesTile.Margin = new Padding(Theme.SpacingSm);
            movementTile.Margin = new Padding(Theme.SpacingSm);
            inventoryTile.Margin = new Padding(Theme.SpacingSm);
            lowStockTile.Margin = new Padding(Theme.SpacingSm);

            grid.Controls.Add(salesTile, 0, 0);
            grid.Controls.Add(movementTile, 1, 0);
            grid.Controls.Add(inventoryTile, 0, 1);
            grid.Controls.Add(lowStockTile, 1, 1);

            return grid;
        }

        private Panel BuildStatusBar()
        {
            var bar = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Background
            };

            _lblStatus = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            bar.Controls.Add(_lblStatus);
            return bar;
        }

        private RoundedPanel BuildTile(string icon, string title, string description, EventHandler onClick)
        {
            var tile = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Radius = Theme.RadiusCard,
                BorderColor = Theme.Divider,
                BorderSize = 1,
                BackColor = Theme.Surface,
                Padding = new Padding(28)
            };

            var iconLabel = new Label
            {
                Text = icon,
                Font = new Font(Theme.IconFontFamily, 32F, FontStyle.Regular),
                ForeColor = Theme.Primary,
                AutoSize = false,
                Size = new Size(60, 52),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            var titleLabel = new Label
            {
                Text = title,
                Font = new Font(Theme.UiFontFamily, 16F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Size = new Size(340, 30),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };

            var descLabel = new Label
            {
                Text = description,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(360, 60),
                TextAlign = ContentAlignment.TopLeft,
                BackColor = Color.Transparent
            };

            var btn = UiFactory.CreateButton("Generate PDF", UiFactory.ButtonStyle.Primary, 160, 42);
            btn.Click += onClick;

            tile.Controls.Add(iconLabel);
            tile.Controls.Add(titleLabel);
            tile.Controls.Add(descLabel);
            tile.Controls.Add(btn);

            tile.Resize += (s, e) =>
            {
                int contentW = Math.Max(200, tile.ClientSize.Width - 56);
                int cx = 28;

                iconLabel.Location = new Point(cx, 20);
                titleLabel.Location = new Point(cx + 68, 28);
                titleLabel.Width = Math.Max(120, contentW - 68);
                descLabel.Location = new Point(cx + 68, 60);
                descLabel.Width = Math.Max(120, contentW - 68);
                btn.Location = new Point(cx + 68, Math.Max(126, tile.ClientSize.Height - 70));
            };

            return tile;
        }

        // ============================================================
        // RANGE HELPERS
        // ============================================================

        private (DateTime From, DateTime ToExclusive, string Label) GetSelectedRange()
        {
            DateTime from = _dtpFrom.Value.Date;
            DateTime to = _dtpTo.Value.Date;

            if (to < from) to = from;

            string label = from == to
                ? from.ToString("MMMM d, yyyy")
                : $"{from:MMM d, yyyy} to {to:MMM d, yyyy}";

            return (from, to.AddDays(1), label);
        }

        // ============================================================
        // SALES REPORT
        // ============================================================

        private async Task GenerateSalesReportAsync()
        {
            if (_isBusy) return;

            var (from, toExclusive, rangeLabel) = GetSelectedRange();

            SetBusy(true);
            try
            {
                var result = await _salesService.GenerateRevenueReportAsync(from, toExclusive, rangeLabel);

                if (!result.Success)
                {
                    ShowStatus(result.ErrorMessage, true);
                    MessageBox.Show(result.ErrorMessage, "Report Failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (result.DailyRows.Count == 0)
                {
                    ShowStatus("No sales found in the selected range.", true);
                    MessageBox.Show("No sales found in the selected range.", "No Data",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var table = new ReportTable
                {
                    Title = "Sales Revenue Report",
                    Subtitle = $"{rangeLabel}   ·   Generated {DateTime.Now:MMM d, yyyy h:mm tt}",
                    FooterNote = result.IsCostComplete
                        ? "All rows have complete cost data. Net Income is exact."
                        : $"{result.DaysWithMissingCost} day(s) have incomplete cost data.",
                    SummaryItems = new List<(string Label, string Value)>
                    {
                        ("Total Revenue",  "₱ " + result.TotalRevenue.ToString("N2")),
                        ("Total Cost",     "₱ " + result.TotalCost.ToString("N2")),
                        ("Net Income",     "₱ " + result.TotalNetIncome.ToString("N2")),
                        ("Transactions",   result.TotalTransactions.ToString("N0")),
                        ("Voided (excl.)", result.VoidedTransactions.ToString("N0"))
                    },
                    Headers = new List<string>
                    {
                        "Date", "Transactions", "Revenue (PHP)", "Cost (PHP)", "Net Income (PHP)"
                    }
                };

                foreach (var row in result.DailyRows.OrderByDescending(r => r.SaleDay))
                {
                    string netIncome = row.HasCompleteCost
                        ? row.NetIncome!.Value.ToString("N2")
                        : "—";

                    table.Rows.Add(new List<string>
                    {
                        row.SaleDay.ToString("MMM d, yyyy"),
                        row.TransactionCount.ToString(),
                        row.Revenue.ToString("N2"),
                        row.HasCompleteCost ? row.KnownCost.ToString("N2") : "—",
                        netIncome
                    });
                }

                await SaveAndReportAsync(table, "SalesReport");
            }
            finally
            {
                SetBusy(false);
            }
        }

        // ============================================================
        // MOVEMENT REPORT
        // ============================================================

        private async Task GenerateMovementReportAsync()
        {
            if (_isBusy) return;

            var (from, toExclusive, rangeLabel) = GetSelectedRange();

            SetBusy(true);
            try
            {
                var (success, error, movements) =
                    await _stockMovementService.GetFilteredAsync(from, toExclusive, null, null);

                if (!success)
                {
                    ShowStatus(error, true);
                    MessageBox.Show(error, "Report Failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (movements.Count == 0)
                {
                    ShowStatus("No stock movements in the selected range.", true);
                    MessageBox.Show("No stock movements in the selected range.", "No Data",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var table = new ReportTable
                {
                    Title = "Stock Movement Report",
                    Subtitle = $"{rangeLabel}   ·   Generated {DateTime.Now:MMM d, yyyy h:mm tt}",
                    FooterNote = $"{movements.Count} movement(s) recorded",
                    SummaryItems = new List<(string Label, string Value)>
                    {
                        ("Movements",  movements.Count.ToString("N0")),
                        ("Sales",      movements.Count(m => m.MovementType == "Sale").ToString("N0")),
                        ("Stock-Ins",  movements.Count(m => m.MovementType == "StockIn").ToString("N0")),
                        ("Adjustments", movements.Count(m => m.MovementType == "Adjustment").ToString("N0"))
                    },
                    Headers = new List<string>
                    {
                        "Date & Time", "Item", "Type", "Stock In", "Stock Out", "Start Qty", "End Qty"
                    }
                };

                foreach (var m in movements.OrderByDescending(x => x.MovementDate))
                {
                    string item = string.IsNullOrWhiteSpace(m.ProductBrand)
                        ? (m.ProductName ?? "(unknown item)")
                        : $"{m.ProductName} — {m.ProductBrand}";

                    string typeLabel = m.MovementType switch
                    {
                        "StockIn" => "Stock-In",
                        "Sale" => "Sale",
                        "Adjustment" => "Adjustment",
                        "Void" => "Void",
                        "Initial" => "Initial",
                        _ => m.MovementType
                    };

                    table.Rows.Add(new List<string>
                    {
                        m.MovementDate.ToString("MMM d, yyyy · h:mm tt"),
                        item,
                        typeLabel,
                        m.QuantityChange > 0 ? $"+{m.QuantityChange}" : "",
                        m.QuantityChange < 0 ? $"{m.QuantityChange}" : "",
                        m.QuantityBefore.ToString(),
                        m.QuantityAfter.ToString()
                    });
                }

                await SaveAndReportAsync(table, "StockMovement");
            }
            finally
            {
                SetBusy(false);
            }
        }

        // ============================================================
        // INVENTORY SNAPSHOT
        // ============================================================

        private async Task GenerateInventorySnapshotAsync()
        {
            if (_isBusy) return;

            SetBusy(true);
            try
            {
                var (prodOk, prodErr, products) = await _productService.GetAllAsync();
                if (!prodOk)
                {
                    ShowStatus(prodErr, true);
                    return;
                }

                var categories = (await _categoryService.GetAllCategoriesAsync())?.ToList()
                    ?? new List<Category>();
                var catById = categories.ToDictionary(c => c.CategoryId);

                var active = products.Where(p => p.IsActive).ToList();

                if (active.Count == 0)
                {
                    ShowStatus("No active items to report.", true);
                    MessageBox.Show("No active items to report.", "No Data",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var table = new ReportTable
                {
                    Title = "Inventory Snapshot",
                    Subtitle = $"Generated {DateTime.Now:MMM d, yyyy h:mm tt}",
                    FooterNote = $"{active.Count} active item(s)",
                    SummaryItems = new List<(string Label, string Value)>
                    {
                        ("Total Items",   active.Count.ToString("N0")),
                        ("OK",            active.Count(p => p.QuantityOnHand > p.ReorderLevel).ToString("N0")),
                        ("Low",           active.Count(p => p.QuantityOnHand > 0 && p.QuantityOnHand <= p.ReorderLevel).ToString("N0")),
                        ("Out of Stock",  active.Count(p => p.QuantityOnHand == 0).ToString("N0"))
                    },
                    Headers = new List<string>
                    {
                        "Product", "Brand", "Category", "Unit", "On Hand", "Reorder At", "Status"
                    }
                };

                foreach (var p in active.OrderBy(p => p.ProductName).ThenBy(p => p.Brand))
                {
                    string category = catById.TryGetValue(p.CategoryId, out var c) ? c.CategoryName : "—";
                    string status = p.QuantityOnHand == 0
                        ? "Out of stock"
                        : (p.QuantityOnHand <= p.ReorderLevel ? "Low" : "OK");

                    table.Rows.Add(new List<string>
                    {
                        p.ProductName ?? string.Empty,
                        p.Brand ?? string.Empty,
                        category,
                        p.Unit ?? string.Empty,
                        p.QuantityOnHand.ToString(),
                        p.ReorderLevel.ToString(),
                        status
                    });
                }

                await SaveAndReportAsync(table, "InventorySnapshot");
            }
            finally
            {
                SetBusy(false);
            }
        }

        // ============================================================
        // LOW STOCK REPORT
        // ============================================================

        private async Task GenerateLowStockReportAsync()
        {
            if (_isBusy) return;

            SetBusy(true);
            try
            {
                var (prodOk, prodErr, products) = await _productService.GetAllAsync();
                if (!prodOk)
                {
                    ShowStatus(prodErr, true);
                    return;
                }

                var categories = (await _categoryService.GetAllCategoriesAsync())?.ToList()
                    ?? new List<Category>();
                var catById = categories.ToDictionary(c => c.CategoryId);

                var low = products
                    .Where(p => p.IsActive && p.QuantityOnHand <= p.ReorderLevel)
                    .OrderBy(p => p.QuantityOnHand)
                    .ThenBy(p => p.ProductName)
                    .ToList();

                if (low.Count == 0)
                {
                    ShowStatus("No items are currently low in stock.", false);
                    MessageBox.Show("No items are currently low in stock.", "No Data",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                int outCount = low.Count(p => p.QuantityOnHand == 0);
                int lowOnly = low.Count - outCount;

                var table = new ReportTable
                {
                    Title = "Low Stock Report",
                    Subtitle = $"Generated {DateTime.Now:MMM d, yyyy h:mm tt}",
                    FooterNote = $"{low.Count} item(s) at or below reorder level",
                    SummaryItems = new List<(string Label, string Value)>
                    {
                        ("Total",         low.Count.ToString("N0")),
                        ("Out of Stock",  outCount.ToString("N0")),
                        ("Low Stock",     lowOnly.ToString("N0"))
                    },
                    Headers = new List<string>
                    {
                        "Product", "Brand", "Category", "Unit", "On Hand", "Reorder At", "Status"
                    }
                };

                foreach (var p in low)
                {
                    string category = catById.TryGetValue(p.CategoryId, out var c) ? c.CategoryName : "—";
                    string status = p.QuantityOnHand == 0 ? "Out of stock" : "Low";

                    table.Rows.Add(new List<string>
                    {
                        p.ProductName ?? string.Empty,
                        p.Brand ?? string.Empty,
                        category,
                        p.Unit ?? string.Empty,
                        p.QuantityOnHand.ToString(),
                        p.ReorderLevel.ToString(),
                        status
                    });
                }

                await SaveAndReportAsync(table, "LowStock");
            }
            finally
            {
                SetBusy(false);
            }
        }

        // ============================================================
        // SAVE DIALOG + EXPORT
        // ============================================================

        private async Task SaveAndReportAsync(ReportTable table, string filePrefix)
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                DefaultExt = ".pdf",
                AddExtension = true,
                FileName = $"{filePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf",
                InitialDirectory = GetDefaultReportFolder()
            };

            if (sfd.ShowDialog(FindForm()) != DialogResult.OK)
            {
                ShowStatus("Report cancelled.", false);
                return;
            }

            string path = sfd.FileName;
            await Task.Run(() => ReportService.ExportPdf(table, path));

            var openIt = MessageBox.Show(
                $"PDF saved to:\n{path}\n\nOpen the folder?",
                "Report Saved",
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

            ShowStatus($"Saved: {Path.GetFileName(path)}", false);
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
            _dtpFrom.Enabled = !busy;
            _dtpTo.Enabled = !busy;
        }

        private void ShowStatus(string message, bool isError)
        {
            if (_lblStatus == null) return;
            _lblStatus.ForeColor = isError ? Theme.Danger : Theme.Success;
            _lblStatus.Text = message ?? string.Empty;
        }
    }
}