using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace TodangMotor.Common
{
    /// <summary>
    /// The Todang Motor logo mark.
    /// If a real logo image exists at TodangMotor/Assets/Logo.png, it is drawn.
    /// Otherwise, a brand-blue rounded tile with a white "T" is shown as a placeholder.
    /// Drop-in replacement: no code change required when the real logo arrives.
    /// </summary>
    public class LogoPlaceholder : Control
    {
        // ============================================================
        // STATIC HELPERS — where the real logo lives
        // ============================================================

        /// <summary>
        /// Full path where a real logo PNG is expected.
        /// Looked up relative to the running assembly so it works in Debug and Release.
        /// </summary>
        public static string RealLogoPath
        {
            get
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                return Path.Combine(baseDir, "Assets", "Logo.png");
            }
        }

        /// <summary>True when a real logo PNG exists on disk.</summary>
        public static bool HasRealLogo => File.Exists(RealLogoPath);

        // ============================================================
        // INSTANCE
        // ============================================================

        private int _radius = 10;
        private Color _backgroundColor = Theme.Primary;
        private Color _letterColor = Color.White;
        private string _letter = "T";
        private Image _cachedImage;
        private bool _imageLoadAttempted;

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

        // ------------------------------------------------------------
        // PROPERTIES
        // ------------------------------------------------------------

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
            set { _backgroundColor = value; Invalidate(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color LetterColor
        {
            get => _letterColor;
            set { _letterColor = value; Invalidate(); }
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

        // ------------------------------------------------------------
        // HELPERS
        // ------------------------------------------------------------

        private void ApplyRegion()
        {
            if (Width <= 0 || Height <= 0) return;
            Theme.ApplyRoundedRegion(this, _radius);
        }

        /// <summary>
        /// Loads the real logo once and caches it. Returns null when no real logo exists.
        /// </summary>
        private Image TryGetCachedImage()
        {
            if (_imageLoadAttempted) return _cachedImage;
            _imageLoadAttempted = true;

            try
            {
                if (File.Exists(RealLogoPath))
                {
                    // Copy into memory so the file is not locked on disk.
                    using (var fs = new FileStream(RealLogoPath, FileMode.Open, FileAccess.Read))
                    {
                        _cachedImage = Image.FromStream(fs);
                    }
                }
            }
            catch
            {
                // If the file is corrupt or unreadable, silently fall back to the placeholder letter.
                _cachedImage = null;
            }
            return _cachedImage;
        }

        // ------------------------------------------------------------
        // PAINTING
        // ------------------------------------------------------------

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // Clear to parent background so rounded corners blend in.
            e.Graphics.Clear(Parent?.BackColor ?? Color.Transparent);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.RoundedRect(rect, _radius))
            {
                // 1. Base tile fill.
                using (var brush = new SolidBrush(_backgroundColor))
                    e.Graphics.FillPath(brush, path);

                // 2. Real logo, if present.
                var img = TryGetCachedImage();
                if (img != null)
                {
                    // Draw the logo centered, scaled to fit inside the tile with 3px inner padding.
                    int pad = 3;
                    var target = new Rectangle(
                        pad, pad,
                        Math.Max(1, Width - pad * 2),
                        Math.Max(1, Height - pad * 2));

                    e.Graphics.DrawImage(img, target);
                }
                else
                {
                    // 3. Placeholder letter, centered.
                    using (var brush = new SolidBrush(_letterColor))
                    {
                        using (var fmt = new StringFormat
                        {
                            Alignment = StringAlignment.Center,
                            LineAlignment = StringAlignment.Center
                        })
                        {
                            // Size the letter relative to tile size.
                            float fontSize = Math.Max(12f, Width * 0.45f);
                            using (var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
                            {
                                e.Graphics.DrawString(_letter, font, brush, new RectangleF(0, 0, Width, Height), fmt);
                            }
                        }
                    }
                }
            }
        }

        // ------------------------------------------------------------
        // DISPOSE
        // ------------------------------------------------------------

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