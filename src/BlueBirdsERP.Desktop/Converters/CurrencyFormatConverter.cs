using System.Globalization;
using System.Windows.Data;

namespace BlueBirdsERP.Desktop.Converters;

public class CurrencyFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            decimal d => $"Rs. {d:N2}",
            int i => $"Rs. {i:N2}",
            double d => $"Rs. {d:N2}",
            _ => "Rs. 0.00"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s)
        {
            s = s.Replace("Rs.", "").Replace(",", "").Trim();
            if (decimal.TryParse(s, out decimal result))
                return result;
        }
        return 0m;
    }
}
