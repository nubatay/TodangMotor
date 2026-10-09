using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using TodangMotor.Common;

namespace TodangMotor.Forms
{
    /// <summary>
    /// Base class for every borderless window in Todang Motor.
    /// Provides a header bar, minimize/maximize/close buttons,
    /// edge resize, drag-to-move, and rounded corners.
    /// Subclasses place their real content inside ContentPanel.
    /// </summary>
    public class ShellForm : Form
    {
        // ============================================================
        // WIN32
        // ============================================================

        private const int WM_NCHITTEST = 0x0084;
        private const int WM_NCLBUTTONDOWN = 0x00A1;
        private const int HTCLIENT = 0x0001;
        private const int HTCAPTION = 0x0002;
        private const int HTLEFT = 0x000A;
        private const int HTRIGHT = 0x000B;
        private const int HTTOP = 0x000C;
        private const int HTTOPLEFT = 0x000D;
        private const int HTTOPRIGHT = 0x000E;
        private const int HTBOTTOM = 0x000F;
        private const int HTBOTTOMLEFT = 0x0010;
        private const int HTBOTTOMRIGHT = 0x0011;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        private const int MONITOR_DEFAULTTONEAREST = 0x00000002;
        private const int WM_GETMINMAXINFO = 0x0024;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
            public POINT(int x, int y) { X = x; Y = y; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        // ============================================================
        // FIELDS
        // ============================================================

        private int _cornerRadius = 12;
        private bool _showMaximizeButton;
        private bool _showMinimizeButton = true;
        private bool _showHeader = true;
        private string _headerTitle = string.Empty;

        protected Panel HeaderPanel;
        protected Panel ContentPanel;
        protected Panel ChromeContainer;
        protected Label HeaderTitleLabel;
        protected Button MinimizeButton;
        protected Button MaximizeButton;
        protected Button CloseButton;

        // ============================================================
        // CONSTRUCTION
        // ============================================================

        public ShellForm()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Background;
            Font = Theme.FontBody;
            DoubleBuffered = true;
            MinimumSize = new Size(900, 600);       // was 480x320 — too small for real layouts
            ClientSize = new Size(1024, 700);      // provides a real "restore" size

            BuildChrome();
        }

        private void BuildChrome()
        {
            // ---------- Header ----------
            HeaderPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = Theme.HeaderHeight,
                BackColor = Theme.Surface
            };

            HeaderTitleLabel = new Label
            {
                Text = string.Empty,
                Font = Theme.FontH3,
                ForeColor = Theme.TextPrimary,
                AutoSize = false,
                Height = Theme.HeaderHeight,
                Dock = DockStyle.Left,
                Width = 400,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(20, 0, 0, 0),
                BackColor = Color.Transparent
            };

            // ---------- Chrome buttons (right-aligned, in a sized container) ----------
            // Container is docked Right. Buttons are positioned absolutely inside
            // it so they stay perfect 40x40 squares regardless of header height.
            ChromeContainer = new Panel
            {
                Dock = DockStyle.Right,
                Width = 120,
                BackColor = Color.Transparent
            };

            CloseButton = UiFactory.CreateChromeButton("\uE8BB", isClose: true);
            CloseButton.Dock = DockStyle.None;
            CloseButton.Size = new Size(40, 40);
            CloseButton.Location = new Point(80, 8);
            CloseButton.Click += (s, e) => Close();

            MaximizeButton = UiFactory.CreateChromeButton("\uE922");
            MaximizeButton.Dock = DockStyle.None;
            MaximizeButton.Size = new Size(40, 40);
            MaximizeButton.Location = new Point(40, 8);
            MaximizeButton.Visible = false;
            MaximizeButton.Click += (s, e) => ToggleMaximize();

            MinimizeButton = UiFactory.CreateChromeButton("\uE921");
            MinimizeButton.Dock = DockStyle.None;
            MinimizeButton.Size = new Size(40, 40);
            MinimizeButton.Location = new Point(0, 8);
            MinimizeButton.Click += (s, e) => WindowState = FormWindowState.Minimized;

