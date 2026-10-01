using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace TodangMotor.Common
{
    public static class UiFactory
    {
        public enum ButtonStyle { Primary, Secondary, Danger, Ghost, Success }

        // ============================================================
        // BUTTONS
        // ============================================================

        public static Button CreateButton(string text, ButtonStyle style = ButtonStyle.Primary, int width = 120, int height = 38)
        {
            var b = new Button
            {
                Text = text,
                Width = width,
                Height = height,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Font = Theme.FontBodyBold,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(0)
            };
            b.FlatAppearance.BorderSize = 0;

            Color normal, hover, pressed, fore;

            switch (style)
            {
                case ButtonStyle.Secondary:
                    normal = Theme.Surface;
                    hover = Color.FromArgb(237, 241, 255);
                    pressed = Color.FromArgb(220, 228, 255);
                    fore = Theme.Primary;
                    b.FlatAppearance.BorderSize = 1;
                    b.FlatAppearance.BorderColor = Theme.Primary;
                    break;

                case ButtonStyle.Danger:
                    normal = Theme.Danger;
                    hover = Theme.DangerHover;
                    pressed = Color.FromArgb(170, 40, 45);
                    fore = Color.White;
                    break;

                case ButtonStyle.Success:
                    normal = Theme.Success;
                    hover = Color.FromArgb(36, 138, 55);
                    pressed = Color.FromArgb(28, 110, 44);
                    fore = Color.White;
                    break;

                case ButtonStyle.Ghost:
                    normal = Color.Transparent;
                    hover = Color.FromArgb(235, 238, 245);
                    pressed = Color.FromArgb(220, 224, 235);
                    fore = Theme.TextSecondary;
                    break;

                default:
                    normal = Theme.Primary;
                    hover = Theme.PrimaryHover;
                    pressed = Theme.PrimaryPressed;
                    fore = Color.White;
                    break;
            }

            b.BackColor = normal;
            b.ForeColor = fore;

            Theme.ApplyRoundedRegion(b, Theme.RadiusSmall);
            b.Resize += (s, e) => Theme.ApplyRoundedRegion(b, Theme.RadiusSmall);

            AttachHoverAnimation(b, normal, hover, pressed);

            return b;
        }

        public static Button CreateSidebarItem(string icon, string label)
        {
            var b = new Button
            {
                Text = $"{icon}   {label}",
                Width = Theme.SidebarWidth,
                Height = 44,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Font = Theme.FontBody,
                ForeColor = Theme.TextOnSidebar,
                BackColor = Theme.SidebarBg,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(20, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;

            AttachHoverAnimation(b, Theme.SidebarBg, Theme.SidebarHover, Theme.SidebarHover);
            return b;
        }

        public static Button CreateChromeButton(string glyph, bool isClose = false)
        {
            var b = new Button
            {
                Text = glyph,
                Width = 40,
                Height = 40,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Font = Theme.FontIcon,
                ForeColor = Theme.TextSecondary,
                BackColor = Theme.Surface,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                TabStop = false
            };
            b.FlatAppearance.BorderSize = 0;

            Color hover = isClose
                ? Theme.Danger
                : Color.FromArgb(220, 228, 245);

            AttachHoverAnimation(b, Theme.Surface, hover, hover);

            if (isClose)
            {
                b.MouseEnter += (s, e) => b.ForeColor = Color.White;
                b.MouseLeave += (s, e) => b.ForeColor = Theme.TextSecondary;
            }

            return b;
        }

        // ============================================================
        // INPUTS
        // ============================================================

        public static RoundedTextBox CreateTextBox(int width = 240, bool isPassword = false)
        {
            var t = new RoundedTextBox
            {
                Width = width,
                Height = 40
            };
            if (isPassword)
            {
                t.UseSystemPasswordChar = true;
            }
            return t;
        }

        public static ComboBox CreateComboBox(int width = 240)
        {
            return new ComboBox
            {
                Width = width,
                Height = 40,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Font = Theme.FontBody,
                BackColor = Theme.InputBackground,
                ForeColor = Theme.TextPrimary
            };
        }

        // ============================================================
        // LABELS
        // ============================================================

        public static Label CreateLabel(string text, Font font = null, Color? color = null)
        {
            return new Label
            {
                Text = text,
                Font = font ?? Theme.FontBody,
                ForeColor = color ?? Theme.TextPrimary,
                AutoSize = true,
                BackColor = Color.Transparent
            };
        }

        public static Label CreateFieldLabel(string text)
        {
            return new Label
            {
                Text = text,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = true,
                BackColor = Color.Transparent
            };
        }

        // ============================================================
        // CONTAINERS
        // ============================================================

        public static RoundedPanel CreateCard(int radius = Theme.RadiusCard, bool withBorder = true)
        {
            return new RoundedPanel
            {
                BackColor = Theme.Surface,
                Radius = radius,
                BorderColor = withBorder ? Theme.Divider : Color.Transparent,
                BorderSize = withBorder ? 1 : 0,
                Padding = new Padding(Theme.SpacingMd)
            };
        }

        public static Panel CreateDivider(int width = 200)
        {
            return new Panel
            {
                Width = width,
                Height = 1,
                BackColor = Theme.Divider
            };
        }

        // ============================================================
        // DATAGRIDVIEW
        // ============================================================

        public static void StyleGrid(DataGridView grid)
        {
            grid.BackgroundColor = Theme.Surface;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = Theme.Divider;
            grid.EnableHeadersVisualStyles = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.ReadOnly = true;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = 40;
            grid.RowTemplate.Height = 34;
            grid.Font = Theme.FontBody;

            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 239, 248);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(55, 65, 81);
            grid.ColumnHeadersDefaultCellStyle.Font = Theme.FontBodyBold;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(235, 239, 248);
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.FromArgb(55, 65, 81);
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 8, 0);

            grid.DefaultCellStyle.BackColor = Theme.Surface;
            grid.DefaultCellStyle.ForeColor = Theme.TextPrimary;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(232, 238, 255);
            grid.DefaultCellStyle.SelectionForeColor = Theme.TextPrimary;
            grid.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
        }

        // ============================================================
        // HOVER ANIMATION
        // ============================================================

        private static void AttachHoverAnimation(Button b, Color normal, Color hover, Color pressed)
        {
            bool inside = false;

            b.MouseEnter += (s, e) => { inside = true; AnimateBackColor(b, hover); };
            b.MouseLeave += (s, e) => { inside = false; AnimateBackColor(b, normal); };
            b.MouseDown += (s, e) => AnimateBackColor(b, pressed);
            b.MouseUp += (s, e) => AnimateBackColor(b, inside ? hover : normal);
        }

        private static void AnimateBackColor(Control c, Color target, int durationMs = 120)
        {
            Color from = c.BackColor;
            if (from == target) return;

            var timer = new Timer { Interval = 16 };
            var sw = Stopwatch.StartNew();

            timer.Tick += (s, e) =>
            {
                if (c.IsDisposed)
                {
                    timer.Stop();
                    timer.Dispose();
                    return;
                }

                float t = (float)sw.ElapsedMilliseconds / durationMs;
                if (t >= 1f)
                {
                    c.BackColor = target;
                    timer.Stop();
                    timer.Dispose();
                    return;
                }
                c.BackColor = Theme.Blend(from, target, t);
            };
            timer.Start();
        }
    }

    // ================================================================
    // ROUNDED TEXTBOX
    // ================================================================

    public class RoundedTextBox : Panel
    {
        private readonly TextBox _inner;
        private Button _toggleButton;
        private bool _focused;
        private string _placeholder = string.Empty;
        private bool _showPasswordToggle;
        private bool _passwordVisible;
        private bool _showingPlaceholder;
        private bool _suppressTextChanged;

        public event KeyEventHandler InputKeyDown;

        public RoundedTextBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);

            BackColor = Theme.InputBackground;
            Padding = new Padding(12, 9, 12, 9);
            Height = 40;

            _inner = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Theme.InputBackground,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontBody,
                Dock = DockStyle.Fill
            };

            _inner.GotFocus += (s, e) =>
            {
                _focused = true;
                HidePlaceholder();
                Invalidate();
            };
            _inner.LostFocus += (s, e) =>
            {
                _focused = false;
                ShowPlaceholderIfEmpty();
                Invalidate();
            };
            _inner.TextChanged += (s, e) =>
            {
                if (_suppressTextChanged) return;
                OnTextChanged(EventArgs.Empty);
            };
            _inner.KeyDown += (s, e) => InputKeyDown?.Invoke(this, e);

            Controls.Add(_inner);

            Resize += (s, e) => Theme.ApplyRoundedRegion(this, Theme.RadiusSmall);
            Theme.ApplyRoundedRegion(this, Theme.RadiusSmall);

            BuildToggleButton();
        }

        private void ShowPlaceholderIfEmpty()
        {
            if (!string.IsNullOrEmpty(_inner.Text)) return;
            if (string.IsNullOrEmpty(_placeholder)) return;

            _suppressTextChanged = true;
            _showingPlaceholder = true;
            _inner.ForeColor = Theme.TextMuted;
            _inner.Text = _placeholder;
            _suppressTextChanged = false;
        }

        private void HidePlaceholder()
        {
            if (!_showingPlaceholder) return;

            _suppressTextChanged = true;
            _showingPlaceholder = false;
            _inner.ForeColor = Theme.TextPrimary;
            _inner.Text = string.Empty;
            _suppressTextChanged = false;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Placeholder
        {
            get => _placeholder;
            set
            {
                _placeholder = value ?? string.Empty;
                if (!_focused && string.IsNullOrEmpty(_showingPlaceholder ? string.Empty : _inner.Text))
                {
                    ShowPlaceholderIfEmpty();
                }
            }
        }

        public override string Text
        {
            get => _showingPlaceholder ? string.Empty : _inner.Text;
            set
            {
                _suppressTextChanged = true;
                _showingPlaceholder = false;
                _inner.ForeColor = Theme.TextPrimary;
                _inner.Text = value ?? string.Empty;
                _suppressTextChanged = false;

                if (string.IsNullOrEmpty(_inner.Text) && !_focused)
                    ShowPlaceholderIfEmpty();
            }
        }

        private void BuildToggleButton()
        {
            _toggleButton = new Button
            {
                Text = "\uE7B3",
                Font = new Font(Theme.IconFontFamily, 11F),
                ForeColor = Theme.TextMuted,
                BackColor = Theme.InputBackground,
                FlatStyle = FlatStyle.Flat,
                Width = 30,
                Dock = DockStyle.Right,
                Cursor = Cursors.Hand,
                TabStop = false,
                Visible = false
            };
            _toggleButton.FlatAppearance.BorderSize = 0;
            _toggleButton.FlatAppearance.MouseOverBackColor = Theme.InputBackground;
            _toggleButton.FlatAppearance.MouseDownBackColor = Theme.InputBackground;

            _toggleButton.MouseEnter += (s, e) =>
                _toggleButton.ForeColor = _passwordVisible ? Theme.PrimaryPressed : Theme.Primary;
            _toggleButton.MouseLeave += (s, e) =>
                _toggleButton.ForeColor = _passwordVisible ? Theme.Primary : Theme.TextMuted;

            _toggleButton.Click += (s, e) =>
            {
                TogglePasswordVisibility();
                _inner.Focus();
            };
        }

        private void TogglePasswordVisibility()
        {
            _passwordVisible = !_passwordVisible;
            _inner.UseSystemPasswordChar = !_passwordVisible;
            _toggleButton.ForeColor = _passwordVisible ? Theme.Primary : Theme.TextMuted;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowPasswordToggle
        {
            get => _showPasswordToggle;
            set
            {
                if (_showPasswordToggle == value) return;
                _showPasswordToggle = value;

                if (value)
                {
                    if (!Controls.Contains(_toggleButton))
                        Controls.Add(_toggleButton);

                    _toggleButton.Visible = true;
                    _toggleButton.BringToFront();
                }
                else
                {
                    _toggleButton.Visible = false;
                }
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool UseSystemPasswordChar
        {
            get => _inner.UseSystemPasswordChar;
            set => _inner.UseSystemPasswordChar = value;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public char PasswordChar
        {
            get => _inner.PasswordChar;
            set => _inner.PasswordChar = value;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int MaxLength
        {
            get => _inner.MaxLength;
            set => _inner.MaxLength = value;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ReadOnly
        {
            get => _inner.ReadOnly;
            set
            {
                _inner.ReadOnly = value;
                _inner.BackColor = value ? Color.FromArgb(238, 238, 238) : Theme.InputBackground;
                BackColor = value ? Color.FromArgb(238, 238, 238) : Theme.InputBackground;
                Invalidate();
            }
        }

        public void FocusInput() => _inner.Focus();
        public void SelectAll() => _inner.SelectAll();

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using (var brush = new SolidBrush(BackColor))
                e.Graphics.FillRectangle(brush, ClientRectangle);

            Color borderColor = _focused ? Theme.FocusBorder : Theme.InputBorder;
            float thickness = _focused ? 2f : 1f;

            var rect = new Rectangle(1, 1, Width - 3, Height - 3);
            using (var path = Theme.RoundedRect(rect, Theme.RadiusSmall))
            using (var pen = new Pen(borderColor, thickness))
                e.Graphics.DrawPath(pen, path);
        }
    }

    // ================================================================
    // ROUNDED COMBOBOX
    // ================================================================

    /// <summary>
    /// Flat, rounded dropdown that matches RoundedTextBox.
    /// The native ComboBox arrow is clipped off the right edge; a small
    /// overlay panel on top of the ComboBox paints our custom chevron.
    /// </summary>
    public class RoundedComboBox : Panel
    {
        private readonly ComboBox _inner;
        private readonly Panel _arrowOverlay;
        private bool _focused;
        private bool _hoverArrow;

        public event EventHandler SelectedIndexChanged
        {
            add => _inner.SelectedIndexChanged += value;
            remove => _inner.SelectedIndexChanged -= value;
        }

        public RoundedComboBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);

            BackColor = Theme.InputBackground;
            Height = 40;
            Padding = new Padding(0);

            _inner = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.InputBackground,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontBody,
                IntegralHeight = false
            };

            _inner.GotFocus += (s, e) =>
            {
                _focused = true;
                // Windows overrides BackColor with white when a ComboBox gets
                // focus. Force it back on the next message pump cycle.
                BeginInvoke(new Action(() =>
                {
                    if (!_inner.IsDisposed)
                        _inner.BackColor = Theme.InputBackground;
                }));
                Invalidate();
            };
            _inner.LostFocus += (s, e) =>
            {
                _focused = false;
                _inner.BackColor = Theme.InputBackground;
                Invalidate();
            };

            Controls.Add(_inner);

            // Arrow overlay: sits on top of the ComboBox's right side,
            // matching its background and painting our chevron.
            _arrowOverlay = new Panel
            {
                BackColor = Theme.InputBackground,
                Cursor = Cursors.Hand
            };
            _arrowOverlay.Paint += ArrowOverlay_Paint;
            _arrowOverlay.Click += (s, e) => _inner.Focus();
            _arrowOverlay.MouseEnter += (s, e) => { _hoverArrow = true; _arrowOverlay.Invalidate(); };
            _arrowOverlay.MouseLeave += (s, e) => { _hoverArrow = false; _arrowOverlay.Invalidate(); };

            Controls.Add(_arrowOverlay);
            _arrowOverlay.BringToFront();

            Resize += (s, e) =>
            {
                Theme.ApplyRoundedRegion(this, Theme.RadiusSmall);
                LayoutInner();
            };
            Theme.ApplyRoundedRegion(this, Theme.RadiusSmall);

            LayoutInner();
        }

        /// <summary>
        /// Positions the inner ComboBox so its native dropdown arrow is
        /// clipped past the right edge, and pins the arrow overlay just
        /// inside the panel's right border.
        /// </summary>
        private void LayoutInner()
        {
            if (_inner == null || _arrowOverlay == null) return;
            if (Width <= 0 || Height <= 0) return;

            int comboH = _inner.Height > 0 ? _inner.Height : 24;
            int y = Math.Max(0, (Height - comboH) / 2);

            _inner.Location = new Point(12, y);
            // Extra 30 px pushes the native arrow off the panel.
            _inner.Width = Width - 12 + 30;
            _inner.DropDownWidth = Math.Max(120, Width - 12);

            // Arrow overlay: 32 px wide, inset 3 px from the right edge so
            // it doesn't cover the panel's rounded border.
            const int overlayW = 32;
            int overlayH = Math.Max(comboH, 20);
            int overlayX = Width - overlayW - 3;
            int overlayY = Math.Max(2, (Height - overlayH) / 2);

            _arrowOverlay.SetBounds(overlayX, overlayY, overlayW, overlayH);
            _arrowOverlay.BringToFront();
        }

        private void ArrowOverlay_Paint(object? sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Color arrowColor = _focused ? Theme.FocusBorder
                              : _hoverArrow ? Theme.Primary
                              : Theme.TextSecondary;

            using var pen = new Pen(arrowColor, 1.6f);
            int cx = _arrowOverlay.Width / 2;
            int cy = _arrowOverlay.Height / 2;

            e.Graphics.DrawLine(pen, cx - 4, cy - 1, cx, cy + 3);
            e.Graphics.DrawLine(pen, cx, cy + 3, cx + 4, cy - 1);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ComboBox.ObjectCollection Items => _inner.Items;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int SelectedIndex
        {
            get => _inner.SelectedIndex;
            set => _inner.SelectedIndex = value;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public object? SelectedItem
        {
            get => _inner.SelectedItem;
            set => _inner.SelectedItem = value;
        }

        public new void Focus() => _inner.Focus();

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using (var brush = new SolidBrush(Theme.InputBackground))
                e.Graphics.FillRectangle(brush, ClientRectangle);

            Color borderColor = _focused ? Theme.FocusBorder : Theme.InputBorder;
            float thickness = _focused ? 2f : 1f;

            var rect = new Rectangle(1, 1, Width - 3, Height - 3);
            using (var path = Theme.RoundedRect(rect, Theme.RadiusSmall))
            using (var pen = new Pen(borderColor, thickness))
                e.Graphics.DrawPath(pen, path);
        }
    }

    // ================================================================
    // ROUNDED PANEL
    // ================================================================

    public class RoundedPanel : Panel
    {
        private int _radius = Theme.RadiusCard;
        private Color _borderColor = Theme.Divider;
        private int _borderSize = 1;

        public RoundedPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);

            BackColor = Theme.Surface;
            Resize += (s, e) => ApplyRegion();
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Radius
        {
            get => _radius;
            set { _radius = value; ApplyRegion(); Invalidate(); }
        }

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
            set { _borderSize = value; Invalidate(); }
        }

        private void ApplyRegion()
        {
            if (Width <= 0 || Height <= 0) return;
            Theme.ApplyRoundedRegion(this, _radius);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent?.BackColor ?? Theme.Background);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.RoundedRect(rect, _radius))
            {
                using (var brush = new SolidBrush(BackColor))
                    e.Graphics.FillPath(brush, path);

                if (_borderSize > 0 && _borderColor != Color.Transparent)
                {
                    using (var pen = new Pen(_borderColor, _borderSize))
                        e.Graphics.DrawPath(pen, path);
                }
            }
        }
    }
}