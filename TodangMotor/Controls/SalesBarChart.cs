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

        // Hover state — index of the bar under the mouse, or -1.
        private int _hoverIndex = -1;

        // Layout constants.
        private const int TopPadding = 22;
        private const int BottomPadding = 30;
        private const int SidePadding = 10;
        private const int BarGapPx = 6;

        /// <summary>Fired when a bar is clicked. Payload is the bucket's label.</summary>
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

            // Baseline (0-line)
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

                // Value above the bar when hovered.
                if (i == _hoverIndex && bucket.Value > 0)
                {
                    string valueText = "₱" + bucket.Value.ToString("N0");
                    using var valueFont = new Font(Theme.UiFontFamily, 9F, FontStyle.Bold);
                    var textSize = e.Graphics.MeasureString(valueText, valueFont);
                    float textX = x + (barWidth - textSize.Width) / 2f;
                    float textY = Math.Max(2, y - textSize.Height - 4);

                    using var valueBrush = new SolidBrush(Theme.TextPrimary);
                    e.Graphics.DrawString(valueText, valueFont, valueBrush, textX, textY);
                }

                // Bucket label below the baseline.
                using var labelFont = new Font(Theme.UiFontFamily, 8.5F, FontStyle.Regular);
                var labelSize = e.Graphics.MeasureString(bucket.Label, labelFont);
                float labelX = x + (barWidth - labelSize.Width) / 2f;
                float labelY = TopPadding + chartHeight + 8;

                using var labelBrush = new SolidBrush(
                    i == _hoverIndex ? Theme.Primary : Theme.TextMuted);

                e.Graphics.DrawString(bucket.Label, labelFont, labelBrush, labelX, labelY);
            }

            // Y-axis max value — small label at top-left.
            if (maxValue > 0)
            {
                using var maxFont = new Font(Theme.UiFontFamily, 8F, FontStyle.Regular);
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

            if (mouseY < TopPadding - 10 || mouseY > TopPadding + chartHeight + 30)
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