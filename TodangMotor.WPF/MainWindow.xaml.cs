using System;
using System.Windows;
using Wpf.Ui.Controls;

namespace TodangMotor.WPF
{
    public partial class MainWindow : FluentWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            StateChanged += MainWindow_StateChanged;
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (MaximizeIcon != null)
            {
                MaximizeIcon.Symbol = WindowState == WindowState.Maximized
                    ? SymbolRegular.SquareMultiple24
                    : SymbolRegular.Square24;
            }
        }
    }
}