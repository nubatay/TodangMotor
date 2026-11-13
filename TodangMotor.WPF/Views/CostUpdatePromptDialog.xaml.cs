using System.Collections.Generic;
using System.Windows;
using TodangMotor.Services;
using TodangMotor.WPF.ViewModels;
using Wpf.Ui.Controls;

namespace TodangMotor.WPF.Views
{
    public partial class CostUpdatePromptDialog : FluentWindow
    {
        public CostUpdatePromptDialog(List<CostComparisonItem> changes, List<CostComparisonItem> skipped)
        {
            InitializeComponent();

            var vm = new CostUpdatePromptViewModel(changes, skipped);
            DataContext = vm;

            vm.RequestClose += (result) =>
            {
                DialogResult = result;
                Close();
            };

            this.Loaded += (s, e) =>
            {
                if (Owner != null)
                {
                    Left = Owner.Left + (Owner.Width - Width) / 2;
                    Top = Owner.Top + (Owner.Height - Height) / 2;
                }
            };
        }
    }
}