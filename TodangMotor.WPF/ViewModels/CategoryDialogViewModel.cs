using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TodangMotor.Data;
using TodangMotor.Models;
using TodangMotor.Services;

namespace TodangMotor.WPF.ViewModels
{
    public partial class CategoryDialogViewModel : ObservableObject
    {
        private readonly CategoryService _categoryService;
        private readonly Category? _categoryToEdit;

        public bool IsEditMode => _categoryToEdit != null;
        public string DialogTitle => IsEditMode ? "Edit Category" : "Add Category";

        [ObservableProperty] private string _categoryName = string.Empty;

        // FIXED: Renamed from ErrorMessage to match the XAML
        [ObservableProperty] private string _categoryNameError = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotBusy))]
        private bool _isBusy;

        public bool IsNotBusy => !IsBusy;

        public event Action<bool>? RequestClose;

        public CategoryDialogViewModel(CategoryService categoryService, Category? categoryToEdit = null)
        {
            _categoryService = categoryService;
            _categoryToEdit = categoryToEdit;

            if (IsEditMode)
            {
                CategoryName = _categoryToEdit!.CategoryName ?? string.Empty;
            }
        }

        // Clear error as soon as they type
        partial void OnCategoryNameChanged(string value)
        {
            CategoryNameError = string.Empty;
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            // 1. Reset Errors
            CategoryNameError = string.Empty;

            // 2. Field-Level Validation
            if (string.IsNullOrWhiteSpace(CategoryName))
            {
                CategoryNameError = "Category name is required.";
                return;
            }

            // 3. Call Service
            IsBusy = true;
            try
            {
                (bool Success, string ErrorMessage) result;

                if (IsEditMode)
                {
                    result = await _categoryService.UpdateCategoryAsync(_categoryToEdit!.CategoryId, CategoryName);
                }
                else
                {
                    result = await _categoryService.AddCategoryAsync(CategoryName);
                }

                if (result.Success)
                {
                    RequestClose?.Invoke(true);
                }
                else
                {
                    // If the DB rejects it (e.g., "Category already exists"), show it inline too
                    CategoryNameError = result.ErrorMessage;
                }
            }
            catch (Exception)
            {
                CategoryNameError = "An unexpected error occurred.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            RequestClose?.Invoke(false);
        }
    }
}