using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using TodangMotor.Services;

namespace TodangMotor.Forms
{
    public class SupplierForm : Form
    {
        // --- Win32 helper for dragging a borderless window ---
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        // --- Win32 constants for edge-resize hit testing (WM_NCHITTEST) ---
        private const int WM_NCHITTEST = 0x0084;
        private const int HTCLIENT = 1;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;
        private const int ResizeBorderThickness = 8;

        // --- Colors (MODERN LIGHT theme, matching CategoryForm exactly) ---
        private readonly Color _bgColor = Color.FromArgb(250, 248, 245); // warm off-white
        private readonly Color _headerBg = Color.White;
        private readonly Color _headerTitleColor = Color.FromArgb(45, 45, 45);
        private readonly Color _cardBorder = Color.FromArgb(232, 234, 238);
        private readonly Color _orange = Color.FromArgb(230, 126, 34);
        private readonly Color _orangeHover = Color.FromArgb(211, 84, 0);
        private readonly Color _textColor = Color.FromArgb(35, 35, 35);
        private readonly Color _mutedText = Color.FromArgb(140, 140, 140);
        private readonly Color _inputBg = Color.FromArgb(248, 249, 251);
        private readonly Color _inputBorder = Color.FromArgb(220, 223, 228);
        private readonly Color _inputBorderFocus = Color.FromArgb(230, 126, 34);
        private readonly Color _chromeIcon = Color.FromArgb(120, 120, 120);
        private readonly Color _chromeHoverBg = Color.FromArgb(238, 238, 238);
        private readonly Color _chromeHoverFg = Color.FromArgb(45, 45, 45);
        private readonly Color _closeHoverBg = Color.FromArgb(232, 17, 35);
        private readonly Color _gridHeaderBg = Color.FromArgb(245, 246, 250);
        private readonly Color _gridAltRow = Color.FromArgb(248, 249, 251);

        private const int HeaderHeight = 52;

        private readonly SupplierService _supplierService;

        // --- Window chrome controls ---
        private Panel headerPanel;
        private Label lblHeaderTitle;
        private Button btnMinimize;
        private Button btnCloseChrome;

        // --- Functional controls ---
        private DataGridView dgvSuppliers = null!;

        private Label lblSupplierName = null!;
        private Panel txtSupplierNameWrapper = null!;
        private TextBox txtSupplierName = null!;

        private Label lblContactNumber = null!;
        private Panel txtContactNumberWrapper = null!;
        private TextBox txtContactNumber = null!;
        private Label lblContactHint = null!;

        private Label lblAddress = null!;
        private Panel txtAddressWrapper = null!;
        private TextBox txtAddress = null!;

        private Button btnAdd = null!;
        private Button btnUpdate = null!;
        private Button btnDeactivate = null!;
        private Button btnReactivate = null!;
        private Button btnClose = null!;
        private Label lblStatus = null!;

        private int _selectedSupplierId = 0;

        public SupplierForm()
        {
            _supplierService = new SupplierService();

            InitializeControls();
            LoadSuppliersAsync();
        }

        private void InitializeControls()
        {
            // --- Form settings (borderless, opens centered at normal size) ---
            this.Text = "Supplier Management";
            this.Size = new Size(760, 660);
            this.MinimumSize = new Size(650, 630);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = _bgColor;
            this.KeyPreview = true;
            this.DoubleBuffered = true;

            this.Load += SupplierForm_Load;
            this.KeyDown += SupplierForm_KeyDown;
            this.Resize += SupplierForm_Resize;

            BuildHeader();

            // ---- Grid ----
            dgvSuppliers = new DataGridView
            {
                Location = new Point(30, HeaderHeight + 20),
                Size = new Size(700, 260),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = _cardBorder,
                RowHeadersVisible = false,
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeight = 38,
                RowTemplate = { Height = 32 },
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = _textColor
            };

            dgvSuppliers.ColumnHeadersDefaultCellStyle.BackColor = _gridHeaderBg;
            dgvSuppliers.ColumnHeadersDefaultCellStyle.ForeColor = _headerTitleColor;
            dgvSuppliers.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            dgvSuppliers.ColumnHeadersDefaultCellStyle.SelectionBackColor = _gridHeaderBg;
            dgvSuppliers.ColumnHeadersDefaultCellStyle.SelectionForeColor = _headerTitleColor;
            dgvSuppliers.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 0, 0);

