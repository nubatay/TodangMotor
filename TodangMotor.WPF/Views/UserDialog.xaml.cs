using System.Windows;
using TodangMotor.Models;
using TodangMotor.Services;
using TodangMotor.WPF.ViewModels;
using Wpf.Ui.Controls;

namespace TodangMotor.WPF.Views
{
    public partial class UserDialog : FluentWindow
    {
        public UserDialog(User? userToEdit = null)
        {
            InitializeComponent();

            var service = new UserService();
            var vm = new UserDialogViewModel(service, userToEdit);

            DataContext = vm;

            vm.RequestClose += (success) =>
            {
                DialogResult = success;
                Close();
            };

            // Center the window on load
            this.Loaded += (s, e) =>
            {
                if (Owner != null)
                {
                    Left = Owner.Left + (Owner.Width - Width) / 2;
                    Top = Owner.Top + (Owner.Height - Height) / 2;
                }
            };
        }

        private void PasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is UserDialogViewModel vm)
            {
                vm.Password = PasswordInput.Password;
            }
        }

        private void ConfirmPasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is UserDialogViewModel vm)
            {
                vm.ConfirmPassword = ConfirmPasswordInput.Password;
            }
        }
    }
}