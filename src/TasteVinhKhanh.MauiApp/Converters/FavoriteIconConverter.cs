using System.Globalization;

namespace TasteVinhKhanh.MauiApp.Converters;

public class FavoriteIconConverter : IValueConverter
{
    public static Func<int, bool>? IsFavoriteFunc { get; set; }

    public object? Convert(object? value, Type targetType,
        object? parameter, CultureInfo culture)
    {
        if (value is int poiId && IsFavoriteFunc != null)
            return IsFavoriteFunc(poiId) ? "❤️" : "♡";
        return "♡";
    }

    public object? ConvertBack(object? value, Type targetType,
        object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}