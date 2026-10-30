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
    public class UserDisplayItem
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }

        public string StatusText => IsActive ? "Active" : "Inactive";
        public string StatusColor => IsActive ? "#22C55E" : "#EF4444";
        public string RoleColor => Role == "Owner" ? "#4D6BFE" : "#64748B";
    }

    public partial class UserViewModel : ObservableObject
    {
        private readonly UserService _userService;
        private List<User> _allUsers = new();

        public ObservableCollection<UserDisplayItem> FilteredUsers { get; } = new();
        public ObservableCollection<string> StatusFilters { get; } = new() { "All", "Active", "Inactive" };
        public ObservableCollection<string> RoleFilters { get; } = new() { "All Roles", "Owner", "Cashier" };

        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _selectedStatus = "All";
        [ObservableProperty] private string _selectedRole = "All Roles";
        [ObservableProperty] private bool _isLoading;

        public event Action<User?>? RequestOpenUserDialog;

        public UserViewModel(UserService userService)
        {
            _userService = userService;

            // FORCE DEFAULTS: This fixes the empty table issue
            SelectedStatus = "All";
            SelectedRole = "All Roles";

            _ = LoadDataAsync();
        }

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            IsLoading = true;
            try
            {
                var (success, error, users) = await _userService.GetAllAsync();
                if (success)
                {
                    _allUsers = users ?? new List<User>();
                    ApplyFilter();
                }
            }
            catch (Exception) { }
            finally { IsLoading = false; }
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();
        partial void OnSelectedStatusChanged(string value) => ApplyFilter();
        partial void OnSelectedRoleChanged(string value) => ApplyFilter();

        private void ApplyFilter()
        {
            if (_allUsers == null) return;

            var filtered = _allUsers.AsEnumerable();

            // Null checks prevent the table from disappearing on load
            if (SelectedStatus == "Active") filtered = filtered.Where(u => u.IsActive);
            else if (SelectedStatus == "Inactive") filtered = filtered.Where(u => !u.IsActive);

            if (!string.IsNullOrEmpty(SelectedRole) && SelectedRole != "All Roles")
                filtered = filtered.Where(u => u.Role == SelectedRole);

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string search = SearchText.ToLower();
                filtered = filtered.Where(u =>
                    (u.Username?.ToLower().Contains(search) ?? false) ||
                    (u.FullName?.ToLower().Contains(search) ?? false));
            }

            var displayList = filtered.Select(u => new UserDisplayItem
            {
                UserId = u.UserId,
                Username = u.Username ?? string.Empty,
                FullName = u.FullName ?? string.Empty,
                Role = u.Role ?? string.Empty,
                IsActive = u.IsActive
            }).OrderBy(u => u.Username).ToList();

            FilteredUsers.Clear();
            foreach (var item in displayList) FilteredUsers.Add(item);
        }

        [RelayCommand]
        private void AddUser() => RequestOpenUserDialog?.Invoke(null);

        [RelayCommand]
        private void EditUser(UserDisplayItem item)
        {
            if (item == null) return;
            var rawUser = _allUsers.FirstOrDefault(u => u.UserId == item.UserId);
            if (rawUser != null) RequestOpenUserDialog?.Invoke(rawUser);
        }

        [RelayCommand]
        private async Task ToggleStatusAsync(UserDisplayItem item)
        {
            if (item == null) return;

            if (item.IsActive) await _userService.DeactivateAsync(item.UserId);
            else await _userService.ReactivateAsync(item.UserId);

            await LoadDataAsync();
        }
    }
}