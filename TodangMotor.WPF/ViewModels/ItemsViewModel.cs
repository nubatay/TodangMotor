using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TodangMotor.Models;
using TodangMotor.Services;

namespace TodangMotor.WPF.ViewModels
{
    public class ProductDisplayItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal CostPrice { get; set; }
        public decimal SellingPrice { get; set; }
        public int QuantityOnHand { get; set; }
        public int ReorderLevel { get; set; }
        public bool IsActive { get; set; }

        public string StatusText => IsActive ? "Active" : "Inactive";
        public string StatusColor => IsActive ? "#22C55E" : "#EF4444";
    }

    public partial class ItemsViewModel : ObservableObject
    {
        private readonly ProductService _productService;
        private readonly CategoryService _categoryService;

        private List<Product> _allProducts = new();
        private List<Category> _allCategories = new(); // <-- THIS WAS MISSING

        public ObservableCollection<ProductDisplayItem> FilteredProducts { get; } = new();
        public ObservableCollection<Category> Categories { get; } = new();
        public ObservableCollection<string> StatusFilters { get; } = new() { "All", "Active", "Inactive" };

        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private Category? _selectedCategory;
        [ObservableProperty] private string _selectedStatus = "Active";
        [ObservableProperty] private bool _isLoading;

        public event Action<Product?>? RequestOpenProductDialog;

        public ItemsViewModel(ProductService productService, CategoryService categoryService)
        {
            _productService = productService;
            _categoryService = categoryService;
            _ = LoadDataAsync();
        }

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            IsLoading = true;
            try
            {
                var cats = await _categoryService.GetActiveCategoriesAsync();
                _allCategories = cats?.ToList() ?? new List<Category>();

                Categories.Clear();
                Categories.Add(new Category { CategoryId = 0, CategoryName = "All Categories" });
                foreach (var c in _allCategories) Categories.Add(c);

                if (SelectedCategory == null) SelectedCategory = Categories.FirstOrDefault();

                var (success, error, products) = await _productService.GetAllAsync();
                if (success)
                {
                    _allProducts = products;
                    ApplyFilter();
                }
            }
            catch (Exception) { }
            finally { IsLoading = false; }
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();
        partial void OnSelectedStatusChanged(string value) => ApplyFilter();
        partial void OnSelectedCategoryChanged(Category? value) => ApplyFilter();

        private void ApplyFilter()
        {
            if (_allProducts == null) return;

            var filtered = _allProducts.AsEnumerable();

            if (SelectedStatus == "Active") filtered = filtered.Where(p => p.IsActive);
            else if (SelectedStatus == "Inactive") filtered = filtered.Where(p => !p.IsActive);

            if (SelectedCategory != null && SelectedCategory.CategoryId > 0)
                filtered = filtered.Where(p => p.CategoryId == SelectedCategory.CategoryId);

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string search = SearchText.ToLower();
                filtered = filtered.Where(p =>
                    (p.ProductName?.ToLower().Contains(search) ?? false) ||
                    (p.Brand?.ToLower().Contains(search) ?? false) ||
                    (p.SKU?.ToLower().Contains(search) ?? false));
            }

            var displayList = filtered.Select(p => new ProductDisplayItem
            {
                ProductId = p.ProductId,
                ProductName = p.ProductName ?? string.Empty,
                Brand = p.Brand ?? string.Empty,
                CategoryName = _allCategories.FirstOrDefault(c => c.CategoryId == p.CategoryId)?.CategoryName ?? "Unknown",
                Unit = p.Unit ?? string.Empty,
                CostPrice = p.CostPrice,
                SellingPrice = p.SellingPrice,
                QuantityOnHand = p.QuantityOnHand,
                ReorderLevel = p.ReorderLevel,
                IsActive = p.IsActive
            }).OrderBy(p => p.ProductName).ToList();

            FilteredProducts.Clear();
            foreach (var item in displayList) FilteredProducts.Add(item);
        }

        [RelayCommand]
        private void AddProduct() => RequestOpenProductDialog?.Invoke(null);

        [RelayCommand]
        private void EditProduct(ProductDisplayItem item)
        {
            if (item == null) return;
            var rawProduct = _allProducts.FirstOrDefault(p => p.ProductId == item.ProductId);
            if (rawProduct != null) RequestOpenProductDialog?.Invoke(rawProduct);
        }

        [RelayCommand]
        private async Task ToggleStatusAsync(ProductDisplayItem item)
        {
            if (item == null) return;

            if (item.IsActive) await _productService.DeactivateAsync(item.ProductId);
            else await _productService.ReactivateAsync(item.ProductId);

            await LoadDataAsync();
        }
    }
}