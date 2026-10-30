using System.Windows;
using System.Windows.Forms;
using TodangMotor.Models;
using TodangMotor.Services;
using TodangMotor.WPF.ViewModels;
using Wpf.Ui.Controls;

namespace TodangMotor.WPF.Views
{
    public partial class SupplierDialog : FluentWindow
    {
        public SupplierDialog(Supplier? supplierToEdit = null)
        {
            InitializeComponent();

            var service = new SupplierService();
            var vm = new SupplierDialogViewModel(service, supplierToEdit);

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