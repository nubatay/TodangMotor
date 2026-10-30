using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using TodangMotor.Services;
using TodangMotor.WPF.ViewModels;
using System.Windows;

namespace TodangMotor.WPF.Views
{
    public partial class UserView : UserControl
    {
        public UserView()
        {
            InitializeComponent();

            var userService = App.ServiceProvider.GetRequiredService<UserService>();


            var vm = new UserViewModel(userService);
            DataContext = vm;

            vm.RequestOpenUserDialog += (user) =>
            {
                var dialog = new UserDialog(user);
                dialog.Owner = Window.GetWindow(this);

                if (dialog.ShowDialog() == true)
                {
                    vm.LoadDataCommand.Execute(null);
                }
            };
        }
    }
}
