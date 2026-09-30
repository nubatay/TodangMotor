using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace TodangMotor.Common
{
    /// <summary>
    /// Motion helpers: value interpolation, easing curves, and short
    /// entrance animations for forms and controls.
    /// Nothing longer than 200 ms, per the design brief.
    /// </summary>
    public static class Animator
    {
        // ============================================================
        // EASING CURVES
        // t is always in the range [0, 1]. Return value is also [0, 1].
        // ============================================================

        /// <summary>Starts fast, slows down at the end. Feels responsive.</summary>
        public static double EaseOutCubic(double t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            double inv = 1 - t;
            return 1 - inv * inv * inv;
        }

        /// <summary>Slows at both ends. Good for sliding panels into place.</summary>
        public static double EaseInOutCubic(double t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return t < 0.5
                ? 4 * t * t * t
                : 1 - Math.Pow(-2 * t + 2, 3) / 2;
        }

        /// <summary>Milder than Cubic. Good for opacity fades.</summary>
        public static double EaseOutQuad(double t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return 1 - (1 - t) * (1 - t);
        }

        // ============================================================
        // CORE ANIMATOR
        // ============================================================

        /// <summary>
        /// Runs a value from <paramref name="from"/> to <paramref name="to"/> over
        /// <paramref name="durationMs"/> milliseconds, calling <paramref name="onStep"/>
        /// with the eased value on every frame.
        /// </summary>
        public static void Animate(
            double from,
            double to,
            int durationMs,
            Action<double> onStep,
            Action? onComplete = null,
            Func<double, double>? easing = null)
        {
            if (onStep == null) throw new ArgumentNullException(nameof(onStep));
            if (durationMs < 1) durationMs = 1;

            easing ??= EaseOutCubic;

            var timer = new Timer { Interval = 16 };
            var sw = Stopwatch.StartNew();

            timer.Tick += (s, e) =>
            {
                double t = (double)sw.ElapsedMilliseconds / durationMs;
                bool done = t >= 1.0;

                double raw = done ? 1.0 : t;
                double eased = easing(raw);
                double value = from + (to - from) * eased;

                try
                {
                    onStep(value);
                }
                catch
                {
                    // Swallow paint-time exceptions from disposed controls.
                    timer.Stop();
                    timer.Dispose();
                    return;
                }

                if (done)
                {
                    timer.Stop();
                    timer.Dispose();
                    onComplete?.Invoke();
                }
            };

            timer.Start();
        }

        // ============================================================
        // FORM ENTRANCE EFFECTS
        // ============================================================

        /// <summary>
        /// Fades a form from transparent to fully solid.
        /// Uses the native Form.Opacity property, so it is GPU-smooth.
        /// </summary>
        public static void FadeInForm(Form form, int durationMs = 150, double startOpacity = 0.0)
        {
            if (form == null) return;

            form.Opacity = startOpacity;

            Animate(startOpacity, 1.0, durationMs,
                v =>
                {
                    if (!form.IsDisposed)
                        form.Opacity = v;
                },
                () =>
                {
                    if (!form.IsDisposed)
                        form.Opacity = 1.0;
                },
                EaseOutQuad);
        }

        /// <summary>
        /// Fades the form in while nudging it downward from above.
        /// The modern "popup" feel. Used for small CRUD dialogs.
        /// </summary>
        public static void SlideFadeInForm(Form form, int durationMs = 180, int fromOffsetY = 12)
        {
            if (form == null) return;

            int finalY = form.Top;
            int startY = finalY - fromOffsetY;

            form.Top = startY;
            form.Opacity = 0.0;

            Animate(0.0, 1.0, durationMs,
                v =>
                {
                    if (form.IsDisposed) return;
                    form.Opacity = v;
                    form.Top = startY + (int)((finalY - startY) * v);
                },
                () =>
                {
                    if (form.IsDisposed) return;
                    form.Opacity = 1.0;
                    form.Top = finalY;
                },
                EaseOutCubic);
        }

        /// <summary>Fades a form out, then closes it.</summary>
        public static void FadeOutAndClose(Form form, int durationMs = 120)
        {
            if (form == null) return;

            double startOpacity = form.Opacity;

            Animate(startOpacity, 0.0, durationMs,
                v =>
                {
                    if (!form.IsDisposed)
                        form.Opacity = v;
                },
                () =>
                {
                    if (!form.IsDisposed)
                        form.Close();
                },
                EaseOutQuad);
        }

        // ============================================================
        // CONTROL ENTRANCE EFFECTS
        // ============================================================

        /// <summary>
        /// Slides a control into place from a horizontal offset.
        /// Temporarily undocks the control, animates its X position,
        /// then restores docking. Used for sidebar module swaps.
        /// </summary>
        public static void SlideInControl(Control control, int fromOffsetX = -30, int durationMs = 180)
        {
            if (control == null || control.Parent == null) return;

            var parent = control.Parent;
            var originalDock = control.Dock;

            control.Dock = DockStyle.None;
            control.Bounds = new Rectangle(
                fromOffsetX,
                0,
                parent.ClientSize.Width,
                parent.ClientSize.Height);

            Animate(fromOffsetX, 0, durationMs,
                x =>
                {
                    if (control.IsDisposed) return;
                    control.Left = (int)Math.Round(x);
                },
                () =>
                {
                    if (control.IsDisposed) return;
                    control.Dock = originalDock;
                },
                EaseOutCubic);
        }

        /// <summary>
        /// Gently rises a control into place from a vertical offset.
        /// Undocks, animates Top, restores docking.
        /// Used for cards and stat tiles.
        /// </summary>
        public static void RiseInControl(Control control, int fromOffsetY = 14, int durationMs = 180)
        {
            if (control == null || control.Parent == null) return;

            var originalDock = control.Dock;
            int finalTop = control.Top;
            int finalLeft = control.Left;
            int finalWidth = control.Width;
            int finalHeight = control.Height;

            control.Dock = DockStyle.None;
            control.Bounds = new Rectangle(
                finalLeft,
                finalTop + fromOffsetY,
                finalWidth,
                finalHeight);

            Animate(0.0, 1.0, durationMs,
                v =>
                {
                    if (control.IsDisposed) return;
                    control.Top = finalTop + (int)Math.Round(fromOffsetY * (1 - v));
                },
                () =>
                {
                    if (control.IsDisposed) return;
                    control.Bounds = new Rectangle(finalLeft, finalTop, finalWidth, finalHeight);
                    control.Dock = originalDock;
                },
                EaseOutCubic);
        }
    }
}