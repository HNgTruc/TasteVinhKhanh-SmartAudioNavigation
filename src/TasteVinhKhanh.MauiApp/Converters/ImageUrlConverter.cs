using System.Globalization;

namespace TasteVinhKhanh.MauiApp.Converters;

/// <summary>
/// Chuyển "/images/poi_1/xxx.jpg" → full URL
/// và giữ nguyên URL đầy đủ (https://, http://).
/// </summary>
public class ImageUrlConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType,
        object? parameter, CultureInfo culture)
    {
        if (value is not string url || string.IsNullOrWhiteSpace(url))
            return null;

        // Đã là URL tuyệt đối → trả nguyên
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        // Tương đối → nối base URL
        var cleanPath = url.TrimStart('/');
        return $"http://10.0.2.2:5000/{cleanPath}";
    }

    public object? ConvertBack(object? value, Type targetType,
        object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
