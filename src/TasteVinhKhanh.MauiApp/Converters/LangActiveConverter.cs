using System.Globalization;

namespace TasteVinhKhanh.MauiApp.Converters;

public class LangActiveConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType,
        object? parameter, CultureInfo culture)
        => value?.ToString()?.ToLower() == parameter?.ToString()?.ToLower();

    public object? ConvertBack(object? value, Type targetType,
        object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}