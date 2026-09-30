using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using TodangMotor.Common;
using TodangMotor.Data;
using TodangMotor.Models;
using TodangMotor.Services;

namespace TodangMotor.Forms
{
    /// <summary>
    /// Add / Edit product popup.
    /// Owner-only, single-record editor.
    /// - Add mode: Owner can enter initial stock (logged as an Adjustment movement).
    /// - Edit mode: Owner can adjust stock (also logged).
    /// </summary>
    public class ProductForm : ShellForm
    {
        private readonly ProductService _productService;
        private readonly CategoryRepository _categoryRepository;

        private readonly Product? _productToEdit;
        private bool IsEditMode => _productToEdit != null;

        private int _originalQuantity;

        // ---- Layout constants ----
        private const int RowStep = 58;
        private const int LeftX = 32;
        private const int LeftLabelW = 110;
        private const int LeftFieldX = 148;
        private const int LeftFieldW = 260;

        private const int RightX = 440;
        private const int RightLabelW = 100;
        private const int RightFieldX = 548;
        private const int RightFieldW = 200;

        // ---- Fields ----
        private RoundedTextBox _txtProductName;
        private RoundedTextBox _txtBrand;
        private ComboBox _cmbCategory;
        private RoundedTextBox _txtUnit;
        private RoundedTextBox _txtCostPrice;
        private RoundedTextBox _txtSellingPrice;
        private RoundedTextBox _txtReorderLevel;
        private RoundedTextBox _txtQuantityOnHand;
        private Panel _notesWrapper;
        private TextBox _txtNotes;

        private Button _btnSave;
        private Button _btnCancel;
        private Label _lblStatus;

        // ============================================================
        // CONSTRUCTION
        // ============================================================

        public ProductForm(Product? productToEdit = null)
        {
            _productService = new ProductService();
            _categoryRepository = new CategoryRepository();
            _productToEdit = productToEdit;

            HeaderTitle = IsEditMode ? "Edit Product" : "Add Product";
            ShowMaximizeButton = false;
            ShowMinimizeButton = true;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(780, 560);
            MinimumSize = new Size(740, 540);
            BackColor = Theme.Background;
            KeyPreview = true;

            BuildLayout();

            Load += ProductForm_LoadAsync;
            KeyDown += ProductForm_KeyDown;
        }

        // ============================================================
        // LAYOUT
        // ============================================================

