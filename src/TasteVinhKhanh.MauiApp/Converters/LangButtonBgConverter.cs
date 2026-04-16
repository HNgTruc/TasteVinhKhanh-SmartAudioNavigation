using System.Globalization;

namespace TasteVinhKhanh.MauiApp.Converters;

public class LangButtonBgConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var selected = value?.ToString();
        var current = parameter?.ToString();
        return string.Equals(selected, current, StringComparison.OrdinalIgnoreCase)
            ? Color.FromArgb("#FF6B35")
            : Color.FromArgb("#252525");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
