using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TodangMotor.Common;
using TodangMotor.Data;
using TodangMotor.Models;
using TodangMotor.Services;

namespace TodangMotor.Forms
{
    /// <summary>
    /// Add / Edit product popup. Owner-only.
    /// Two-column stacked-label layout: label above each field.
    /// </summary>
    public class ProductForm : ShellForm
    {
        private readonly ProductService _productService = new();
        private readonly CategoryService _categoryService = new(new CategoryRepository());
        private readonly SupplierService _supplierService = new();

        private readonly Product? _productToEdit;
        private bool IsEditMode => _productToEdit != null;

        private List<Supplier> _allSuppliers = new();
        private List<Category> _allCategories = new();

        private readonly Dictionary<string, Supplier> _supplierByDisplay = new();
        private readonly Dictionary<string, Category> _categoryByDisplay = new();

        private class SupplierCheckItem
        {
            public int SupplierId { get; set; }
            public string DisplayName { get; set; } = string.Empty;
            public override string ToString() => DisplayName;
        }

        // ============================================================
        // LAYOUT CONSTANTS
        // ============================================================

        private const int PadX = 32;
        private const int ColGap = 32;
        private const int LabelH = 22;
        private const int FieldH = 44;
        private const int RowGap = 8;

        private const int ColW = 402;
        private const int LeftColX = PadX;
        private const int RightColX = LeftColX + ColW + ColGap;

        private const int PanelBlockH = 130;

        // ============================================================
        // UI CONTROLS
        // ============================================================

        private RoundedTextBox _txtProductName = null!;
        private RoundedTextBox _txtBrand = null!;
        private RoundedComboBox _cmbCategory = null!;
        private RoundedTextBox _txtUnit = null!;
        private RoundedTextBox _txtCostPrice = null!;
        private RoundedTextBox _txtSellingPrice = null!;
        private RoundedTextBox _txtReorderLevel = null!;
        private DateTimePicker _dtpExpiration = null!;
        private RoundedComboBox _cmbSupplier = null!;
        private CheckedListBox _alternateSuppliersList = null!;
        private Panel _alternateSuppliersWrapper = null!;
        private Panel _descriptionWrapper = null!;
        private TextBox _txtDescription = null!;

        private Button _btnSave = null!;
        private Button _btnCancel = null!;
        private Label _lblStatus = null!;

        // ============================================================
        // CONSTRUCTION
        // ============================================================

        public ProductForm(Product? productToEdit = null)
        {
            _productToEdit = productToEdit;

            HeaderTitle = IsEditMode ? "Edit Item" : "Add Item";
            ShowMaximizeButton = false;
            ShowMinimizeButton = true;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(900, 830);
            MinimumSize = new Size(880, 800);
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
            // ---------- Footer (docked bottom) ----------
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 88,
                BackColor = Theme.Background
            };

            var divider = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = Theme.Divider
            };
            footer.Controls.Add(divider);

            int btnW = 140;
            int btnH = UiFactory.PrimaryButtonHeight;

            // ---- Status label lives INSIDE the footer, left side ----
            _lblStatus = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.Danger,
                AutoSize = false,
                Location = new Point(PadX, 22),
                Height = btnH,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            _btnSave = UiFactory.CreateButton("Save", UiFactory.ButtonStyle.Primary, btnW, btnH);
            _btnSave.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnSave.Click += async (s, e) => await SaveAsync();

            _btnCancel = UiFactory.CreateButton("Cancel", UiFactory.ButtonStyle.Ghost, btnW, btnH);
            _btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            footer.Controls.Add(_lblStatus);
            footer.Controls.Add(_btnSave);
            footer.Controls.Add(_btnCancel);

            footer.Resize += (s, e) =>
            {
                _btnSave.Location = new Point(footer.ClientSize.Width - PadX - btnW, 22);
                _btnCancel.Location = new Point(footer.ClientSize.Width - PadX - btnW - 8 - btnW, 22);
                _lblStatus.Width = Math.Max(100,
                    footer.ClientSize.Width - PadX - btnW * 2 - 8 - PadX - 16);
            };

            ContentPanel.Controls.Add(footer);

            // ---------- Form body ----------
            int y = 24;

