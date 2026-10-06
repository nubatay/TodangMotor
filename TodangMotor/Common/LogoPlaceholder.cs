using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace TodangMotor.Common
{
    /// <summary>
    /// The Todang Motor logo mark.
    /// If a real logo image exists at Assets/Logo.png, it is drawn with
    /// aspect ratio preserved. Otherwise, a rounded tile with a letter is shown.
    /// Set ForcePlaceholder = true to always use the letter tile (ignores the image).
    /// Optionally draws a thin border around the tile — useful on light backgrounds.
    /// </summary>
    public class LogoPlaceholder : Control
    {
        // ============================================================
        // STATIC HELPERS
        // ============================================================

        /// <summary>
        /// Full path where the real logo PNG is expected.
        /// </summary>
        public static string RealLogoPath
        {
            get
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                return Path.Combine(baseDir, "Assets", "Logo.png");
            }
        }

        public static bool HasRealLogo => File.Exists(RealLogoPath);

        // ============================================================
        // INSTANCE
        // ============================================================

        private int _radius = 12;
        private Color _backgroundColor = Color.White;
        private Color _letterColor = Theme.Primary;
        private Color _borderColor = Color.Transparent;
        private int _borderSize = 1;
        private string _letter = "T";
        private Image? _cachedImage;
        private bool _imageLoadAttempted;
        private bool _forcePlaceholder;

        public LogoPlaceholder()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Width = 40;
            Height = 40;
            MinimumSize = new Size(24, 24);

            Resize += (s, e) => ApplyRegion();
            ApplyRegion();
        }

        // ============================================================
        // PROPERTIES
        // ============================================================

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Radius
        {
            get => _radius;
            set { _radius = value; ApplyRegion(); Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color TileColor
        {
            get => _backgroundColor;
            set { _backgroundColor = value; ApplyRegion(); Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color LetterColor
        {
            get => _letterColor;
            set { _letterColor = value; Invalidate(); }
        }

        /// <summary>
        /// Optional border around the tile. Default is transparent (no border).
        /// Set to a light gray (e.g. #E5E7EB) when the tile sits on a
        /// similarly-bright background and needs definition.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color BorderColor
        {
            get => _borderColor;
            set { _borderColor = value; Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int BorderSize
        {
            get => _borderSize;
            set { _borderSize = Math.Max(0, value); Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Letter
        {
            get => _letter;
            set
            {
                _letter = string.IsNullOrEmpty(value) ? "T" : value.Substring(0, 1).ToUpperInvariant();
                Invalidate();
            }
        }

        /// <summary>
        /// When true, the control always draws the fallback tile + letter,
        /// ignoring any real logo file on disk.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ForcePlaceholder
        {
            get => _forcePlaceholder;
            set
            {
                if (_forcePlaceholder == value) return;
                _forcePlaceholder = value;
                Invalidate();
            }
        }

        // ============================================================
        // INTERNAL
        // ============================================================

        private void ApplyRegion()
        {
            if (Width <= 0 || Height <= 0) return;

            if (_backgroundColor.A == 0 || _radius <= 0)
            {
                var old = Region;
                Region = null;
                old?.Dispose();
                return;
            }

            Theme.ApplyRoundedRegion(this, _radius);
        }

        private Image? TryGetCachedImage()
        {
            if (_forcePlaceholder) return null;
            if (_imageLoadAttempted) return _cachedImage;
            _imageLoadAttempted = true;

            try
            {
                string path = RealLogoPath;
                if (File.Exists(path))
                {
                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                    _cachedImage = Image.FromStream(fs);
                }
            }
            catch
            {
                _cachedImage = null;
            }
            return _cachedImage;
        }

        // ============================================================
        // PAINT
        // ============================================================

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            e.Graphics.Clear(Parent?.BackColor ?? Color.Transparent);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.RoundedRect(rect, _radius))
            {
                // Tile fill (skipped when TileColor is transparent).
                if (_backgroundColor.A > 0)
                {
                    using var brush = new SolidBrush(_backgroundColor);
                    e.Graphics.FillPath(brush, path);
                }

                // Optional border.
                if (_borderSize > 0 && _borderColor.A > 0)
                {
                    using var pen = new Pen(_borderColor, _borderSize);
                    e.Graphics.DrawPath(pen, path);
                }

                // Logo image or fallback letter.
                var img = TryGetCachedImage();
                if (img != null)
                {
                    int pad = 1;
                    int innerW = Math.Max(1, Width - pad * 2);
                    int innerH = Math.Max(1, Height - pad * 2);

                    float scaleX = (float)innerW / img.Width;
                    float scaleY = (float)innerH / img.Height;
                    float scale = Math.Min(scaleX, scaleY);

                    int drawW = Math.Max(1, (int)(img.Width * scale));
                    int drawH = Math.Max(1, (int)(img.Height * scale));
                    int drawX = (Width - drawW) / 2;
                    int drawY = (Height - drawH) / 2;

                    e.Graphics.DrawImage(img, drawX, drawY, drawW, drawH);
                }
                else
                {
                    using (var brush = new SolidBrush(_letterColor))
                    using (var fmt = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    })
                    {
                        float fontSize = Math.Max(12f, Math.Min(Width, Height) * 0.45f);
                        using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                        e.Graphics.DrawString(_letter, font, brush,
                            new RectangleF(0, 0, Width, Height), fmt);
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _cachedImage != null)
            {
                _cachedImage.Dispose();
                _cachedImage = null;
            }
            base.Dispose(disposing);
        }
    }
}