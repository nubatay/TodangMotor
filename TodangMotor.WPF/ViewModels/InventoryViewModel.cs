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
    public class StockLevelItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int OnHand { get; set; }
        public int ReorderLevel { get; set; }

        public string StatusText => OnHand == 0 ? "Out of Stock" : (OnHand <= ReorderLevel ? "Low" : "OK");
        public string StatusColor => OnHand == 0 ? "#EF4444" : (OnHand <= ReorderLevel ? "#F59E0B" : "#22C55E");
    }

    public partial class InventoryViewModel : ObservableObject
    {
        private readonly ProductService _productService;
        private readonly CategoryService _categoryService;

        private List<Product> _allProducts = new();
        private List<Category> _allCategories = new();

        public ObservableCollection<StockLevelItem> FilteredStock { get; } = new();
        public ObservableCollection<Category> Categories { get; } = new();
        public ObservableCollection<string> StockStatusFilters { get; } = new() { "All", "OK", "Low", "Out of Stock" };

        [ObservableProperty] private string _stockSearchText = string.Empty;
        [ObservableProperty] private Category? _selectedCategory;
        [ObservableProperty] private string _selectedStockStatus = "All";
        [ObservableProperty] private bool _isLoading;

        public event Action<Product>? RequestOpenAdjustStockDialog;

        public InventoryViewModel(ProductService productService, CategoryService categoryService)
        {
            _productService = productService;
            _categoryService = categoryService;

            SelectedStockStatus = "All";

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
                    ApplyStockFilter();
                }
            }
            catch (Exception) { }
            finally { IsLoading = false; }
        }

        partial void OnStockSearchTextChanged(string value) => ApplyStockFilter();
        partial void OnSelectedStockStatusChanged(string value) => ApplyStockFilter();
        partial void OnSelectedCategoryChanged(Category? value) => ApplyStockFilter();

        private void ApplyStockFilter()
        {
            if (_allProducts == null) return;

            var filtered = _allProducts.Where(p => p.IsActive).AsEnumerable();

            if (SelectedCategory != null && SelectedCategory.CategoryId > 0)
                filtered = filtered.Where(p => p.CategoryId == SelectedCategory.CategoryId);

            if (SelectedStockStatus == "OK") filtered = filtered.Where(p => p.QuantityOnHand > p.ReorderLevel);
            else if (SelectedStockStatus == "Low") filtered = filtered.Where(p => p.QuantityOnHand > 0 && p.QuantityOnHand <= p.ReorderLevel);
            else if (SelectedStockStatus == "Out of Stock") filtered = filtered.Where(p => p.QuantityOnHand == 0);

            if (!string.IsNullOrWhiteSpace(StockSearchText))
            {
                string search = StockSearchText.ToLower();
                filtered = filtered.Where(p =>
                    (p.ProductName?.ToLower().Contains(search) ?? false) ||
                    (p.Brand?.ToLower().Contains(search) ?? false) ||
                    (p.SKU?.ToLower().Contains(search) ?? false));
            }

            var displayList = filtered.Select(p => new StockLevelItem
            {
                ProductId = p.ProductId,
                ProductName = p.ProductName ?? string.Empty,
                Brand = p.Brand ?? string.Empty,
                CategoryName = _allCategories.FirstOrDefault(c => c.CategoryId == p.CategoryId)?.CategoryName ?? "Unknown",
                OnHand = p.QuantityOnHand,
                ReorderLevel = p.ReorderLevel
            }).OrderBy(p => p.ProductName).ToList();

            FilteredStock.Clear();
            foreach (var item in displayList) FilteredStock.Add(item);
        }

        [RelayCommand]
        private void AdjustStock(StockLevelItem item)
        {
            if (item == null) return;
            var rawProduct = _allProducts.FirstOrDefault(p => p.ProductId == item.ProductId);
            if (rawProduct != null) RequestOpenAdjustStockDialog?.Invoke(rawProduct);
        }
    }
}