using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using TodangMotor.Common;

namespace TodangMotor.Controls
{
    /// <summary>
    /// One slice of a donut chart.
    /// </summary>
    public class ChartSlice
    {
        public string Label { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public Color Color { get; set; } = Theme.Primary;
    }

    /// <summary>
    /// A pure-GDI+ donut chart. Displays a ring of segments with a legend
    /// on the right. Supports hover highlighting and click events.
    /// </summary>
    public class DonutChart : Control
    {
        private List<ChartSlice> _slices = new();
        private string _emptyMessage = "No data to display.";
        private string _centerLabel = "Total";
        private int _hoverIndex = -1;

        // Layout
        private const int PaddingPx = 12;
        private const int LegendRowHeight = 30;
        private const float InnerRadiusRatio = 0.58f;
        private const int SegmentBorderPx = 2;

        // Cache the hover segment rects so hit-testing and paint agree.
        private readonly List<RectangleF> _segmentBounds = new();
        private readonly List<float> _segmentStartAngles = new();
        private readonly List<float> _segmentSweepAngles = new();
        private PointF _centerPoint;
        private float _outerRadius;
        private float _innerRadius;

        /// <summary>Fired when a slice is clicked. Payload is the slice that was clicked.</summary>
        public event Action<ChartSlice>? SliceClicked;

        public DonutChart()
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
            MouseClick += (s, e) => HandleClick(e.X, e.Y);
        }

        // ============================================================
        // PUBLIC API
        // ============================================================

        public void SetData(List<ChartSlice>? slices)
        {
            _slices = slices ?? new List<ChartSlice>();

            // Assign colors from the theme palette if a slice has no color set.
            for (int i = 0; i < _slices.Count; i++)
            {
                if (_slices[i].Color == default || _slices[i].Color.A == 0)
                {
                    _slices[i].Color = Theme.ChartPalette[i % Theme.ChartPalette.Length];
                }
            }

            _hoverIndex = -1;
            Invalidate();
        }

        public void SetEmptyMessage(string message)
        {
            _emptyMessage = string.IsNullOrWhiteSpace(message) ? "No data." : message;
            Invalidate();
        }

