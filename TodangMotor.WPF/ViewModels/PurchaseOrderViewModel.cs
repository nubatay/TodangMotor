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
    // Wrapper for the line items being built
    public partial class POBuilderLine : ObservableObject
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }

    // Wrapper for the history grid
    public class POHistoryItem
    {
        public int PurchaseOrderId { get; set; }
        public string PONumber { get; set; } = string.Empty;
        public string OrderDate { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public int ItemCount { get; set; }
    }

    public partial class PurchaseOrderViewModel : ObservableObject
    {
        private readonly PurchaseOrderService _poService;

        // --- Tab Control ---
        [ObservableProperty] private int _selectedTabIndex = 0;

        // --- Tab 1: Build PO ---
        public ObservableCollection<Supplier> Suppliers { get; } = new();
        [ObservableProperty] private Supplier? _selectedSupplier;
        [ObservableProperty] private DateTime? _orderDate = DateTime.Today;
        [ObservableProperty] private string _notes = string.Empty;

        public ObservableCollection<Product> AvailableProducts { get; } = new();
        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] private string _quantityText = "1";

        public ObservableCollection<POBuilderLine> CurrentLines { get; } = new();
        [ObservableProperty] private string _totalItemsText = "0 items";
        [ObservableProperty] private string _totalUnitsText = "0 units";

        // --- Tab 2: History ---
        public ObservableCollection<POHistoryItem> HistoryItems { get; } = new();
        [ObservableProperty] private string _historySearchText = string.Empty;

        [ObservableProperty] private bool _isBusy;

        // Events for PDF export and Viewing Details
        public event Action<PurchaseOrderDetail>? RequestGeneratePdf;
        public event Action<int>? RequestOpenPODetailDialog;

        public PurchaseOrderViewModel(PurchaseOrderService poService)
        {
            _poService = poService;
            _ = LoadInitialDataAsync();
        }

        [RelayCommand]
        private async Task LoadInitialDataAsync()
        {
            IsBusy = true;
            try
            {
                Suppliers.Clear();
                var activeSuppliers = await _poService.GetActiveSuppliersAsync();
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
            var products = await _poService.GetProductsBySupplierAsync(SelectedSupplier.SupplierId);
            foreach (var p in products) AvailableProducts.Add(p);

            if (AvailableProducts.Any()) SelectedProduct = AvailableProducts.First();
            else SelectedProduct = null;
        }

        // --- Tab 1 Commands ---

        [RelayCommand]
        private void AddLine()
        {
            if (SelectedProduct == null) return;
            if (!int.TryParse(QuantityText, out int qty) || qty <= 0) return;

            if (CurrentLines.Any(l => l.ProductId == SelectedProduct.ProductId))
            {
                // Optional: Show a message that it's already in the list
                return;
            }

            CurrentLines.Add(new POBuilderLine
            {
                ProductId = SelectedProduct.ProductId,
                ProductName = SelectedProduct.ProductName ?? string.Empty,
                Brand = SelectedProduct.Brand ?? string.Empty,
                Unit = SelectedProduct.Unit ?? string.Empty,
                Quantity = qty
            });

            QuantityText = "1";
            UpdateTotals();
        }

        [RelayCommand]
        private void RemoveLine(POBuilderLine line)
        {
            if (line == null) return;
            CurrentLines.Remove(line);
            UpdateTotals();
        }

        private void UpdateTotals()
        {
            int totalItems = CurrentLines.Count;
            int totalUnits = CurrentLines.Sum(l => l.Quantity);
            TotalItemsText = $"{totalItems} item(s)";
            TotalUnitsText = $"{totalUnits} unit(s)";
        }

        [RelayCommand]
        private async Task SaveAndExportAsync()
        {
            if (SelectedSupplier == null || CurrentLines.Count == 0 || !OrderDate.HasValue) return;

            IsBusy = true;
            try
            {
                var requestLines = CurrentLines.Select(l => new PurchaseOrderRequestLine
                {
                    ProductId = l.ProductId,
                    Quantity = l.Quantity
                }).ToList();

                var (success, error, poNumber) = await _poService.SavePurchaseOrderAsync(
                    SelectedSupplier.SupplierId,
                    OrderDate.Value,
                    Notes,
                    requestLines);

                if (!success) return; // Show error

                // Fetch the newly created PO to generate PDF
                var (detailOk, detailErr, detailList) = await _poService.GetAllAsync(poNumber);
                if (detailOk && detailList.Count > 0)
                {
                    var poHeader = detailList.FirstOrDefault(o => o.PONumber == poNumber);
                    if (poHeader != null)
                    {
                        var (detOk, detErr, detail) = await _poService.GetDetailAsync(poHeader.PurchaseOrderId);
                        if (detOk && detail != null)
                        {
                            RequestGeneratePdf?.Invoke(detail);
                        }
                    }
                }

                // Clear form
                CurrentLines.Clear();
                Notes = string.Empty;
                UpdateTotals();
                _ = LoadHistoryAsync(); // Refresh history tab
            }
            catch (Exception) { }
            finally { IsBusy = false; }
        }

        // --- Tab 2 Commands ---

        [RelayCommand]
        private async Task LoadHistoryAsync()
        {
            IsBusy = true;
            try
            {
                var (success, error, orders) = await _poService.GetAllAsync();
                if (success && orders != null)
                {
                    var filtered = orders.AsEnumerable();
                    if (!string.IsNullOrWhiteSpace(HistorySearchText))
                    {
                        string search = HistorySearchText.ToLower();
                        filtered = filtered.Where(o =>
                            (o.PONumber?.ToLower().Contains(search) ?? false) ||
                            (o.SupplierName?.ToLower().Contains(search) ?? false));
                    }

                    HistoryItems.Clear();
                    foreach (var o in filtered.OrderByDescending(x => x.OrderDate))
                    {
                        HistoryItems.Add(new POHistoryItem
                        {
                            PurchaseOrderId = o.PurchaseOrderId,
                            PONumber = o.PONumber ?? string.Empty,
                            OrderDate = o.OrderDate.ToString("MMM d, yyyy"),
                            SupplierName = o.SupplierName ?? "—",
                            ItemCount = o.ItemCount
                        });
                    }
                }
            }
            catch (Exception) { }
            finally { IsBusy = false; }
        }

        partial void OnSelectedTabIndexChanged(int value)
        {
            if (value == 1) _ = LoadHistoryAsync();
        }

        partial void OnHistorySearchTextChanged(string value) => _ = LoadHistoryAsync();

        [RelayCommand]
        private void ViewPODetail(POHistoryItem item)
        {
            if (item == null) return;
            RequestOpenPODetailDialog?.Invoke(item.PurchaseOrderId);
        }
    }
}