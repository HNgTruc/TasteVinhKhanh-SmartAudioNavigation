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

// ═══════════════════════════════════════════════════════════════════════════════
// TOUR DTOs
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Một dòng trong bảng danh sách tour</summary>
public class TourListItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int PoiCount { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Chi tiết một tour kèm danh sách POI theo thứ tự lộ trình</summary>
public class TourDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<TourPoiDto> Pois { get; set; } = new();
}

/// <summary>Một POI trong danh sách tour</summary>
public class TourPoiDto
{
    public int PoiId { get; set; }
    public string PoiName { get; set; } = string.Empty;
    public bool PoiIsActive { get; set; }
    public int StopOrder { get; set; }
}

/// <summary>Phân trang danh sách tour</summary>
public class TourPagedDto
{
    public List<TourListItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

/// <summary>Tạo tour mới</summary>
public class CreateTourRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Danh sách PoiId theo thứ tự muốn sắp xếp trong tour</summary>
    public List<int> PoiIds { get; set; } = new();
}

/// <summary>Cập nhật tour (thông tin + toàn bộ POI)</summary>
public class UpdateTourRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Thay thế toàn bộ danh sách POI trong tour</summary>
    public List<int> PoiIds { get; set; } = new();
}

/// <summary>Chỉ cập nhật thứ tự POI trong tour</summary>
public class ReorderTourRequest
{
    /// <summary>Danh sách PoiId theo thứ tự mới</summary>
    public List<int> PoiIds { get; set; } = new();
}
