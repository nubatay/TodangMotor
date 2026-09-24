using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using TodangMotor.Data;
using TodangMotor.Models;
using TodangMotor.Services;

namespace TodangMotor.Forms
{
    public class ProductListForm : Form
    {
        // --- Win32 helper for dragging a borderless window ---
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        // --- Win32 constants for edge-resize hit testing ---
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

        // --- Colors (matches CategoryForm / SupplierForm / ProductForm) ---
        private readonly Color _bgColor = Color.FromArgb(250, 248, 245);
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
        private readonly Color _lowStockRow = Color.FromArgb(255, 235, 235);

        private const int HeaderHeight = 52;

        private readonly ProductService _productService;
        private readonly CategoryRepository _categoryRepository;

        // In-memory copies used for filtering without hitting the DB again.
        private List<Product> _allProducts = new();
        private Dictionary<int, string> _categoryLookup = new();
        private int _selectedProductId = 0;

        // --- Window chrome controls ---
        private Panel headerPanel = null!;
        private Label lblHeaderTitle = null!;
        private Button btnMinimize = null!;
        private Button btnCloseChrome = null!;

        // --- Filter row controls ---
        private Label lblSearch = null!;
        private Panel txtSearchWrapper = null!;
        private TextBox txtSearch = null!;
        private Label lblCategoryFilter = null!;
        private ComboBox cmbCategoryFilter = null!;
        private CheckBox chkShowInactive = null!;

        // --- Grid ---
        private DataGridView dgvProducts = null!;

        // --- Action buttons ---
        private Button btnAddNew = null!;
        private Button btnEdit = null!;
        private Button btnDeactivate = null!;
        private Button btnReactivate = null!;
        private Button btnClose = null!;
        private Label lblStatus = null!;

        // Small helper class just for the category filter dropdown
        // (needs a "All Categories" option that isn't a real Category row).
        private class CategoryFilterItem
        {
            public int CategoryId { get; set; }
            public string CategoryName { get; set; } = string.Empty;
        }

        public ProductListForm()
        {
            _productService = new ProductService();
            _categoryRepository = new CategoryRepository();

            InitializeControls();
            this.Load += ProductListForm_Load;

            LoadDataAsync();
        }

