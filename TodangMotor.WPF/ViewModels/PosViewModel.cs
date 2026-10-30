using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TodangMotor.Services; // For SalesService and CashierProductView

namespace TodangMotor.WPF.ViewModels
{
    // 1. Wrapper for the Cart Items to handle Qty changes
    public partial class CartItemViewModel : ObservableObject
    {
        private readonly Action _onCartChanged;
        private readonly int _maxOnHand;

        public int ProductId { get; }
        public string ProductName { get; }
        public string Brand { get; }
        public decimal UnitPrice { get; }

        [ObservableProperty] private int _quantity;

        public decimal LineTotal => UnitPrice * Quantity;
        public string DisplayName => string.IsNullOrEmpty(Brand) ? ProductName : $"{ProductName} — {Brand}";

        public CartItemViewModel(CashierProductView product, int qty, Action onCartChanged)
        {
            ProductId = product.ProductId;
            ProductName = product.ProductName;
            Brand = product.Brand;
            UnitPrice = product.SellingPrice;
            _quantity = qty;
            _maxOnHand = product.QuantityOnHand;
            _onCartChanged = onCartChanged;
        }

        // Triggered when the cashier types a new Qty in the cart
        partial void OnQuantityChanged(int value)
        {
            if (value <= 0) { Quantity = 1; return; }
            if (value > _maxOnHand) { Quantity = _maxOnHand; return; }

            OnPropertyChanged(nameof(LineTotal));
            _onCartChanged?.Invoke();
        }
    }

    public partial class PosViewModel : ObservableObject
    {
        private readonly SalesService _salesService;

        // --- Master Product List ---
        private List<CashierProductView> _masterProductList = new();

        // --- Observable Collections for the UI ---
        public ObservableCollection<CashierProductView> FilteredProducts { get; } = new();
        public ObservableCollection<CartItemViewModel> Cart { get; } = new();

        // --- Filtering ---
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _selectedCategory = "All Categories";
        public ObservableCollection<string> Categories { get; } = new() { "All Categories" };

        // --- Cart & Checkout Properties ---
        [ObservableProperty] private string _customerName = string.Empty;
        [ObservableProperty] private string _paymentMethod = "Cash";
        [ObservableProperty] private string _tenderedAmountText = string.Empty;
        [ObservableProperty] private string _gcashReference = string.Empty;

        // --- Inline Validations ---
        [ObservableProperty] private string _tenderedError = string.Empty;
        [ObservableProperty] private string _gcashError = string.Empty;

