using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TodangMotor.Models;
using TodangMotor.Services;

namespace TodangMotor.WPF.ViewModels
{
    public partial class SupplierDialogViewModel : ObservableObject
    {
        private readonly SupplierService _supplierService;
        private readonly Supplier? _supplierToEdit;

        public bool IsEditMode => _supplierToEdit != null;
        public string DialogTitle => IsEditMode ? "Edit Supplier" : "Add Supplier";

        [ObservableProperty] private string _supplierName = string.Empty;
        [ObservableProperty] private string _contactNumber = string.Empty;
        [ObservableProperty] private string _address = string.Empty;

        // Field-specific errors
        [ObservableProperty] private string _supplierNameError = string.Empty;
        [ObservableProperty] private string _contactNumberError = string.Empty;

        // Global error for DB/Service rejections
        [ObservableProperty] private string _errorMessage = string.Empty;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotBusy))]
        private bool _isBusy;

        public bool IsNotBusy => !IsBusy;

        public event Action<bool>? RequestClose;

        public SupplierDialogViewModel(SupplierService supplierService, Supplier? supplierToEdit = null)
        {
            _supplierService = supplierService;
            _supplierToEdit = supplierToEdit;

            if (IsEditMode)
            {
                SupplierName = _supplierToEdit!.SupplierName ?? string.Empty;
                ContactNumber = _supplierToEdit.ContactNumber ?? string.Empty;
                Address = _supplierToEdit.Address ?? string.Empty;
            }
        }

        // Clear errors as soon as they start typing
        partial void OnSupplierNameChanged(string value) => SupplierNameError = string.Empty;
        partial void OnContactNumberChanged(string value) => ContactNumberError = string.Empty;

        [RelayCommand]
        private async Task SaveAsync()
        {
            // 1. Reset all errors
            SupplierNameError = string.Empty;
            ContactNumberError = string.Empty;
            ErrorMessage = string.Empty;
            bool isValid = true;

            // 2. Field-Level Validation
            if (string.IsNullOrWhiteSpace(SupplierName))
            {
                SupplierNameError = "Supplier name is required.";
                isValid = false;
            }

            // Validate Contact Number (if provided)
            if (!string.IsNullOrWhiteSpace(ContactNumber))
            {
                if (!Regex.IsMatch(ContactNumber, @"^09\d{9}$"))
                {
                    ContactNumberError = "Must be 11 digits starting with 09 (e.g., 09171234567).";
                    isValid = false;
                }
            }

            if (!isValid) return;

            // 3. Call Service
            IsBusy = true;
            try
            {
                (bool Success, string ErrorMessage) result;

                if (IsEditMode)
                {
                    result = await _supplierService.UpdateAsync(
                        _supplierToEdit!.SupplierId,
                        SupplierName,
                        ContactNumber,
                        Address);
                }
                else
                {
                    result = await _supplierService.AddAsync(
                        SupplierName,
                        ContactNumber,
                        Address);
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
        private void Cancel()
        {
            RequestClose?.Invoke(false);
        }
    }
}