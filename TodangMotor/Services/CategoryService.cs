using TodangMotor.Data;
using TodangMotor.Models;

namespace TodangMotor.Services
{
    public class CategoryService
    {
        private readonly CategoryRepository _categoryRepository;

        public CategoryService(CategoryRepository categoryRepository)
        {
            _categoryRepository = categoryRepository;
        }

        // Get every category (active + inactive) - Owner sees all
        public async Task<IEnumerable<Category>> GetAllCategoriesAsync()
        {
            return await _categoryRepository.GetAllAsync();
        }

        // Get only active categories - used for dropdowns (e.g. Product form)
        public async Task<IEnumerable<Category>> GetActiveCategoriesAsync()
        {
            return await _categoryRepository.GetActiveAsync();
        }

        // Add a brand new category
        public async Task<(bool Success, string ErrorMessage)> AddCategoryAsync(string? categoryName)
        {
            // 1. Null/empty check
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return (false, "Category name is required.");
            }

            // 2. Trim BEFORE checking length
            var trimmedName = categoryName.Trim();

            // 3. Length check (matches VARCHAR(100) column)
            if (trimmedName.Length > 100)
            {
                return (false, "Category name cannot exceed 100 characters.");
            }

            try
            {
                // 4. Duplicate check (case-insensitive, checks active AND inactive)
                var existing = await _categoryRepository.GetByNameAsync(trimmedName);
                if (existing != null)
                {
                    return (false, "A category with this name already exists.");
                }

                // 5. All good - insert it
                var newCategory = new Category
                {
                    CategoryName = trimmedName,
                    IsActive = true
                };
                await _categoryRepository.InsertAsync(newCategory);

                return (true, string.Empty);
            }
            catch (Exception)
            {
                // 6. Catch unexpected DB errors, never crash the app
                return (false, "Something went wrong while saving. Please try again.");
            }
        }

        // Update an existing category's name
        public async Task<(bool Success, string ErrorMessage)> UpdateCategoryAsync(int categoryId, string? categoryName)
        {
            // 1. Sanity check on ID
            if (categoryId <= 0)
            {
                return (false, "Invalid category selected.");
            }

            // 2. Null/empty check
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return (false, "Category name is required.");
            }

            var trimmedName = categoryName.Trim();

            // 3. Length check
            if (trimmedName.Length > 100)
            {
                return (false, "Category name cannot exceed 100 characters.");
            }

            try
            {
                // 4. Does this category even exist?
                var category = await _categoryRepository.GetByIdAsync(categoryId);
                if (category == null)
                {
                    return (false, "Category not found. It may have been deleted.");
                }

                // 5. Duplicate check, excluding itself
                var existing = await _categoryRepository.GetByNameAsync(trimmedName);
                if (existing != null && existing.CategoryId != categoryId)
                {
                    return (false, "A category with this name already exists.");
                }

                // 6. All good - update it
                category.CategoryName = trimmedName;
                await _categoryRepository.UpdateAsync(category);

                return (true, string.Empty);
            }
            catch (Exception)
            {
                return (false, "Something went wrong while saving. Please try again.");
            }
        }

        // Deactivate (soft delete) a category
        public async Task<(bool Success, string ErrorMessage)> DeactivateCategoryAsync(int categoryId)
        {
            if (categoryId <= 0)
            {
                return (false, "Invalid category selected.");
            }

            try
            {
                var category = await _categoryRepository.GetByIdAsync(categoryId);
                if (category == null)
                {
                    return (false, "Category not found. It may have been deleted.");
                }

                if (!category.IsActive)
                {
                    return (false, "This category is already inactive.");
                }

                // Block if active products still use this category
                var activeProductCount = await _categoryRepository.CountActiveProductsUsingCategoryAsync(categoryId);
                if (activeProductCount > 0)
                {
                    return (false, $"Cannot deactivate — {activeProductCount} active product(s) still use this category.");
                }

                await _categoryRepository.SetActiveStatusAsync(categoryId, false);
                return (true, string.Empty);
            }
            catch (Exception)
            {
                return (false, "Something went wrong. Please try again.");
            }
        }

        // Reactivate a previously deactivated category
        public async Task<(bool Success, string ErrorMessage)> ReactivateCategoryAsync(int categoryId)
        {
            if (categoryId <= 0)
            {
                return (false, "Invalid category selected.");
            }

            try
            {
                var category = await _categoryRepository.GetByIdAsync(categoryId);
                if (category == null)
                {
                    return (false, "Category not found. It may have been deleted.");
                }

                if (category.IsActive)
                {
                    return (false, "This category is already active.");
                }

                await _categoryRepository.SetActiveStatusAsync(categoryId, true);
                return (true, string.Empty);
            }
            catch (Exception)
            {
                return (false, "Something went wrong. Please try again.");
            }
        }
    }
}