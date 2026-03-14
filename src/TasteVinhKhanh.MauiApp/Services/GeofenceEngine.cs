using TasteVinhKhanh.MauiApp.Data;

namespace TasteVinhKhanh.MauiApp.Services;

/// <summary>
/// Tính khoảng cách Haversine, phát hiện POI trong tầm,
/// kích hoạt NarrationEngine khi người dùng đến gần
/// </summary>
public class GeofenceEngine
{
    private readonly AppDatabase _db;
    private readonly NarrationEngine _narration;

    public event Action<LocalPoi, double>? PoiTriggered;

    public GeofenceEngine(AppDatabase db, NarrationEngine narration)
    {
        _db = db;
        _narration = narration;
    }

    public async Task CheckLocationAsync(Location location)
    {
        var pois = await _db.GetAllPoisAsync();

        // Tìm POI gần nhất trong bán kính, ưu tiên Priority cao
        var inRange = pois
            .Select(p => new
            {
                Poi = p,
                Distance = HaversineMeters(
                    location.Latitude, location.Longitude,
                    p.Latitude, p.Longitude)
            })
            .Where(x => x.Distance <= x.Poi.TriggerRadiusMeters)
            .OrderByDescending(x => x.Poi.Priority)
            .ThenBy(x => x.Distance)
            .FirstOrDefault();

        if (inRange == null) return;

        // Kiểm tra cooldown 5 phút
        if (await _db.WasRecentlyPlayedAsync(inRange.Poi.Id, TimeSpan.FromMinutes(5)))
            return;

        PoiTriggered?.Invoke(inRange.Poi, inRange.Distance);
        await _narration.PlayAsync(inRange.Poi, inRange.Distance, location);
    }

    /// <summary>Công thức Haversine — tính khoảng cách giữa 2 toạ độ (mét)</summary>
    public static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000;
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRad(double deg) => deg * Math.PI / 180;
}