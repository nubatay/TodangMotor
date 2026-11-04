using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using TodangMotor.Data;
using TodangMotor.Services;
using TodangMotor.WPF.ViewModels;
using TodangMotor.WPF.Views;

namespace TodangMotor.WPF
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            ServiceProvider = serviceCollection.BuildServiceProvider();

            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();

            // THIS LINE IS CRUCIAL! If this is missing, the UI doesn't know about the ViewModels.
            mainWindow.DataContext = ServiceProvider.GetRequiredService<MainWindowViewModel>();

            mainWindow.Show();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // 1. Register Repositories & Services
            services.AddSingleton<ProductRepository>();
            services.AddSingleton<ProductService>();
            services.AddSingleton<AuthService>();
            services.AddSingleton<DashboardService>();
            services.AddSingleton<SalesService>();
            services.AddSingleton<CategoryRepository>();
            services.AddSingleton<CategoryService>();
            services.AddSingleton<SupplierRepository>();
            services.AddSingleton<SupplierService>();
            services.AddSingleton<UserService>();
            services.AddSingleton<UserRepository>();
            services.AddSingleton<StockMovementService>();
            services.AddSingleton<BackupRestoreService>();


            // 2. Register ViewModels
            services.AddSingleton<DashboardViewModel>();
            services.AddSingleton<PosViewModel>();
            services.AddSingleton<LoginViewModel>();
            services.AddSingleton<MainWindowViewModel>();
            services.AddSingleton<ItemsViewModel>();
            services.AddSingleton<CategoryDialogViewModel>();;
            services.AddSingleton<CategoryViewModel>();
            services.AddSingleton<SupplierViewModel>();
            services.AddSingleton<UserViewModel>();
            services.AddSingleton<AdjustStockDialogViewModel>();
            services.AddSingleton<InventoryViewModel>();
            services.AddSingleton<SettingsViewModel>();

            // 3. Register Views
            services.AddSingleton<MainWindow>();
            services.AddTransient<DashboardView>();
            services.AddTransient<PosView>();
            services.AddSingleton<LoginView>();
            services.AddTransient<ItemsView>();
            services.AddTransient<CategoryView>();
            services.AddTransient<SupplierView>();
            services.AddTransient<UserView>();
            services.AddTransient<InventoryView>();
            services.AddTransient<SettingsView>();

            // 4. Register Dialogs
            services.AddSingleton<CategoryDialog>();
            services.AddTransient<ProductDialog>();
            services.AddSingleton<SupplierDialog>();
            services.AddSingleton<UserDialog>();

        }
    }
}