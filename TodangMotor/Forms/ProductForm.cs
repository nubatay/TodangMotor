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
    /// Two-column layout. Includes SKU, Expiration Date, Description,
    /// Primary Supplier, and Alternate Suppliers.
    /// No Current Stock field — that is managed via Inventory.
    /// </summary>
    public class ProductForm : ShellForm
    {
        // ============================================================
        // SERVICES
        // ============================================================

        private readonly ProductService _productService = new();
        private readonly CategoryService _categoryService = new(new CategoryRepository());
        private readonly SupplierService _supplierService = new();

        // ============================================================
        // STATE
        // ============================================================

        private readonly Product? _productToEdit;
        private bool IsEditMode => _productToEdit != null;

        private List<Supplier> _allSuppliers = new();
        private List<Category> _allCategories = new();

        // Display-name → object lookups (RoundedComboBox works with strings only).
        private readonly Dictionary<string, Supplier> _supplierByDisplay = new();
        private readonly Dictionary<string, Category> _categoryByDisplay = new();

        /// <summary>Wrapper for checkbox-list items so we can store the SupplierId.</summary>
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
        private const int LabelW = 120;
        private const int LeftFieldX = 152;
        private const int RightLabelX = 412;
        private const int RightFieldX = 532;
        private const int HalfFieldW = 240;
        private const int FullFieldW = 616;
        private const int RowStep = 52;

        // ============================================================
        // UI CONTROLS
        // ============================================================

        private RoundedTextBox _txtProductName;
        private RoundedTextBox _txtBrand;
        private RoundedTextBox _txtSku;
        private RoundedComboBox _cmbCategory;
        private RoundedTextBox _txtUnit;
        private RoundedTextBox _txtCostPrice;
        private RoundedTextBox _txtSellingPrice;
        private RoundedTextBox _txtReorderLevel;
        private DateTimePicker _dtpExpiration;
        private RoundedComboBox _cmbSupplier;
        private CheckedListBox _alternateSuppliersList;
        private Panel _alternateSuppliersWrapper;
        private Panel _descriptionWrapper;
        private TextBox _txtDescription;

        private Button _btnSave;
        private Button _btnCancel;
        private Label _lblStatus;

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
            ClientSize = new Size(800, 740);
            MinimumSize = new Size(780, 720);
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
            // ---- Footer (docked bottom) ----
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 76,
                BackColor = Theme.Background
            };

            const int btnW = 130;
            const int btnH = 44;

            _btnSave = UiFactory.CreateButton("Save", UiFactory.ButtonStyle.Primary, btnW, btnH);
            _btnSave.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnSave.Click += async (s, e) => await SaveAsync();

            _btnCancel = UiFactory.CreateButton("Cancel", UiFactory.ButtonStyle.Ghost, btnW, btnH);
            _btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            footer.Controls.Add(_btnSave);
            footer.Controls.Add(_btnCancel);

            footer.Resize += (s, e) =>
            {
                _btnSave.Location = new Point(footer.ClientSize.Width - PadX - btnW, 16);
                _btnCancel.Location = new Point(footer.ClientSize.Width - PadX - btnW - 10 - btnW, 16);
            };

            ContentPanel.Controls.Add(footer);

            // ---- Form fields (absolute positions) ----
            int y = 16;

            // Row 1: Product Name (full width)
            AddLabel("Product Name:", PadX, y);
            _txtProductName = UiFactory.CreateTextBox(FullFieldW);
            _txtProductName.Location = new Point(LeftFieldX, y);
            _txtProductName.MaxLength = 150;
            ContentPanel.Controls.Add(_txtProductName);
            y += RowStep;

            // Row 2: Brand | SKU
            AddLabel("Brand:", PadX, y);
            _txtBrand = UiFactory.CreateTextBox(HalfFieldW);
            _txtBrand.Location = new Point(LeftFieldX, y);
            _txtBrand.MaxLength = 100;
            ContentPanel.Controls.Add(_txtBrand);

            AddLabel("SKU:", RightLabelX, y);
            _txtSku = UiFactory.CreateTextBox(HalfFieldW);
            _txtSku.Location = new Point(RightFieldX, y);
            _txtSku.MaxLength = 50;
            _txtSku.Placeholder = "Optional";
            ContentPanel.Controls.Add(_txtSku);
            y += RowStep;

            // Row 3: Category | Unit
            AddLabel("Category:", PadX, y);
            _cmbCategory = new RoundedComboBox
            {
                Width = HalfFieldW,
                Location = new Point(LeftFieldX, y)
            };
            ContentPanel.Controls.Add(_cmbCategory);

            AddLabel("Unit:", RightLabelX, y);
            _txtUnit = UiFactory.CreateTextBox(HalfFieldW);
            _txtUnit.Location = new Point(RightFieldX, y);
            _txtUnit.MaxLength = 20;
            ContentPanel.Controls.Add(_txtUnit);
            y += RowStep;

            // Row 4: Cost Price | Selling Price
            AddLabel("Cost Price (PHP):", PadX, y);
            _txtCostPrice = UiFactory.CreateTextBox(HalfFieldW);
            _txtCostPrice.Location = new Point(LeftFieldX, y);
            _txtCostPrice.MaxLength = 12;
            ContentPanel.Controls.Add(_txtCostPrice);

            AddLabel("Selling Price (PHP):", RightLabelX, y);
            _txtSellingPrice = UiFactory.CreateTextBox(HalfFieldW);
            _txtSellingPrice.Location = new Point(RightFieldX, y);
            _txtSellingPrice.MaxLength = 12;
            ContentPanel.Controls.Add(_txtSellingPrice);
            y += RowStep;

            // Row 5: Reorder Level | Expiration Date
            AddLabel("Reorder Level:", PadX, y);
            _txtReorderLevel = UiFactory.CreateTextBox(HalfFieldW);
            _txtReorderLevel.Location = new Point(LeftFieldX, y);
            _txtReorderLevel.MaxLength = 6;
            ContentPanel.Controls.Add(_txtReorderLevel);

            AddLabel("Expiration Date:", RightLabelX, y);
            _dtpExpiration = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "MMM d, yyyy",
                ShowCheckBox = true,
                Checked = false,
                Font = Theme.FontBody,
                Location = new Point(RightFieldX, y + 4),
                Size = new Size(HalfFieldW, 32)
            };
            ContentPanel.Controls.Add(_dtpExpiration);
            y += RowStep;

            // Row 6: Primary Supplier (full width)
            AddLabel("Primary Supplier:", PadX, y);
            _cmbSupplier = new RoundedComboBox
            {
                Width = FullFieldW,
                Location = new Point(LeftFieldX, y)
            };
            ContentPanel.Controls.Add(_cmbSupplier);
            y += RowStep;

            // Row 7: Alternate Suppliers | Description
            AddLabel("Alternate Suppliers:", PadX, y);

            _alternateSuppliersWrapper = new Panel
            {
                Location = new Point(LeftFieldX, y + 24),
                Size = new Size(HalfFieldW, 120),
                BackColor = Theme.InputBackground
            };
            _alternateSuppliersWrapper.Paint += AlternateWrapper_Paint;

            _alternateSuppliersList = new CheckedListBox
            {
                Location = new Point(4, 4),
                Size = new Size(HalfFieldW - 8, 112),
                Font = Theme.FontBody,
                BackColor = Theme.InputBackground,
                ForeColor = Theme.TextPrimary,
                BorderStyle = BorderStyle.None,
                CheckOnClick = true,
                IntegralHeight = false,
                HorizontalScrollbar = false
            };
            _alternateSuppliersWrapper.Controls.Add(_alternateSuppliersList);
            ContentPanel.Controls.Add(_alternateSuppliersWrapper);

            AddLabel("Description:", RightLabelX, y);

            _descriptionWrapper = new Panel
            {
                Location = new Point(RightFieldX, y + 24),
                Size = new Size(HalfFieldW, 120),
                BackColor = Theme.InputBackground
            };
            _descriptionWrapper.Paint += DescriptionWrapper_Paint;

            _txtDescription = new TextBox
            {
                Location = new Point(8, 8),
                Size = new Size(HalfFieldW - 16, 104),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
                BackColor = Theme.InputBackground,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontBody,
                MaxLength = 500
            };
            _txtDescription.Enter += (s, e) => _descriptionWrapper.Invalidate();
            _txtDescription.Leave += (s, e) => _descriptionWrapper.Invalidate();
            _descriptionWrapper.Controls.Add(_txtDescription);
            ContentPanel.Controls.Add(_descriptionWrapper);

            y += 152;

            // ---- Status label ----
            _lblStatus = new Label
            {
                Text = string.Empty,
                Font = Theme.FontSmall,
                ForeColor = Theme.Danger,
                AutoSize = false,
                Location = new Point(PadX, y),
                Size = new Size(ClientSize.Width - PadX * 2, 24),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            ContentPanel.Controls.Add(_lblStatus);
        }

        private void AddLabel(string text, int x, int y)
        {
            var lbl = new Label
            {
                Text = text,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = false,
                Size = new Size(LabelW, 40),
                Location = new Point(x, y),
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

                // Primary supplier dropdown — items are display names,
                // mapped back to Supplier objects via dictionary.
                _supplierByDisplay.Clear();
                _cmbSupplier.Items.Clear();
                foreach (var s in _allSuppliers)
                {
                    _supplierByDisplay[s.SupplierName] = s;
                    _cmbSupplier.Items.Add(s.SupplierName);
                }

                // Alternate suppliers checkbox list.
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
            _txtSku.Text = p.SKU ?? string.Empty;
            _txtUnit.Text = p.Unit ?? "pcs";
            _txtCostPrice.Text = p.CostPrice.ToString("0.00");
            _txtSellingPrice.Text = p.SellingPrice.ToString("0.00");
            _txtReorderLevel.Text = p.ReorderLevel.ToString();
            _txtDescription.Text = p.Description ?? string.Empty;

            // Category — select by name.
            if (p.CategoryId > 0 && _cmbCategory.Items.Count > 0)
            {
                var cat = _allCategories.FirstOrDefault(c => c.CategoryId == p.CategoryId);
                if (cat != null)
                    _cmbCategory.SelectedItem = cat.CategoryName;
            }

            // Primary Supplier — select by name.
            if (p.SupplierId.HasValue && p.SupplierId.Value > 0)
            {
                var sup = _allSuppliers.FirstOrDefault(s => s.SupplierId == p.SupplierId.Value);
                if (sup != null)
                    _cmbSupplier.SelectedItem = sup.SupplierName;
            }

            // Expiration Date
            if (p.ExpirationDate.HasValue)
            {
                _dtpExpiration.Value = p.ExpirationDate.Value;
                _dtpExpiration.Checked = true;
            }
            else
            {
                _dtpExpiration.Checked = false;
            }

            // Alternate Suppliers — check the ones that match.
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
            // ---- Read selected category ----
            string? selectedCatName = _cmbCategory.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedCatName)
                || !_categoryByDisplay.TryGetValue(selectedCatName, out var selectedCategory))
            {
                ShowError("Please select a category.");
                return;
            }

            // ---- Read selected supplier ----
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

            // Gather alternate supplier IDs (excluding the primary one).
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
                        string.Empty, // Notes — hidden in UI
                        _txtSku.Text,
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
                        string.Empty, // Notes
                        _txtSku.Text,
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
            _txtSku.Enabled = !busy;
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