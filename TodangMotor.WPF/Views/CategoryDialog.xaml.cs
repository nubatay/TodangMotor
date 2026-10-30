using System.Windows;
using TodangMotor.Data;
using TodangMotor.Models;
using TodangMotor.Services;
using TodangMotor.WPF.ViewModels;
using Wpf.Ui.Controls;

namespace TodangMotor.WPF.Views
{
    public partial class CategoryDialog : FluentWindow
    {
        public CategoryDialog(Category? categoryToEdit = null)
        {
            InitializeComponent();

            // Create the service and viewmodel
            var service = new CategoryService(new CategoryRepository());
            var vm = new CategoryDialogViewModel(service, categoryToEdit);

            DataContext = vm;

            // Listen for the close request from the ViewModel
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