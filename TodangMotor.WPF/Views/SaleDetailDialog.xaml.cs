using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using TodangMotor.Services;
using TodangMotor.WPF.ViewModels;
using Wpf.Ui.Controls;

namespace TodangMotor.WPF.Views
{
    public partial class SaleDetailDialog : FluentWindow
    {
        private readonly SaleDetailViewModel _viewModel;

        public SaleDetailDialog(int saleId)
        {
            InitializeComponent();

            var salesService = App.ServiceProvider.GetRequiredService<SalesService>();
            _viewModel = new SaleDetailViewModel(salesService);
            DataContext = _viewModel;

            this.Loaded += async (s, e) => {
                if (Owner != null)
                {
                    Left = Owner.Left + (Owner.Width - Width) / 2;
                    Top = Owner.Top + (Owner.Height - Height) / 2;
                }
                await _viewModel.LoadDataAsync(saleId);
            };
        }

        // ADD THIS METHOD
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                Print_Click(null, null);
                e.Handled = true;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            PrintDialog printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                // ... (Keep your existing FlowDocument printing code) ...
                FlowDocument doc = new FlowDocument
                {
                    PagePadding = new Thickness(50),
                    ColumnWidth = double.PositiveInfinity,
                    FontFamily = new FontFamily("Segoe UI")
                };

                doc.Blocks.Add(new Paragraph(new Run("TODANG MOTOR")) { FontSize = 24, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center });
                doc.Blocks.Add(new Paragraph(new Run("Parts & Accessories")) { FontSize = 14, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 20) });
                doc.Blocks.Add(new Paragraph(new Run($"Invoice: {_viewModel.InvoiceNo}\nDate: {_viewModel.DateDisplay}\nCashier: {_viewModel.CashierName}\nCustomer: {_viewModel.CustomerName}")) { FontSize = 12 });

                Table table = new Table { CellSpacing = 0, Margin = new Thickness(0, 20, 0, 20) };
                table.Columns.Add(new TableColumn { Width = new GridLength(3, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(1.5, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(1.5, GridUnitType.Star) });

                TableRowGroup headerGroup = new TableRowGroup();
                TableRow headerRow = new TableRow { FontWeight = FontWeights.Bold, Background = Brushes.LightGray };
                headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Item"))));
                headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Qty"))));
                headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Price"))));
                headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Total"))));
                headerGroup.Rows.Add(headerRow);
                table.RowGroups.Add(headerGroup);

                TableRowGroup bodyGroup = new TableRowGroup();
                foreach (var line in _viewModel.Lines)
                {
                    TableRow row = new TableRow();
                    row.Cells.Add(new TableCell(new Paragraph(new Run(line.ProductName))));
                    row.Cells.Add(new TableCell(new Paragraph(new Run(line.Quantity.ToString()))));
                    row.Cells.Add(new TableCell(new Paragraph(new Run(line.UnitPrice.ToString("N2")))));
                    row.Cells.Add(new TableCell(new Paragraph(new Run(line.LineTotal.ToString("N2")))));
                    bodyGroup.Rows.Add(row);
                }
                table.RowGroups.Add(bodyGroup);
                doc.Blocks.Add(table);

                doc.Blocks.Add(new Paragraph(new Run($"Subtotal: {_viewModel.Subtotal}\n{_viewModel.PaymentMethod} Tendered: {_viewModel.Tendered}\nChange: {_viewModel.Change}")) { FontSize = 14, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 20, 0, 0) });
                doc.Blocks.Add(new Paragraph(new Run("Thank you for your purchase!")) { FontSize = 12, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 30, 0, 0) });

                IDocumentPaginatorSource idpSource = doc;
                printDialog.PrintDocument(idpSource.DocumentPaginator, $"Receipt - {_viewModel.InvoiceNo}");
            }
        }
    }
}