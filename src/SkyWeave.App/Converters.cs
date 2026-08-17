using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace SkyWeave.App;

public class ThresholdToBrushConverter : IValueConverter
{
    public static readonly ThresholdToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            var threshold = parameter is string s && double.TryParse(s, out var t) ? t : 0.5;
            if (d >= threshold)
                return new SolidColorBrush(Color.FromRgb(255, 71, 87));
            return new SolidColorBrush(Color.FromRgb(0, 210, 211));
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class FlightCategoryToBrushConverter : IValueConverter
{
    public static readonly FlightCategoryToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string category)
        {
            return category.ToUpper() switch
            {
                "VFR" => new SolidColorBrush(Color.FromRgb(46, 213, 115)),
                "MVFR" => new SolidColorBrush(Color.FromRgb(255, 165, 2)),
                "IFR" => new SolidColorBrush(Color.FromRgb(255, 71, 87)),
                "LIFR" => new SolidColorBrush(Color.FromRgb(255, 71, 87)),
                _ => new SolidColorBrush(Color.FromRgb(170, 170, 170))
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class IndexToProgressConverter : IValueConverter
{
    public static readonly IndexToProgressConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && s.EndsWith("%"))
        {
            if (double.TryParse(s[..^1], out var d))
                return Math.Clamp(d / 100.0, 0.0, 1.0);
        }
        return 0.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class BoolToOpacityConverter : IValueConverter
{
    public static readonly BoolToOpacityConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? 1.0 : 0.3;
        return 0.3;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
