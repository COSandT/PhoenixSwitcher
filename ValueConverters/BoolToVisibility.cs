using System.Windows;
using System.Windows.Data;
using System.Globalization;

namespace PhoenixSwitcher.ValueConverter
{
    public class BoolToVisibility : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool result)
            {
                if (parameter != null)
                {
                    string? parameterString = parameter as string;
                    if (parameterString != null
                        && (parameterString.Contains("1") || parameterString.Contains("!")))
                    {
                        result = !result;
                    }
                }
                return result ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Visible;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