            // Row 1 — Product Name (full width)
            AddFieldLabel("Product Name", LeftColX, y, ColW * 2 + ColGap);
            _txtProductName = UiFactory.CreateTextBox(ColW * 2 + ColGap);
            _txtProductName.Location = new Point(LeftColX, y + LabelH + 4);
            _txtProductName.MaxLength = 150;
            ContentPanel.Controls.Add(_txtProductName);
            y += LabelH + 4 + FieldH + 24;

            // Row 2 — Brand | Unit
            AddFieldLabel("Brand", LeftColX, y, ColW);
            _txtBrand = UiFactory.CreateTextBox(ColW);
            _txtBrand.Location = new Point(LeftColX, y + LabelH + 4);
            _txtBrand.MaxLength = 100;
            ContentPanel.Controls.Add(_txtBrand);

            AddFieldLabel("Unit", RightColX, y, ColW);
            _txtUnit = UiFactory.CreateTextBox(ColW);
            _txtUnit.Location = new Point(RightColX, y + LabelH + 4);
            _txtUnit.MaxLength = 20;
            ContentPanel.Controls.Add(_txtUnit);
            y += LabelH + 4 + FieldH + RowGap;

            // Row 3 — Category | Reorder Level
            AddFieldLabel("Category", LeftColX, y, ColW);
            _cmbCategory = new RoundedComboBox
            {
                Width = ColW,
                Location = new Point(LeftColX, y + LabelH + 4)
            };
            ContentPanel.Controls.Add(_cmbCategory);

            AddFieldLabel("Reorder Level", RightColX, y, ColW);
            _txtReorderLevel = UiFactory.CreateTextBox(ColW);
            _txtReorderLevel.Location = new Point(RightColX, y + LabelH + 4);
            _txtReorderLevel.MaxLength = 6;
            ContentPanel.Controls.Add(_txtReorderLevel);
            y += LabelH + 4 + FieldH + RowGap;

            // Row 4 — Cost | Selling
            AddFieldLabel("Cost Price (₱)", LeftColX, y, ColW);
            _txtCostPrice = UiFactory.CreateTextBox(ColW);
            _txtCostPrice.Location = new Point(LeftColX, y + LabelH + 4);
            _txtCostPrice.MaxLength = 12;
            ContentPanel.Controls.Add(_txtCostPrice);

            AddFieldLabel("Selling Price (₱)", RightColX, y, ColW);
            _txtSellingPrice = UiFactory.CreateTextBox(ColW);
            _txtSellingPrice.Location = new Point(RightColX, y + LabelH + 4);
            _txtSellingPrice.MaxLength = 12;
            ContentPanel.Controls.Add(_txtSellingPrice);
            y += LabelH + 4 + FieldH + RowGap;

