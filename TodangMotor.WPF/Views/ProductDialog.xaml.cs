using System.Windows;
using System.Windows.Input;
using TodangMotor.Data;
using TodangMotor.Models;
using TodangMotor.Services;
using TodangMotor.WPF.ViewModels;
using Wpf.Ui.Controls;

namespace TodangMotor.WPF.Views
{
    public partial class ProductDialog : FluentWindow
    {
        public ProductDialog(Product? productToEdit = null)
        {
            InitializeComponent();

            var productService = new ProductService();
            var categoryService = new CategoryService(new CategoryRepository());
            var supplierService = new SupplierService();

            var vm = new ProductDialogViewModel(productService, categoryService, supplierService, productToEdit);
            DataContext = vm;

            vm.RequestClose += (success) =>
            {
                DialogResult = success;
                Close();
            };

            // ADD THIS LINE
            this.Loaded += ProductDialog_Loaded;
        }

        // ADD THIS METHOD
        private void ProductDialog_Loaded(object sender, RoutedEventArgs e)
        {
            if (Owner != null)
            {
                Left = Owner.Left + (Owner.Width - Width) / 2;
                Top = Owner.Top + (Owner.Height - Height) / 2;
            }
        }

        private void Description_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                var textBox = sender as System.Windows.Controls.TextBox;
                if (textBox != null)
                {
                    int caretIndex = textBox.CaretIndex;
                    textBox.Text = textBox.Text.Insert(caretIndex, Environment.NewLine);
                    textBox.CaretIndex = caretIndex + Environment.NewLine.Length;
                }
            }
        }
    }
}