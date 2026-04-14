using TasteVinhKhanh.MauiApp.Data;

namespace TasteVinhKhanh.MauiApp.Services;

/// <summary>
/// Tính khoảng cách Haversine, phát hiện POI trong tầm,
/// kích hoạt NarrationEngine khi người dùng đến gần.
/// </summary>
public class GeofenceEngine
{
    private readonly AppDatabase _db;
    private readonly NarrationEngine _narration;
    private readonly NotificationService _notif;
    private readonly HashSet<int> _autoPlayedPoiIds = new();

    public event Action<LocalPoi, double>? PoiTriggered;

    /// <summary>
    /// True = geofence đang bị chặn (user đang nghe thủ công từ AudioPage).
    /// Set true khi bắt đầu phát thủ công, false khi phát xong hoặc dừng.
    /// </summary>
    private bool _geofenceBlocked = false;
    public bool GeofenceBlocked { get => _geofenceBlocked; set => _geofenceBlocked = value; }

    public void SetGeofenceBlocked(bool blocked) => _geofenceBlocked = blocked;

    /// <summary>
    /// Reset danh sách đã auto-play (nếu muốn mở lại theo phiên mới).
    /// </summary>
    public void ResetSessionAutoplay() => _autoPlayedPoiIds.Clear();

    public GeofenceEngine(AppDatabase db, NarrationEngine narration, NotificationService notif)
    {
        _db = db;
        _narration = narration;
        _notif = notif;
    }

    public async Task CheckLocationAsync(Location location)
    {
        // Bỏ qua nếu user đang nghe thủ công từ AudioPage/PoiDetail
        if (_geofenceBlocked) return;
        if (_narration.IsPlaying) return;
        if (_narration.IsPaused) return;

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
            .OrderBy(x => x.Distance)
            .ThenByDescending(x => x.Poi.Priority)
            .FirstOrDefault();

        if (inRange == null) return;
        if (_autoPlayedPoiIds.Contains(inRange.Poi.Id)) return;

        // Kiểm tra cooldown 5 phút
        if (await _db.WasRecentlyPlayedAsync(inRange.Poi.Id, TimeSpan.FromMinutes(5)))
            return;

        // Gửi notification để người dùng biết (dù điện thoại đang khóa)
        await _notif.ShowPoiNotificationAsync(inRange.Poi.Name, inRange.Distance);

        // Kích hoạt thuyết minh cho POI vừa vào vùng
        _autoPlayedPoiIds.Add(inRange.Poi.Id);
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
