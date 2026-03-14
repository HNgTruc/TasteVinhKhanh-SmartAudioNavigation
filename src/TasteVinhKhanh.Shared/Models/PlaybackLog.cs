namespace TasteVinhKhanh.Shared.Models;

/// <summary>
/// Ghi lại mỗi lần app phát thuyết minh.
/// Dùng cho 2 mục đích:
///   1. Chống phát lại — kiểm tra cooldown trước khi phát
///   2. Analytics — gửi lên server khi có mạng để thống kê
/// </summary>
public class PlaybackLog
{
    public int Id { get; set; }

    public int PoiPointId { get; set; }
    public string LanguageCode { get; set; } = "vi";
    public DateTime PlayedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Vị trí người dùng lúc kích hoạt</summary>
    public double UserLatitude { get; set; }
    public double UserLongitude { get; set; }

    /// <summary>Khoảng cách tới POI lúc phát (mét)</summary>
    public double DistanceMeters { get; set; }

    /// <summary>Cách kích hoạt: "geofence_proximity" | "geofence_enter" | "qr_scan"</summary>
    public string TriggerType { get; set; } = "geofence_proximity";

    /// <summary>ID thiết bị ẩn danh — không lưu thông tin cá nhân</summary>
    public string AnonymousDeviceId { get; set; } = string.Empty;

    /// <summary>Đã gửi lên server chưa</summary>
    public bool IsSynced { get; set; } = false;

    // Navigation
    public PoiPoint? PoiPoint { get; set; }
}
