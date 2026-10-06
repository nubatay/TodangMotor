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
    /// One row of a horizontal bar chart.
    /// </summary>
    public class ChartBar
    {
        public string Label { get; set; } = string.Empty;
        public string SubLabel { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public Color Color { get; set; } = Theme.Primary;
    }

    /// <summary>
    /// A pure-GDI+ horizontal bar chart. Each row: label + bar + value.
    /// Supports hover highlighting and click events.
    /// </summary>
    public class HorizontalBarChart : Control
    {
        private List<ChartBar> _bars = new();
        private string _emptyMessage = "No data to display.";
        private int _hoverIndex = -1;

        // Layout constants
        private const int PaddingPx = 12;
        private const int RowHeight = 48;
        private const int BarHeight = 22;
        private const int LabelRowPx = 16;   // height of the label above the bar
        private const int ValueWidthPx = 90;   // reserved space on the right for the value
        private const int BarLeftPx = 8;    // inset of the bar from control left

        private readonly List<RectangleF> _barBounds = new();

        /// <summary>Fired when a bar is clicked.</summary>
        public event Action<ChartBar>? BarClicked;

        public HorizontalBarChart()
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

        public void SetData(List<ChartBar>? bars)
        {
            _bars = bars ?? new List<ChartBar>();

            for (int i = 0; i < _bars.Count; i++)
            {
                if (_bars[i].Color == default || _bars[i].Color.A == 0)
                {
                    _bars[i].Color = Theme.ChartPalette[i % Theme.ChartPalette.Length];
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

        // ============================================================
        // PAINT
        // ============================================================

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            e.Graphics.Clear(Parent?.BackColor ?? Theme.Surface);

            if (_bars.Count == 0 || _bars.All(b => b.Value <= 0))
            {
                DrawEmptyState(e.Graphics);
                return;
            }

            decimal maxValue = _bars.Max(b => b.Value);
            if (maxValue <= 0) maxValue = 1;

            _barBounds.Clear();

            int y = PaddingPx;

            for (int i = 0; i < _bars.Count; i++)
            {
                if (y + RowHeight > Height - PaddingPx) break;

                var bar = _bars[i];
                bool isHover = i == _hoverIndex;

                DrawRow(e.Graphics, bar, i, y, maxValue, isHover);

                y += RowHeight;
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

        private void DrawRow(Graphics g, ChartBar bar, int index, int y, decimal maxValue, bool isHover)
        {
            int usableWidth = Width - PaddingPx * 2;
            int labelWidth = usableWidth - ValueWidthPx;

            // ---- Top line: label + sub-label ----
            using var labelFont = new Font(Theme.UiFontFamily, 9F, FontStyle.Bold);
            using var subFont = new Font(Theme.UiFontFamily, 8F, FontStyle.Regular);
            using var labelBrush = new SolidBrush(isHover ? Theme.Primary : Theme.TextPrimary);
            using var subBrush = new SolidBrush(Theme.TextMuted);

            string labelText = string.IsNullOrEmpty(bar.SubLabel)
                ? bar.Label
                : $"{bar.Label}  ·  {bar.SubLabel}";

            string truncated = TruncateToWidth(g, labelText, labelFont, labelWidth);
            g.DrawString(truncated, labelFont, labelBrush,
                PaddingPx + BarLeftPx, y);

            // ---- Value on the right (top-right aligned with label row) ----
            using var valueFont = new Font(Theme.UiFontFamily, 9F, FontStyle.Bold);
            using var valueBrush = new SolidBrush(isHover ? Theme.Primary : Theme.TextSecondary);

            string valueText = "₱" + bar.Value.ToString("N2");
            var valueSize = g.MeasureString(valueText, valueFont);
            g.DrawString(valueText, valueFont, valueBrush,
                Width - PaddingPx - valueSize.Width, y);

            // ---- Bar track ----
            int barY = y + LabelRowPx + 4;
            int barTrackWidth = usableWidth - BarLeftPx * 2;

            if (barTrackWidth <= 0 || barY + BarHeight > Height)
                return;

            float fillRatio = maxValue > 0 ? (float)(bar.Value / maxValue) : 0f;
            if (fillRatio < 0f) fillRatio = 0f;
            if (fillRatio > 1f) fillRatio = 1f;

            int barFillWidth = (int)(barTrackWidth * fillRatio);
            if (barFillWidth < 4 && bar.Value > 0) barFillWidth = 4;

            var trackRect = new Rectangle(
                PaddingPx + BarLeftPx,
                barY,
                barTrackWidth,
                BarHeight);

            var fillRect = new Rectangle(
                PaddingPx + BarLeftPx,
                barY,
                barFillWidth,
                BarHeight);

            // Track background
            using (var trackBrush = new SolidBrush(Color.FromArgb(232, 236, 244)))
            using (var trackPath = Theme.RoundedRect(trackRect, 6))
            {
                g.FillPath(trackBrush, trackPath);
            }

            // Bar fill
            Color barColor = isHover
                ? Theme.Blend(bar.Color, Theme.PrimaryPressed, 0.35f)
                : bar.Color;

            using (var fillBrush = new SolidBrush(barColor))
            using (var fillPath = Theme.RoundedRect(fillRect, 6))
            {
                g.FillPath(fillBrush, fillPath);
            }

            // Remember bounds for hover / click.
            _barBounds.Add(new RectangleF(
                trackRect.X, y, trackRect.Width, RowHeight));
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
            if (index >= 0 && index < _bars.Count)
            {
                BarClicked?.Invoke(_bars[index]);
            }
        }

        private int ComputeHoverIndex(int mouseX, int mouseY)
        {
            // Whole-row hover — the row reacts if the mouse is anywhere
            // inside its 48px vertical band. Simpler than hitting the bar
            // exactly, and much friendlier for demo clicks.
            int y = PaddingPx;

            for (int i = 0; i < _bars.Count; i++)
            {
                if (y + RowHeight > Height - PaddingPx) break;

                if (mouseY >= y && mouseY < y + RowHeight)
                    return i;

                y += RowHeight;
            }
            return -1;
        }
    }
}