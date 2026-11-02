using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using TodangMotor.Services;
using TodangMotor.WPF.ViewModels;
using System.Windows;

namespace TodangMotor.WPF.Views
{
    public partial class InventoryView : UserControl
    {
        public InventoryView()
        {
            InitializeComponent();

            var productService = App.ServiceProvider.GetRequiredService<ProductService>();
            var categoryService = App.ServiceProvider.GetRequiredService<CategoryService>();

            // Removed StockMovementService
            var vm = new InventoryViewModel(productService, categoryService);
            DataContext = vm;

            vm.RequestOpenAdjustStockDialog += (product) =>
            {
                var dialog = new AdjustStockDialog(product);
                dialog.Owner = Window.GetWindow(this);

                if (dialog.ShowDialog() == true)
                {
                    vm.LoadDataCommand.Execute(null);
                }
            };
        }
    }
}