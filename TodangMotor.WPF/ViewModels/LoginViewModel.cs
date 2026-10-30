using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TodangMotor.Common;
using TodangMotor.Services;

namespace TodangMotor.WPF.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly AuthService _authService;
        private readonly DispatcherTimer _lockoutTimer;

        [ObservableProperty] private string _username;
        [ObservableProperty] private string _errorMessage;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private bool _isLockoutActive;

        // NEW: Field-specific validation errors
        [ObservableProperty] private string _usernameError;
        [ObservableProperty] private string _passwordError;

        [ObservableProperty] private string _buttonText = "Sign In";
        [ObservableProperty] private bool _isButtonEnabled = true;

        private int _lockoutSecondsRemaining;

        public event Action LoginSuccessful;

        public LoginViewModel(AuthService authService)
        {
            _authService = authService;
            _lockoutTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _lockoutTimer.Tick += LockoutTimer_Tick;
        }

        // Clear errors when the user starts typing again
        partial void OnUsernameChanged(string value)
        {
            UsernameError = string.Empty;
            ErrorMessage = string.Empty;
        }

        [RelayCommand]
        private async Task SignInAsync(string password)
        {
            if (IsBusy || IsLockoutActive) return;

            // 1. Reset all errors
            UsernameError = string.Empty;
            PasswordError = string.Empty;
            ErrorMessage = string.Empty;
            bool isValid = true;

            // 2. Field-Level Validation
            if (string.IsNullOrWhiteSpace(Username))
            {
                UsernameError = "Username is required.";
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                PasswordError = "Password is required.";
                isValid = false;
            }

            if (!isValid) return;

            // 3. Proceed to Authentication
            IsBusy = true;
            IsButtonEnabled = false;
            ButtonText = "Signing in...";

            try
            {
                var (result, user, lockoutSeconds) = await _authService.LoginAsync(Username, password);

                switch (result)
                {
                    case LoginResult.Success:
                        if (user != null)
                        {
                            SessionManager.Login(user);
                            LoginSuccessful?.Invoke();
                        }
                        break;

                    case LoginResult.LockedOut:
                        StartLockoutCountdown(lockoutSeconds);
                        break;

                    case LoginResult.AccountDisabled:
                        ErrorMessage = "Your account has been disabled. Please contact the Owner.";
                        break;

                    default: // Invalid Credentials
                        PasswordError = "Invalid username or password."; // <-- Puts the error right under the password
                        ErrorMessage = string.Empty; // Clears the bottom badge so it's not duplicated
                        break;
                }
            }
            catch (Exception)
            {
                ErrorMessage = "Something went wrong. Please try again.";
            }
            finally
            {
                if (!IsLockoutActive)
                {
                    IsBusy = false;
                    IsButtonEnabled = true;
                    ButtonText = "Sign In";
                }
            }
        }

        private void StartLockoutCountdown(int seconds)
        {
            if (seconds <= 0) seconds = 1;
            IsLockoutActive = true;
            _lockoutSecondsRemaining = seconds;
            UpdateLockoutMessage();
            _lockoutTimer.Start();
        }

        private void LockoutTimer_Tick(object? sender, EventArgs e)
        {
            _lockoutSecondsRemaining--;
            if (_lockoutSecondsRemaining <= 0)
            {
                _lockoutTimer.Stop();
                IsLockoutActive = false;
                ErrorMessage = string.Empty;
                IsButtonEnabled = true;
                IsBusy = false;
                ButtonText = "Sign In";
            }
            else
            {
                UpdateLockoutMessage();
            }
        }

        private void UpdateLockoutMessage()
        {
            int min = _lockoutSecondsRemaining / 60;
            int sec = _lockoutSecondsRemaining % 60;
            ErrorMessage = $"Account locked. Try again in {min}:{sec:00}.";
            IsButtonEnabled = false;
            IsBusy = false;
            ButtonText = "Locked";
        }
    }
}