        public void SetCenterLabel(string label)
        {
            _centerLabel = label ?? string.Empty;
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

            decimal total = _slices.Sum(s => s.Value);

            if (_slices.Count == 0 || total <= 0)
            {
                DrawEmptyState(e.Graphics);
                return;
            }

            // Layout regions
            int legendWidth = ComputeLegendWidth();
            int chartWidth = Width - legendWidth - PaddingPx * 2;
            int chartHeight = Height - PaddingPx * 2;

            if (chartWidth < 60 || chartHeight < 60)
            {
                // Too small to render meaningfully.
                return;
            }

            var chartRect = new Rectangle(PaddingPx, PaddingPx, chartWidth, chartHeight);
            var legendRect = new Rectangle(
                PaddingPx + chartWidth,
                PaddingPx,
                legendWidth,
                chartHeight);

            DrawSegments(e.Graphics, chartRect, total);
            DrawCenterLabel(e.Graphics, total);
            DrawLegend(e.Graphics, legendRect, total);
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

        private void DrawSegments(Graphics g, Rectangle chartRect, decimal total)
        {
            _segmentBounds.Clear();
            _segmentStartAngles.Clear();
            _segmentSweepAngles.Clear();

            // Center + radii
            float cx = chartRect.X + chartRect.Width / 2f;
            float cy = chartRect.Y + chartRect.Height / 2f;
            _centerPoint = new PointF(cx, cy);

            float outer = Math.Min(chartRect.Width, chartRect.Height) / 2f - 4;
            if (outer < 20) return;

            _outerRadius = outer;
            _innerRadius = outer * InnerRadiusRatio;

            // Draw each slice as an annular segment.
            float startAngle = -90f; // start at 12 o'clock

            for (int i = 0; i < _slices.Count; i++)
            {
                var slice = _slices[i];
                if (slice.Value <= 0) continue;

                float sweep = (float)(slice.Value / total) * 360f;

                // Clamp to avoid gaps from floating point rounding.
                if (i == _slices.Count - 1 && Math.Abs(sweep) > 0.01f)
                {
                    // Last slice — absorb any leftover degrees.
                    float consumed = _segmentStartAngles.Sum(a => 0f); // placeholder
                }

                var path = BuildAnnularSegment(
                    cx, cy, _outerRadius, _innerRadius, startAngle, sweep);

                bool isHover = i == _hoverIndex;

                using (var brush = new SolidBrush(slice.Color))
                    g.FillPath(brush, path);

                // Border stroke to separate slices.
                using (var pen = new Pen(Theme.Surface, SegmentBorderPx))
                    g.DrawPath(pen, path);

                // Hover — subtle white overlay + thicker border.
                if (isHover)
                {
                    using var overlay = new SolidBrush(Color.FromArgb(35, 255, 255, 255));
                    g.FillPath(overlay, path);

                    using var hoverPen = new Pen(Theme.PrimaryPressed, 2f);
                    g.DrawPath(hoverPen, path);
                }

                path.Dispose();

                var segmentBounds = new RectangleF(
                    cx - _outerRadius,
                    cy - _outerRadius,
                    _outerRadius * 2,
                    _outerRadius * 2);

                _segmentBounds.Add(segmentBounds);
                _segmentStartAngles.Add(startAngle);
                _segmentSweepAngles.Add(sweep);

                startAngle += sweep;
            }
        }

        private void DrawCenterLabel(Graphics g, decimal total)
        {
            if (_outerRadius <= 0) return;

            // Show total in the hole.
            string totalText = "₱" + total.ToString("N2");
            float holeRadius = _innerRadius - 2f;
            float availableSize = holeRadius * 1.7f; // diameter-ish safe area

            // Fit font size to the hole.
            float fontSize = 14f;
            using (var testFont = new Font(Theme.UiFontFamily, fontSize, FontStyle.Bold))
            {
                var measured = g.MeasureString(totalText, testFont);
                while (measured.Width > availableSize && fontSize > 8f)
                {
                    fontSize -= 0.5f;
                    using var smaller = new Font(Theme.UiFontFamily, fontSize, FontStyle.Bold);
                    measured = g.MeasureString(totalText, smaller);
                }
            }

            using var totalFont = new Font(Theme.UiFontFamily, fontSize, FontStyle.Bold);
            using var totalBrush = new SolidBrush(Theme.TextPrimary);

            var totalSize = g.MeasureString(totalText, totalFont);

            // Label above the total.
            using var labelFont = new Font(Theme.UiFontFamily, 8F, FontStyle.Regular);
            using var labelBrush = new SolidBrush(Theme.TextMuted);
            var labelSize = g.MeasureString(_centerLabel, labelFont);

            float centerX = _centerPoint.X;
            float centerY = _centerPoint.Y;

            float totalY = centerY - totalSize.Height / 2f;
            float labelY = totalY - labelSize.Height;

            // Only draw the label if there's room above the total.
            if (labelY > centerY - holeRadius)
            {
                g.DrawString(_centerLabel, labelFont, labelBrush,
                    centerX - labelSize.Width / 2f, labelY);
            }

            g.DrawString(totalText, totalFont, totalBrush,
                centerX - totalSize.Width / 2f, totalY);
        }

        private void DrawLegend(Graphics g, Rectangle legendRect, decimal total)
        {
            if (_slices.Count == 0) return;

            using var swatchBrush = new SolidBrush(Color.Transparent);
            using var labelFont = new Font(Theme.UiFontFamily, 9F, FontStyle.Regular);
            using var valueFont = new Font(Theme.UiFontFamily, 9F, FontStyle.Bold);
            using var labelBrush = new SolidBrush(Theme.TextPrimary);
            using var valueBrush = new SolidBrush(Theme.TextSecondary);

            int y = legendRect.Y;
            const int swatchSize = 12;
            const int swatchGap = 10;

            foreach (var slice in _slices)
            {
                if (y + LegendRowHeight > legendRect.Bottom) break;

                // Color swatch
                swatchBrush.Color = slice.Color;
                g.FillRectangle(swatchBrush,
                    legendRect.X,
                    y + (LegendRowHeight - swatchSize) / 2f,
                    swatchSize,
                    swatchSize);

                // Hover highlight for the legend row.
                bool isHover = _slices.IndexOf(slice) == _hoverIndex;

                // Label
                float labelX = legendRect.X + swatchSize + swatchGap;
                float labelWidth = legendRect.Width - swatchSize - swatchGap - 80;

                var labelText = TruncateToWidth(g, slice.Label, labelFont, labelWidth);
                g.DrawString(labelText, labelFont, labelBrush,
                    labelX, y + (LegendRowHeight - labelFont.Height) / 2f);

                // Value + percent
                float percent = total > 0 ? (float)(slice.Value / total) * 100f : 0f;
                string valueText = "₱" + slice.Value.ToString("N0") + "  " + percent.ToString("0.#") + "%";

                var valueSize = g.MeasureString(valueText, valueFont);
                g.DrawString(valueText, valueFont, valueBrush,
                    legendRect.Right - valueSize.Width,
                    y + (LegendRowHeight - valueFont.Height) / 2f);

                y += LegendRowHeight;
            }
        }

        private static string TruncateToWidth(Graphics g, string text, Font font, float maxWidth)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            var size = g.MeasureString(text, font);
            if (size.Width <= maxWidth) return text;

            string current = text;
            while (current.Length > 1)
            {
                current = current.Substring(0, current.Length - 1);
                var m = g.MeasureString(current + "…", font);
                if (m.Width <= maxWidth)
                    return current + "…";
            }
            return "…";
        }

