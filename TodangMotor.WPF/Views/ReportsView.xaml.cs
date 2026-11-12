using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using TodangMotor.Services;
using TodangMotor.WPF.ViewModels;

namespace TodangMotor.WPF.Views
{
    public partial class ReportsView : UserControl
    {
        public ReportsView()
        {
            InitializeComponent();

            var salesService = App.ServiceProvider.GetRequiredService<SalesService>();
            var vm = new ReportsViewModel(salesService);
            DataContext = vm;

            vm.RequestGeneratePdf += (result) =>
            {
                var saveDialog = new SaveFileDialog
                {
                    Title = "Save Sales Report",
                    Filter = "PDF files (*.pdf)|*.pdf",
                    DefaultExt = ".pdf",
                    AddExtension = true,
                    FileName = $"SalesReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf",
                    InitialDirectory = GetDefaultReportFolder()
                };

                if (saveDialog.ShowDialog() == true)
                {
                    try
                    {
                        var table = BuildReportTable(result);
                        ReportService.ExportPdf(table, saveDialog.FileName);

                        var openFolder = MessageBox.Show(
                            $"PDF saved successfully to:\n{saveDialog.FileName}\n\nOpen the folder?",
                            "Report Saved", MessageBoxButton.YesNo, MessageBoxImage.Information);

                        if (openFolder == MessageBoxResult.Yes)
                        {
                            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{saveDialog.FileName}\"");
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Could not generate PDF.\n\n{ex.Message}", "PDF Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            };
        }

        private static ReportTable BuildReportTable(RevenueReportResult result)
        {
            var table = new ReportTable
            {
                Title = "Sales Revenue Report",
                Subtitle = $"{result.PeriodLabel}   ·   Generated {DateTime.Now:MMM d, yyyy h:mm tt}",
                FooterNote = result.IsCostComplete
                    ? "All rows have complete cost data. Net Income is exact."
                    : $"{result.DaysWithMissingCost} day(s) have incomplete cost data.",
                SummaryItems = new List<(string Label, string Value)>
                {
                    ("Total Revenue", "₱ " + result.TotalRevenue.ToString("N2")),
                    ("Total Cost",    "₱ " + result.TotalCost.ToString("N2")),
                    ("Net Income",    "₱ " + result.TotalNetIncome.ToString("N2")),
                    ("Transactions",  result.TotalTransactions.ToString("N0"))
                },
                Headers = new List<string> { "Date", "Transactions", "Revenue (PHP)", "Cost (PHP)", "Net Income (PHP)" }
            };

            foreach (var row in result.DailyRows.OrderByDescending(r => r.SaleDay))
            {
                string netIncome = row.HasCompleteCost ? row.NetIncome!.Value.ToString("N2") : "—";
                table.Rows.Add(new List<string>
                {
                    row.SaleDay.ToString("MMM d, yyyy"),
                    row.TransactionCount.ToString(),
                    row.Revenue.ToString("N2"),
                    row.HasCompleteCost ? row.KnownCost.ToString("N2") : "—",
                    netIncome
                });
            }

            return table;
        }

        private static string GetDefaultReportFolder()
        {
            try
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string folder = Path.Combine(docs, "TodangMotor", "Reports");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                return folder;
            }
            catch { return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments); }
        }
    }
}