using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using TodangMotor.Common;
using TodangMotor.Services;

namespace TodangMotor.Controls
{
    /// <summary>
    /// Custom bar chart — draws a series of vertical bars using GDI+.
    /// No NuGet library. Matches the app theme.
    /// </summary>
    public class SalesBarChart : Control
    {
        private List<ChartBucket> _buckets = new();
        private string _emptyMessage = "No data for this period.";

        private int _hoverIndex = -1;

        private const int TopPadding = 24;
        private const int BottomPadding = 34;
        private const int SidePadding = 10;
        private const int BarGapPx = 6;

        public event Action<string>? BarClicked;

        public SalesBarChart()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            DoubleBuffered = true;

            MouseMove += (s, e) => UpdateHover(e.X, e.Y);
            MouseLeave += (s, e) => { _hoverIndex = -1; Invalidate(); };
            MouseClick += (s, e) =>
            {
                int index = ComputeHoverIndex(e.X, e.Y);
                if (index >= 0 && index < _buckets.Count)
                    BarClicked?.Invoke(_buckets[index].Label);
            };
        }

        // ============================================================
        // PUBLIC API
        // ============================================================

        public void SetData(List<ChartBucket>? buckets)
        {
            _buckets = buckets ?? new List<ChartBucket>();
            _hoverIndex = -1;
            Invalidate();
        }

        public void SetEmptyMessage(string message)
        {
            _emptyMessage = string.IsNullOrEmpty(message) ? "No data." : message;
            Invalidate();
        }

        // ============================================================
        // PAINT
        // ============================================================

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            e.Graphics.Clear(Parent?.BackColor ?? Theme.Surface);

            if (_buckets == null || _buckets.Count == 0)
            {
                DrawEmptyState(e.Graphics);
                return;
            }

            int chartWidth = Width - SidePadding * 2;
            int chartHeight = Height - TopPadding - BottomPadding;

            if (chartWidth <= 20 || chartHeight <= 20) return;

            decimal maxValue = _buckets.Max(b => b.Value);

            using (var gridPen = new Pen(Theme.Divider, 1))
            {
                int baselineY = TopPadding + chartHeight;
                e.Graphics.DrawLine(gridPen,
                    SidePadding, baselineY,
                    SidePadding + chartWidth, baselineY);
            }

            int n = _buckets.Count;
            int totalGaps = BarGapPx * (n - 1);
            float barWidth = (chartWidth - totalGaps) / (float)n;
            if (barWidth < 4) barWidth = 4;

            // ---------- Decide label density ----------
            // Measure the widest label so we know how many pixels each one needs.
            using var labelFont = new Font(Theme.UiFontFamily, 9.5F, FontStyle.Regular);
            using var labelBrush = new SolidBrush(Theme.TextMuted);
            using var labelHoverBrush = new SolidBrush(Theme.Primary);

            float maxLabelWidth = 0f;
            foreach (var b in _buckets)
            {
                if (string.IsNullOrEmpty(b.Label)) continue;
                var s = e.Graphics.MeasureString(b.Label, labelFont);
                if (s.Width > maxLabelWidth) maxLabelWidth = s.Width;
            }

            // Minimum horizontal gap between two drawn labels.
            const float minLabelSpacing = 12f;
            float slotWidth = barWidth + BarGapPx;

            int labelStep = 1;
            if (maxLabelWidth > 0)
            {
                labelStep = (int)Math.Ceiling((maxLabelWidth + minLabelSpacing) / slotWidth);
                if (labelStep < 1) labelStep = 1;
            }

