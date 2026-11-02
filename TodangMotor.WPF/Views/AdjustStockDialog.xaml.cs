using System.Windows;
using TodangMotor.Models;
using TodangMotor.Services;
using TodangMotor.WPF.ViewModels;
using Wpf.Ui.Controls;

namespace TodangMotor.WPF.Views
{
    public partial class AdjustStockDialog : FluentWindow
    {
        public AdjustStockDialog(Product product)
        {
            InitializeComponent();

            var service = new ProductService();
            var vm = new AdjustStockDialogViewModel(service, product);
            DataContext = vm;

            vm.RequestClose += (success) =>
            {
                DialogResult = success;
                Close();
            };

            this.Loaded += (s, e) => {
                if (Owner != null)
                {
                    Left = Owner.Left + (Owner.Width - Width) / 2;
                    Top = Owner.Top + (Owner.Height - Height) / 2;
                }
            };
        }
    }
}