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
    public class SaleDisplayItem
    {
        public int SaleId { get; set; }
        public string InvoiceNo { get; set; } = string.Empty;
        public string DateDisplay { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public decimal Subtotal { get; set; }
    }

    public partial class SalesHistoryViewModel : ObservableObject
    {
        private readonly SalesService _salesService;
        private List<Sale> _allSales = new();

        public ObservableCollection<SaleDisplayItem> FilteredSales { get; } = new();
        public ObservableCollection<string> DateFilters { get; } = new() { "Today", "Last 7 Days", "Last 30 Days", "This Month", "Custom" };

        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _selectedDateFilter = "Today";
        [ObservableProperty] private bool _isCustomDateVisible = false;
        [ObservableProperty] private DateTime? _fromDate = DateTime.Today;
        [ObservableProperty] private DateTime? _toDate = DateTime.Today;
        [ObservableProperty] private bool _isLoading;

        public event Action<int>? RequestOpenReceiptDialog;

        public SalesHistoryViewModel(SalesService salesService)
        {
            _salesService = salesService;
            SelectedDateFilter = "Today";
            ApplyDatePreset("Today");
        }

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            IsLoading = true;
            try
            {
                var (success, error, sales) = await _salesService.GetAllSalesAsync();
                if (success)
                {
                    _allSales = sales ?? new List<Sale>();
                    ApplyFilter();
                }
            }
            catch (Exception) { }
            finally { IsLoading = false; }
        }

        [RelayCommand]
        private void ApplyDatePreset(string preset)
        {
            SelectedDateFilter = preset;
            DateTime today = DateTime.Today;
            IsCustomDateVisible = preset == "Custom";

            switch (preset)
            {
                case "Today": FromDate = today; ToDate = today; break;
                case "Last 7 Days": FromDate = today.AddDays(-6); ToDate = today; break;
                case "Last 30 Days": FromDate = today.AddDays(-29); ToDate = today; break;
                case "This Month": FromDate = new DateTime(today.Year, today.Month, 1); ToDate = today; break;
                case "Custom": return; // Wait for user to pick dates
            }

            _ = LoadDataAsync();
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();

        private void ApplyFilter()
        {
            if (_allSales == null) return;

            var filtered = _allSales.AsEnumerable();

            if (FromDate.HasValue && ToDate.HasValue)
            {
                var from = FromDate.Value.Date;
                var to = ToDate.Value.Date.AddDays(1);
                filtered = filtered.Where(s => s.SaleDate >= from && s.SaleDate < to);
            }

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string search = SearchText.ToLower();
                filtered = filtered.Where(s => s.InvoiceNo?.ToLower().Contains(search) ?? false);
            }

            var displayList = filtered
                .OrderByDescending(s => s.SaleDate)
                .Select(s => new SaleDisplayItem
                {
                    SaleId = s.SaleId,
                    InvoiceNo = s.InvoiceNo ?? string.Empty,
                    DateDisplay = s.SaleDate.ToString("MMM d, yyyy · h:mm tt"),
                    CustomerName = string.IsNullOrEmpty(s.CustomerName) ? "Walk-in" : s.CustomerName,
                    PaymentMethod = s.PaymentMethod ?? string.Empty,
                    Subtotal = s.Subtotal
                }).ToList();

            FilteredSales.Clear();
            foreach (var item in displayList) FilteredSales.Add(item);
        }

        [RelayCommand]
        private void ViewReceipt(SaleDisplayItem item)
        {
            if (item == null) return;
            RequestOpenReceiptDialog?.Invoke(item.SaleId);
        }
    }
}