        // ============================================================
        // GEOMETRY
        // ============================================================

        private static GraphicsPath BuildAnnularSegment(
            float cx, float cy,
            float outerRadius, float innerRadius,
            float startAngle, float sweepAngle)
        {
            var path = new GraphicsPath();

            if (sweepAngle <= 0) return path;

            // Clamp sweep so we never try to draw a full circle in one arc call
            // (GDI+ cannot draw 360 degrees in a single arc).
            if (sweepAngle >= 359.99f) sweepAngle = 359.99f;

            var outerRect = new RectangleF(
                cx - outerRadius, cy - outerRadius,
                outerRadius * 2, outerRadius * 2);

            var innerRect = new RectangleF(
                cx - innerRadius, cy - innerRadius,
                innerRadius * 2, innerRadius * 2);

            // Outer arc (clockwise).
            path.AddArc(outerRect, startAngle, sweepAngle);

            // Connect to the inner arc endpoint.
            float endAngle = startAngle + sweepAngle;
            float endRad = endAngle * (float)Math.PI / 180f;
            var outerEnd = new PointF(
                cx + outerRadius * (float)Math.Cos(endRad),
                cy + outerRadius * (float)Math.Sin(endRad));
            var innerEnd = new PointF(
                cx + innerRadius * (float)Math.Cos(endRad),
                cy + innerRadius * (float)Math.Sin(endRad));
            path.AddLine(outerEnd, innerEnd);

            // Inner arc (counter-clockwise) — negative sweep.
            path.AddArc(innerRect, endAngle, -sweepAngle);

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

        private void HandleClick(int mouseX, int mouseY)
        {
            int index = ComputeHoverIndex(mouseX, mouseY);
            if (index >= 0 && index < _slices.Count)
            {
                SliceClicked?.Invoke(_slices[index]);
            }
        }

        private int ComputeHoverIndex(int mouseX, int mouseY)
        {
            if (_outerRadius <= 0) return -1;

            float dx = mouseX - _centerPoint.X;
            float dy = mouseY - _centerPoint.Y;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);

            if (dist < _innerRadius || dist > _outerRadius)
                return -1;

            // Angle in our drawing convention: 0 = 3 o'clock, increasing clockwise.
            float angleDeg = (float)(Math.Atan2(dy, dx) * 180.0 / Math.PI);
            // Normalize so it lines up with our draw convention (start at -90 = 12 o'clock).
            float fromTop = angleDeg + 90f;
            if (fromTop < 0) fromTop += 360f;
            if (fromTop >= 360f) fromTop -= 360f;

            decimal total = _slices.Sum(s => s.Value);
            if (total <= 0) return -1;

            float cumulative = 0f;
            for (int i = 0; i < _slices.Count; i++)
            {
                float sweep = (float)(_slices[i].Value / total) * 360f;
                if (fromTop >= cumulative && fromTop < cumulative + sweep)
                    return i;
                cumulative += sweep;
            }

            return -1;
        }

        private int ComputeLegendWidth()
        {
            if (_slices.Count == 0) return 0;

            // Between 40% and 55% of total width — enough room for
            // labels but always leaves space for the donut.
            int preferred = (int)(Width * 0.42);
            return Math.Max(140, Math.Min(preferred, Width - 160));
        }
    }
}