using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TodangMotor.Services;

namespace TodangMotor.WPF.ViewModels
{
    public partial class CostUpdatePromptViewModel : ObservableObject
    {
        public ObservableCollection<CostComparisonItem> Changes { get; } = new();
        public ObservableCollection<CostComparisonItem> Skipped { get; } = new();

        public bool HasChanges => Changes.Count > 0;
        public bool HasSkipped => Skipped.Count > 0;

        public event Action<bool>? RequestClose;

        public CostUpdatePromptViewModel(List<CostComparisonItem> changes, List<CostComparisonItem> skipped)
        {
            foreach (var c in changes) Changes.Add(c);
            foreach (var s in skipped) Skipped.Add(s);
        }

        [RelayCommand]
        private void UpdateCosts()
        {
            RequestClose?.Invoke(true); // true = DialogResult.Yes (Update)
        }

        [RelayCommand]
        private void KeepCurrent()
        {
            RequestClose?.Invoke(false); // false = DialogResult.No (Keep Current)
        }
    }
}