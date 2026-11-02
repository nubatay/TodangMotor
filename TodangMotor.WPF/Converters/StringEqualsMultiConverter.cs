using System;
using System.Globalization;
using System.Windows.Data;

namespace TodangMotor.WPF.Converters
{
    public class StringEqualsMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2) return false;

            string val1 = values[0]?.ToString();
            string val2 = values[1]?.ToString();

            // Returns true if both strings match (e.g., "Last 30 Days" == "Last 30 Days")
            return val1 == val2;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}