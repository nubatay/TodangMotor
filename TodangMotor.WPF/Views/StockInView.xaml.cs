using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using TodangMotor.Services;
using TodangMotor.WPF.ViewModels;

namespace TodangMotor.WPF.Views
{
    public partial class StockInView : UserControl
    {
        private readonly ProductService _productService;

        public StockInView()
        {
            InitializeComponent();

            // Resolve required services
            var stockInService = App.ServiceProvider.GetRequiredService<StockInService>();
            var productService = App.ServiceProvider.GetRequiredService<ProductService>();
            var supplierService = App.ServiceProvider.GetRequiredService<SupplierService>();
            _productService = productService; // Keep a reference for the cost update

            var vm = new StockInViewModel(stockInService, productService, supplierService);
            DataContext = vm;

            // Listen for the prompt to open the dialog
            vm.RequestCostUpdatePrompt += async (changes, skipped) =>
            {
                var dialog = new CostUpdatePromptDialog(changes, skipped);
                dialog.Owner = Window.GetWindow(this);

                if (dialog.ShowDialog() == true)
                {
                    // User clicked "Update Costs"
                    try
                    {
                        var updates = changes.Select(c => (c.ProductId, c.NewCost)).ToList();
                        var result = await _productService.UpdateCostPricesAsync(updates);

                        if (!result.Success)
                        {
                            MessageBox.Show(
                                "Stock-In was saved, but some product cost prices could not be updated:\n\n" + result.ErrorMessage,
                                "Cost Update Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error updating costs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            };
        }
    }
}