            ChromeContainer.Controls.Add(CloseButton);
            ChromeContainer.Controls.Add(MaximizeButton);
            ChromeContainer.Controls.Add(MinimizeButton);

            HeaderPanel.Controls.Add(ChromeContainer);
            HeaderPanel.Controls.Add(HeaderTitleLabel);

            // Header drag-to-move.
            HeaderPanel.MouseDown += Header_MouseDown;
            HeaderTitleLabel.MouseDown += Header_MouseDown;

            // ---------- Content ----------
            ContentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Background
            };

            Controls.Add(ContentPanel);
            Controls.Add(HeaderPanel);

            // Initial chrome layout based on which buttons are currently visible.
            UpdateChromeContainerWidth();
        }

        // ============================================================
        // PUBLIC SURFACE FOR SUBCLASSES
        // ============================================================

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string HeaderTitle
        {
            get => _headerTitle;
            set
            {
                _headerTitle = value ?? string.Empty;
                if (HeaderTitleLabel != null)
                    HeaderTitleLabel.Text = _headerTitle;
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowMaximizeButton
        {
            get => _showMaximizeButton;
            set
            {
                _showMaximizeButton = value;
                if (MaximizeButton != null)
                    MaximizeButton.Visible = value;
                UpdateChromeContainerWidth();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowMinimizeButton
        {
            get => _showMinimizeButton;
            set
            {
                _showMinimizeButton = value;
                if (MinimizeButton != null)
                    MinimizeButton.Visible = value;
                UpdateChromeContainerWidth();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowHeader
        {
            get => _showHeader;
            set
            {
                _showHeader = value;
                if (HeaderPanel != null)
                    HeaderPanel.Visible = value;
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int CornerRadius
        {
            get => _cornerRadius;
            set
            {
                _cornerRadius = Math.Max(0, value);
                ApplyRoundedCorners();
                Invalidate();
            }
        }

        // ============================================================
        // CHROME BUTTON LAYOUT
        // ============================================================

        /// <summary>
        /// Resizes the chrome container to fit only the visible buttons,
        /// then repositions them right-to-left.
        /// </summary>
        private void UpdateChromeContainerWidth()
        {
            if (ChromeContainer == null) return;

            const int btnSize = 40;
            int visibleCount = 0;
            if (MinimizeButton != null && MinimizeButton.Visible) visibleCount++;
            if (MaximizeButton != null && MaximizeButton.Visible) visibleCount++;
            if (CloseButton != null && CloseButton.Visible) visibleCount++;

            ChromeContainer.Width = Math.Max(btnSize, visibleCount * btnSize);
            RelayoutChromeButtons();
        }

        /// <summary>
        /// Positions the chrome buttons left-to-right based on which are visible.
        /// Order is always Minimize · Maximize · Close (rightmost).
        /// </summary>
        private void RelayoutChromeButtons()
        {
            if (ChromeContainer == null) return;
            if (MinimizeButton == null || MaximizeButton == null || CloseButton == null) return;

            const int btnSize = 40;
            const int btnY = 8;

            int x = ChromeContainer.Width - btnSize;
            CloseButton.Location = new Point(x, btnY);

            if (MaximizeButton.Visible)
            {
                x -= btnSize;
                MaximizeButton.Location = new Point(x, btnY);
            }

            if (MinimizeButton.Visible)
            {
                x -= btnSize;
                MinimizeButton.Location = new Point(x, btnY);
            }
        }

        // ============================================================
        // HEADER DRAG
        // ============================================================

        private void Header_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (WindowState == FormWindowState.Maximized) return;

            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
        }

        // ============================================================
        // MAXIMIZE / RESTORE
        // ============================================================

        private void ToggleMaximize()
        {
            WindowState = WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal
                : FormWindowState.Maximized;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyRoundedCorners();

            if (MaximizeButton != null)
            {
                MaximizeButton.Text = WindowState == FormWindowState.Maximized ? "\uE923" : "\uE922";
            }
        }

        // ============================================================
        // ROUNDED CORNERS
        // ============================================================

        private void ApplyRoundedCorners()
        {
            if (WindowState == FormWindowState.Maximized || _cornerRadius <= 0)
            {
                var oldRegion = Region;
                Region = null;
                oldRegion?.Dispose();
                return;
            }

            if (Width <= 0 || Height <= 0) return;

            using (var path = Theme.RoundedRect(new Rectangle(0, 0, Width, Height), _cornerRadius))
            {
                var oldRegion = Region;
                Region = new Region(path);
                oldRegion?.Dispose();
            }
        }

        // ============================================================
        // EDGE RESIZE (WM_NCHITTEST)
        // ============================================================

        protected override void WndProc(ref Message m)
        {
            // Handle maximize bounds FIRST — before base.WndProc lets Windows
            // decide. Without this, borderless forms compute garbage bounds.
            if (m.Msg == WM_GETMINMAXINFO)
            {
                HandleGetMinMaxInfo(m.HWnd, m.LParam);
            }

            base.WndProc(ref m);

            if (m.Msg != WM_NCHITTEST) return;
            if (WindowState == FormWindowState.Maximized) return;

            if ((int)m.Result != HTCLIENT) return;

            int screenX = unchecked((short)(long)m.LParam);
            int screenY = unchecked((short)((long)m.LParam >> 16));
            var pt = PointToClient(new Point(screenX, screenY));

            int b = Theme.ResizeBorder;

            bool left = pt.X <= b;
            bool right = pt.X >= ClientSize.Width - b;
            bool top = pt.Y <= b;
            bool bottom = pt.Y >= ClientSize.Height - b;

            if (top && left) m.Result = (IntPtr)HTTOPLEFT;
            else if (top && right) m.Result = (IntPtr)HTTOPRIGHT;
            else if (bottom && left) m.Result = (IntPtr)HTBOTTOMLEFT;
            else if (bottom && right) m.Result = (IntPtr)HTBOTTOMRIGHT;
            else if (left) m.Result = (IntPtr)HTLEFT;
            else if (right) m.Result = (IntPtr)HTRIGHT;
            else if (top) m.Result = (IntPtr)HTTOP;
            else if (bottom) m.Result = (IntPtr)HTBOTTOM;
        }

        private static void HandleGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
        {
            // Load the structure Windows is asking us to fill in.
            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

            // Which monitor is this form on?
            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero)
            {
                var monitorInfo = new MONITORINFO();
                monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));

                if (GetMonitorInfo(monitor, ref monitorInfo))
                {
                    // Working area = screen minus taskbar.
                    var work = monitorInfo.rcWork;
                    var mon = monitorInfo.rcMonitor;

                    // Position: where the maximized window's top-left goes
                    // (relative to the monitor's top-left).
                    mmi.ptMaxPosition.X = Math.Abs(work.Left - mon.Left);
                    mmi.ptMaxPosition.Y = Math.Abs(work.Top - mon.Top);

                    // Size: how big the maximized window should be.
                    mmi.ptMaxSize.X = Math.Abs(work.Right - work.Left);
                    mmi.ptMaxSize.Y = Math.Abs(work.Bottom - work.Top);

                    // Also constrain the drag-to-resize maximum so users can't
                    // drag the form larger than the working area.
                    mmi.ptMaxTrackSize.X = mmi.ptMaxSize.X;
                    mmi.ptMaxTrackSize.Y = mmi.ptMaxSize.Y;
                }
            }

            // Write it back.
            Marshal.StructureToPtr(mmi, lParam, true);
        }

        // ============================================================
        // WS_EX_COMPOSITED — eliminates flicker across the whole form
        // ============================================================

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                // 0x02000000 = WS_EX_COMPOSITED
                cp.ExStyle |= 0x02000000;
                return cp;
            }
        }

        // ============================================================
        // PAINT (frame border for borderless window)
        // ============================================================

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (WindowState == FormWindowState.Maximized) return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using (var path = Theme.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), _cornerRadius))
            using (var pen = new Pen(Theme.Divider, 1))
            {
                e.Graphics.DrawPath(pen, path);
            }
        }
    }
}