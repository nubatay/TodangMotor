using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using TodangMotor.Data;
using TodangMotor.Models;
using TodangMotor.Services;

namespace TodangMotor.Forms
{
    public class CategoryForm : Form
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

        // --- Colors (MODERN LIGHT theme, matching LoginForm / OwnerDashboard) ---
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

        private readonly CategoryService _categoryService;

        // --- Window chrome controls ---
        private Panel headerPanel;
        private Label lblHeaderTitle;
        private Button btnMinimize;
        private Button btnCloseChrome;

        // --- Functional controls (names preserved exactly) ---
        private DataGridView dgvCategories = null!;
        private TextBox txtCategoryName = null!;
        private Label lblCategoryName = null!;
        private Button btnAdd = null!;
        private Button btnUpdate = null!;
        private Button btnDeactivate = null!;
        private Button btnReactivate = null!;
        private Button btnClose = null!;
        private Label lblStatus = null!;

        // Wrapper panel for the TextBox so we can give it rounded corners
        private Panel txtCategoryNameWrapper = null!;

        private int _selectedCategoryId = 0;

        public CategoryForm()
        {
            var categoryRepository = new CategoryRepository();
            _categoryService = new CategoryService(categoryRepository);

            InitializeControls();
            LoadCategoriesAsync();
        }

