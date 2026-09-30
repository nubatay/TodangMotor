using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using TodangMotor.Data;
using TodangMotor.Models;
using TodangMotor.Services;

namespace TodangMotor.Forms
{
    public class ProductForm : Form
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

        // --- Colors (matches CategoryForm / SupplierForm exactly) ---
        private readonly Color _bgColor = Color.FromArgb(250, 248, 245);
        private readonly Color _headerBg = Color.White;
        private readonly Color _headerTitleColor = Color.FromArgb(45, 45, 45);
        private readonly Color _orange = Color.FromArgb(230, 126, 34);
        private readonly Color _orangeHover = Color.FromArgb(211, 84, 0);
        private readonly Color _textColor = Color.FromArgb(35, 35, 35);
        private readonly Color _mutedText = Color.FromArgb(140, 140, 140);
        private readonly Color _inputBg = Color.FromArgb(248, 249, 251);
        private readonly Color _disabledInputBg = Color.FromArgb(238, 238, 238);
        private readonly Color _inputBorder = Color.FromArgb(220, 223, 228);
        private readonly Color _inputBorderFocus = Color.FromArgb(230, 126, 34);
        private readonly Color _chromeIcon = Color.FromArgb(120, 120, 120);
        private readonly Color _chromeHoverBg = Color.FromArgb(238, 238, 238);
        private readonly Color _chromeHoverFg = Color.FromArgb(45, 45, 45);
        private readonly Color _closeHoverBg = Color.FromArgb(232, 17, 35);

        private const int HeaderHeight = 52;

        private readonly ProductService _productService;
        private readonly CategoryRepository _categoryRepository;

        // The product being edited, or null if we're adding a new one.
        private readonly Product? _productToEdit;
        private bool IsEditMode => _productToEdit != null;

        // --- Window chrome controls ---
        private Panel headerPanel = null!;
        private Label lblHeaderTitle = null!;
        private Button btnMinimize = null!;
        private Button btnCloseChrome = null!;

        // --- Functional controls ---
        private Label lblProductName = null!;
        private Panel txtProductNameWrapper = null!;
        private TextBox txtProductName = null!;

        private Label lblBrand = null!;
        private Panel txtBrandWrapper = null!;
        private TextBox txtBrand = null!;

        private Label lblCategory = null!;
        private ComboBox cmbCategory = null!;

        private Label lblUnit = null!;
        private Panel txtUnitWrapper = null!;
        private TextBox txtUnit = null!;

        private Label lblCostPrice = null!;
        private Panel txtCostPriceWrapper = null!;
        private TextBox txtCostPrice = null!;

        private Label lblSellingPrice = null!;
        private Panel txtSellingPriceWrapper = null!;
        private TextBox txtSellingPrice = null!;

        private Label lblReorderLevel = null!;
        private Panel txtReorderLevelWrapper = null!;
        private TextBox txtReorderLevel = null!;

        private Label lblQuantityOnHand = null!;
        private Panel txtQuantityOnHandWrapper = null!;
        private TextBox txtQuantityOnHand = null!;
        private Label lblQuantityHint = null!;

        private Button btnSave = null!;
        private Button btnCancel = null!;
        private Label lblStatus = null!;

        // Pass null for Add mode, or an existing Product for Edit mode.
        public ProductForm(Product? productToEdit = null)
        {
            _productService = new ProductService();
            _categoryRepository = new CategoryRepository();
            _productToEdit = productToEdit;

            InitializeControls();
            this.Load += ProductForm_LoadAsync;
        }

