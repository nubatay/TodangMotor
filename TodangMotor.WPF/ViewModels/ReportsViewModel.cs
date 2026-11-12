using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TodangMotor.Services;

namespace TodangMotor.WPF.ViewModels
{
    public class DailyReportItem
    {
        public string Date { get; set; } = string.Empty;
        public int Transactions { get; set; }
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
        public decimal? NetIncome { get; set; }
        public bool HasCompleteCost { get; set; }
    }

    public partial class ReportsViewModel : ObservableObject
    {
        private readonly SalesService _salesService;

        // NEW: Store the original result to pass to the PDF generator
        private RevenueReportResult? _currentResult;

        [ObservableProperty] private string _totalRevenueText = "₱ 0.00";
        [ObservableProperty] private string _totalCostText = "₱ 0.00";
        [ObservableProperty] private string _netIncomeText = "₱ 0.00";
        [ObservableProperty] private string _transactionsText = "0";

        [ObservableProperty] private string _selectedDateFilter = "Last 30 Days";
        [ObservableProperty] private bool _isCustomDateVisible = false;
        [ObservableProperty] private DateTime? _fromDate = DateTime.Today.AddDays(-29);
        [ObservableProperty] private DateTime? _toDate = DateTime.Today;
        [ObservableProperty] private bool _isBusy;

        public ObservableCollection<string> DateFilters { get; } = new() { "Today", "Last 7 Days", "Last 30 Days", "This Month", "Custom" };
        public ObservableCollection<DailyReportItem> DailyRows { get; } = new();

        public event Action<RevenueReportResult>? RequestGeneratePdf;

        public ReportsViewModel(SalesService salesService)
        {
            _salesService = salesService;
            SelectedDateFilter = "Last 30 Days";
            ApplyDatePreset("Last 30 Days");
        }

        [RelayCommand]
        private async Task LoadReportAsync()
        {
            if (!FromDate.HasValue || !ToDate.HasValue) return;

            IsBusy = true;
            try
            {
                var from = FromDate.Value.Date;
                var toExclusive = ToDate.Value.Date.AddDays(1);

                var result = await _salesService.GenerateRevenueReportAsync(from, toExclusive, SelectedDateFilter);

                if (!result.Success) return;

                _currentResult = result; // Save the original result

                TotalRevenueText = $"₱ {result.TotalRevenue:N2}";
                TotalCostText = $"₱ {result.TotalCost:N2}";
                NetIncomeText = $"₱ {result.TotalNetIncome:N2}";
                TransactionsText = result.TotalTransactions.ToString("N0");

                DailyRows.Clear();
                foreach (var row in result.DailyRows.OrderByDescending(r => r.SaleDay))
                {
                    DailyRows.Add(new DailyReportItem
                    {
                        Date = row.SaleDay.ToString("MMM d, yyyy"),
                        Transactions = row.TransactionCount,
                        Revenue = row.Revenue,
                        Cost = row.KnownCost,
                        NetIncome = row.NetIncome,
                        HasCompleteCost = row.HasCompleteCost
                    });
                }
            }
            catch (Exception) { }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private void ApplyDatePreset(string preset)
        {
            SelectedDateFilter = preset;
            DateTime today = DateTime.Today;
            IsCustomDateVisible = preset == "Custom";

            switch (preset)
            {
                case "Today": FromDate = today; ToDate = today; break;
                case "Last 7 Days": FromDate = today.AddDays(-6); ToDate = today; break;
                case "Last 30 Days": FromDate = today.AddDays(-29); ToDate = today; break;
                case "This Month": FromDate = new DateTime(today.Year, today.Month, 1); ToDate = today; break;
                case "Custom": return;
            }

            _ = LoadReportAsync();
        }

        [RelayCommand]
        private void GeneratePdf()
        {
            // Check if we have a valid report loaded
            if (_currentResult == null || !_currentResult.Success) return;

            // Pass the original data directly to the view!
            RequestGeneratePdf?.Invoke(_currentResult);
        }
    }
}