        // --- Totals ---
        [ObservableProperty] private string _subtotalText = "₱ 0.00";
        [ObservableProperty] private string _changeText = "₱ 0.00";
        [ObservableProperty] private string _changeColor = "#64748B"; // Muted
        [ObservableProperty] private bool _isGcash;
        [ObservableProperty] private bool _isCash = true;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotBusy))]
        private bool _isBusy;

        public bool IsNotBusy => !IsBusy;

        public PosViewModel(SalesService salesService)
        {
            _salesService = salesService;
            
        }

        // --- Commands ---

        [RelayCommand]
        private async Task LoadProductsAsync()
        {
            var (success, error, products) = await _salesService.GetActiveProductsForCashierAsync();
            if (success)
            {
                _masterProductList = products;
                ApplyFilter();
            }
        }

        [RelayCommand]
        private void AddToCart(CashierProductView product)
        {
            if (product == null) return;

            var existingItem = Cart.FirstOrDefault(c => c.ProductId == product.ProductId);
            int currentInCart = existingItem?.Quantity ?? 0;

            if (currentInCart + 1 > product.QuantityOnHand)
            {
                // In a real app, you might trigger a Toast notification here
                return;
            }

            if (existingItem != null)
            {
                existingItem.Quantity++;
            }
            else
            {
                Cart.Add(new CartItemViewModel(product, 1, RecalculateTotals));
            }

            RecalculateTotals();
        }

        [RelayCommand]
        private void RemoveFromCart(CartItemViewModel item)
        {
            if (Cart.Contains(item))
            {
                Cart.Remove(item);
                RecalculateTotals();
            }
        }

        [RelayCommand]
        private void ClearCart()
        {
            Cart.Clear();
            TenderedAmountText = string.Empty;
            CustomerName = string.Empty;
            GcashReference = string.Empty;
            TenderedError = string.Empty;
            GcashError = string.Empty;
            RecalculateTotals();
        }

        [RelayCommand]
        private async Task CompleteSaleAsync()
        {
            // 1. Reset Validations
            TenderedError = string.Empty;
            GcashError = string.Empty;

            if (Cart.Count == 0) return;

            decimal subtotal = Cart.Sum(c => c.LineTotal);
            decimal tendered = 0;

            // 2. Validate Cash
            if (PaymentMethod == "Cash")
            {
                if (string.IsNullOrWhiteSpace(TenderedAmountText))
                {
                    TenderedError = "Amount tendered is required.";
                    return;
                }
                if (!decimal.TryParse(TenderedAmountText, out tendered))
                {
                    TenderedError = "Please enter a valid number.";
                    return;
                }
                if (tendered < subtotal)
                {
                    TenderedError = $"Amount is short by ₱{(subtotal - tendered):N2}";
                    return;
                }
            }
            // 3. Validate GCash
            else
            {
                if (string.IsNullOrWhiteSpace(GcashReference))
                {
                    GcashError = "GCash Reference Number is required.";
                    return;
                }
                tendered = subtotal; // Auto-settle for GCash
            }

            // 4. Process Sale
            IsBusy = true;
            var lines = Cart.Select(c => new SaleRequestLine { ProductId = c.ProductId, Quantity = c.Quantity }).ToList();

            var (success, error, saleId, invoiceNo) = await _salesService.CompleteSaleAsync(
                lines, PaymentMethod, tendered, CustomerName, GcashReference);

            IsBusy = false;

            if (success)
            {
                // Show success (we will wire up a receipt later)
                System.Windows.MessageBox.Show($"Sale Completed! Invoice: {invoiceNo}\nChange: ₱{(tendered - subtotal):N2}", "Success");
                ClearCart();
                await LoadProductsAsync(); // Refresh stock
            }
            else
            {
                System.Windows.MessageBox.Show(error, "Sale Failed");
            }
        }

        // --- UI Updates ---

        // Triggered when SearchText or SelectedCategory changes
        partial void OnSearchTextChanged(string value) => ApplyFilter();
        partial void OnSelectedCategoryChanged(string value) => ApplyFilter();

        // Triggered when Payment Method changes
        partial void OnPaymentMethodChanged(string value)
        {
            IsCash = value == "Cash";
            IsGcash = value == "GCash";
            TenderedError = string.Empty;
            GcashError = string.Empty;
            RecalculateTotals();
        }

        // Triggered when Tendered Amount changes (Live Validation)
        partial void OnTenderedAmountTextChanged(string value)
        {
            RecalculateTotals();
        }

        private void ApplyFilter()
        {
            FilteredProducts.Clear();
            var filtered = _masterProductList.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                filtered = filtered.Where(p =>
                    p.ProductName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    p.Brand.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            // If you have CategoryId mapped, you can filter by category here
            foreach (var p in filtered.OrderBy(p => p.ProductName))
            {
                FilteredProducts.Add(p);
            }
        }

        private void RecalculateTotals()
        {
            decimal subtotal = Cart.Sum(c => c.LineTotal);
            SubtotalText = "₱ " + subtotal.ToString("N2");

            if (IsGcash)
            {
                ChangeText = "₱ 0.00";
                ChangeColor = "#64748B";
                return;
            }

            if (string.IsNullOrWhiteSpace(TenderedAmountText))
            {
                ChangeText = "₱ 0.00";
                ChangeColor = "#64748B";
                return;
            }

            if (!decimal.TryParse(TenderedAmountText, out decimal tendered))
            {
                ChangeText = "Invalid";
                ChangeColor = "#EF4444"; // Red
                return;
            }

            if (tendered < subtotal)
            {
                ChangeText = $"₱ {(subtotal - tendered):N2} short";
                ChangeColor = "#EF4444"; // Red
            }
            else
            {
                ChangeText = "₱ " + (tendered - subtotal).ToString("N2");
                ChangeColor = "#22C55E"; // Green
            }
        }
    }
}