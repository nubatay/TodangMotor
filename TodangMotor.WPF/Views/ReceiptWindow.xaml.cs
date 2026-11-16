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
                // Print the Receipt Area Border, not the whole window (omits the buttons)
                printDialog.PrintVisual(ReceiptPrintArea, "Todang Motor Receipt");
            }
        }
    }

    // --- Data Models for the Receipt ---
    public class ReceiptData
    {
        public string InvoiceNo { get; set; } = string.Empty;
        public string DateFormatted => DateTime.Now.ToString("MMM dd, yyyy hh:mm tt");
        public string CashierName { get; set; } = "Admin";
        public string CustomerName { get; set; } = "Walk-in";
        public string PaymentMethod { get; set; } = string.Empty;
        public string GcashReference { get; set; } = string.Empty;

        public decimal Subtotal { get; set; }
        public string SubtotalFormatted => $"₱ {Subtotal:N2}";

        public decimal Tendered { get; set; }
        public string TenderedFormatted => $"₱ {Tendered:N2}";

        public decimal Change { get; set; }
        public string ChangeFormatted => $"₱ {Change:N2}";

        public bool IsCash => PaymentMethod == "Cash";

        public List<ReceiptLineData> Items { get; set; } = new();
    }

    public class ReceiptLineData
    {
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal LineTotal { get; set; }
        public string LineTotalFormatted => $"₱ {LineTotal:N2}";
    }
}