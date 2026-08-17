using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SkyWeave.App;

public class BoolToBrushConverter : IValueConverter
{
    public static readonly BoolToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b)
            return new SolidColorBrush(Colors.LimeGreen);
        return new SolidColorBrush(Colors.Red);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
