using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentFormat.OpenXml.ExtendedProperties;
using System;
using System.Threading.Tasks;
using TodangMotor.Models;
using TodangMotor.Services;

namespace TodangMotor.WPF.ViewModels
{
    public partial class AdjustStockDialogViewModel : ObservableObject
    {
        private readonly ProductService _productService;
        private readonly Product _product;

        public string ProductName => _product.ProductName ?? string.Empty;
        public string BrandName => _product.Brand ?? string.Empty;
        public int CurrentQuantity => _product.QuantityOnHand;

        [ObservableProperty] private string _newQuantityText = string.Empty;
        [ObservableProperty] private string _notes = string.Empty;

        [ObservableProperty] private string _newQuantityError = string.Empty;
        [ObservableProperty] private string _errorMessage = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotBusy))]
        private bool _isBusy;

        public bool IsNotBusy => !IsBusy;
        public event Action<bool>? RequestClose;

        public AdjustStockDialogViewModel(ProductService productService, Product product)
        {
            _productService = productService;
            _product = product;
            NewQuantityText = product.QuantityOnHand.ToString();
        }

        partial void OnNewQuantityTextChanged(string value) => NewQuantityError = string.Empty;

        [RelayCommand]
        private async Task SaveAsync()
        {
            NewQuantityError = string.Empty;
            ErrorMessage = string.Empty;

            if (!int.TryParse(NewQuantityText, out int newQty) || newQty < 0)
            {
                NewQuantityError = "Must be a positive whole number (0 or higher).";
                return;
            }

            if (newQty == CurrentQuantity)
            {
                NewQuantityError = "New quantity is the same as current quantity.";
                return;
            }

            IsBusy = true;
            try
            {
                var (success, error) = await _productService.UpdateStockAsync(_product.ProductId, newQty, Notes);

                if (success) RequestClose?.Invoke(true);
                else ErrorMessage = error;
            }
            catch (Exception) { ErrorMessage = "An unexpected error occurred."; }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private void Cancel() => RequestClose?.Invoke(false);
    }
}