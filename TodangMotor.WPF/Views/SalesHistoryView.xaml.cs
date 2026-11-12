using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using TodangMotor.Services;
using TodangMotor.WPF.ViewModels;
using System.Windows;

namespace TodangMotor.WPF.Views
{
    public partial class SalesHistoryView : UserControl
    {
        public SalesHistoryView()
        {
            InitializeComponent();

            var salesService = App.ServiceProvider.GetRequiredService<SalesService>();
            var vm = new SalesHistoryViewModel(salesService);
            DataContext = vm;

            vm.RequestOpenReceiptDialog += (saleId) =>
            {
                var dialog = new SaleDetailDialog(saleId);
                dialog.Owner = Window.GetWindow(this);
                dialog.ShowDialog();
            };
        }
    }
}