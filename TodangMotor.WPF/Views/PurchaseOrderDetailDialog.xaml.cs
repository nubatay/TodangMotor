using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using TodangMotor.Services;
using Wpf.Ui.Controls;

namespace TodangMotor.WPF.Views
{
    public partial class PurchaseOrderDetailDialog : FluentWindow
    {
        private readonly int _poId;
        private readonly PurchaseOrderService _poService;
        private PurchaseOrderDetail? _detail;

        public PurchaseOrderDetailDialog(int poId)
        {
            InitializeComponent();
            _poId = poId;
            _poService = App.ServiceProvider.GetRequiredService<PurchaseOrderService>();

            this.Loaded += async (s, e) => {
                if (Owner != null)
                {
                    Left = Owner.Left + (Owner.Width - Width) / 2;
                    Top = Owner.Top + (Owner.Height - Height) / 2;
                }
                await LoadDetailAsync();
            };
        }

        private async System.Threading.Tasks.Task LoadDetailAsync()
        {
            var (success, error, detail) = await _poService.GetDetailAsync(_poId);
            if (success && detail != null)
            {
                _detail = detail;
                TxtPONumber.Text = detail.Order.PONumber;
                TxtOrderDate.Text = detail.Order.OrderDate.ToString("MMMM d, yyyy");
                TxtSupplier.Text = detail.Order.SupplierName;
                TxtNotes.Text = string.IsNullOrWhiteSpace(detail.Order.Notes) ? "" : $"Notes: {detail.Order.Notes}";
                TxtTotalItems.Text = $"{detail.Items.Count} items   ·   {detail.Items.Sum(i => i.Quantity)} total units";

                LinesGrid.ItemsSource = detail.Items;
            }
            else
            {
                System.Windows.MessageBox.Show(error ?? "Could not load Purchase Order.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                Close();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            if (_detail == null) return;

            var dialog = new SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                DefaultExt = ".pdf",
                AddExtension = true,
                FileName = $"{_detail.Order.PONumber}.pdf"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var table = BuildReportTable(_detail);
                    ReportService.ExportPdf(table, dialog.FileName);
                    System.Windows.MessageBox.Show($"PDF exported successfully to:\n{dialog.FileName}", "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Could not export PDF.\n\n{ex.Message}", "PDF Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        // Helper from your original code
        public static ReportTable BuildReportTable(PurchaseOrderDetail detail)
        {
            var order = detail.Order;
            var table = new ReportTable
            {
                Title = "PURCHASE ORDER",
                Subtitle = $"{order.PONumber}   ·   Order Date: {order.OrderDate:MMMM d, yyyy}   ·   Generated {DateTime.Now:MMM d, yyyy h:mm tt}",
                FooterNote = string.IsNullOrWhiteSpace(order.Notes) ? string.Empty : $"Notes: {order.Notes}",
                SummaryItems = new List<(string Label, string Value)>
                {
                    ("Supplier", order.SupplierName ?? "—"),
                    ("Items",    detail.Items.Count.ToString("N0")),
                    ("Total Qty", detail.Items.Sum(i => i.Quantity).ToString("N0"))
                },
                Headers = new List<string> { "Product", "Brand", "Unit", "Qty" }
            };

            foreach (var item in detail.Items)
            {
                table.Rows.Add(new List<string>
                {
                    item.ProductName ?? string.Empty,
                    item.ProductBrand ?? string.Empty,
                    item.ProductUnit ?? string.Empty,
                    item.Quantity.ToString()
                });
            }

            return table;
        }
    }
}