        private void InitializeControls()
        {
            this.Text = "Product Details";
            this.Size = new Size(700, 650);
            this.MinimumSize = new Size(620, 610);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = _bgColor;
            this.KeyPreview = true;
            this.DoubleBuffered = true;

            this.KeyDown += ProductForm_KeyDown;
            this.Resize += ProductForm_Resize;

            BuildHeader();

            int y = HeaderHeight + 30;
            int rowStep = 56;
            int labelX = 30;
            int fieldX = 170;
            int fieldWidth = 480;

            // ---- ProductName ----
            lblProductName = MakeLabel("Product Name:", labelX, y);
            (txtProductNameWrapper, txtProductName) = MakeTextInput(fieldX, y - 5, fieldWidth, 150);
            y += rowStep;

            // ---- Brand ----
            lblBrand = MakeLabel("Brand:", labelX, y);
            (txtBrandWrapper, txtBrand) = MakeTextInput(fieldX, y - 5, fieldWidth, 100);
            y += rowStep;

            // ---- Category ----
            lblCategory = MakeLabel("Category:", labelX, y);
            cmbCategory = new ComboBox
            {
                Location = new Point(fieldX, y - 5),
                Size = new Size(fieldWidth, 30),
                Font = new Font("Segoe UI", 10.5f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                DisplayMember = "CategoryName",
                ValueMember = "CategoryId"
            };
            y += rowStep;

            // ---- Unit ----
            lblUnit = MakeLabel("Unit:", labelX, y);
            (txtUnitWrapper, txtUnit) = MakeTextInput(fieldX, y - 5, fieldWidth, 20);
            y += rowStep;

            // ---- CostPrice ----
            lblCostPrice = MakeLabel("Cost Price:", labelX, y);
            (txtCostPriceWrapper, txtCostPrice) = MakeTextInput(fieldX, y - 5, 200, 10);
            y += rowStep;

            // ---- SellingPrice ----
            lblSellingPrice = MakeLabel("Selling Price:", labelX, y);
            (txtSellingPriceWrapper, txtSellingPrice) = MakeTextInput(fieldX, y - 5, 200, 10);
            y += rowStep;

            // ---- ReorderLevel ----
            lblReorderLevel = MakeLabel("Reorder Level:", labelX, y);
            (txtReorderLevelWrapper, txtReorderLevel) = MakeTextInput(fieldX, y - 5, 200, 6);
            y += rowStep;

            // ---- QuantityOnHand (read-only) ----
            lblQuantityOnHand = MakeLabel("Current Stock:", labelX, y);
            (txtQuantityOnHandWrapper, txtQuantityOnHand) = MakeTextInput(fieldX, y - 5, 200, 20);
            txtQuantityOnHand.ReadOnly = true;
            txtQuantityOnHand.TabStop = false;
            txtQuantityOnHand.BackColor = _disabledInputBg;
            txtQuantityOnHandWrapper.BackColor = _disabledInputBg;

            lblQuantityHint = new Label
            {
                Text = "Stock is managed via Stock-In, not editable here.",
                Font = new Font("Segoe UI", 8f),
                ForeColor = _mutedText,
                AutoSize = true,
                Location = new Point(fieldX + 210, y + 4),
                BackColor = Color.Transparent
            };
            y += rowStep + 10;

            // ---- Buttons ----
            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(fieldX + 280, y),
                Size = new Size(100, 36),
                BackColor = Color.White,
                ForeColor = _textColor,
                Font = new Font("Segoe UI", 10f),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 1;
            btnCancel.FlatAppearance.BorderColor = _inputBorder;
            btnCancel.MouseEnter += (s, e) => btnCancel.ForeColor = _orange;
            btnCancel.MouseLeave += (s, e) => btnCancel.ForeColor = _textColor;
            btnCancel.Click += (s, e) =>
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            btnSave = new Button
            {
                Text = "Save",
                Location = new Point(fieldX + 390, y),
                Size = new Size(100, 36),
                BackColor = _orange,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.FlatAppearance.MouseOverBackColor = _orangeHover;
            btnSave.MouseEnter += (s, e) => btnSave.BackColor = _orangeHover;
            btnSave.MouseLeave += (s, e) => btnSave.BackColor = _orange;
            btnSave.Click += BtnSave_Click;

            y += 50;

            // ---- Status label ----
            lblStatus = new Label
            {
                Text = string.Empty,
                AutoSize = false,
                Font = new Font("Segoe UI", 9f),
                Size = new Size(fieldWidth + (fieldX - labelX), 40),
                Location = new Point(labelX, y),
                ForeColor = Color.Firebrick,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.TopLeft
            };

            this.Controls.Add(lblProductName);
            this.Controls.Add(txtProductNameWrapper);
            this.Controls.Add(lblBrand);
            this.Controls.Add(txtBrandWrapper);
            this.Controls.Add(lblCategory);
            this.Controls.Add(cmbCategory);
            this.Controls.Add(lblUnit);
            this.Controls.Add(txtUnitWrapper);
            this.Controls.Add(lblCostPrice);
            this.Controls.Add(txtCostPriceWrapper);
            this.Controls.Add(lblSellingPrice);
            this.Controls.Add(txtSellingPriceWrapper);
            this.Controls.Add(lblReorderLevel);
            this.Controls.Add(txtReorderLevelWrapper);
            this.Controls.Add(lblQuantityOnHand);
            this.Controls.Add(txtQuantityOnHandWrapper);
            this.Controls.Add(lblQuantityHint);
            this.Controls.Add(btnCancel);
            this.Controls.Add(btnSave);
            this.Controls.Add(lblStatus);

            this.Controls.Add(headerPanel);
            headerPanel.BringToFront();
        }

        // ==================== SMALL BUILDER HELPERS ====================

        private Label MakeLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = _textColor,
                AutoSize = true,
                Location = new Point(x, y),
                BackColor = Color.Transparent
            };
        }

