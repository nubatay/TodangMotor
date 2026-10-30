using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using TodangMotor.Services;
using TodangMotor.WPF.ViewModels;

namespace TodangMotor.WPF.Views
{
    public partial class PosView : UserControl
    {
        public PosView()
        {
            InitializeComponent();
            DataContext = App.ServiceProvider.GetRequiredService<PosViewModel>();
        }

        private void ProductCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is CashierProductView product)
            {
                if (DataContext is PosViewModel vm)
                {
                    vm.AddToCartCommand.Execute(product);
                }
            }
        }
    }
}