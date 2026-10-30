using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using TodangMotor.WPF.ViewModels;

namespace TodangMotor.WPF.Views
{
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
            DataContext = App.ServiceProvider.GetRequiredService<DashboardViewModel>();
        }
    }
}