        private void InitializeControls()
        {
            // --- Form settings (borderless, opens centered at normal size) ---
            this.Text = "Category Management";
            this.Size = new Size(760, 550);
            this.MinimumSize = new Size(620, 510);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = _bgColor;
            this.KeyPreview = true;
            this.DoubleBuffered = true;

            this.Load += CategoryForm_Load;
            this.KeyDown += CategoryForm_KeyDown;
            this.Resize += CategoryForm_Resize;

            BuildHeader();

            // ---- Grid ----
            dgvCategories = new DataGridView
            {
                Location = new Point(30, HeaderHeight + 20),
                Size = new Size(700, 300),
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

            // Header row styling
            dgvCategories.ColumnHeadersDefaultCellStyle.BackColor = _gridHeaderBg;
            dgvCategories.ColumnHeadersDefaultCellStyle.ForeColor = _headerTitleColor;
            dgvCategories.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            dgvCategories.ColumnHeadersDefaultCellStyle.SelectionBackColor = _gridHeaderBg;
            dgvCategories.ColumnHeadersDefaultCellStyle.SelectionForeColor = _headerTitleColor;
            dgvCategories.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 0, 0);

            // Body row styling
            dgvCategories.DefaultCellStyle.BackColor = Color.White;
            dgvCategories.DefaultCellStyle.ForeColor = _textColor;
            dgvCategories.DefaultCellStyle.SelectionBackColor = Color.FromArgb(255, 244, 230);
            dgvCategories.DefaultCellStyle.SelectionForeColor = _orange;
            dgvCategories.DefaultCellStyle.Padding = new Padding(8, 0, 0, 0);
            dgvCategories.AlternatingRowsDefaultCellStyle.BackColor = _gridAltRow;
            dgvCategories.AlternatingRowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(255, 244, 230);
            dgvCategories.AlternatingRowsDefaultCellStyle.SelectionForeColor = _orange;

            dgvCategories.SelectionChanged += DgvCategories_SelectionChanged;

            // NEW: clicking on empty grid space (below the last row) clears
            // the selection, which in turn clears the textbox via the
            // SelectionChanged handler above.
            dgvCategories.MouseDown += DgvCategories_MouseDown;

            // ---- Name label ----
            lblCategoryName = new Label
            {
                Text = "Category Name:",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = _textColor,
                AutoSize = true,
                Location = new Point(30, HeaderHeight + 340),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                BackColor = Color.Transparent
            };

            // ---- TextBox wrapped in a rounded container (visual only) ----
            txtCategoryNameWrapper = new Panel
            {
                Location = new Point(150, HeaderHeight + 335),
                Size = new Size(300, 34),
                BackColor = _inputBg,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            txtCategoryNameWrapper.Paint += InputWrapper_Paint;

            txtCategoryName = new TextBox
            {
                Location = new Point(12, 7),
                Size = new Size(276, 22),
                Font = new Font("Segoe UI", 10.5f),
                BorderStyle = BorderStyle.None,
                BackColor = _inputBg,
                ForeColor = _textColor,
                MaxLength = 100
            };
            txtCategoryName.Enter += (s, e) => txtCategoryNameWrapper.Invalidate();
            txtCategoryName.Leave += (s, e) => txtCategoryNameWrapper.Invalidate();

            txtCategoryNameWrapper.Controls.Add(txtCategoryName);

            // ---- Buttons (all preserved with same handlers) ----

            // Add — primary orange
            btnAdd = new Button
            {
                Text = "Add",
                Location = new Point(470, HeaderHeight + 335),
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

            // Update — primary orange
            btnUpdate = new Button
            {
                Text = "Update",
                Location = new Point(570, HeaderHeight + 335),
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

            // Deactivate — outlined / secondary
            btnDeactivate = new Button
            {
                Text = "Deactivate",
                Location = new Point(30, HeaderHeight + 380),
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

            // Reactivate — outlined / secondary
            btnReactivate = new Button
            {
                Text = "Reactivate",
                Location = new Point(150, HeaderHeight + 380),
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

            // Close — outlined / secondary (kept in addition to the header X)
            btnClose = new Button
            {
                Text = "Close",
                Location = new Point(570, HeaderHeight + 380),
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
                Location = new Point(30, HeaderHeight + 430),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ForeColor = Color.Firebrick,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // ---- Add all controls to the form ----
            this.Controls.Add(dgvCategories);
            this.Controls.Add(lblCategoryName);
            this.Controls.Add(txtCategoryNameWrapper);
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
                Text = "Category Management",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = _headerTitleColor,
                AutoSize = true,
                Location = new Point(20, 15),
                BackColor = Color.Transparent
            };
            lblHeaderTitle.MouseDown += HeaderPanel_MouseDown;

            // ---- Minimize button ----
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

            // ---- Close button (chrome) ----
            // FIX: switched from Unicode "✕" to plain "X" — the Unicode
            // multiplication symbol doesn't render in bold Segoe UI on the
            // client's machine (same root cause already fixed in LoginForm).
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
        private void CategoryForm_Load(object? sender, EventArgs e)
        {
            // Rounded window corners (18px) — matches the rest of the app.
            this.Region = new Region(GetRoundedRectPath(this.ClientRectangle, 18));
            RoundControl(txtCategoryNameWrapper, 10);
            RoundControl(btnAdd, 8);
            RoundControl(btnUpdate, 8);
            RoundControl(btnDeactivate, 8);
            RoundControl(btnReactivate, 8);
            RoundControl(btnClose, 8);
        }

        private void CategoryForm_Resize(object? sender, EventArgs e)
        {
            // Re-cut the rounded-corner shape to match the CURRENT window size.
            // Without this, resizing the window leaves the old (smaller) shape
            // in place, which invisibly clips anything positioned beyond the
            // original boundary (e.g., header buttons, right-anchored controls).
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

        // Focus highlight border for the TextBox wrapper
        private void InputWrapper_Paint(object? sender, PaintEventArgs e)
        {
            bool focused = txtCategoryName != null && txtCategoryName.Focused;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, txtCategoryNameWrapper.Width - 1, txtCategoryNameWrapper.Height - 1);
            using var path = GetRoundedRectPath(rect, 10);
            using var pen = new Pen(focused ? _inputBorderFocus : _inputBorder, focused ? 1.6f : 1f);
            e.Graphics.DrawPath(pen, path);
        }

        private void CategoryForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
            }
        }

        // NEW: clicking below the last row (empty grid space) clears the
        // current selection. HitTest tells us WHERE inside the grid the
        // click landed; "None" means it landed on empty space, not a cell.
        private void DgvCategories_MouseDown(object? sender, MouseEventArgs e)
        {
            var hit = dgvCategories.HitTest(e.X, e.Y);
            if (hit.Type == DataGridViewHitTestType.None)
            {
                dgvCategories.ClearSelection();
            }
        }

        // ==================== LOGIC (UNCHANGED) ====================
        private async void LoadCategoriesAsync()
        {
            try
            {
                var categories = await _categoryService.GetAllCategoriesAsync();

                dgvCategories.DataSource = categories
                    .Select(c => new
                    {
                        c.CategoryId,
                        c.CategoryName,
                        Status = c.IsActive ? "Active" : "Inactive"
                    })
                    .ToList();

                ClearInputs();
            }
            catch (Exception)
            {
                ShowError("Could not load categories. Please check your database connection.");
            }
        }

        private void DgvCategories_SelectionChanged(object? sender, EventArgs e)
        {
            if (dgvCategories.SelectedRows.Count == 0)
            {
                _selectedCategoryId = 0;
                txtCategoryName.Text = string.Empty;
                return;
            }

            var row = dgvCategories.SelectedRows[0];
            _selectedCategoryId = (int)row.Cells["CategoryId"].Value;
            txtCategoryName.Text = row.Cells["CategoryName"].Value.ToString();
        }

        private async void BtnAdd_Click(object? sender, EventArgs e)
        {
            SetButtonsEnabled(false);
            try
            {
                var (success, errorMessage) = await _categoryService.AddCategoryAsync(txtCategoryName.Text);

                if (success)
                {
                    ShowSuccess("Category added successfully.");
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
            if (_selectedCategoryId <= 0)
            {
                ShowError("Please select a category from the list first.");
                return;
            }

            SetButtonsEnabled(false);
            try
            {
                var (success, errorMessage) = await _categoryService.UpdateCategoryAsync(_selectedCategoryId, txtCategoryName.Text);

                if (success)
                {
                    ShowSuccess("Category updated successfully.");
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
            if (_selectedCategoryId <= 0)
            {
                ShowError("Please select a category from the list first.");
                return;
            }

            var confirm = MessageBox.Show(
                "Are you sure you want to deactivate this category?",
                "Confirm Deactivate",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            SetButtonsEnabled(false);
            try
            {
                var (success, errorMessage) = await _categoryService.DeactivateCategoryAsync(_selectedCategoryId);

                if (success)
                {
                    ShowSuccess("Category deactivated.");
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
            if (_selectedCategoryId <= 0)
            {
                ShowError("Please select a category from the list first.");
                return;
            }

            SetButtonsEnabled(false);
            try
            {
                var (success, errorMessage) = await _categoryService.ReactivateCategoryAsync(_selectedCategoryId);

                if (success)
                {
                    ShowSuccess("Category reactivated.");
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
            var categories = await _categoryService.GetAllCategoriesAsync();

            dgvCategories.DataSource = categories
                .Select(c => new
                {
                    c.CategoryId,
                    c.CategoryName,
                    Status = c.IsActive ? "Active" : "Inactive"
                })
                .ToList();

            ClearInputs();
        }

        private void ClearInputs()
        {
            txtCategoryName.Text = string.Empty;
            _selectedCategoryId = 0;
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