using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using TodangMotor.Services;
using TodangMotor.WPF.ViewModels;

namespace TodangMotor.WPF.Views
{
    public partial class PosView : UserControl
    {
        public PosView()
        {
            InitializeComponent();
            DataContext = App.ServiceProvider.GetRequiredService<PosViewModel>();

            Loaded += (s, e) => Dispatcher.BeginInvoke(
                new Action(() => SearchInput.Focus()),
                System.Windows.Threading.DispatcherPriority.Input);
        }

        private void ProductCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is CashierProductView product)
            {
                if (DataContext is PosViewModel vm)
                {
                    if (product.QuantityOnHand <= 0) return;
                    vm.AddToCartCommand.Execute(product);
                }
            }
        }

        // Redirects mouse-wheel events away from the quantity TextBox
        // so the cart's ScrollViewer scrolls instead of the TextBox's hidden content.
        private void QuantityBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not DependencyObject d) return;

            // Swallow the wheel event so the TextBox never scrolls its own content
            e.Handled = true;

            // Forward it to the nearest ancestor ScrollViewer (the cart list)
            var sv = FindAncestor<ScrollViewer>(d);
            if (sv == null) return;

            var args = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sv
            };
            sv.RaiseEvent(args);
        }

        private void SearchInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            if (DataContext is not PosViewModel vm) return;

            // Only auto-add when there's exactly one match — prevents accidents.
            if (vm.FilteredProducts.Count != 1)
            {
                e.Handled = true; // swallow Enter so it doesn't bubble anywhere else
                return;
            }

            var product = vm.FilteredProducts[0];

            // Mirror the tile-click guard: never add OOS
            if (product.QuantityOnHand <= 0)
            {
                e.Handled = true;
                return;
            }

            vm.AddToCartCommand.Execute(product);
            vm.SearchText = "";
            SearchInput.Focus(); // ensure cursor stays in search for rapid entry
            e.Handled = true;
        }

        private void PosView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var focused = Keyboard.FocusedElement;

            // Ctrl+F → focus search (from anywhere)
            if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                SearchInput.Focus();
                e.Handled = true;
                return;
            }

            // F2 → focus Customer Name
            if (e.Key == Key.F2 && Keyboard.Modifiers == ModifierKeys.None)
            {
                CustomerNameInput.Focus();
                e.Handled = true;
                return;
            }

            // "/" → focus search, but only when NOT already typing in a text box
            if ((e.Key == Key.OemQuestion || e.Key == Key.Divide)
                && Keyboard.Modifiers == ModifierKeys.None
                && focused is not TextBox)
            {
                SearchInput.Focus();
                e.Handled = true;
                return;
            }

            // Escape in search box → clear text
            if (e.Key == Key.Escape && focused == SearchInput)
            {
                if (DataContext is PosViewModel vm && !string.IsNullOrEmpty(vm.SearchText))
                {
                    vm.SearchText = "";
                    e.Handled = true;
                }
                return;
            }
        }

        private static T? FindAncestor<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null && parent is not T)
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as T;
        }
    }
}