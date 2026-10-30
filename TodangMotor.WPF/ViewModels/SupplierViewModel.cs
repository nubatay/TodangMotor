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
    public class SupplierDisplayItem
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public bool IsActive { get; set; }

        public string StatusText => IsActive ? "Active" : "Inactive";
        public string StatusColor => IsActive ? "#22C55E" : "#EF4444";
    }

    public partial class SupplierViewModel : ObservableObject
    {
        private readonly SupplierService _supplierService;

        private List<Supplier> _allSuppliers = new();

        public ObservableCollection<SupplierDisplayItem> FilteredSuppliers { get; } = new();
        public ObservableCollection<string> StatusFilters { get; } = new() { "All", "Active", "Inactive" };

        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _selectedStatus = "Active";
        [ObservableProperty] private bool _isLoading;

        public event Action<Supplier?>? RequestOpenSupplierDialog;

        public SupplierViewModel(SupplierService supplierService)
        {
            _supplierService = supplierService;
            _ = LoadDataAsync();
        }

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            IsLoading = true;
            try
            {
                var (success, error, suppliers) = await _supplierService.GetAllAsync();
                if (success)
                {
                    _allSuppliers = suppliers ?? new List<Supplier>();
                    ApplyFilter();
                }
            }
            catch (Exception) { }
            finally { IsLoading = false; }
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();
        partial void OnSelectedStatusChanged(string value) => ApplyFilter();

        private void ApplyFilter()
        {
            if (_allSuppliers == null) return;

            var filtered = _allSuppliers.AsEnumerable();

            if (SelectedStatus == "Active") filtered = filtered.Where(s => s.IsActive);
            else if (SelectedStatus == "Inactive") filtered = filtered.Where(s => !s.IsActive);

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string search = SearchText.ToLower();
                filtered = filtered.Where(s =>
                    (s.SupplierName?.ToLower().Contains(search) ?? false) ||
                    (s.ContactNumber?.ToLower().Contains(search) ?? false));
            }

            var displayList = filtered.Select(s => new SupplierDisplayItem
            {
                SupplierId = s.SupplierId,
                SupplierName = s.SupplierName ?? string.Empty,
                ContactNumber = s.ContactNumber ?? string.Empty,
                Address = s.Address ?? string.Empty,
                IsActive = s.IsActive
            }).OrderBy(s => s.SupplierName).ToList();

            FilteredSuppliers.Clear();
            foreach (var item in displayList) FilteredSuppliers.Add(item);
        }

        [RelayCommand]
        private void AddSupplier() => RequestOpenSupplierDialog?.Invoke(null);

        [RelayCommand]
        private void EditSupplier(SupplierDisplayItem item)
        {
            if (item == null) return;
            var rawSupplier = _allSuppliers.FirstOrDefault(s => s.SupplierId == item.SupplierId);
            if (rawSupplier != null) RequestOpenSupplierDialog?.Invoke(rawSupplier);
        }

        [RelayCommand]
        private async Task ToggleStatusAsync(SupplierDisplayItem item)
        {
            if (item == null) return;

            if (item.IsActive) await _supplierService.DeactivateAsync(item.SupplierId);
            else await _supplierService.ReactivateAsync(item.SupplierId);

            await LoadDataAsync();
        }
    }
}