        private void BuildLayout()
        {
            int y = 24;

            // ---- Row 1: Product Name (left) | Brand (right) ----
            AddLabel("Product Name:", LeftX, y, LeftLabelW);
            _txtProductName = UiFactory.CreateTextBox(LeftFieldW);
            _txtProductName.Location = new Point(LeftFieldX, y - 8);
            _txtProductName.MaxLength = 150;
            ContentPanel.Controls.Add(_txtProductName);

            AddLabel("Brand:", RightX, y, RightLabelW);
            _txtBrand = UiFactory.CreateTextBox(RightFieldW);
            _txtBrand.Location = new Point(RightFieldX, y - 8);
            _txtBrand.MaxLength = 100;
            ContentPanel.Controls.Add(_txtBrand);
            y += RowStep;

            // ---- Row 2: Category (left) | Unit (right) ----
            AddLabel("Category:", LeftX, y, LeftLabelW);
            _cmbCategory = UiFactory.CreateComboBox(LeftFieldW);
            _cmbCategory.Location = new Point(LeftFieldX, y - 6);
            _cmbCategory.DisplayMember = "CategoryName";
            _cmbCategory.ValueMember = "CategoryId";
            ContentPanel.Controls.Add(_cmbCategory);

            AddLabel("Unit:", RightX, y, RightLabelW);
            _txtUnit = UiFactory.CreateTextBox(RightFieldW);
            _txtUnit.Location = new Point(RightFieldX, y - 8);
            _txtUnit.MaxLength = 20;
            ContentPanel.Controls.Add(_txtUnit);
            y += RowStep;

            // ---- Row 3: Cost Price (left) | Selling Price (right) ----
            AddLabel("Cost Price (PHP):", LeftX, y, LeftLabelW);
            _txtCostPrice = UiFactory.CreateTextBox(LeftFieldW);
            _txtCostPrice.Location = new Point(LeftFieldX, y - 8);
            _txtCostPrice.MaxLength = 12;
            ContentPanel.Controls.Add(_txtCostPrice);

            AddLabel("Selling Price (PHP):", RightX, y, RightLabelW);
            _txtSellingPrice = UiFactory.CreateTextBox(RightFieldW);
            _txtSellingPrice.Location = new Point(RightFieldX, y - 8);
            _txtSellingPrice.MaxLength = 12;
            ContentPanel.Controls.Add(_txtSellingPrice);
            y += RowStep;

            // ---- Row 4: Reorder Level (left) | Current Stock (right) ----
            AddLabel("Reorder Level:", LeftX, y, LeftLabelW);
            _txtReorderLevel = UiFactory.CreateTextBox(LeftFieldW);
            _txtReorderLevel.Location = new Point(LeftFieldX, y - 8);
            _txtReorderLevel.MaxLength = 6;
            ContentPanel.Controls.Add(_txtReorderLevel);

            AddLabel("Current Stock:", RightX, y, RightLabelW);
            _txtQuantityOnHand = UiFactory.CreateTextBox(RightFieldW);
            _txtQuantityOnHand.Location = new Point(RightFieldX, y - 8);
            _txtQuantityOnHand.MaxLength = 9;
            ContentPanel.Controls.Add(_txtQuantityOnHand);
            y += RowStep;

            // ---- Row 5: Notes (full width) ----
            AddLabel("Notes:", LeftX, y, LeftLabelW);

            _notesWrapper = new Panel
            {
                Location = new Point(LeftFieldX, y - 8),
                Size = new Size(RightFieldX + RightFieldW - LeftFieldX, 100),
                BackColor = Theme.InputBackground,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _notesWrapper.Paint += NotesWrapper_Paint;

            _txtNotes = new TextBox
            {
                Location = new Point(12, 10),
                Size = new Size(_notesWrapper.Width - 28, _notesWrapper.Height - 20),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
                BackColor = Theme.InputBackground,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontBody,
                MaxLength = 500,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _txtNotes.Enter += (s, e) => _notesWrapper.Invalidate();
            _txtNotes.Leave += (s, e) => _notesWrapper.Invalidate();
            _notesWrapper.Controls.Add(_txtNotes);
            _notesWrapper.Resize += (s, e) =>
            {
                _txtNotes.Size = new Size(
                    Math.Max(20, _notesWrapper.Width - 28),
                    Math.Max(20, _notesWrapper.Height - 20));
            };

            ContentPanel.Controls.Add(_notesWrapper);
            y += 100 + 20;

            // ---- Status ----
            _lblStatus = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.Danger,
                AutoSize = false,
                Location = new Point(LeftX, y),
                Size = new Size(ClientSize.Width - LeftX - 32, 22),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            ContentPanel.Controls.Add(_lblStatus);
            y += 30;

            // ---- Buttons (bottom-right) ----
            int btnW = 120;
            int btnH = 42;
            int btnY = y;

            _btnSave = UiFactory.CreateButton("Save", UiFactory.ButtonStyle.Primary, btnW, btnH);
            _btnSave.Location = new Point(ClientSize.Width - 32 - btnW, btnY);
            _btnSave.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnSave.Click += async (s, e) => await SaveAsync();

            _btnCancel = UiFactory.CreateButton("Cancel", UiFactory.ButtonStyle.Ghost, btnW, btnH);
            _btnCancel.Location = new Point(ClientSize.Width - 32 - btnW - 8 - btnW, btnY);
            _btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            ContentPanel.Controls.Add(_btnSave);
            ContentPanel.Controls.Add(_btnCancel);
        }

        private void AddLabel(string text, int x, int y, int width)
        {
            var lbl = new Label
            {
                Text = text,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(width, 20),
                Location = new Point(x, y),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            ContentPanel.Controls.Add(lbl);
        }

        private void NotesWrapper_Paint(object? sender, PaintEventArgs e)
        {
            if (_notesWrapper == null) return;
            bool focused = _txtNotes != null && _txtNotes.Focused;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, _notesWrapper.Width - 1, _notesWrapper.Height - 1);
            using var path = Theme.RoundedRect(rect, Theme.RadiusSmall);
            using var pen = new Pen(focused ? Theme.FocusBorder : Theme.InputBorder, focused ? 2f : 1f);
            e.Graphics.DrawPath(pen, path);
        }

        // ============================================================
        // LOAD
        // ============================================================

        private async void ProductForm_LoadAsync(object? sender, EventArgs e)
        {
            Animator.SlideFadeInForm(this, 180, 12);

            try
            {
                var categories = await _categoryRepository.GetActiveAsync();
                _cmbCategory.DataSource = categories;
            }
            catch (Exception)
            {
                ShowError("Could not load categories. Please check your database connection.");
                return;
            }

            if (IsEditMode)
            {
                var p = _productToEdit!;
                _txtProductName.Text = p.ProductName ?? string.Empty;
                _txtBrand.Text = p.Brand ?? string.Empty;
                _txtUnit.Text = p.Unit ?? "pcs";
                _txtCostPrice.Text = p.CostPrice.ToString("0.00");
                _txtSellingPrice.Text = p.SellingPrice.ToString("0.00");
                _txtReorderLevel.Text = p.ReorderLevel.ToString();
                _txtQuantityOnHand.Text = p.QuantityOnHand.ToString();
                _txtNotes.Text = p.Notes ?? string.Empty;
                _cmbCategory.SelectedValue = p.CategoryId;

                _originalQuantity = p.QuantityOnHand;
            }
            else
            {
                _txtUnit.Text = "pcs";
                _txtCostPrice.Text = "0";
                _txtReorderLevel.Text = "5";
                _txtQuantityOnHand.Text = "0";

                if (_cmbCategory.Items.Count > 0)
                    _cmbCategory.SelectedIndex = 0;

                _originalQuantity = 0;
            }
        }

        // ============================================================
        // SAVE
        // ============================================================

        private async Task SaveAsync()
        {
            if (_cmbCategory.SelectedValue == null)
            {
                ShowError("Please select a category.");
                return;
            }

            int categoryId = (int)_cmbCategory.SelectedValue;

            SetBusy(true);
            try
            {
                if (IsEditMode)
                {
                    // 1. Save editable fields (and Notes).
                    var update = await _productService.UpdateAsync(
                        _productToEdit!.ProductId,
                        categoryId,
                        _txtProductName.Text,
                        _txtBrand.Text,
                        _txtUnit.Text,
                        _txtCostPrice.Text,
                        _txtSellingPrice.Text,
                        _txtReorderLevel.Text,
                        _txtNotes.Text);

                    if (!update.Success)
                    {
                        ShowError(update.ErrorMessage);
                        return;
                    }

                    // 2. If stock changed, log an adjustment.
                    if (int.TryParse(_txtQuantityOnHand.Text.Trim(), out int newQty)
                        && newQty != _originalQuantity)
                    {
                        var stockResult = await _productService.UpdateStockAsync(
                            _productToEdit.ProductId,
                            newQty,
                            _txtNotes.Text);

                        if (!stockResult.Success)
                        {
                            ShowError(stockResult.ErrorMessage);
                            return;
                        }
                    }

                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    // Add mode: create the product.
                    var add = await _productService.AddAsync(
                        categoryId,
                        _txtProductName.Text,
                        _txtBrand.Text,
                        _txtUnit.Text,
                        _txtCostPrice.Text,
                        _txtSellingPrice.Text,
                        _txtReorderLevel.Text,
                        _txtNotes.Text);

                    if (!add.Success)
                    {
                        ShowError(add.ErrorMessage);
                        return;
                    }

                    // If Owner entered initial stock, log it as an adjustment.
                    if (int.TryParse(_txtQuantityOnHand.Text.Trim(), out int initialQty)
                        && initialQty > 0)
                    {
                        var stockResult = await _productService.UpdateStockAsync(
                            add.NewProductId,
                            initialQty,
                            _txtNotes.Text);

                        if (!stockResult.Success)
                        {
                            ShowError("Product created, but stock could not be set: " + stockResult.ErrorMessage);
                            return;
                        }
                    }

                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
            finally
            {
                SetBusy(false);
            }
        }

        // ============================================================
        // EVENTS
        // ============================================================

        private void ProductForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private void SetBusy(bool busy)
        {
            _btnSave.Enabled = !busy;
            _btnCancel.Enabled = !busy;
            _txtProductName.Enabled = !busy;
            _txtBrand.Enabled = !busy;
            _cmbCategory.Enabled = !busy;
            _txtUnit.Enabled = !busy;
            _txtCostPrice.Enabled = !busy;
            _txtSellingPrice.Enabled = !busy;
            _txtReorderLevel.Enabled = !busy;
            _txtQuantityOnHand.Enabled = !busy;
            _txtNotes.Enabled = !busy;
        }

        private void ShowError(string message)
        {
            _lblStatus.ForeColor = Theme.Danger;
            _lblStatus.Text = message ?? string.Empty;
        }
    }
}