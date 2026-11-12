using CommunityToolkit.Mvvm.ComponentModel;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using TodangMotor.Services;

namespace TodangMotor.WPF.ViewModels
{
    public partial class SaleDetailViewModel : ObservableObject
    {
        private readonly SalesService _salesService;

        [ObservableProperty] private string _invoiceNo = string.Empty;
        [ObservableProperty] private string _dateDisplay = string.Empty;
        [ObservableProperty] private string _cashierName = string.Empty;
        [ObservableProperty] private string _customerName = string.Empty;
        [ObservableProperty] private string _paymentMethod = string.Empty;
        [ObservableProperty] private string _referenceNo = string.Empty;

        [ObservableProperty] private string _subtotal = "₱ 0.00";
        [ObservableProperty] private string _tendered = "₱ 0.00";
        [ObservableProperty] private string _change = "₱ 0.00";

        public ObservableCollection<SaleLineDisplay> Lines { get; } = new();

        public SaleDetailViewModel(SalesService salesService)
        {
            _salesService = salesService;
        }

        public async Task LoadDataAsync(int saleId)
        {
            var (success, error, detail) = await _salesService.GetSaleDetailAsync(saleId);

            if (success && detail != null)
            {
                InvoiceNo = detail.Sale.InvoiceNo ?? string.Empty;
                DateDisplay = detail.Sale.SaleDate.ToString("MMM d, yyyy h:mm tt");
                CashierName = string.IsNullOrEmpty(detail.CashierName) ? "—" : detail.CashierName;
                CustomerName = string.IsNullOrEmpty(detail.Sale.CustomerName) ? "Walk-in" : detail.Sale.CustomerName;
                PaymentMethod = detail.Sale.PaymentMethod ?? string.Empty;
                ReferenceNo = string.IsNullOrWhiteSpace(detail.Sale.ReferenceNo) ? "—" : detail.Sale.ReferenceNo;

                Subtotal = $"₱ {detail.Sale.Subtotal:N2}";
                Tendered = $"₱ {detail.Sale.AmountTendered:N2}";
                Change = $"₱ {detail.Sale.ChangeAmount:N2}";

                Lines.Clear();
                foreach (var line in detail.Lines)
                {
                    Lines.Add(line);
                }
            }
        }
    }
}