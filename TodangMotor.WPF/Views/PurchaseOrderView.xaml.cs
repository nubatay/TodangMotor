using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using TodangMotor.Services;
using TodangMotor.WPF.ViewModels;

namespace TodangMotor.WPF.Views
{
    public partial class PurchaseOrderView : UserControl
    {
        public PurchaseOrderView()
        {
            InitializeComponent();

            var poService = App.ServiceProvider.GetRequiredService<PurchaseOrderService>();
            var vm = new PurchaseOrderViewModel(poService);
            DataContext = vm;

            // Listen for PDF export request
            vm.RequestGeneratePdf += (detail) =>
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "PDF files (*.pdf)|*.pdf",
                    DefaultExt = ".pdf",
                    AddExtension = true,
                    FileName = $"{detail.Order.PONumber}.pdf"
                };

                if (dialog.ShowDialog() == true)
                {
                    var table = PurchaseOrderDetailDialog.BuildReportTable(detail);
                    ReportService.ExportPdf(table, dialog.FileName);
                    MessageBox.Show($"PO saved and PDF exported successfully to:\n{dialog.FileName}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            };

            // Listen for View Detail request
            vm.RequestOpenPODetailDialog += (poId) =>
            {
                var dialog = new PurchaseOrderDetailDialog(poId);
                dialog.Owner = Window.GetWindow(this);
                dialog.ShowDialog();
            };
        }
    }
}