            dgvSuppliers.DefaultCellStyle.BackColor = Color.White;
            dgvSuppliers.DefaultCellStyle.ForeColor = _textColor;
            dgvSuppliers.DefaultCellStyle.SelectionBackColor = Color.FromArgb(255, 244, 230);
            dgvSuppliers.DefaultCellStyle.SelectionForeColor = _orange;
            dgvSuppliers.DefaultCellStyle.Padding = new Padding(8, 0, 0, 0);
            dgvSuppliers.AlternatingRowsDefaultCellStyle.BackColor = _gridAltRow;
            dgvSuppliers.AlternatingRowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(255, 244, 230);
            dgvSuppliers.AlternatingRowsDefaultCellStyle.SelectionForeColor = _orange;

            dgvSuppliers.SelectionChanged += DgvSuppliers_SelectionChanged;

            // NEW: clicking on empty grid space (below the last row) clears
            // the current selection, matching the same fix applied to
            // CategoryForm.
            dgvSuppliers.MouseDown += DgvSuppliers_MouseDown;

            // ---- Supplier Name field ----
            lblSupplierName = new Label
            {
                Text = "Supplier Name:",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = _textColor,
                AutoSize = true,
                Location = new Point(30, HeaderHeight + 300),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                BackColor = Color.Transparent
            };

