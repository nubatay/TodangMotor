using System;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TodangMotor.Models;
using TodangMotor.Services;

namespace TodangMotor.WPF.ViewModels
{
    public partial class UserDialogViewModel : ObservableObject
    {
        private readonly UserService _userService;
        private readonly User? _userToEdit;

        public bool IsEditMode => _userToEdit != null;
        public string DialogTitle => IsEditMode ? "Edit User" : "Add User";

        public ObservableCollection<string> Roles { get; } = new() { "Cashier", "Owner" };

        [ObservableProperty] private string _username = string.Empty;
        [ObservableProperty] private string _fullName = string.Empty;
        [ObservableProperty] private string _role = "Cashier";
        [ObservableProperty] private string _password = string.Empty;
        [ObservableProperty] private string _confirmPassword = string.Empty;

        [ObservableProperty] private string _usernameError = string.Empty;
        [ObservableProperty] private string _fullNameError = string.Empty;
        [ObservableProperty] private string _passwordError = string.Empty;
        [ObservableProperty] private string _confirmPasswordError = string.Empty;
        [ObservableProperty] private string _errorMessage = string.Empty;

        // FIXED: Added IsNotBusy to enable the save button
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotBusy))]
        private bool _isBusy;

        public bool IsNotBusy => !IsBusy;

        public string PasswordHint => IsEditMode
            ? "Leave blank to keep the current password. Min 6 characters if changing."
            : "Minimum 6 characters.";

        public event Action<bool>? RequestClose;

        public UserDialogViewModel(UserService userService, User? userToEdit = null)
        {
            _userService = userService;
            _userToEdit = userToEdit;

            if (IsEditMode)
            {
                Username = _userToEdit!.Username ?? string.Empty;
                FullName = _userToEdit.FullName ?? string.Empty;
                Role = _userToEdit.Role ?? "Cashier";
            }
        }

        partial void OnUsernameChanged(string value) => UsernameError = string.Empty;
        partial void OnFullNameChanged(string value) => FullNameError = string.Empty;
        partial void OnPasswordChanged(string value) => PasswordError = string.Empty;
        partial void OnConfirmPasswordChanged(string value) => ConfirmPasswordError = string.Empty;

        [RelayCommand]
        private async Task SaveAsync()
        {
            UsernameError = FullNameError = PasswordError = ConfirmPasswordError = ErrorMessage = string.Empty;
            bool isValid = true;

            // Validate Username
            if (string.IsNullOrWhiteSpace(Username)) { UsernameError = "Username is required."; isValid = false; }
            else if (!Regex.IsMatch(Username, @"^[a-zA-Z0-9_.]+$")) { UsernameError = "Only letters, digits, _ and . allowed."; isValid = false; }

            // Validate Full Name
            if (string.IsNullOrWhiteSpace(FullName)) { FullNameError = "Full name is required."; isValid = false; }

            // Validate Passwords
            bool passwordChanged = !string.IsNullOrEmpty(Password);
            if (!IsEditMode && !passwordChanged) { PasswordError = "Password is required for new users."; isValid = false; }
            if (passwordChanged && Password.Length < 6) { PasswordError = "Password must be at least 6 characters."; isValid = false; }
            if (passwordChanged && Password != ConfirmPassword) { ConfirmPasswordError = "Passwords do not match."; isValid = false; }

            if (!isValid) return;

            IsBusy = true;
            try
            {
                (bool Success, string ErrorMessage) result;

                if (IsEditMode)
                {
                    result = await _userService.UpdateAsync(_userToEdit!.UserId, Username, FullName, Role, Password);
                }
                else
                {
                    result = await _userService.AddAsync(Username, FullName, Role, Password);
                }

                if (result.Success) RequestClose?.Invoke(true);
                else ErrorMessage = result.ErrorMessage;
            }
            catch (Exception) { ErrorMessage = "An unexpected error occurred."; }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private void Cancel() => RequestClose?.Invoke(false);
    }
}