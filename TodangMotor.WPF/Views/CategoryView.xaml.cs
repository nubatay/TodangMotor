using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using TodangMotor.Data;
using TodangMotor.Services;
using TodangMotor.WPF.ViewModels;
using System.Windows;

namespace TodangMotor.WPF.Views
{
    public partial class CategoryView : UserControl
    {
        public CategoryView()
        {
            InitializeComponent();

            // Get services from DI container
            var categoryService = App.ServiceProvider.GetRequiredService<CategoryService>();

            var vm = new CategoryViewModel(categoryService);
            DataContext = vm;

            // Listen for the event to open the dialog
            vm.RequestOpenCategoryDialog += (category) =>
            {
                var dialog = new CategoryDialog(category);
                dialog.Owner = Window.GetWindow(this);

                if (dialog.ShowDialog() == true)
                {
                    vm.LoadDataCommand.Execute(null);
                }
            };
        }
    }
}