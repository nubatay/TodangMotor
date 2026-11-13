using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentFormat.OpenXml.ExtendedProperties;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TodangMotor.Models;
using TodangMotor.Services;

namespace TodangMotor.WPF.ViewModels
{
    // Wrapper for the lines in the builder
    public partial class StockInLine : ObservableObject
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal LineTotal => Quantity * UnitCost;
    }

    public partial class StockInViewModel : ObservableObject
    {
        private readonly StockInService _stockInService;
        private readonly ProductService _productService;
        private readonly SupplierService _supplierService;

        // --- Lookups ---
        public ObservableCollection<Supplier> Suppliers { get; } = new();
        public ObservableCollection<Product> AvailableProducts { get; } = new();

        // --- Form Fields ---
        [ObservableProperty] private Supplier? _selectedSupplier;
        [ObservableProperty] private DateTime? _deliveryDate = DateTime.Today;
        [ObservableProperty] private string _referenceNo = string.Empty;
        [ObservableProperty] private string _notes = string.Empty;

        // --- Line Builder ---
        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] private string _quantityText = string.Empty;
        [ObservableProperty] private string _unitCostText = string.Empty;

        // --- Inline Validation ---
        [ObservableProperty] private string _quantityError = string.Empty;
        [ObservableProperty] private string _unitCostError = string.Empty;
        [ObservableProperty] private string _errorMessage = string.Empty;

        // --- Grid & Totals ---
        public ObservableCollection<StockInLine> CurrentLines { get; } = new();
        [ObservableProperty] private string _totalItemsText = "0 items";
        [ObservableProperty] private string _totalUnitsText = "0 units";
        [ObservableProperty] private string _totalCostText = "₱ 0.00";

        // --- State ---
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotBusy))]
        private bool _isBusy;
        public bool IsNotBusy => !IsBusy;

        // Event to open the CostUpdatePromptDialog
        public event Action<List<CostComparisonItem>, List<CostComparisonItem>>? RequestCostUpdatePrompt;

        public StockInViewModel(StockInService stockInService, ProductService productService, SupplierService supplierService)
        {
            _stockInService = stockInService;
            _productService = productService;
            _supplierService = supplierService;

            _ = LoadInitialDataAsync();
        }

        [RelayCommand]
        private async Task LoadInitialDataAsync()
        {
            IsBusy = true;
            try
            {
                var activeSuppliers = await _stockInService.GetActiveSuppliersAsync();
                Suppliers.Clear();
                foreach (var s in activeSuppliers) Suppliers.Add(s);

                if (Suppliers.Any()) SelectedSupplier = Suppliers.First();
            }
            catch (Exception) { }
            finally { IsBusy = false; }
        }

        partial void OnSelectedSupplierChanged(Supplier? value)
        {
            _ = LoadProductsForSupplierAsync();
        }

        private async Task LoadProductsForSupplierAsync()
        {
            if (SelectedSupplier == null) return;

            AvailableProducts.Clear();
            var products = await _stockInService.GetActiveProductsBySupplierAsync(SelectedSupplier.SupplierId);
            foreach (var p in products) AvailableProducts.Add(p);

            SelectedProduct = AvailableProducts.FirstOrDefault();
            QuantityText = "1";
            UnitCostText = SelectedProduct?.CostPrice.ToString("0.00") ?? string.Empty;
            QuantityError = UnitCostError = ErrorMessage = string.Empty;
        }

        partial void OnSelectedProductChanged(Product? value)
        {
            if (value != null)
            {
                UnitCostText = value.CostPrice.ToString("0.00");
                if (string.IsNullOrEmpty(QuantityText)) QuantityText = "1";
                QuantityError = UnitCostError = string.Empty;
            }
        }

        partial void OnQuantityTextChanged(string value) => QuantityError = string.Empty;
        partial void OnUnitCostTextChanged(string value) => UnitCostError = string.Empty;

        [RelayCommand]
        private void AddLine()
        {
            QuantityError = UnitCostError = ErrorMessage = string.Empty;

            if (SelectedProduct == null)
            {
                ErrorMessage = "Please select a product.";
                return;
            }

            if (!int.TryParse(QuantityText, out int qty) || qty <= 0)
            {
                QuantityError = "Must be a whole number greater than 0.";
                return;
            }

            if (!decimal.TryParse(UnitCostText, out decimal cost) || cost < 0)
            {
                UnitCostError = "Must be a valid number (0 or higher).";
                return;
            }

            if (CurrentLines.Any(l => l.ProductId == SelectedProduct.ProductId))
            {
                ErrorMessage = "This product is already in the list. Remove it first to change the quantity.";
                return;
            }

            CurrentLines.Add(new StockInLine
            {
                ProductId = SelectedProduct.ProductId,
                ProductName = SelectedProduct.ProductName ?? string.Empty,
                Brand = SelectedProduct.Brand ?? string.Empty,
                Unit = SelectedProduct.Unit ?? string.Empty,
                Quantity = qty,
                UnitCost = cost
            });

            UpdateTotals();

            // Reset builder
            QuantityText = "1";
            UnitCostText = SelectedProduct.CostPrice.ToString("0.00");
            SelectedProduct = AvailableProducts.FirstOrDefault(); // Refresh focus
        }

        [RelayCommand]
        private void RemoveLine(StockInLine line)
        {
            if (line == null) return;
            CurrentLines.Remove(line);
            UpdateTotals();
        }

        private void UpdateTotals()
        {
            int items = CurrentLines.Count;
            int units = CurrentLines.Sum(l => l.Quantity);
            decimal total = CurrentLines.Sum(l => l.LineTotal);

            TotalItemsText = $"{items} item(s)";
            TotalUnitsText = $"{units} unit(s)";
            TotalCostText = $"₱ {total:N2}";
        }

        [RelayCommand]
        private async Task CompleteStockInAsync()
        {
            ErrorMessage = string.Empty;

            if (SelectedSupplier == null || !DeliveryDate.HasValue)
            {
                ErrorMessage = "Please select a supplier and delivery date.";
                return;
            }

            if (CurrentLines.Count == 0)
            {
                ErrorMessage = "Add at least one line item before completing.";
                return;
            }

            IsBusy = true;
            try
            {
                var requestLines = CurrentLines.Select(l => new StockInRequestLine
                {
                    ProductId = l.ProductId,
                    Quantity = l.Quantity,
                    UnitCost = l.UnitCost
                }).ToList();

                var result = await _stockInService.CompleteStockInAsync(
                    SelectedSupplier.SupplierId,
                    DeliveryDate.Value,
                    ReferenceNo,
                    Notes,
                    requestLines);

                if (!result.Success)
                {
                    ErrorMessage = result.ErrorMessage;
                    return;
                }

                // Check for cost changes
                var changes = result.CostComparisons.Where(c => c.HasChange).ToList();
                var skipped = result.CostComparisons.Where(c => !c.CanUpdate).ToList();

                if (changes.Count > 0 || skipped.Count > 0)
                {
                    // Trigger the dialog
                    RequestCostUpdatePrompt?.Invoke(changes, skipped);
                }

                // Clear form after successful save
                CurrentLines.Clear();
                ReferenceNo = string.Empty;
                Notes = string.Empty;
                DeliveryDate = DateTime.Today;
                UpdateTotals();

                // Reload products to get latest stock/cost
                await LoadProductsForSupplierAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = "An unexpected error occurred: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}