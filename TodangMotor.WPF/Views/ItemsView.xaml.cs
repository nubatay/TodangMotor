using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using TodangMotor.WPF.ViewModels;
using System.Windows;

namespace TodangMotor.WPF.Views
{
    public partial class ItemsView : UserControl
    {
        public ItemsView()
        {
            InitializeComponent();

            // Get the ViewModel from the DI container
            var vm = App.ServiceProvider.GetRequiredService<ItemsViewModel>();
            DataContext = vm;

            // Listen for the event to open the dialog
            vm.RequestOpenProductDialog += (product) =>
            {
                var dialog = new ProductDialog(product);
                dialog.Owner = Window.GetWindow(this);

                if (dialog.ShowDialog() == true)
                {
                    // If saved successfully, refresh the grid
                    vm.LoadDataCommand.Execute(null);
                }
            };
        }
    }
}