using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using TodangMotor.WPF.ViewModels;

namespace TodangMotor.WPF.Views
{
    public partial class LoginView : UserControl
    {
        public LoginView()
        {
            InitializeComponent();
            DataContext = App.ServiceProvider.GetRequiredService<LoginViewModel>();
            this.Loaded += LoginView_Loaded;
        }

        private void LoginView_Loaded(object sender, RoutedEventArgs e)
        {
            UserInput.Focus();
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is LoginViewModel vm)
            {
                // Get password from whichever box is currently visible
                string password = PassInput.Visibility == Visibility.Visible ? PassInput.Password : PassInputPlain.Text;

                if (vm.SignInCommand.CanExecute(password))
                {
                    vm.SignInCommand.Execute(password);
                }
            }
        }

        // Hide placeholder and clear errors when typing
        private void PassInput_PasswordChanged(object sender, RoutedEventArgs e)
        {
            PasswordPlaceholder.Visibility = string.IsNullOrEmpty(PassInput.Password) ? Visibility.Visible : Visibility.Collapsed;

            // Keep the visible text box in sync
            if (PassInputPlain.Visibility == Visibility.Collapsed)
                PassInputPlain.Text = PassInput.Password;

            if (DataContext is LoginViewModel vm) vm.PasswordError = string.Empty;
        }

        private void PassInputPlain_TextChanged(object sender, TextChangedEventArgs e)
        {
            PasswordPlaceholder.Visibility = string.IsNullOrEmpty(PassInputPlain.Text) ? Visibility.Visible : Visibility.Collapsed;

            // Keep the password box in sync
            if (PassInput.Visibility == Visibility.Collapsed)
                PassInput.Password = PassInputPlain.Text;

            if (DataContext is LoginViewModel vm) vm.PasswordError = string.Empty;
        }

        // Toggle Eye Icon
        private void TogglePasswordBtn_Click(object sender, RoutedEventArgs e)
        {
            if (PassInput.Visibility == Visibility.Visible)
            {
                // Switch to Plain Text (Show password)
                PassInputPlain.Text = PassInput.Password;
                PassInput.Visibility = Visibility.Collapsed;
                PassInputPlain.Visibility = Visibility.Visible;
                PassInputPlain.Focus();
                PassInputPlain.CaretIndex = PassInputPlain.Text.Length;

                // FIXED: Use the Enum instead of a string
                EyeIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.EyeOff24;
            }
            else
            {
                // Switch to Password (Hide password)
                PassInput.Password = PassInputPlain.Text;
                PassInputPlain.Visibility = Visibility.Collapsed;
                PassInput.Visibility = Visibility.Visible;
                PassInput.Focus();

                // FIXED: Use the Enum instead of a string
                EyeIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.Eye24;
            }
        }
    }
}