using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TodangMotor.Common;
using TodangMotor.Services;

namespace TodangMotor.Forms
{
    /// <summary>
    /// Popup shown after a successful Stock-In. Lists products whose
    /// UnitCost differs from their current CostPrice and offers to update
    /// those cost prices. Also lists skipped products whose new cost
    /// exceeds the current selling price.
    /// </summary>
    public class CostUpdatePromptForm : ShellForm
    {
        private const int ColBrandW = 140;
        private const int ColCostW = 130;
        private const int ColSellW = 130;

        public CostUpdatePromptForm(
            List<CostComparisonItem> changes,
            List<CostComparisonItem> skipped)
        {
            HeaderTitle = "Update Product Costs?";
            ShowMaximizeButton = false;
            ShowMinimizeButton = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(780, 640);
            MinimumSize = new Size(720, 580);
            BackColor = Theme.Background;

            BuildLayout(changes ?? new List<CostComparisonItem>(),
                        skipped ?? new List<CostComparisonItem>());
        }

        private void BuildLayout(List<CostComparisonItem> changes, List<CostComparisonItem> skipped)
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

            int btnW = 160;
            int btnH = UiFactory.PrimaryButtonHeight;
            const int padX = 32;

            var btnNo = UiFactory.CreateButton("Keep Current", UiFactory.ButtonStyle.Ghost, btnW, btnH);
            btnNo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnNo.Click += (s, e) => { DialogResult = DialogResult.No; Close(); };

            var btnYes = UiFactory.CreateButton("Update Costs", UiFactory.ButtonStyle.Primary, btnW, btnH);
            btnYes.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnYes.Click += (s, e) => { DialogResult = DialogResult.Yes; Close(); };

            footer.Controls.Add(btnNo);
            footer.Controls.Add(btnYes);

            footer.Resize += (s, e) =>
            {
                btnYes.Location = new Point(footer.ClientSize.Width - padX - btnW, 22);
                btnNo.Location = new Point(btnYes.Left - 8 - btnW, 22);
            };

            ContentPanel.Controls.Add(footer);

            // ---------- Scrollable body ----------
            var body = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Theme.Background,
                Padding = new Padding(32, 20, 32, 0)
            };
            ContentPanel.Controls.Add(body);
            body.BringToFront();

            int width = ClientSize.Width - 32 * 2 - 20; // allowance for scrollbar

            var intro = new Label
            {
                Text = "Stock-In saved successfully.\n\n" +
                       "The following product(s) were delivered at a cost that differs " +
                       "from their current Cost Price. Update the product Cost Price to match?",
                Font = Theme.FontBody,
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(32, 20),
                Size = new Size(width, 70),
                TextAlign = ContentAlignment.TopLeft,
                BackColor = Color.Transparent
            };
            body.Controls.Add(intro);

            int y = 106;

            if (changes.Count > 0)
            {
                var lbl = new Label
                {
                    Text = $"WILL BE UPDATED  ({changes.Count})",
                    Font = new Font(Theme.UiFontFamily, 10F, FontStyle.Bold),
                    ForeColor = Theme.Primary,
                    AutoSize = false,
                    Location = new Point(32, y),
                    Size = new Size(width, 24),
                    TextAlign = ContentAlignment.MiddleLeft,
                    BackColor = Color.Transparent
                };
                body.Controls.Add(lbl);
                y += 28;

                var grid = BuildChangeGrid(changes, width, 180);
                grid.Location = new Point(32, y);
                body.Controls.Add(grid);
                y += 180 + 20;
            }

            if (skipped.Count > 0)
            {
                var lbl = new Label
                {
                    Text = $"⚠  SKIPPED — new cost exceeds current selling price  ({skipped.Count})",
                    Font = new Font(Theme.UiFontFamily, 10F, FontStyle.Bold),
                    ForeColor = Theme.Danger,
                    AutoSize = false,
                    Location = new Point(32, y),
                    Size = new Size(width, 24),
                    TextAlign = ContentAlignment.MiddleLeft,
                    BackColor = Color.Transparent
                };
                body.Controls.Add(lbl);
                y += 28;

                var grid = BuildSkipGrid(skipped, width, 160);
                grid.Location = new Point(32, y);
                body.Controls.Add(grid);
            }
        }

        private DataGridView BuildChangeGrid(List<CostComparisonItem> items, int width, int height)
        {
            var grid = new DataGridView
            {
                Size = new Size(width, height),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true
            };
            UiFactory.StyleGridModern(grid);
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            grid.DataSource = items.Select(c => new
            {
                Product = c.ProductName ?? string.Empty,
                Brand = c.Brand ?? string.Empty,
                OldCost = c.CurrentCost.ToString("N2"),
                NewCost = c.NewCost.ToString("N2")
            }).ToList();

            ConfigureColumns(grid,
                ("Product", 300, DataGridViewContentAlignment.MiddleLeft),
                ("Brand", ColBrandW, DataGridViewContentAlignment.MiddleLeft),
                ("OldCost", ColCostW, DataGridViewContentAlignment.MiddleRight),
                ("NewCost", ColCostW, DataGridViewContentAlignment.MiddleRight));

            grid.Columns["OldCost"].HeaderText = "Current Cost (₱)";
            grid.Columns["NewCost"].HeaderText = "New Cost (₱)";

            if (grid.Columns.Contains("Product"))
                grid.Columns["Product"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            grid.ClearSelection();
            return grid;
        }

        private DataGridView BuildSkipGrid(List<CostComparisonItem> items, int width, int height)
        {
            var grid = new DataGridView
            {
                Size = new Size(width, height),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true
            };
            UiFactory.StyleGridModern(grid);
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            grid.DataSource = items.Select(c => new
            {
                Product = c.ProductName ?? string.Empty,
                Brand = c.Brand ?? string.Empty,
                NewCost = c.NewCost.ToString("N2"),
                Selling = c.CurrentSellingPrice.ToString("N2")
            }).ToList();

            ConfigureColumns(grid,
                ("Product", 300, DataGridViewContentAlignment.MiddleLeft),
                ("Brand", ColBrandW, DataGridViewContentAlignment.MiddleLeft),
                ("NewCost", ColCostW, DataGridViewContentAlignment.MiddleRight),
                ("Selling", ColSellW, DataGridViewContentAlignment.MiddleRight));

            grid.Columns["NewCost"].HeaderText = "New Cost (₱)";
            grid.Columns["Selling"].HeaderText = "Selling Price (₱)";

            if (grid.Columns.Contains("Product"))
                grid.Columns["Product"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            grid.ClearSelection();
            return grid;
        }

        private static void ConfigureColumns(
            DataGridView grid,
            params (string name, int width, DataGridViewContentAlignment align)[] specs)
        {
            foreach (var (name, width, align) in specs)
            {
                if (!grid.Columns.Contains(name)) continue;
                grid.Columns[name].Width = width;
                grid.Columns[name].SortMode = DataGridViewColumnSortMode.NotSortable;
                grid.Columns[name].Resizable = DataGridViewTriState.False;
                grid.Columns[name].DefaultCellStyle.Alignment = align;
            }
        }
    }
}