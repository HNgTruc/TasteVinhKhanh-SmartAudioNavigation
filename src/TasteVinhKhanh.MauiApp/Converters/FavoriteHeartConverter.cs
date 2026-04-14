using System.Globalization;
using Microsoft.Maui.Storage;

namespace TasteVinhKhanh.MauiApp.Converters;

/// <summary>
/// Reads favorite state directly from Preferences and returns the heart emoji.
/// Used on MapPage cards and PoiDetailPage Favorite tab.
/// </summary>
public class FavoriteHeartConverter : IValueConverter
{
    private const string FavKey = "favorite_pois";

    public object? Convert(object? value, Type targetType,
        object? parameter, CultureInfo culture)
    {
        if (value is int poiId)
        {
            var stored = Preferences.Get(FavKey, "");
            if (string.IsNullOrEmpty(stored))
                return "♡";
            try
            {
                var ids = new HashSet<int>(
                    stored.Split(',', StringSplitOptions.RemoveEmptyEntries)
                          .Select(s => int.TryParse(s.Trim(), out var v) ? v : -1)
                          .Where(v => v >= 0));
                return ids.Contains(poiId) ? "❤️" : "♡";
            }
            catch
            {
                return "♡";
            }
        }
        return "♡";
    }

    public object? ConvertBack(object? value, Type targetType,
        object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}