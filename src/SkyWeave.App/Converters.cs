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
            var color = FlightCategoryColor(category);
            return new SolidColorBrush(color);
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    public static Color FlightCategoryColor(string category)
    {
        return category.ToUpper() switch
        {
            "VFR" => Color.FromRgb(0x2E, 0xCC, 0x71),
            "MVFR" => Color.FromRgb(0xF3, 0x9C, 0x12),
            "IFR" => Color.FromRgb(0xE6, 0x7E, 0x22),
            "LIFR" => Color.FromRgb(0xFF, 0x4D, 0x4D),
            _ => Color.FromRgb(0x9A, 0xA3, 0xB2)
        };
    }
}

public class FlightCategoryToChipBrushConverter : IValueConverter
{
    public static readonly FlightCategoryToChipBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var color = value is string category
            ? FlightCategoryToBrushConverter.FlightCategoryColor(category)
            : Color.FromRgb(0x9A, 0xA3, 0xB2);
        return new SolidColorBrush(new Color(51, color.R, color.G, color.B));
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
