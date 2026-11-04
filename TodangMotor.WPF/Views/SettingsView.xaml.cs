using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using TodangMotor.Services;
using TodangMotor.WPF.ViewModels;
using System.Windows;

namespace TodangMotor.WPF.Views
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();

            var backupService = App.ServiceProvider.GetRequiredService<BackupRestoreService>();
            var vm = new SettingsViewModel(backupService);

            DataContext = vm;

            // Listen for Message Box requests from the ViewModel
            vm.ShowMessage += (title, message, isError) =>
            {
                var icon = isError ? MessageBoxImage.Error : MessageBoxImage.Information;
                MessageBox.Show(message, title, MessageBoxButton.OK, icon);
            };
        }
    }
}