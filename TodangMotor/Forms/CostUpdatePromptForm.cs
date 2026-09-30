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
        public CostUpdatePromptForm(
            List<CostComparisonItem> changes,
            List<CostComparisonItem> skipped)
        {
            HeaderTitle = "Update Product Costs?";
            ShowMaximizeButton = false;
            ShowMinimizeButton = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(720, 600);
            MinimumSize = new Size(660, 520);
            BackColor = Theme.Background;

            BuildLayout(changes ?? new List<CostComparisonItem>(),
                        skipped ?? new List<CostComparisonItem>());
        }

        private void BuildLayout(List<CostComparisonItem> changes, List<CostComparisonItem> skipped)
        {
            // ---- Footer ----
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 76,
                BackColor = Theme.Background
            };

            const int btnW = 160;
            const int btnH = 44;
            const int padX = 24;

            var btnNo = UiFactory.CreateButton("Keep Current", UiFactory.ButtonStyle.Ghost, btnW, btnH);
            btnNo.Location = new Point(0, 16);
            btnNo.Click += (s, e) => { DialogResult = DialogResult.No; Close(); };

            var btnYes = UiFactory.CreateButton("Update Costs", UiFactory.ButtonStyle.Primary, btnW, btnH);
            btnYes.Location = new Point(0, 16);
            btnYes.Click += (s, e) => { DialogResult = DialogResult.Yes; Close(); };

            footer.Controls.Add(btnNo);
            footer.Controls.Add(btnYes);

            footer.Resize += (s, e) =>
            {
                btnYes.Location = new Point(footer.ClientSize.Width - padX - btnW, 16);
                btnNo.Location = new Point(footer.ClientSize.Width - padX - btnW - 8 - btnW, 16);
            };

            ContentPanel.Controls.Add(footer);

            // ---- Scrollable body ----
            var body = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Theme.Background
            };
            ContentPanel.Controls.Add(body);
            body.BringToFront();

            int left = 24;
            int width = ClientSize.Width - left * 2;

            var intro = new Label
            {
                Text = "Stock-In saved successfully.\n\n" +
                       "The following product(s) were delivered at a cost that differs " +
                       "from their current Cost Price. Update the product Cost Price to match?",
                Font = Theme.FontBody,
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Location = new Point(left, 16),
                Size = new Size(width, 68),
                TextAlign = ContentAlignment.TopLeft,
                BackColor = Color.Transparent
            };
            body.Controls.Add(intro);

            int y = 92;

            if (changes.Count > 0)
            {
                var lbl = new Label
                {
                    Text = $"Will be updated ({changes.Count}):",
                    Font = Theme.FontBodyBold,
                    ForeColor = Theme.TextPrimary,
                    AutoSize = false,
                    Location = new Point(left, y),
                    Size = new Size(width, 22),
                    TextAlign = ContentAlignment.MiddleLeft,
                    BackColor = Color.Transparent
                };
                body.Controls.Add(lbl);
                y += 28;

                var grid = BuildChangeGrid(changes);
                grid.Location = new Point(left, y);
                grid.Size = new Size(width, 160);
                body.Controls.Add(grid);
                y += 180;
            }

            if (skipped.Count > 0)
            {
                var lbl = new Label
                {
                    Text = $"⚠ Skipped — new cost exceeds current selling price ({skipped.Count}):",
                    Font = Theme.FontBodyBold,
                    ForeColor = Theme.Danger,
                    AutoSize = false,
                    Location = new Point(left, y),
                    Size = new Size(width, 22),
                    TextAlign = ContentAlignment.MiddleLeft,
                    BackColor = Color.Transparent
                };
                body.Controls.Add(lbl);
                y += 28;

                var grid = BuildSkipGrid(skipped);
                grid.Location = new Point(left, y);
                grid.Size = new Size(width, 140);
                body.Controls.Add(grid);
            }
        }

        private DataGridView BuildChangeGrid(List<CostComparisonItem> items)
        {
            var grid = new DataGridView();
            UiFactory.StyleGrid(grid);

            grid.DataSource = items.Select(c => new
            {
                Product = c.ProductName,
                Brand = c.Brand,
                OldCost = c.CurrentCost.ToString("N2"),
                NewCost = c.NewCost.ToString("N2")
            }).ToList();

            if (grid.Columns.Contains("Product")) grid.Columns["Product"].HeaderText = "Product";
            if (grid.Columns.Contains("Brand")) grid.Columns["Brand"].HeaderText = "Brand";
            if (grid.Columns.Contains("OldCost")) grid.Columns["OldCost"].HeaderText = "Current Cost";
            if (grid.Columns.Contains("NewCost")) grid.Columns["NewCost"].HeaderText = "New Cost";

            grid.ReadOnly = true;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            return grid;
        }

        private DataGridView BuildSkipGrid(List<CostComparisonItem> items)
        {
            var grid = new DataGridView();
            UiFactory.StyleGrid(grid);

            grid.DataSource = items.Select(c => new
            {
                Product = c.ProductName,
                Brand = c.Brand,
                NewCost = c.NewCost.ToString("N2"),
                Selling = c.CurrentSellingPrice.ToString("N2")
            }).ToList();

            if (grid.Columns.Contains("Product")) grid.Columns["Product"].HeaderText = "Product";
            if (grid.Columns.Contains("Brand")) grid.Columns["Brand"].HeaderText = "Brand";
            if (grid.Columns.Contains("NewCost")) grid.Columns["NewCost"].HeaderText = "New Cost";
            if (grid.Columns.Contains("Selling")) grid.Columns["Selling"].HeaderText = "Selling Price";

            grid.ReadOnly = true;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            return grid;
        }
    }
}