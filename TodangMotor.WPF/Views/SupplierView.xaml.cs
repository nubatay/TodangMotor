using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using TodangMotor.Services;
using TodangMotor.WPF.ViewModels;
using System.Windows;

namespace TodangMotor.WPF.Views
{
    public partial class SupplierView : UserControl
    {
        public SupplierView()
        {
            InitializeComponent();

            var supplierService = App.ServiceProvider.GetRequiredService<SupplierService>();

            var vm = new SupplierViewModel(supplierService);
            DataContext = vm;

            vm.RequestOpenSupplierDialog += (supplier) =>
            {
                var dialog = new SupplierDialog(supplier);
                dialog.Owner = Window.GetWindow(this);

                if (dialog.ShowDialog() == true)
                {
                    vm.LoadDataCommand.Execute(null);
                }
            };
        }
    }
}