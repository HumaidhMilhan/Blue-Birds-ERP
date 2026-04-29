using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using BlueBirdsERP.Domain.Enums;

namespace BlueBirdsERP.Desktop.Converters;

public class StatusToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush GreenBrush = new(Color.FromRgb(0x38, 0x8E, 0x3C));
    private static readonly SolidColorBrush AmberBrush = new(Color.FromRgb(0xF5, 0x7F, 0x17));
    private static readonly SolidColorBrush RedBrush = new(Color.FromRgb(0xD3, 0x2F, 0x2F));
    private static readonly SolidColorBrush GrayBrush = new(Color.FromRgb(0x75, 0x75, 0x75));
    private static readonly SolidColorBrush BlueBrush = new(Color.FromRgb(0x19, 0x76, 0xD2));

    static StatusToColorConverter()
    {
        GreenBrush.Freeze();
        AmberBrush.Freeze();
        RedBrush.Freeze();
        GrayBrush.Freeze();
        BlueBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            PaymentStatus ps => ps switch
            {
                PaymentStatus.Paid => GreenBrush,
                PaymentStatus.Partial => AmberBrush,
                PaymentStatus.Pending => GrayBrush,
                PaymentStatus.Void => RedBrush,
                _ => GrayBrush
            },
            BatchStatus bs => bs switch
            {
                BatchStatus.Active => GreenBrush,
                BatchStatus.Exhausted => GrayBrush,
                BatchStatus.Expired => RedBrush,
                BatchStatus.Recalled => AmberBrush,
                _ => GrayBrush
            },
            BusinessAccountStatus bas => bas switch
            {
                BusinessAccountStatus.Active => GreenBrush,
                BusinessAccountStatus.Suspended => RedBrush,
                _ => GrayBrush
            },
            _ => GrayBrush
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
