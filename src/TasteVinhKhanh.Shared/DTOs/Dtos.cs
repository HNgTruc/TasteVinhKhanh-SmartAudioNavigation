namespace TasteVinhKhanh.Shared.DTOs;

// ═══════════════════════════════════════════════════════════════════════════════
// POI DTOs
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Dữ liệu POI trả về từ API — dùng cho cả Admin và MauiApp</summary>
public class PoiDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double TriggerRadiusMeters { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public string? ImageUrl { get; set; }
    public string? MapUrl { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Scripts của tất cả ngôn ngữ đi kèm</summary>
    public List<AudioScriptDto> AudioScripts { get; set; } = new();
}

/// <summary>Tạo POI mới</summary>
public class CreatePoiRequest
{
    public string Name { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double TriggerRadiusMeters { get; set; } = 50;
    public int Priority { get; set; } = 1;
    public string? ImageUrl { get; set; }
    public string? MapUrl { get; set; }
}

/// <summary>Cập nhật POI — kế thừa Create, thêm IsActive</summary>
public class UpdatePoiRequest : CreatePoiRequest
{
    public bool IsActive { get; set; } = true;
}

// ═══════════════════════════════════════════════════════════════════════════════
// AUDIO SCRIPT DTOs
// ═══════════════════════════════════════════════════════════════════════════════

public class AudioScriptDto
{
    public int Id { get; set; }
    public int PoiPointId { get; set; }
    public string LanguageCode { get; set; } = "vi";
    public string TtsScript { get; set; } = string.Empty;
    public string? AudioFileUrl { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Tạo hoặc cập nhật script — dùng chung vì logic là upsert</summary>
public class UpsertAudioScriptRequest
{
    public string LanguageCode { get; set; } = "vi";
    public string TtsScript { get; set; } = string.Empty;
    public string? AudioFileUrl { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════════
// SYNC DTOs — dùng cho MauiApp tải dữ liệu về SQLite
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Server trả về khi MauiApp gọi GET /api/sync.
/// Chỉ chứa những POI thay đổi sau lastSyncAt để tiết kiệm băng thông.
/// </summary>
public class SyncResponse
{
    public List<PoiDto> Pois { get; set; } = new();
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;

    /// <summary>False = không có gì mới, app không cần update SQLite</summary>
    public bool HasChanges { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════════
// ANALYTICS DTOs — MauiApp gửi log lên server
// ═══════════════════════════════════════════════════════════════════════════════

public class PlaybackLogRequest
{
    public int PoiPointId { get; set; }
    public string LanguageCode { get; set; } = "vi";
    public DateTime PlayedAt { get; set; }
    public double UserLatitude { get; set; }
    public double UserLongitude { get; set; }
    public double DistanceMeters { get; set; }
    public string TriggerType { get; set; } = "geofence_proximity";
    public string AnonymousDeviceId { get; set; } = string.Empty;
}

/// <summary>Gửi nhiều log một lúc để tiết kiệm request</summary>
public class BatchPlaybackLogRequest
{
    public List<PlaybackLogRequest> Logs { get; set; } = new();
}

// ═══════════════════════════════════════════════════════════════════════════════
// AUTH DTOs
// ═══════════════════════════════════════════════════════════════════════════════

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
