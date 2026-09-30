using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TodangMotor.Common
{
    /// <summary>
    /// Single source of truth for the Todang Motor visual language.
    /// Colors, fonts, sizes, and small GDI+ helpers.
    /// Nothing here touches the database or business rules.
    /// </summary>
    public static class Theme
    {
        // ============================================================
        // BRAND COLORS
        // ============================================================

        /// <summary>Primary brand blue — DeepSeek Blue #4D6BFE.</summary>
        public static readonly Color Primary = Color.FromArgb(77, 107, 254);
        public static readonly Color PrimaryHover = Color.FromArgb(59, 85, 217);
        public static readonly Color PrimaryPressed = Color.FromArgb(47, 68, 176);

        // ============================================================
        // BACKGROUNDS
        // ============================================================

        /// <summary>Warm off-white — main app background.</summary>
        public static readonly Color Background = Color.FromArgb(250, 250, 252);

        /// <summary>Pure white — cards, panels, popup surfaces.</summary>
        public static readonly Color Surface = Color.FromArgb(255, 255, 255);

        /// <summary>Deep navy sidebar background.</summary>
        public static readonly Color SidebarBg = Color.FromArgb(31, 42, 68);
        public static readonly Color SidebarHover = Color.FromArgb(42, 56, 84);

        /// <summary>Active sidebar item — uses the brand blue.</summary>
        public static readonly Color SidebarActive = Primary;

        // ============================================================
        // TEXT
        // ============================================================

        public static readonly Color TextPrimary = Color.FromArgb(26, 26, 26);
        public static readonly Color TextSecondary = Color.FromArgb(107, 114, 128);
        public static readonly Color TextMuted = Color.FromArgb(160, 166, 178);
        public static readonly Color TextOnPrimary = Color.FromArgb(255, 255, 255);
        public static readonly Color TextOnSidebar = Color.FromArgb(230, 233, 244);

        // ============================================================
        // INPUTS
        // ============================================================

        public static readonly Color InputBackground = Color.FromArgb(245, 246, 250);
        public static readonly Color InputBorder = Color.FromArgb(214, 218, 229);
        public static readonly Color FocusBorder = Primary;

        // ============================================================
        // STATUS
        // ============================================================

        public static readonly Color Danger = Color.FromArgb(229, 72, 77);
        public static readonly Color DangerHover = Color.FromArgb(200, 55, 60);
        public static readonly Color Success = Color.FromArgb(46, 160, 67);
        public static readonly Color Warning = Color.FromArgb(240, 173, 78);

        // ============================================================
        // DIVIDERS / BORDERS
        // ============================================================

        public static readonly Color Divider = Color.FromArgb(230, 233, 240);
        public static readonly Color DividerDark = Color.FromArgb(50, 62, 90);

        // ============================================================
        // FONTS
        // Segoe UI for normal text. Segoe MDL2 Assets for icons.
        // ============================================================

        public const string UiFontFamily = "Segoe UI";
        public const string IconFontFamily = "Segoe MDL2 Assets";

        public static readonly Font FontH1 = new Font(UiFontFamily, 20F, FontStyle.Bold);
        public static readonly Font FontH2 = new Font(UiFontFamily, 15F, FontStyle.Bold);
        public static readonly Font FontH3 = new Font(UiFontFamily, 12F, FontStyle.Bold);
        public static readonly Font FontBody = new Font(UiFontFamily, 10F, FontStyle.Regular);
        public static readonly Font FontBodyBold = new Font(UiFontFamily, 10F, FontStyle.Bold);
        public static readonly Font FontSmall = new Font(UiFontFamily, 9F, FontStyle.Regular);
        public static readonly Font FontTiny = new Font(UiFontFamily, 8F, FontStyle.Regular);

        public static readonly Font FontIcon = new Font(IconFontFamily, 12F, FontStyle.Regular);
        public static readonly Font FontIconLarge = new Font(IconFontFamily, 16F, FontStyle.Regular);

        // ============================================================
        // SIZES / SPACING
        // ============================================================

        public const int Radius = 8;
        public const int RadiusSmall = 6;
        public const int RadiusCard = 12;
        public const int RadiusPill = 999;

        public const int HeaderHeight = 56;
        public const int SidebarWidth = 220;
        public const int ResizeBorder = 8;

        public const int SpacingXs = 4;
        public const int SpacingSm = 8;
        public const int SpacingMd = 16;
        public const int SpacingLg = 24;
        public const int SpacingXl = 32;

        // ============================================================
        // GDI+ HELPERS
        // ============================================================

        /// <summary>
        /// Builds the geometry for a rectangle with rounded corners.
        /// Caller must dispose the returned GraphicsPath.
        /// </summary>
        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            int d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>
        /// Applies a rounded shape to a control so its corners are physically rounded.
        /// IMPORTANT: for resizable controls, call this again on every Resize event.
        /// </summary>
        public static void ApplyRoundedRegion(Control control, int radius)
        {
            if (control == null) return;
            if (control.Width <= 0 || control.Height <= 0) return;

            using (var path = RoundedRect(new Rectangle(0, 0, control.Width, control.Height), radius))
            {
                var old = control.Region;
                control.Region = new Region(path);
                old?.Dispose();
            }
        }

        /// <summary>
        /// Mixes two colors. t = 0 returns A, t = 1 returns B.
        /// Used for hover and pressed animation states.
        /// </summary>
        public static Color Blend(Color a, Color b, float t)
        {
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;

            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        /// <summary>
        /// Same color, but with a custom transparency (0 = invisible, 255 = solid).
        /// Used for shadows and overlays.
        /// </summary>
        public static Color WithAlpha(Color c, int alpha)
        {
            if (alpha < 0) alpha = 0;
            if (alpha > 255) alpha = 255;
            return Color.FromArgb(alpha, c.R, c.G, c.B);
        }
    }
}