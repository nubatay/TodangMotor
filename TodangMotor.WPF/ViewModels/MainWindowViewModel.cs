using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Globalization;

namespace TodangMotor.WPF.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty] private ObservableObject _currentViewModel;
        [ObservableProperty] private bool _isLoggedIn;
        [ObservableProperty] private bool _isOwner;
        [ObservableProperty] private bool _isCashier;

        private readonly DashboardViewModel _dashboardViewModel;
        private readonly PosViewModel _posViewModel;
        private readonly LoginViewModel _loginViewModel;
        private readonly ItemsViewModel _itemsViewModel;
        private readonly CategoryViewModel _categoryViewModel;
        private readonly SupplierViewModel _supplierViewModel;
        private readonly InventoryViewModel _inventoryViewModel;
        private readonly SettingsViewModel _settingsViewModel;
        private readonly UserViewModel _userViewModel;
        private readonly SalesHistoryViewModel _salesHistoryViewModel;
        private readonly ReportsViewModel _reportsViewModel;
        private readonly PurchaseOrderViewModel _purchaseOrderViewModel;
        private readonly StockInViewModel _stockInViewModel;

        public MainWindowViewModel(
            DashboardViewModel dashboard,
            PosViewModel pos,
            LoginViewModel login,
            ItemsViewModel items,
            CategoryViewModel categories,
            SupplierViewModel suppliers,
            UserViewModel users,
            InventoryViewModel inventory,
            SettingsViewModel settings,
            SalesHistoryViewModel salesHistory,
            ReportsViewModel reports,
            PurchaseOrderViewModel purchaseOrders,
            StockInViewModel stockIn) 
        {
            _dashboardViewModel = dashboard;
            _posViewModel = pos;
            _loginViewModel = login;
            _itemsViewModel = items;
            _categoryViewModel = categories;
            _supplierViewModel = suppliers;
            _userViewModel = users;
            _inventoryViewModel = inventory;
            _settingsViewModel = settings;
            _reportsViewModel = reports;
            _salesHistoryViewModel = salesHistory;
            _purchaseOrderViewModel = purchaseOrders;
            _stockInViewModel = stockIn;

            CurrentViewModel = _loginViewModel;
            IsLoggedIn = false;

            _loginViewModel.LoginSuccessful += OnLoginSuccessful;
            _dashboardViewModel.NavigateRequested += OnDashboardNavigateRequested;
        }

        private void OnLoginSuccessful()
        {
            IsLoggedIn = true;

            // Set roles based on SessionManager
            IsOwner = TodangMotor.Common.SessionManager.IsOwner;
            IsCashier = TodangMotor.Common.SessionManager.IsCashier;

            CurrentViewModel = _dashboardViewModel;
        }

        private void OnDashboardNavigateRequested(string destination)
        {
            switch (destination.ToLower())
            {
                case "pos": NavigateToPos(); break;
                case "inventory": NavigateToItems(); break;
            }
        }

        [RelayCommand]
        private void NavigateToDashboard()
        {
            CurrentViewModel = _dashboardViewModel;
            _dashboardViewModel.LoadDataCommand.Execute(null);
        }

        [RelayCommand]
        private void NavigateToPos()
        {
            CurrentViewModel = _posViewModel;
            _posViewModel.LoadProductsCommand.Execute(null);
        }

        [RelayCommand]
        private void NavigateToSales()
        {
            CurrentViewModel = _salesHistoryViewModel;
            _salesHistoryViewModel.LoadDataCommand.Execute(null);
        }

        [RelayCommand]
        private void NavigateToItems()
        {
            CurrentViewModel = _itemsViewModel;
            _itemsViewModel.LoadDataCommand.Execute(null);
        }

        [RelayCommand]
        private void NavigateToCategories()
        {
            CurrentViewModel = _categoryViewModel;
            _categoryViewModel.LoadDataCommand.Execute(null);
        }

        [RelayCommand]
        private void NavigateToSuppliers()
        {
            CurrentViewModel = _supplierViewModel;
            _supplierViewModel.LoadDataCommand.Execute(null);
        }

        [RelayCommand]
        private void NavigateToUsers()
        {
            CurrentViewModel = _userViewModel;
            _userViewModel.LoadDataCommand.Execute(null);
        }

        [RelayCommand]
        private void NavigateToInventories()
        {
            CurrentViewModel = _inventoryViewModel;
            _inventoryViewModel.LoadDataCommand.Execute(null);
        }

        [RelayCommand]
        private void NavigateToSettings()
        {
            CurrentViewModel = _settingsViewModel;
            _settingsViewModel.RefreshBackupInfoCommand.Execute(null);
        }

        [RelayCommand]
        private void NavigateToReports()
        {
            CurrentViewModel = _reportsViewModel;
        }

        [RelayCommand]
        private void NavigateToStockIn()
        {
            CurrentViewModel = _stockInViewModel;
        }

        // <-- ADDED THIS COMMAND -->
        [RelayCommand]
        private void NavigateToPurchaseOrders()
        {
            CurrentViewModel = _purchaseOrderViewModel;
        }

        [RelayCommand]
        private void Logout()
        {
            TodangMotor.Common.SessionManager.Logout();
            IsLoggedIn = false;
            _loginViewModel.Username = string.Empty;
            _loginViewModel.ErrorMessage = string.Empty;
            CurrentViewModel = _loginViewModel;
        }
    }
}