        private (Panel wrapper, TextBox box) MakeTextInput(int x, int y, int width, int maxLength)
        {
            var wrapper = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(width, 34),
                BackColor = _inputBg
            };

            var box = new TextBox
            {
                Location = new Point(12, 7),
                Size = new Size(width - 24, 22),
                Font = new Font("Segoe UI", 10.5f),
                BorderStyle = BorderStyle.None,
                BackColor = _inputBg,
                ForeColor = _textColor,
                MaxLength = maxLength
            };

            wrapper.Paint += (s, e) => PaintInputWrapper(e, wrapper, box);
            box.Enter += (s, e) => wrapper.Invalidate();
            box.Leave += (s, e) => wrapper.Invalidate();
            wrapper.Controls.Add(box);

            return (wrapper, box);
        }

        private void PaintInputWrapper(PaintEventArgs e, Panel wrapper, TextBox box)
        {
            bool focused = box.Focused && !box.ReadOnly;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, wrapper.Width - 1, wrapper.Height - 1);
            using var path = GetRoundedRectPath(rect, 10);
            using var pen = new Pen(focused ? _inputBorderFocus : _inputBorder, focused ? 1.6f : 1f);
            e.Graphics.DrawPath(pen, path);
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
                Text = "Product Details",
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
            btnCloseChrome.Click += (s, e) =>
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };
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
        private void ProductForm_Resize(object? sender, EventArgs e)
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

        private void ProductForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            }
        }

        // ==================== LOAD / POPULATE ====================
        private async void ProductForm_LoadAsync(object? sender, EventArgs e)
        {
            this.Region = new Region(GetRoundedRectPath(this.ClientRectangle, 18));

            try
            {
                var categories = await _categoryRepository.GetActiveAsync();
                cmbCategory.DataSource = categories;
            }
            catch (Exception)
            {
                ShowError("Could not load categories. Please check your database connection.");
                return;
            }

            if (IsEditMode)
            {
                lblHeaderTitle.Text = "Edit Product";
                txtProductName.Text = _productToEdit!.ProductName;
                txtBrand.Text = _productToEdit.Brand;
                txtUnit.Text = _productToEdit.Unit;
                txtCostPrice.Text = _productToEdit.CostPrice.ToString("0.00");
                txtSellingPrice.Text = _productToEdit.SellingPrice.ToString("0.00");
                txtReorderLevel.Text = _productToEdit.ReorderLevel.ToString();
                txtQuantityOnHand.Text = $"{_productToEdit.QuantityOnHand} {_productToEdit.Unit}";
                cmbCategory.SelectedValue = _productToEdit.CategoryId;
            }
            else
            {
                lblHeaderTitle.Text = "Add Product";
                txtUnit.Text = "pcs";
                txtCostPrice.Text = "0";
                txtReorderLevel.Text = "5";
                txtQuantityOnHand.Text = "0 (New Product)";
                if (cmbCategory.Items.Count > 0)
                    cmbCategory.SelectedIndex = 0;
            }
        }

        // ==================== SAVE ====================
        private async void BtnSave_Click(object? sender, EventArgs e)
        {
            if (cmbCategory.SelectedValue == null)
            {
                ShowError("Please select a category.");
                return;
            }

            int categoryId = (int)cmbCategory.SelectedValue;

            btnSave.Enabled = false;
            btnCancel.Enabled = false;

            try
            {
                (bool success, string errorMessage) result;

                if (IsEditMode)
                {
                    result = await _productService.UpdateAsync(
                        _productToEdit!.ProductId, categoryId, txtProductName.Text, txtBrand.Text,
                        txtUnit.Text, txtCostPrice.Text, txtSellingPrice.Text, txtReorderLevel.Text);
                }
                else
                {
                    result = await _productService.AddAsync(
                        categoryId, txtProductName.Text, txtBrand.Text,
                        txtUnit.Text, txtCostPrice.Text, txtSellingPrice.Text, txtReorderLevel.Text);
                }

                if (result.success)
                {
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    ShowError(result.errorMessage);
                }
            }
            finally
            {
                btnSave.Enabled = true;
                btnCancel.Enabled = true;
            }
        }

        private void ShowError(string message)
        {
            lblStatus.ForeColor = Color.Firebrick;
            lblStatus.Text = message;
        }
    }
}