        private void InitializeControls()
        {
            this.Text = "Inventory - Product Management";
            this.Size = new Size(1020, 650);
            this.MinimumSize = new Size(900, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = _bgColor;
            this.KeyPreview = true;
            this.DoubleBuffered = true;

            this.KeyDown += ProductListForm_KeyDown;
            this.Resize += ProductListForm_Resize;

            BuildHeader();

            // ---- Filter row: search + category dropdown + show inactive ----
            lblSearch = new Label
            {
                Text = "Search:",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = _textColor,
                AutoSize = true,
                Location = new Point(30, HeaderHeight + 22),
                BackColor = Color.Transparent
            };

            txtSearchWrapper = new Panel
            {
                Location = new Point(95, HeaderHeight + 17),
                Size = new Size(230, 32),
                BackColor = _inputBg
            };
            txtSearchWrapper.Paint += (s, e) => PaintInputWrapper(e, txtSearchWrapper, txtSearch);

            txtSearch = new TextBox
            {
                Location = new Point(10, 6),
                Size = new Size(210, 20),
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.None,
                BackColor = _inputBg,
                ForeColor = _textColor,
                PlaceholderText = "Product name or brand..."
            };
            txtSearch.Enter += (s, e) => txtSearchWrapper.Invalidate();
            txtSearch.Leave += (s, e) => txtSearchWrapper.Invalidate();
            txtSearch.TextChanged += (s, e) => ApplyFiltersAndBind();
            txtSearchWrapper.Controls.Add(txtSearch);

            lblCategoryFilter = new Label
            {
                Text = "Category:",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = _textColor,
                AutoSize = true,
                Location = new Point(345, HeaderHeight + 22),
                BackColor = Color.Transparent
            };

            cmbCategoryFilter = new ComboBox
            {
                Location = new Point(415, HeaderHeight + 18),
                Size = new Size(200, 30),
                Font = new Font("Segoe UI", 10f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                DisplayMember = "CategoryName",
                ValueMember = "CategoryId"
            };
            cmbCategoryFilter.SelectedIndexChanged += (s, e) => ApplyFiltersAndBind();

            chkShowInactive = new CheckBox
            {
                Text = "Show inactive products",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = _textColor,
                AutoSize = true,
                Location = new Point(635, HeaderHeight + 24),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            chkShowInactive.CheckedChanged += (s, e) => ApplyFiltersAndBind();

            // ---- Grid ----
            dgvProducts = new DataGridView
            {
                Location = new Point(30, HeaderHeight + 65),
                Size = new Size(960, 400),
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

            dgvProducts.ColumnHeadersDefaultCellStyle.BackColor = _gridHeaderBg;
            dgvProducts.ColumnHeadersDefaultCellStyle.ForeColor = _headerTitleColor;
            dgvProducts.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            dgvProducts.ColumnHeadersDefaultCellStyle.SelectionBackColor = _gridHeaderBg;
            dgvProducts.ColumnHeadersDefaultCellStyle.SelectionForeColor = _headerTitleColor;
            dgvProducts.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 0, 0);

            dgvProducts.DefaultCellStyle.BackColor = Color.White;
            dgvProducts.DefaultCellStyle.ForeColor = _textColor;
            dgvProducts.DefaultCellStyle.SelectionBackColor = Color.FromArgb(255, 244, 230);
            dgvProducts.DefaultCellStyle.SelectionForeColor = _orange;
            dgvProducts.DefaultCellStyle.Padding = new Padding(8, 0, 0, 0);
            dgvProducts.AlternatingRowsDefaultCellStyle.BackColor = _gridAltRow;
            dgvProducts.AlternatingRowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(255, 244, 230);
            dgvProducts.AlternatingRowsDefaultCellStyle.SelectionForeColor = _orange;

            dgvProducts.SelectionChanged += DgvProducts_SelectionChanged;
            dgvProducts.MouseDown += DgvProducts_MouseDown;
            dgvProducts.CellDoubleClick += DgvProducts_CellDoubleClick;
            dgvProducts.DataBindingComplete += DgvProducts_DataBindingComplete;

            // ---- Action buttons ----
            btnAddNew = new Button
            {
                Text = "Add New",
                Location = new Point(30, HeaderHeight + 490),
                Size = new Size(110, 36),
                BackColor = _orange,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            btnAddNew.FlatAppearance.BorderSize = 0;
            btnAddNew.FlatAppearance.MouseOverBackColor = _orangeHover;
            btnAddNew.MouseEnter += (s, e) => btnAddNew.BackColor = _orangeHover;
            btnAddNew.MouseLeave += (s, e) => btnAddNew.BackColor = _orange;
            btnAddNew.Click += BtnAddNew_Click;

            btnEdit = new Button
            {
                Text = "Edit",
                Location = new Point(150, HeaderHeight + 490),
                Size = new Size(100, 36),
                BackColor = Color.White,
                ForeColor = _textColor,
                Font = new Font("Segoe UI", 10f),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            btnEdit.FlatAppearance.BorderSize = 1;
            btnEdit.FlatAppearance.BorderColor = _inputBorder;
            btnEdit.MouseEnter += (s, e) => btnEdit.ForeColor = _orange;
            btnEdit.MouseLeave += (s, e) => btnEdit.ForeColor = _textColor;
            btnEdit.Click += BtnEdit_Click;

            btnDeactivate = new Button
            {
                Text = "Deactivate",
                Location = new Point(270, HeaderHeight + 490),
                Size = new Size(110, 36),
                BackColor = Color.White,
                ForeColor = _textColor,
                Font = new Font("Segoe UI", 10f),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            btnDeactivate.FlatAppearance.BorderSize = 1;
            btnDeactivate.FlatAppearance.BorderColor = _inputBorder;
            btnDeactivate.MouseEnter += (s, e) => btnDeactivate.ForeColor = _orange;
            btnDeactivate.MouseLeave += (s, e) => btnDeactivate.ForeColor = _textColor;
            btnDeactivate.Click += BtnDeactivate_Click;

            btnReactivate = new Button
            {
                Text = "Reactivate",
                Location = new Point(390, HeaderHeight + 490),
                Size = new Size(110, 36),
                BackColor = Color.White,
                ForeColor = _textColor,
                Font = new Font("Segoe UI", 10f),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            btnReactivate.FlatAppearance.BorderSize = 1;
            btnReactivate.FlatAppearance.BorderColor = _inputBorder;
            btnReactivate.MouseEnter += (s, e) => btnReactivate.ForeColor = _orange;
            btnReactivate.MouseLeave += (s, e) => btnReactivate.ForeColor = _textColor;
            btnReactivate.Click += BtnReactivate_Click;

            btnClose = new Button
            {
                Text = "Close",
                Location = new Point(870, HeaderHeight + 490),
                Size = new Size(90, 36),
                BackColor = Color.White,
                ForeColor = _textColor,
                Font = new Font("Segoe UI", 10f),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnClose.FlatAppearance.BorderSize = 1;
            btnClose.FlatAppearance.BorderColor = _inputBorder;
            btnClose.MouseEnter += (s, e) => btnClose.ForeColor = _orange;
            btnClose.MouseLeave += (s, e) => btnClose.ForeColor = _textColor;
            btnClose.Click += (s, e) => this.Close();

            lblStatus = new Label
            {
                Text = string.Empty,
                AutoSize = false,
                Font = new Font("Segoe UI", 9f),
                Size = new Size(960, 22),
                Location = new Point(30, HeaderHeight + 535),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ForeColor = Color.Firebrick,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft
            };

            this.Controls.Add(lblSearch);
            this.Controls.Add(txtSearchWrapper);
            this.Controls.Add(lblCategoryFilter);
            this.Controls.Add(cmbCategoryFilter);
            this.Controls.Add(chkShowInactive);
            this.Controls.Add(dgvProducts);
            this.Controls.Add(btnAddNew);
            this.Controls.Add(btnEdit);
            this.Controls.Add(btnDeactivate);
            this.Controls.Add(btnReactivate);
            this.Controls.Add(btnClose);
            this.Controls.Add(lblStatus);

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

            lblHeaderTitle = new Label
            {
                Text = "Inventory - Product Management",
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
            btnMinimize.Click += (s, e) => this.WindowState = FormWindowState.Minimized;
            btnMinimize.MouseEnter += (s, e) => btnMinimize.ForeColor = _chromeHoverFg;
            btnMinimize.MouseLeave += (s, e) => btnMinimize.ForeColor = _chromeIcon;

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
        private void ProductListForm_Load(object? sender, EventArgs e)
        {
            this.Region = new Region(GetRoundedRectPath(this.ClientRectangle, 18));
            RoundControl(txtSearchWrapper, 10);
            RoundControl(btnAddNew, 8);
            RoundControl(btnEdit, 8);
            RoundControl(btnDeactivate, 8);
            RoundControl(btnReactivate, 8);
            RoundControl(btnClose, 8);
        }

        private void ProductListForm_Resize(object? sender, EventArgs e)
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

        private void PaintInputWrapper(PaintEventArgs e, Panel wrapper, TextBox box)
        {
            bool focused = box != null && box.Focused;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, wrapper.Width - 1, wrapper.Height - 1);
            using var path = GetRoundedRectPath(rect, 10);
            using var pen = new Pen(focused ? _inputBorderFocus : _inputBorder, focused ? 1.6f : 1f);
            e.Graphics.DrawPath(pen, path);
        }

        private void ProductListForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
            }
        }

        private void DgvProducts_MouseDown(object? sender, MouseEventArgs e)
        {
            var hit = dgvProducts.HitTest(e.X, e.Y);
            if (hit.Type == DataGridViewHitTestType.None)
            {
                dgvProducts.ClearSelection();
            }
        }

        private void DgvProducts_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && _selectedProductId > 0)
            {
                OpenEditForSelectedProduct();
            }
        }

        // Paints low-stock rows with a light red/pink background so they
        // stand out at a glance. Runs every time the grid gets new data.
        private void DgvProducts_DataBindingComplete(object? sender, DataGridViewBindingCompleteEventArgs e)
        {
            foreach (DataGridViewRow row in dgvProducts.Rows)
            {
                var qtyValue = row.Cells["QuantityOnHand"].Value;
                var reorderValue = row.Cells["ReorderLevel"].Value;

                if (qtyValue == null || reorderValue == null) continue;

                int qty = Convert.ToInt32(qtyValue);
                int reorder = Convert.ToInt32(reorderValue);

                if (qty <= reorder)
                {
                    row.DefaultCellStyle.BackColor = _lowStockRow;
                }
            }
        }

        // ==================== LOAD / FILTER LOGIC ====================
        private async void LoadDataAsync()
        {
            // Lookup dictionary built from ALL categories (not just active)
            // so that even an inactive product with an inactive category
            // still shows a proper name instead of blank text.
            try
            {
                var allCategories = await _categoryRepository.GetAllAsync();
                _categoryLookup = allCategories.ToDictionary(c => c.CategoryId, c => c.CategoryName);

                var activeCategories = await _categoryRepository.GetActiveAsync();
                var filterItems = new List<CategoryFilterItem>
                {
                    new CategoryFilterItem { CategoryId = 0, CategoryName = "All Categories" }
                };
                filterItems.AddRange(activeCategories.Select(c => new CategoryFilterItem
                {
                    CategoryId = c.CategoryId,
                    CategoryName = c.CategoryName
                }));

                cmbCategoryFilter.DataSource = filterItems;
                cmbCategoryFilter.SelectedIndex = 0;
            }
            catch (Exception)
            {
                ShowError("Could not load categories. Please check your database connection.");
                return;
            }

            var (success, errorMessage, products) = await _productService.GetAllAsync();
            if (!success)
            {
                ShowError(errorMessage);
                return;
            }

            _allProducts = products;
            ApplyFiltersAndBind();
        }

        private async Task ReloadProductsAsync()
        {
            var (success, errorMessage, products) = await _productService.GetAllAsync();
            if (!success)
            {
                ShowError(errorMessage);
                return;
            }

            _allProducts = products;
            ApplyFiltersAndBind();
        }

        // Filters the in-memory product list based on the current search
        // text, category dropdown, and show-inactive checkbox, then
        // re-binds the grid. No database call needed for filtering.
        private void ApplyFiltersAndBind()
        {
            IEnumerable<Product> filtered = _allProducts;

            if (!chkShowInactive.Checked)
            {
                filtered = filtered.Where(p => p.IsActive);
            }

            if (cmbCategoryFilter.SelectedItem is CategoryFilterItem selectedCategory && selectedCategory.CategoryId > 0)
            {
                filtered = filtered.Where(p => p.CategoryId == selectedCategory.CategoryId);
            }

            var searchText = txtSearch.Text.Trim();
            if (!string.IsNullOrEmpty(searchText))
            {
                filtered = filtered.Where(p =>
                    (p.ProductName?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (p.Brand?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            var displayRows = filtered
                .Select(p => new
                {
                    p.ProductId,
                    p.ProductName,
                    Brand = p.Brand ?? string.Empty,
                    CategoryName = _categoryLookup.TryGetValue(p.CategoryId, out var name) ? name : "(Unknown)",
                    p.Unit,
                    p.CostPrice,
                    p.SellingPrice,
                    p.QuantityOnHand,
                    p.ReorderLevel,
                    Status = p.IsActive ? "Active" : "Inactive"
                })
                .ToList();

            dgvProducts.DataSource = displayRows;

            if (dgvProducts.Columns["ProductId"] != null)
                dgvProducts.Columns["ProductId"].Visible = false;

            RenameColumnHeaders();
            FormatPriceColumns();
        }

        private void RenameColumnHeaders()
        {
            SetHeaderIfExists("ProductName", "Product Name");
            SetHeaderIfExists("Brand", "Brand");
            SetHeaderIfExists("CategoryName", "Category");
            SetHeaderIfExists("Unit", "Unit");
            SetHeaderIfExists("CostPrice", "Cost Price");
            SetHeaderIfExists("SellingPrice", "Selling Price");
            SetHeaderIfExists("QuantityOnHand", "Stock");
            SetHeaderIfExists("ReorderLevel", "Reorder Lvl");
            SetHeaderIfExists("Status", "Status");
        }

        private void SetHeaderIfExists(string columnName, string headerText)
        {
            if (dgvProducts.Columns[columnName] != null)
                dgvProducts.Columns[columnName].HeaderText = headerText;
        }

        private void FormatPriceColumns()
        {
            if (dgvProducts.Columns["CostPrice"] != null)
                dgvProducts.Columns["CostPrice"].DefaultCellStyle.Format = "N2";

            if (dgvProducts.Columns["SellingPrice"] != null)
                dgvProducts.Columns["SellingPrice"].DefaultCellStyle.Format = "N2";
        }

        private void DgvProducts_SelectionChanged(object? sender, EventArgs e)
        {
            if (dgvProducts.SelectedRows.Count == 0)
            {
                _selectedProductId = 0;
                return;
            }

            var row = dgvProducts.SelectedRows[0];
            var idValue = row.Cells["ProductId"].Value;
            _selectedProductId = (idValue == null || idValue == DBNull.Value)
                ? 0
                : Convert.ToInt32(idValue);
        }

        // ==================== ACTIONS ====================
        private void BtnAddNew_Click(object? sender, EventArgs e)
        {
            using var form = new ProductForm();
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                ShowSuccess("Product added successfully.");
                _ = ReloadProductsAsync();
            }
        }

        private void BtnEdit_Click(object? sender, EventArgs e)
        {
            if (_selectedProductId <= 0)
            {
                ShowError("Please select a product from the list first.");
                return;
            }

            OpenEditForSelectedProduct();
        }

        private void OpenEditForSelectedProduct()
        {
            var product = _allProducts.FirstOrDefault(p => p.ProductId == _selectedProductId);
            if (product == null)
            {
                ShowError("Product not found. Please refresh the list.");
                return;
            }

            using var form = new ProductForm(product);
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                ShowSuccess("Product updated successfully.");
                _ = ReloadProductsAsync();
            }
        }

        private async void BtnDeactivate_Click(object? sender, EventArgs e)
        {
            if (_selectedProductId <= 0)
            {
                ShowError("Please select a product from the list first.");
                return;
            }

            var product = _allProducts.FirstOrDefault(p => p.ProductId == _selectedProductId);
            if (product == null)
            {
                ShowError("Product not found. Please refresh the list.");
                return;
            }

            // Warn-and-allow: if stock remains, tell the Owner before they
            // proceed, but never block the action itself.
            string confirmMessage = product.QuantityOnHand > 0
                ? $"This product still has {product.QuantityOnHand} {product.Unit} in stock. Deactivate anyway?"
                : "Are you sure you want to deactivate this product?";

            var confirm = MessageBox.Show(
                confirmMessage,
                "Confirm Deactivate",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            SetActionButtonsEnabled(false);
            try
            {
                var (success, errorMessage) = await _productService.DeactivateAsync(_selectedProductId);

                if (success)
                {
                    ShowSuccess("Product deactivated.");
                    await ReloadProductsAsync();
                }
                else
                {
                    ShowError(errorMessage);
                }
            }
            finally
            {
                SetActionButtonsEnabled(true);
            }
        }

        private async void BtnReactivate_Click(object? sender, EventArgs e)
        {
            if (_selectedProductId <= 0)
            {
                ShowError("Please select a product from the list first.");
                return;
            }

            SetActionButtonsEnabled(false);
            try
            {
                var (success, errorMessage) = await _productService.ReactivateAsync(_selectedProductId);

                if (success)
                {
                    ShowSuccess("Product reactivated.");
                    await ReloadProductsAsync();
                }
                else
                {
                    ShowError(errorMessage);
                }
            }
            finally
            {
                SetActionButtonsEnabled(true);
            }
        }

        private void SetActionButtonsEnabled(bool enabled)
        {
            btnAddNew.Enabled = enabled;
            btnEdit.Enabled = enabled;
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