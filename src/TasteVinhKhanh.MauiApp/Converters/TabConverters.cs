using System.Globalization;
using Microsoft.Maui.Graphics;

namespace TasteVinhKhanh.MauiApp.Converters;

/// <summary>Tab Background: active = cam mờ, inactive = trong suốt.</summary>
public class TabBgConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType,
        object? parameter, CultureInfo culture)
    {
        if (value is bool isActive && isActive)
            return Color.FromArgb("#20FF6B35");
        return Colors.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType,
        object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Tab Text Color: active = cam, inactive = xám.</summary>
public class TabTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType,
        object? parameter, CultureInfo culture)
    {
        if (value is bool isActive && isActive)
            return Color.FromArgb("#FF6B35");
        return Color.FromArgb("#666666");
    }

    public object? ConvertBack(object? value, Type targetType,
        object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
