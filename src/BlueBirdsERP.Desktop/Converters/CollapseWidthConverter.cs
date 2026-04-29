using System.Globalization;
using System.Windows.Data;

namespace BlueBirdsERP.Desktop.Converters;

public class CollapseWidthConverter : IValueConverter
{
    public double ExpandedWidth { get; set; } = 220;
    public double CollapsedWidth { get; set; } = 64;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isCollapsed = value is bool b && b;
        return isCollapsed ? CollapsedWidth : ExpandedWidth;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
