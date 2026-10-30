using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TodangMotor.Data;
using TodangMotor.Models;
using TodangMotor.Services;

namespace TodangMotor.WPF.ViewModels
{
    public class CategoryDisplayItem
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public bool IsActive { get; set; }

        public string StatusText => IsActive ? "Active" : "Inactive";
        public string StatusColor => IsActive ? "#22C55E" : "#EF4444";
    }

    public partial class CategoryViewModel : ObservableObject
    {
        private readonly CategoryService _categoryService;

        private List<Category> _allCategories = new();

        public ObservableCollection<CategoryDisplayItem> FilteredCategories { get; } = new();
        public ObservableCollection<string> StatusFilters { get; } = new() { "All", "Active", "Inactive" };

        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _selectedStatus = "Active";
        [ObservableProperty] private bool _isLoading;

        public event Action<Category?>? RequestOpenCategoryDialog;

        public CategoryViewModel(CategoryService categoryService)
        {
            _categoryService = categoryService;
            _ = LoadDataAsync();
        }

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            IsLoading = true;
            try
            {
                var cats = await _categoryService.GetAllCategoriesAsync();
                _allCategories = cats?.ToList() ?? new List<Category>();
                ApplyFilter();
            }
            catch (Exception) { }
            finally { IsLoading = false; }
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();
        partial void OnSelectedStatusChanged(string value) => ApplyFilter();

        private void ApplyFilter()
        {
            if (_allCategories == null) return;

            var filtered = _allCategories.AsEnumerable();

            if (SelectedStatus == "Active") filtered = filtered.Where(c => c.IsActive);
            else if (SelectedStatus == "Inactive") filtered = filtered.Where(c => !c.IsActive);

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string search = SearchText.ToLower();
                filtered = filtered.Where(c => c.CategoryName?.ToLower().Contains(search) ?? false);
            }

            var displayList = filtered.Select(c => new CategoryDisplayItem
            {
                CategoryId = c.CategoryId,
                CategoryName = c.CategoryName ?? string.Empty,
                IsActive = c.IsActive
            }).OrderBy(c => c.CategoryName).ToList();

            FilteredCategories.Clear();
            foreach (var item in displayList) FilteredCategories.Add(item);
        }

        [RelayCommand]
        private void AddCategory() => RequestOpenCategoryDialog?.Invoke(null);

        [RelayCommand]
        private void EditCategory(CategoryDisplayItem item)
        {
            if (item == null) return;
            var rawCat = _allCategories.FirstOrDefault(c => c.CategoryId == item.CategoryId);
            if (rawCat != null) RequestOpenCategoryDialog?.Invoke(rawCat);
        }

        [RelayCommand]
        private async Task ToggleStatusAsync(CategoryDisplayItem item)
        {
            if (item == null) return;

            if (item.IsActive) await _categoryService.DeactivateCategoryAsync(item.CategoryId);
            else await _categoryService.ReactivateCategoryAsync(item.CategoryId);

            await LoadDataAsync();
        }
    }
}