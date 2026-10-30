using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentFormat.OpenXml.Bibliography;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TodangMotor.Data;
using TodangMotor.Models;
using TodangMotor.Services;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Rebar;

namespace TodangMotor.WPF.ViewModels
{
    // Helper class for the Alternate Suppliers checkboxes
    public partial class SupplierSelectionItem : ObservableObject
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;

        [ObservableProperty] private bool _isSelected;
    }

    public partial class ProductDialogViewModel : ObservableObject
    {
        private readonly ProductService _productService;
        private readonly CategoryService _categoryService;
        private readonly SupplierService _supplierService;
        private readonly Product? _productToEdit;

        public bool IsEditMode => _productToEdit != null;
        public string DialogTitle => IsEditMode ? "Edit Item" : "Add Item";

        // Collections for Dropdowns
        public ObservableCollection<Category> Categories { get; } = new();
        public ObservableCollection<Supplier> Suppliers { get; } = new();
        public ObservableCollection<SupplierSelectionItem> AlternateSuppliers { get; } = new();

        // Form Fields
        [ObservableProperty] private string _productName = string.Empty;
        [ObservableProperty] private string _brand = string.Empty;
        [ObservableProperty] private string _unit = "pcs";
        [ObservableProperty] private Category? _selectedCategory;
        [ObservableProperty] private string _reorderLevel = "5";
        [ObservableProperty] private string _costPrice = "0.00";
        [ObservableProperty] private string _sellingPrice = "0.00";
        [ObservableProperty] private DateTime? _expirationDate;
        [ObservableProperty] private Supplier? _selectedSupplier;
        [ObservableProperty] private string _description = string.Empty;

        // Field-specific Errors
        [ObservableProperty] private string _productNameError = string.Empty;
        [ObservableProperty] private string _brandError = string.Empty;
        [ObservableProperty] private string _unitError = string.Empty;
        [ObservableProperty] private string _categoryError = string.Empty;
        [ObservableProperty] private string _reorderLevelError = string.Empty;
        [ObservableProperty] private string _costPriceError = string.Empty;
        [ObservableProperty] private string _sellingPriceError = string.Empty;
        [ObservableProperty] private string _supplierError = string.Empty;

        // Global Error
        [ObservableProperty] private string _errorMessage = string.Empty;
        [ObservableProperty] private bool _isBusy;

        public event Action<bool>? RequestClose;

        public ProductDialogViewModel(ProductService productService, CategoryService categoryService, SupplierService supplierService, Product? productToEdit = null)
        {
            _productService = productService;
            _categoryService = categoryService;
            _supplierService = supplierService;
            _productToEdit = productToEdit;

            // Load lookups on startup
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                var cats = await _categoryService.GetActiveCategoriesAsync();
                foreach (var c in cats) Categories.Add(c);

                var (success, error, suppliers) = await _supplierService.GetActiveAsync();
                if (success)
                {
                    foreach (var s in suppliers)
                    {
                        Suppliers.Add(s);
                        AlternateSuppliers.Add(new SupplierSelectionItem { SupplierId = s.SupplierId, SupplierName = s.SupplierName });
                    }
                }

                if (IsEditMode)
                {
                    ProductName = _productToEdit!.ProductName ?? string.Empty;
                    Brand = _productToEdit.Brand ?? string.Empty;
                    Unit = _productToEdit.Unit ?? "pcs";
                    ReorderLevel = _productToEdit.ReorderLevel.ToString();
                    CostPrice = _productToEdit.CostPrice.ToString("0.00");
                    SellingPrice = _productToEdit.SellingPrice.ToString("0.00");
                    ExpirationDate = _productToEdit.ExpirationDate;
                    Description = _productToEdit.Description ?? string.Empty;

                    SelectedCategory = Categories.FirstOrDefault(c => c.CategoryId == _productToEdit.CategoryId);
                    SelectedSupplier = Suppliers.FirstOrDefault(s => s.SupplierId == _productToEdit.SupplierId);

                    if (_productToEdit.AlternateSupplierIds != null)
                    {
                        foreach (var alt in AlternateSuppliers)
                        {
                            if (_productToEdit.AlternateSupplierIds.Contains(alt.SupplierId))
                                alt.IsSelected = true;
                        }
                    }
                }
                else
                {
                    SelectedCategory = Categories.FirstOrDefault();
                    SelectedSupplier = Suppliers.FirstOrDefault();
                }
            }
            catch (Exception)
            {
                ErrorMessage = "Could not load categories or suppliers.";
            }
        }

        // Clear errors on typing
        partial void OnProductNameChanged(string value) => ProductNameError = string.Empty;
        partial void OnBrandChanged(string value) => BrandError = string.Empty;
        partial void OnUnitChanged(string value) => UnitError = string.Empty;
        partial void OnSelectedCategoryChanged(Category? value) => CategoryError = string.Empty;
        partial void OnReorderLevelChanged(string value) => ReorderLevelError = string.Empty;
        partial void OnCostPriceChanged(string value) { CostPriceError = string.Empty; SellingPriceError = string.Empty; }
        partial void OnSellingPriceChanged(string value) { SellingPriceError = string.Empty; }
        partial void OnSelectedSupplierChanged(Supplier? value) => SupplierError = string.Empty;

        [RelayCommand]
        private async Task SaveAsync()
        {
            // 1. Reset Errors
            ProductNameError = BrandError = UnitError = CategoryError = ReorderLevelError = CostPriceError = SellingPriceError = SupplierError = ErrorMessage = string.Empty;
            bool isValid = true;

            // 2. Field Level Validations
            if (string.IsNullOrWhiteSpace(ProductName)) { ProductNameError = "Product name is required."; isValid = false; }
            if (string.IsNullOrWhiteSpace(Brand)) { BrandError = "Brand is required."; isValid = false; }
            if (string.IsNullOrWhiteSpace(Unit)) { UnitError = "Unit is required."; isValid = false; }
            if (SelectedCategory == null) { CategoryError = "Please select a category."; isValid = false; }
            if (SelectedSupplier == null) { SupplierError = "Please select a primary supplier."; isValid = false; }

            if (!int.TryParse(ReorderLevel, out int reorder) || reorder < 0)
            {
                ReorderLevelError = "Must be a whole number (0 or higher)."; isValid = false;
            }

            if (!decimal.TryParse(CostPrice, out decimal cost) || cost < 0)
            {
                CostPriceError = "Must be a valid number (0 or higher)."; isValid = false;
            }

            if (!decimal.TryParse(SellingPrice, out decimal selling) || selling <= 0)
            {
                SellingPriceError = "Must be greater than 0."; isValid = false;
            }
            else if (!string.IsNullOrEmpty(CostPrice) && decimal.TryParse(CostPrice, out decimal c) && selling < c)
            {
                SellingPriceError = "Selling price cannot be lower than cost price."; isValid = false;
            }

            if (ExpirationDate.HasValue && ExpirationDate.Value.Date < DateTime.Today)
            {
                ErrorMessage = "Expiration date cannot be in the past.";
                isValid = false;
            }

            if (!isValid) return;

            // 3. Prepare Alternate Suppliers
            var altIds = AlternateSuppliers.Where(a => a.IsSelected && a.SupplierId != SelectedSupplier!.SupplierId)
                                           .Select(a => a.SupplierId).ToList();

            // 4. Save
            IsBusy = true;
            try
            {
                (bool Success, string ErrorMessage) result;

                if (IsEditMode)
                {
                    // Adjust to match your exact ProductService signature
                    var res = await _productService.UpdateAsync(
                        _productToEdit!.ProductId, SelectedCategory.CategoryId, ProductName, Brand, Unit,
                        CostPrice, SellingPrice, ReorderLevel, string.Empty, null, ExpirationDate,
                        Description, SelectedSupplier.SupplierId, altIds);
                    result = (res.Success, res.ErrorMessage);
                }
                else
                {
                    var res = await _productService.AddAsync(
                        SelectedCategory.CategoryId, ProductName, Brand, Unit,
                        CostPrice, SellingPrice, ReorderLevel, string.Empty, null, ExpirationDate,
                        Description, SelectedSupplier.SupplierId, altIds);
                    result = (res.Success, res.ErrorMessage);
                }

                if (result.Success)
                {
                    RequestClose?.Invoke(true);
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            catch (Exception)
            {
                ErrorMessage = "An unexpected error occurred.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void Cancel() => RequestClose?.Invoke(false);
    }
}