            txtSupplierNameWrapper = new Panel
            {
                Location = new Point(150, HeaderHeight + 295),
                Size = new Size(300, 34),
                BackColor = _inputBg,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            txtSupplierNameWrapper.Paint += (s, e) => PaintInputWrapper(e, txtSupplierNameWrapper, txtSupplierName);

            txtSupplierName = new TextBox
            {
                Location = new Point(12, 7),
                Size = new Size(276, 22),
                Font = new Font("Segoe UI", 10.5f),
                BorderStyle = BorderStyle.None,
                BackColor = _inputBg,
                ForeColor = _textColor,
                MaxLength = 150
            };
            txtSupplierName.Enter += (s, e) => txtSupplierNameWrapper.Invalidate();
            txtSupplierName.Leave += (s, e) => txtSupplierNameWrapper.Invalidate();
            txtSupplierNameWrapper.Controls.Add(txtSupplierName);

            // ---- Contact Number field ----
            lblContactNumber = new Label
            {
                Text = "Contact Number:",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = _textColor,
                AutoSize = true,
                Location = new Point(30, HeaderHeight + 344),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                BackColor = Color.Transparent
            };

            txtContactNumberWrapper = new Panel
            {
                Location = new Point(150, HeaderHeight + 339),
                Size = new Size(300, 34),
                BackColor = _inputBg,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            txtContactNumberWrapper.Paint += (s, e) => PaintInputWrapper(e, txtContactNumberWrapper, txtContactNumber);

            txtContactNumber = new TextBox
            {
                Location = new Point(12, 7),
                Size = new Size(276, 22),
                Font = new Font("Segoe UI", 10.5f),
                BorderStyle = BorderStyle.None,
                BackColor = _inputBg,
                ForeColor = _textColor,
                MaxLength = 15
            };
            txtContactNumber.Enter += (s, e) => txtContactNumberWrapper.Invalidate();
            txtContactNumber.Leave += (s, e) => txtContactNumberWrapper.Invalidate();
            txtContactNumberWrapper.Controls.Add(txtContactNumber);

            lblContactHint = new Label
            {
                Text = "Format: 09XXXXXXXXX (11 digits, e.g. 09171234567)",
                Font = new Font("Segoe UI", 8f),
                ForeColor = _mutedText,
                AutoSize = true,
                Location = new Point(150, HeaderHeight + 374),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                BackColor = Color.Transparent
            };

            // ---- Address field ----
            lblAddress = new Label
            {
                Text = "Address:",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = _textColor,
                AutoSize = true,
                Location = new Point(30, HeaderHeight + 398),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                BackColor = Color.Transparent
            };

            txtAddressWrapper = new Panel
            {
                Location = new Point(150, HeaderHeight + 393),
                Size = new Size(470, 34),
                BackColor = _inputBg,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            txtAddressWrapper.Paint += (s, e) => PaintInputWrapper(e, txtAddressWrapper, txtAddress);

            txtAddress = new TextBox
            {
                Location = new Point(12, 7),
                Size = new Size(446, 22),
                Font = new Font("Segoe UI", 10.5f),
                BorderStyle = BorderStyle.None,
                BackColor = _inputBg,
                ForeColor = _textColor,
                MaxLength = 250
            };
            txtAddress.Enter += (s, e) => txtAddressWrapper.Invalidate();
            txtAddress.Leave += (s, e) => txtAddressWrapper.Invalidate();
            txtAddressWrapper.Controls.Add(txtAddress);

            // ---- Buttons row 1: Add / Update ----
            btnAdd = new Button
            {
                Text = "Add",
                Location = new Point(470, HeaderHeight + 443),
                Size = new Size(90, 34),
                BackColor = _orange,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            btnAdd.FlatAppearance.BorderSize = 0;
            btnAdd.FlatAppearance.MouseOverBackColor = _orangeHover;
            btnAdd.FlatAppearance.MouseDownBackColor = _orangeHover;
            btnAdd.MouseEnter += (s, e) => btnAdd.BackColor = _orangeHover;
            btnAdd.MouseLeave += (s, e) => btnAdd.BackColor = _orange;
            btnAdd.Click += BtnAdd_Click;

            btnUpdate = new Button
            {
                Text = "Update",
                Location = new Point(570, HeaderHeight + 443),
                Size = new Size(90, 34),
                BackColor = _orange,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            btnUpdate.FlatAppearance.BorderSize = 0;
            btnUpdate.FlatAppearance.MouseOverBackColor = _orangeHover;
            btnUpdate.FlatAppearance.MouseDownBackColor = _orangeHover;
            btnUpdate.MouseEnter += (s, e) => btnUpdate.BackColor = _orangeHover;
            btnUpdate.MouseLeave += (s, e) => btnUpdate.BackColor = _orange;
            btnUpdate.Click += BtnUpdate_Click;

            // ---- Buttons row 2: Deactivate / Reactivate / Close ----
            btnDeactivate = new Button
            {
                Text = "Deactivate",
                Location = new Point(30, HeaderHeight + 488),
                Size = new Size(110, 34),
                BackColor = Color.White,
                ForeColor = _textColor,
                Font = new Font("Segoe UI", 10f),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            btnDeactivate.FlatAppearance.BorderSize = 1;
            btnDeactivate.FlatAppearance.BorderColor = _inputBorder;
            btnDeactivate.FlatAppearance.MouseOverBackColor = _inputBg;
            btnDeactivate.FlatAppearance.MouseDownBackColor = _inputBg;
            btnDeactivate.MouseEnter += (s, e) => btnDeactivate.ForeColor = _orange;
            btnDeactivate.MouseLeave += (s, e) => btnDeactivate.ForeColor = _textColor;
            btnDeactivate.Click += BtnDeactivate_Click;

            btnReactivate = new Button
            {
                Text = "Reactivate",
                Location = new Point(150, HeaderHeight + 488),
                Size = new Size(110, 34),
                BackColor = Color.White,
                ForeColor = _textColor,
                Font = new Font("Segoe UI", 10f),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            btnReactivate.FlatAppearance.BorderSize = 1;
            btnReactivate.FlatAppearance.BorderColor = _inputBorder;
            btnReactivate.FlatAppearance.MouseOverBackColor = _inputBg;
            btnReactivate.FlatAppearance.MouseDownBackColor = _inputBg;
            btnReactivate.MouseEnter += (s, e) => btnReactivate.ForeColor = _orange;
            btnReactivate.MouseLeave += (s, e) => btnReactivate.ForeColor = _textColor;
            btnReactivate.Click += BtnReactivate_Click;

            btnClose = new Button
            {
                Text = "Close",
                Location = new Point(570, HeaderHeight + 488),
                Size = new Size(90, 34),
                BackColor = Color.White,
                ForeColor = _textColor,
                Font = new Font("Segoe UI", 10f),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnClose.FlatAppearance.BorderSize = 1;
            btnClose.FlatAppearance.BorderColor = _inputBorder;
            btnClose.FlatAppearance.MouseOverBackColor = _inputBg;
            btnClose.FlatAppearance.MouseDownBackColor = _inputBg;
            btnClose.MouseEnter += (s, e) => btnClose.ForeColor = _orange;
            btnClose.MouseLeave += (s, e) => btnClose.ForeColor = _textColor;
            btnClose.Click += (s, e) => this.Close();

            // ---- Status label ----
            lblStatus = new Label
            {
                Text = string.Empty,
                AutoSize = false,
                Font = new Font("Segoe UI", 9f),
                Size = new Size(700, 22),
                Location = new Point(30, HeaderHeight + 533),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ForeColor = Color.Firebrick,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // ---- Add all controls to the form ----
            this.Controls.Add(dgvSuppliers);
            this.Controls.Add(lblSupplierName);
            this.Controls.Add(txtSupplierNameWrapper);
            this.Controls.Add(lblContactNumber);
            this.Controls.Add(txtContactNumberWrapper);
            this.Controls.Add(lblContactHint);
            this.Controls.Add(lblAddress);
            this.Controls.Add(txtAddressWrapper);
            this.Controls.Add(btnAdd);
            this.Controls.Add(btnUpdate);
            this.Controls.Add(btnDeactivate);
            this.Controls.Add(btnReactivate);
            this.Controls.Add(btnClose);
            this.Controls.Add(lblStatus);

            // Header added last so it sits on top of everything visually.
            this.Controls.Add(headerPanel);
            headerPanel.BringToFront();
        }

        // ==================== HEADER (window chrome) ====================
        private void BuildHeader()
        {
            headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = HeaderHeight,
                BackColor = _headerBg
            };
            headerPanel.MouseDown += HeaderPanel_MouseDown;

            // ---- Title text, now living directly in the header bar ----
            lblHeaderTitle = new Label
            {
                Text = "Supplier Management",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = _headerTitleColor,
                AutoSize = true,
                Location = new Point(20, 15),
                BackColor = Color.Transparent
            };
            lblHeaderTitle.MouseDown += HeaderPanel_MouseDown;

            btnMinimize = new Button
            {
                Text = "—",
                ForeColor = _chromeIcon,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                Size = new Size(46, HeaderHeight),
                Location = new Point(this.Width - 92, 0),
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = _headerBg,
                TabStop = false
            };
            btnMinimize.FlatAppearance.BorderSize = 0;
            btnMinimize.FlatAppearance.MouseOverBackColor = _chromeHoverBg;
            btnMinimize.FlatAppearance.MouseDownBackColor = _chromeHoverBg;
            btnMinimize.Click += (s, e) => this.WindowState = FormWindowState.Minimized;
            btnMinimize.MouseEnter += (s, e) => btnMinimize.ForeColor = _chromeHoverFg;
            btnMinimize.MouseLeave += (s, e) => btnMinimize.ForeColor = _chromeIcon;

            // FIX: switched from Unicode "✕" to plain "X" — same font-
            // rendering quirk already fixed in LoginForm and CategoryForm.
            btnCloseChrome = new Button
            {
                Text = "X",
                ForeColor = _chromeIcon,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                Size = new Size(46, HeaderHeight),
                Location = new Point(this.Width - 46, 0),
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = _headerBg,
                TabStop = false
            };
            btnCloseChrome.FlatAppearance.BorderSize = 0;
            btnCloseChrome.FlatAppearance.MouseOverBackColor = _closeHoverBg;
            btnCloseChrome.FlatAppearance.MouseDownBackColor = _closeHoverBg;
            btnCloseChrome.Click += (s, e) => this.Close();
            btnCloseChrome.MouseEnter += (s, e) => btnCloseChrome.ForeColor = Color.White;
            btnCloseChrome.MouseLeave += (s, e) => btnCloseChrome.ForeColor = _chromeIcon;

            headerPanel.Controls.Add(lblHeaderTitle);
            headerPanel.Controls.Add(btnMinimize);
            headerPanel.Controls.Add(btnCloseChrome);
        }

        private void HeaderPanel_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        // ==================== EDGE-RESIZE SUPPORT ====================
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg == WM_NCHITTEST && (int)m.Result == HTCLIENT
                && this.WindowState != FormWindowState.Maximized)
            {
                Point screenPoint = new Point(m.LParam.ToInt32());
                Point clientPoint = this.PointToClient(screenPoint);

                int x = clientPoint.X;
                int y = clientPoint.Y;
                int width = this.ClientSize.Width;
                int height = this.ClientSize.Height;

                if (x <= ResizeBorderThickness && y <= ResizeBorderThickness)
                    m.Result = (IntPtr)HTTOPLEFT;
                else if (x >= width - ResizeBorderThickness && y <= ResizeBorderThickness)
                    m.Result = (IntPtr)HTTOPRIGHT;
                else if (x <= ResizeBorderThickness && y >= height - ResizeBorderThickness)
                    m.Result = (IntPtr)HTBOTTOMLEFT;
                else if (x >= width - ResizeBorderThickness && y >= height - ResizeBorderThickness)
                    m.Result = (IntPtr)HTBOTTOMRIGHT;
                else if (x <= ResizeBorderThickness)
                    m.Result = (IntPtr)HTLEFT;
                else if (x >= width - ResizeBorderThickness)
                    m.Result = (IntPtr)HTRIGHT;
                else if (y <= ResizeBorderThickness)
                    m.Result = (IntPtr)HTTOP;
                else if (y >= height - ResizeBorderThickness)
                    m.Result = (IntPtr)HTBOTTOM;
            }
        }

        // ==================== ROUNDING HELPERS ====================
        private void SupplierForm_Load(object? sender, EventArgs e)
        {
            this.Region = new Region(GetRoundedRectPath(this.ClientRectangle, 18));
            RoundControl(txtSupplierNameWrapper, 10);
            RoundControl(txtContactNumberWrapper, 10);
            RoundControl(txtAddressWrapper, 10);
            RoundControl(btnAdd, 8);
            RoundControl(btnUpdate, 8);
            RoundControl(btnDeactivate, 8);
            RoundControl(btnReactivate, 8);
            RoundControl(btnClose, 8);
        }

        private void SupplierForm_Resize(object? sender, EventArgs e)
        {
            if (this.ClientRectangle.Width > 0 && this.ClientRectangle.Height > 0)
            {
                this.Region = new Region(GetRoundedRectPath(this.ClientRectangle, 18));
            }
        }

        private GraphicsPath GetRoundedRectPath(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0) radius = 1;

            int d = radius * 2;
            if (d > bounds.Width) d = bounds.Width;
            if (d > bounds.Height) d = bounds.Height;

            if (d <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void RoundControl(Control c, int radius)
        {
            if (c.Width <= 0 || c.Height <= 0) return;
            if (radius <= 0) return;
            c.Region = new Region(GetRoundedRectPath(new Rectangle(0, 0, c.Width, c.Height), radius));
        }

        // Shared focus-highlight painter for all three input wrappers.
        private void PaintInputWrapper(PaintEventArgs e, Panel wrapper, TextBox box)
        {
            bool focused = box != null && box.Focused;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, wrapper.Width - 1, wrapper.Height - 1);
            using var path = GetRoundedRectPath(rect, 10);
            using var pen = new Pen(focused ? _inputBorderFocus : _inputBorder, focused ? 1.6f : 1f);
            e.Graphics.DrawPath(pen, path);
        }

        private void SupplierForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
            }
        }

        // NEW: clicking below the last row (empty grid space) clears the
        // current selection, matching the same fix applied to CategoryForm.
        private void DgvSuppliers_MouseDown(object? sender, MouseEventArgs e)
        {
            var hit = dgvSuppliers.HitTest(e.X, e.Y);
            if (hit.Type == DataGridViewHitTestType.None)
            {
                dgvSuppliers.ClearSelection();
            }
        }

        // ==================== LOGIC ====================
        private async void LoadSuppliersAsync()
        {
            var (success, errorMessage, suppliers) = await _supplierService.GetAllAsync();

            if (!success)
            {
                ShowError(errorMessage);
                return;
            }

            dgvSuppliers.DataSource = suppliers
                .Select(s => new
                {
                    s.SupplierId,
                    s.SupplierName,
                    s.ContactNumber,
                    Address = s.Address ?? string.Empty,
                    Status = s.IsActive ? "Active" : "Inactive"
                })
                .ToList();

            ClearInputs();
        }

        private void DgvSuppliers_SelectionChanged(object? sender, EventArgs e)
        {
            if (dgvSuppliers.SelectedRows.Count == 0)
            {
                _selectedSupplierId = 0;
                txtSupplierName.Text = string.Empty;
                txtContactNumber.Text = string.Empty;
                txtAddress.Text = string.Empty;
                return;
            }

            var row = dgvSuppliers.SelectedRows[0];

            var idValue = row.Cells["SupplierId"].Value;
            _selectedSupplierId = (idValue == null || idValue == DBNull.Value)
                ? 0
                : Convert.ToInt32(idValue);

            txtSupplierName.Text = row.Cells["SupplierName"].Value?.ToString() ?? string.Empty;
            txtContactNumber.Text = row.Cells["ContactNumber"].Value?.ToString() ?? string.Empty;
            txtAddress.Text = row.Cells["Address"].Value?.ToString() ?? string.Empty;
        }

        private async void BtnAdd_Click(object? sender, EventArgs e)
        {
            SetButtonsEnabled(false);
            try
            {
                var (success, errorMessage) = await _supplierService.AddAsync(
                    txtSupplierName.Text, txtContactNumber.Text, txtAddress.Text);

                if (success)
                {
                    ShowSuccess("Supplier added successfully.");
                    await ReloadGridAsync();
                }
                else
                {
                    ShowError(errorMessage);
                }
            }
            finally
            {
                SetButtonsEnabled(true);
            }
        }

        private async void BtnUpdate_Click(object? sender, EventArgs e)
        {
            if (_selectedSupplierId <= 0)
            {
                ShowError("Please select a supplier from the list first.");
                return;
            }

            SetButtonsEnabled(false);
            try
            {
                var (success, errorMessage) = await _supplierService.UpdateAsync(
                    _selectedSupplierId, txtSupplierName.Text, txtContactNumber.Text, txtAddress.Text);

                if (success)
                {
                    ShowSuccess("Supplier updated successfully.");
                    await ReloadGridAsync();
                }
                else
                {
                    ShowError(errorMessage);
                }
            }
            finally
            {
                SetButtonsEnabled(true);
            }
        }

        private async void BtnDeactivate_Click(object? sender, EventArgs e)
        {
            if (_selectedSupplierId <= 0)
            {
                ShowError("Please select a supplier from the list first.");
                return;
            }

            var confirm = MessageBox.Show(
                "Are you sure you want to deactivate this supplier?",
                "Confirm Deactivate",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            SetButtonsEnabled(false);
            try
            {
                var (success, errorMessage) = await _supplierService.DeactivateAsync(_selectedSupplierId);

                if (success)
                {
                    ShowSuccess("Supplier deactivated.");
                    await ReloadGridAsync();
                }
                else
                {
                    ShowError(errorMessage);
                }
            }
            finally
            {
                SetButtonsEnabled(true);
            }
        }

        private async void BtnReactivate_Click(object? sender, EventArgs e)
        {
            if (_selectedSupplierId <= 0)
            {
                ShowError("Please select a supplier from the list first.");
                return;
            }

            SetButtonsEnabled(false);
            try
            {
                var (success, errorMessage) = await _supplierService.ReactivateAsync(_selectedSupplierId);

                if (success)
                {
                    ShowSuccess("Supplier reactivated.");
                    await ReloadGridAsync();
                }
                else
                {
                    ShowError(errorMessage);
                }
            }
            finally
            {
                SetButtonsEnabled(true);
            }
        }

        private async Task ReloadGridAsync()
        {
            var (success, errorMessage, suppliers) = await _supplierService.GetAllAsync();

            if (!success)
            {
                ShowError(errorMessage);
                return;
            }

            dgvSuppliers.DataSource = suppliers
                .Select(s => new
                {
                    s.SupplierId,
                    s.SupplierName,
                    s.ContactNumber,
                    Address = s.Address ?? string.Empty,
                    Status = s.IsActive ? "Active" : "Inactive"
                })
                .ToList();

            ClearInputs();
        }

        private void ClearInputs()
        {
            txtSupplierName.Text = string.Empty;
            txtContactNumber.Text = string.Empty;
            txtAddress.Text = string.Empty;
            _selectedSupplierId = 0;
        }

        private void SetButtonsEnabled(bool enabled)
        {
            btnAdd.Enabled = enabled;
            btnUpdate.Enabled = enabled;
            btnDeactivate.Enabled = enabled;
            btnReactivate.Enabled = enabled;
        }

        private void ShowError(string message)
        {
            lblStatus.ForeColor = Color.Firebrick;
            lblStatus.Text = message;
        }

        private void ShowSuccess(string message)
        {
            lblStatus.ForeColor = Color.Green;
            lblStatus.Text = message;
        }
    }
}