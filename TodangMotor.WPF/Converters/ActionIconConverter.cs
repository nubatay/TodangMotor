using System;
using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace TodangMotor.WPF.Converters
{
    public class ActionIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isActive = (bool)value;
            // If active, show the "Delete/Ban" icon (to deactivate). 
            // If inactive, show the "Checkmark" icon (to reactivate).
            return isActive ? SymbolRegular.Delete24 : SymbolRegular.Checkmark24;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}