using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TodangMotor.WPF.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty] private ObservableObject _currentViewModel;
        [ObservableProperty] private bool _isLoggedIn;

        private readonly DashboardViewModel _dashboardViewModel;
        private readonly PosViewModel _posViewModel;
        private readonly LoginViewModel _loginViewModel;
        private readonly ItemsViewModel _itemsViewModel;
        private readonly CategoryViewModel _categoryViewModel;
        private readonly SupplierViewModel _supplierViewModel;
        private readonly InventoryViewModel _inventoryViewModel;
        private readonly SettingsViewModel _settingsViewModel;

        private readonly UserViewModel _userViewModel;

        public MainWindowViewModel(
            DashboardViewModel dashboard, 
            PosViewModel pos, 
            LoginViewModel login, 
            ItemsViewModel items, 
            CategoryViewModel categories, 
            SupplierViewModel suppliers,
            UserViewModel users, 
            InventoryViewModel inventory, 
            SettingsViewModel settings)
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

            CurrentViewModel = _loginViewModel;
            IsLoggedIn = false;

            _loginViewModel.LoginSuccessful += OnLoginSuccessful;
            _dashboardViewModel.NavigateRequested += OnDashboardNavigateRequested;
            _settingsViewModel = settings;
        }

        private void OnLoginSuccessful()
        {
            IsLoggedIn = true;
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
            CurrentViewModel = _settingsViewModel; // Use the private field, not 'settings'
            _settingsViewModel.RefreshBackupInfoCommand.Execute(null);
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