            // Row 5 — Expiration Date (half width, left)
            AddFieldLabel("Expiration Date (optional)", LeftColX, y, ColW);
            _dtpExpiration = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "MMM d, yyyy",
                ShowCheckBox = true,
                Checked = false,
                Font = Theme.FontBody,
                Location = new Point(LeftColX, y + LabelH + 4 + 6),
                Size = new Size(220, 32)
            };
            ContentPanel.Controls.Add(_dtpExpiration);
            y += LabelH + 4 + FieldH + 24;

            // Row 6 — Primary Supplier (full width)
            AddFieldLabel("Primary Supplier", LeftColX, y, ColW * 2 + ColGap);
            _cmbSupplier = new RoundedComboBox
            {
                Width = ColW * 2 + ColGap,
                Location = new Point(LeftColX, y + LabelH + 4)
            };
            ContentPanel.Controls.Add(_cmbSupplier);
            y += LabelH + 4 + FieldH + 24;

            // Row 7 — Alternate Suppliers | Description
            AddFieldLabel("Alternate Suppliers", LeftColX, y, ColW);
            AddFieldLabel("Description", RightColX, y, ColW);

            _alternateSuppliersWrapper = new Panel
            {
                Location = new Point(LeftColX, y + LabelH + 4),
                Size = new Size(ColW, PanelBlockH),
                BackColor = Theme.Surface
            };
            _alternateSuppliersWrapper.Paint += AlternateWrapper_Paint;

            _alternateSuppliersList = new CheckedListBox
            {
                Location = new Point(12, 10),
                Size = new Size(ColW - 24, PanelBlockH - 20),
                Font = Theme.FontBody,
                BackColor = Theme.Surface,
                ForeColor = Theme.TextPrimary,
                BorderStyle = BorderStyle.None,
                CheckOnClick = true,
                IntegralHeight = false,
                HorizontalScrollbar = false
            };
            _alternateSuppliersWrapper.Controls.Add(_alternateSuppliersList);
            ContentPanel.Controls.Add(_alternateSuppliersWrapper);

            _descriptionWrapper = new Panel
            {
                Location = new Point(RightColX, y + LabelH + 4),
                Size = new Size(ColW, PanelBlockH),
                BackColor = Theme.Surface
            };
            _descriptionWrapper.Paint += DescriptionWrapper_Paint;

            _txtDescription = new TextBox
            {
                Location = new Point(12, 12),
                Size = new Size(ColW - 24, PanelBlockH - 24),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
                BackColor = Theme.Surface,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontBody,
                MaxLength = 500
            };
            _txtDescription.Enter += (s, e) => _descriptionWrapper.Invalidate();
            _txtDescription.Leave += (s, e) => _descriptionWrapper.Invalidate();
            _descriptionWrapper.Controls.Add(_txtDescription);
            ContentPanel.Controls.Add(_descriptionWrapper);
        }

        private void AddFieldLabel(string text, int x, int y, int width)
        {
            var lbl = new Label
            {
                Text = text,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Location = new Point(x, y),
                Size = new Size(width, LabelH),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            ContentPanel.Controls.Add(lbl);
        }

        private void AlternateWrapper_Paint(object? sender, PaintEventArgs e)
        {
            if (_alternateSuppliersWrapper == null) return;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0,
                _alternateSuppliersWrapper.Width - 1,
                _alternateSuppliersWrapper.Height - 1);

            using var path = Theme.RoundedRect(rect, Theme.RadiusSmall);
            using var pen = new Pen(Theme.InputBorder, 1f);
            e.Graphics.DrawPath(pen, path);
        }

        private void DescriptionWrapper_Paint(object? sender, PaintEventArgs e)
        {
            if (_descriptionWrapper == null) return;
            bool focused = _txtDescription != null && _txtDescription.Focused;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0,
                _descriptionWrapper.Width - 1,
                _descriptionWrapper.Height - 1);

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

            await LoadCategoriesAsync();
            await LoadSuppliersAsync();

            if (IsEditMode)
            {
                PopulateFromProduct(_productToEdit!);
            }
            else
            {
                _txtUnit.Text = "pcs";
                _txtCostPrice.Text = "0";
                _txtSellingPrice.Text = "0";
                _txtReorderLevel.Text = "5";

                if (_cmbCategory.Items.Count > 0)
                    _cmbCategory.SelectedIndex = 0;

                if (_cmbSupplier.Items.Count > 0)
                    _cmbSupplier.SelectedIndex = 0;

                _txtProductName.FocusInput();
            }
        }

        private async Task LoadCategoriesAsync()
        {
            try
            {
                var cats = await _categoryService.GetActiveCategoriesAsync();
                _allCategories = cats?.ToList() ?? new List<Category>();

                _categoryByDisplay.Clear();
                _cmbCategory.Items.Clear();
                foreach (var c in _allCategories)
                {
                    _categoryByDisplay[c.CategoryName] = c;
                    _cmbCategory.Items.Add(c.CategoryName);
                }
            }
            catch
            {
                ShowError("Could not load categories. Please check your database connection.");
            }
        }

        private async Task LoadSuppliersAsync()
        {
            try
            {
                var (success, error, suppliers) = await _supplierService.GetActiveAsync();
                if (!success)
                {
                    ShowError(error);
                    return;
                }

                _allSuppliers = suppliers ?? new List<Supplier>();

                _supplierByDisplay.Clear();
                _cmbSupplier.Items.Clear();
                foreach (var s in _allSuppliers)
                {
                    _supplierByDisplay[s.SupplierName] = s;
                    _cmbSupplier.Items.Add(s.SupplierName);
                }

                _alternateSuppliersList.Items.Clear();
                foreach (var s in _allSuppliers)
                {
                    _alternateSuppliersList.Items.Add(new SupplierCheckItem
                    {
                        SupplierId = s.SupplierId,
                        DisplayName = s.SupplierName
                    });
                }
            }
            catch
            {
                ShowError("Could not load suppliers. Please check your database connection.");
            }
        }

        private void PopulateFromProduct(Product p)
        {
            _txtProductName.Text = p.ProductName ?? string.Empty;
            _txtBrand.Text = p.Brand ?? string.Empty;
            _txtUnit.Text = p.Unit ?? "pcs";
            _txtCostPrice.Text = p.CostPrice.ToString("0.00");
            _txtSellingPrice.Text = p.SellingPrice.ToString("0.00");
            _txtReorderLevel.Text = p.ReorderLevel.ToString();
            _txtDescription.Text = p.Description ?? string.Empty;

            if (p.CategoryId > 0 && _cmbCategory.Items.Count > 0)
            {
                var cat = _allCategories.FirstOrDefault(c => c.CategoryId == p.CategoryId);
                if (cat != null)
                    _cmbCategory.SelectedItem = cat.CategoryName;
            }

            if (p.SupplierId.HasValue && p.SupplierId.Value > 0)
            {
                var sup = _allSuppliers.FirstOrDefault(s => s.SupplierId == p.SupplierId.Value);
                if (sup != null)
                    _cmbSupplier.SelectedItem = sup.SupplierName;
            }

            if (p.ExpirationDate.HasValue)
            {
                _dtpExpiration.Value = p.ExpirationDate.Value;
                _dtpExpiration.Checked = true;
            }
            else
            {
                _dtpExpiration.Checked = false;
            }

            if (p.AlternateSupplierIds != null && p.AlternateSupplierIds.Count > 0)
            {
                for (int i = 0; i < _alternateSuppliersList.Items.Count; i++)
                {
                    if (_alternateSuppliersList.Items[i] is SupplierCheckItem item
                        && p.AlternateSupplierIds.Contains(item.SupplierId))
                    {
                        _alternateSuppliersList.SetItemChecked(i, true);
                    }
                }
            }

            _txtProductName.FocusInput();
            _txtProductName.SelectAll();
        }

        // ============================================================
        // SAVE
        // ============================================================

        private async Task SaveAsync()
        {
            // Clear previous error
            _lblStatus.Text = string.Empty;

            string? selectedCatName = _cmbCategory.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedCatName)
                || !_categoryByDisplay.TryGetValue(selectedCatName, out var selectedCategory))
            {
                ShowError("Please select a category.");
                return;
            }

            string? selectedSupName = _cmbSupplier.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedSupName)
                || !_supplierByDisplay.TryGetValue(selectedSupName, out var selectedSupplier))
            {
                ShowError("Please select a primary supplier.");
                return;
            }

            int categoryId = selectedCategory.CategoryId;
            int supplierId = selectedSupplier.SupplierId;

            DateTime? expiration = _dtpExpiration.Checked
                ? _dtpExpiration.Value.Date
                : (DateTime?)null;

            var alternateIds = new List<int>();
            foreach (var obj in _alternateSuppliersList.CheckedItems)
            {
                if (obj is SupplierCheckItem item)
                {
                    if (item.SupplierId != supplierId)
                        alternateIds.Add(item.SupplierId);
                }
            }

            SetBusy(true);
            try
            {
                if (IsEditMode)
                {
                    var result = await _productService.UpdateAsync(
                        _productToEdit!.ProductId,
                        categoryId,
                        _txtProductName.Text,
                        _txtBrand.Text,
                        _txtUnit.Text,
                        _txtCostPrice.Text,
                        _txtSellingPrice.Text,
                        _txtReorderLevel.Text,
                        string.Empty,
                        sku: null,
                        expiration,
                        _txtDescription.Text,
                        supplierId,
                        alternateIds);

                    if (!result.Success)
                    {
                        ShowError(result.ErrorMessage);
                        return;
                    }
                }
                else
                {
                    var result = await _productService.AddAsync(
                        categoryId,
                        _txtProductName.Text,
                        _txtBrand.Text,
                        _txtUnit.Text,
                        _txtCostPrice.Text,
                        _txtSellingPrice.Text,
                        _txtReorderLevel.Text,
                        string.Empty,
                        sku: null,
                        expiration,
                        _txtDescription.Text,
                        supplierId,
                        alternateIds);

                    if (!result.Success)
                    {
                        ShowError(result.ErrorMessage);
                        return;
                    }
                }

                DialogResult = DialogResult.OK;
                Close();
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
            _dtpExpiration.Enabled = !busy;
            _cmbSupplier.Enabled = !busy;
            _alternateSuppliersList.Enabled = !busy;
            _txtDescription.Enabled = !busy;
        }

        private void ShowError(string message)
        {
            _lblStatus.ForeColor = Theme.Danger;
            _lblStatus.Text = message ?? string.Empty;
        }
    }
}