            // ---------- Draw bars + labels ----------
            for (int i = 0; i < n; i++)
            {
                var bucket = _buckets[i];

                float x = SidePadding + i * (barWidth + BarGapPx);
                float heightRatio = maxValue > 0 ? (float)(bucket.Value / maxValue) : 0f;
                float barH = chartHeight * heightRatio;

                if (bucket.Value > 0 && barH < 3) barH = 3;
                if (bucket.Value == 0) barH = 0;

                float y = TopPadding + chartHeight - barH;
                var rect = new RectangleF(x, y, barWidth, barH);

                Color barColor = (i == _hoverIndex)
                    ? Theme.PrimaryPressed
                    : Theme.Primary;

                if (barH > 0)
                {
                    using var path = GetTopRoundedRect(rect, Math.Min(6, (int)(barWidth / 2)));
                    using var brush = new SolidBrush(barColor);
                    e.Graphics.FillPath(brush, path);
                }

                // Hovered bar value above the bar.
                if (i == _hoverIndex && bucket.Value > 0)
                {
                    string valueText = "₱" + bucket.Value.ToString("N0");
                    using var valueFont = new Font(Theme.UiFontFamily, 9.5F, FontStyle.Bold);
                    var textSize = e.Graphics.MeasureString(valueText, valueFont);
                    float textX = x + (barWidth - textSize.Width) / 2f;
                    float textY = Math.Max(2, y - textSize.Height - 4);

                    using var valueBrush = new SolidBrush(Theme.TextPrimary);
                    e.Graphics.DrawString(valueText, valueFont, valueBrush, textX, textY);
                }

                // ---------- Label below the baseline ----------
                // Only draw when this is a "shown" index (step-based) OR it's hovered.
                bool isHovered = (i == _hoverIndex);
                bool shouldShowLabel =
                    isHovered ||
                    (i % labelStep == 0) ||
                    (i == n - 1 && (n - 1) % labelStep != 0 && slotWidth * (n - 1 - (n - 1) % labelStep) > 0 ? false : false);

                // Simpler rule: always show first and last; otherwise follow the step.
                shouldShowLabel = isHovered || (i % labelStep == 0);

                if (!shouldShowLabel) continue;

                var labelSize = e.Graphics.MeasureString(bucket.Label, labelFont);
                float labelX = x + (barWidth - labelSize.Width) / 2f;
                float labelY = TopPadding + chartHeight + 10;

                var rowLabelBrush = isHovered ? labelHoverBrush : labelBrush;
                e.Graphics.DrawString(bucket.Label, labelFont, rowLabelBrush, labelX, labelY);
            }

            // Y-axis max value — small label at top-left.
            if (maxValue > 0)
            {
                using var maxFont = new Font(Theme.UiFontFamily, 9F, FontStyle.Regular);
                using var maxBrush = new SolidBrush(Theme.TextMuted);
                e.Graphics.DrawString(
                    "₱" + maxValue.ToString("N0"),
                    maxFont, maxBrush,
                    SidePadding, 2);
            }
        }

        private void DrawEmptyState(Graphics g)
        {
            using var font = new Font(Theme.UiFontFamily, 10F, FontStyle.Regular);
            using var brush = new SolidBrush(Theme.TextMuted);

            var size = g.MeasureString(_emptyMessage, font);
            float x = (Width - size.Width) / 2f;
            float y = (Height - size.Height) / 2f;

            g.DrawString(_emptyMessage, font, brush, x, y);
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private static GraphicsPath GetTopRoundedRect(RectangleF rect, int radius)
        {
            var path = new GraphicsPath();

            if (radius <= 0 || rect.Height <= radius * 2)
            {
                path.AddRectangle(rect);
                return path;
            }

            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddLine(rect.Right, rect.Y + radius, rect.Right, rect.Bottom);
            path.AddLine(rect.Right, rect.Bottom, rect.Left, rect.Bottom);
            path.AddLine(rect.Left, rect.Bottom, rect.Left, rect.Y + radius);
            path.CloseFigure();

            return path;
        }

        // ============================================================
        // HOVER + CLICK
        // ============================================================

        private void UpdateHover(int mouseX, int mouseY)
        {
            int newIndex = ComputeHoverIndex(mouseX, mouseY);

            if (newIndex != _hoverIndex)
            {
                _hoverIndex = newIndex;
                Cursor = newIndex >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        private int ComputeHoverIndex(int mouseX, int mouseY)
        {
            if (_buckets == null || _buckets.Count == 0) return -1;

            int chartWidth = Width - SidePadding * 2;
            int chartHeight = Height - TopPadding - BottomPadding;

            if (mouseY < TopPadding - 10 || mouseY > TopPadding + chartHeight + 34)
                return -1;

            int n = _buckets.Count;
            int totalGaps = BarGapPx * (n - 1);
            float barWidth = (chartWidth - totalGaps) / (float)n;
            if (barWidth < 4) barWidth = 4;

            for (int i = 0; i < n; i++)
            {
                float x = SidePadding + i * (barWidth + BarGapPx);
                if (mouseX >= x && mouseX <= x + barWidth)
                    return i;
            }

            return -1;
        }
    }
}