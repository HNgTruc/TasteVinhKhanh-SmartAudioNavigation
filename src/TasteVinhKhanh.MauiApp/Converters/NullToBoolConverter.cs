using System.Globalization;

namespace TasteVinhKhanh.MauiApp.Converters;

public class NullToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType,
        object? parameter, CultureInfo culture)
    {
        var hasValue = value != null;
        if (parameter is string param && param.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
            hasValue = !hasValue;
        return hasValue;
    }

    public object? ConvertBack(object? value, Type targetType,
        object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
