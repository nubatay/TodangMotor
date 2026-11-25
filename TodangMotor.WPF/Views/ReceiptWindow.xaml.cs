using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace TodangMotor.WPF.Views
{
    public partial class ReceiptWindow : Wpf.Ui.Controls.FluentWindow
    {
        public ReceiptWindow(ReceiptData data)
        {
            InitializeComponent();
            DataContext = data;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                // Print ONLY the receipt paper, omitting the buttons
                printDialog.PrintVisual(ReceiptPrintArea, "Todang Motor Receipt");
            }
        }
    }

    // --- Data Models mapped directly to the SaleDetailDialog XAML ---
    public class ReceiptData
    {
        public string InvoiceNo { get; set; } = string.Empty;
        public string DateDisplay => DateTime.Now.ToString("MMM dd, yyyy hh:mm tt");
        public string CashierName { get; set; } = "Cashier";
        public string CustomerName { get; set; } = "Walk-in";
        public string PaymentMethod { get; set; } = "Cash";

        public string GcashReference { get; set; } = string.Empty;
        public bool IsCash => PaymentMethod == "Cash";
        public bool IsGcash => PaymentMethod == "GCash";

        public decimal Subtotal { get; set; }
        public decimal Tendered { get; set; }
        public decimal Change { get; set; }

        // Notice this is "Lines" to match the SaleDetailDialog XAML
        public List<ReceiptLineData> Lines { get; set; } = new();
    }

    public class ReceiptLineData
    {
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }
}