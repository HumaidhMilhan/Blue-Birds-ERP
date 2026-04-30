using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BlueBirdsERP.Desktop.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool boolValue = value switch
        {
            bool b => b,
            int i => i > 0,
            long l => l > 0,
            double d => d > 0,
            decimal m => m > 0,
            string s => !string.IsNullOrWhiteSpace(s),
            null => false,
            _ => true
        };

        if (parameter is string p && p == "Invert")
            boolValue = !boolValue;

        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isVisible = value is Visibility v && v == Visibility.Visible;

        if (parameter is string s && s == "Invert")
            isVisible = !isVisible;

        return isVisible;
    }
}
