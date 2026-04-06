using System.Globalization;

namespace TasteVinhKhanh.MauiApp.Converters;

/// <summary>True nếu Images.Count > 0</summary>
public class CountToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType,
        object? parameter, CultureInfo culture)
        => value is int count && count > 0;

    public object? ConvertBack(object? value, Type targetType,
        object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>True nếu count == 0</summary>
public class InverseCountToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType,
        object? parameter, CultureInfo culture)
        => value is int count && count == 0;

    public object? ConvertBack(object? value, Type targetType,
        object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
