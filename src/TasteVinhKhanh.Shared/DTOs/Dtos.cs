using System.Text.Json.Serialization;

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
    public string? IconUrl { get; set; }
    public string? MapUrl { get; set; }
    /// <summary>Logo quán — được admin duyệt mới hiển thị</summary>
    public string? LogoUrl { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Scripts của tất cả ngôn ngữ đi kèm</summary>
    public List<AudioScriptDto> AudioScripts { get; set; } = new();

    /// <summary>Ảnh quán</summary>
    public List<RestaurantImageDto> Images { get; set; } = new();
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
    public string? IconUrl { get; set; }
    public string? MapUrl { get; set; }
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Cập nhật POI — kế thừa Create</summary>
public class UpdatePoiRequest : CreatePoiRequest
{
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
    /// <summary>Chỉ dùng internal — app không dùng trực tiếp</summary>
    public string? AudioFilePath { get; set; }
    public bool IsAudioUploaded { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Tạo hoặc cập nhật script — dùng chung vì logic là upsert</summary>
public class UpsertAudioScriptRequest
{
    public string LanguageCode { get; set; } = "vi";
    public string TtsScript { get; set; } = string.Empty;
    /// <summary>Chỉ dùng internal</summary>
    public string? AudioFilePath { get; set; }
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
    public string Role { get; set; } = string.Empty;
    public int? VendorId { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════════
// DEVICE AUTH DTOs — MAUI app
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Device đăng ký để lấy token — không cần password</summary>
public class DeviceRegisterRequest
{
    public string DeviceId { get; set; } = string.Empty;
}

/// <summary>Device nhận về JWT token để tải audio</summary>
public class DeviceTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
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

// ═══════════════════════════════════════════════════════════════════════════════
// RESTAURANT IMAGE DTOs
// ═══════════════════════════════════════════════════════════════════════════════

public class RestaurantImageDto
{
    public int Id { get; set; }
    public int PoiPointId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int SortOrder { get; set; }
}

public class UpsertImageRequest
{
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int SortOrder { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════════
// VENDOR AUTH DTOs
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Đăng ký tài khoản vendor (public)</summary>
public class VendorRegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Address { get; set; }
}

/// <summary>Login vendor — trả JWT với Role=Vendor</summary>
public class VendorLoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>Vendor quên mật khẩu: xác minh email + số điện thoại để đặt lại mật khẩu</summary>
public class VendorForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>Kết quả login cho cả Admin lẫn Vendor</summary>
public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    /// <summary>Chỉ có khi Role = Vendor</summary>
    public int? VendorId { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════════
// VENDOR PROFILE DTOs
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Thông tin vendor trả về cho vendor xem</summary>
public class VendorProfileDto
{
    public int Id { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? RejectedReason { get; set; }
    public DateTime CreatedAt { get; set; }
    /// <summary>POI được gán (null nếu chưa gán)</summary>
    public VendorPoiDto? Poi { get; set; }
}

/// <summary>POI thu gọn trong vendor profile</summary>
public class VendorPoiDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public string? ImageUrl { get; set; }
}

/// <summary>Cập nhật thông tin cá nhân vendor</summary>
public class UpdateVendorProfileRequest
{
    public string OwnerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Address { get; set; }
}

/// <summary>Vendor đổi mật khẩu khi đã đăng nhập</summary>
public class VendorChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

// ═══════════════════════════════════════════════════════════════════════════════
// VENDOR POI UPDATE DTOs
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Vendor gửi yêu cầu cập nhật POI</summary>
public class SubmitPOIUpdateRequest
{
    /// <summary>Tên quán mới</summary>
    public string? Name { get; set; }
    /// <summary>Mô tả mới</summary>
    public string? ShortDescription { get; set; }
    /// <summary>Icon mới (URL sau khi upload)</summary>
    public string? IconUrl { get; set; }
    /// <summary>Bán kính mới</summary>
    public double? TriggerRadiusMeters { get; set; }
    /// <summary>Priority mới</summary>
    public int? Priority { get; set; }
    /// <summary>Danh sách ảnh mới</summary>
    public List<ImagePayloadDto>? Images { get; set; }
    /// <summary>Danh sách script thuyết minh mới</summary>
    public List<ScriptPayloadDto>? Scripts { get; set; }
}

public class ImagePayloadDto
{
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int SortOrder { get; set; }
}

public class ScriptPayloadDto
{
    public string LanguageCode { get; set; } = string.Empty;
    public string TtsScript { get; set; } = string.Empty;
    public string? AudioFileUrl { get; set; }
}

/// <summary>Lịch sử submit của vendor</summary>
public class VendorUpdateHistoryDto
{
    public int Id { get; set; }
    public string PoiName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? AdminNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════════
// ADMIN VENDOR MANAGEMENT DTOs
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Vendor chờ duyệt</summary>
public class PendingVendorDto
{
    public int VendorId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Address { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Danh sách vendor trả về cho trang Admin Vendors</summary>
public class VendorListDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? BusinessName { get; set; }
    public bool IsActive { get; set; }
    /// <summary>Trạng thái vendor: Pending | Approved | Rejected</summary>
    public string Status { get; set; } = string.Empty;
    public int PoiCount { get; set; }
    public int TotalPlays { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Response wrapper cho danh sách vendor</summary>
public class VendorListResponseDto
{
    public List<VendorListDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int TotalPoiCount { get; set; }
}

/// <summary>Duyệt vendor + gán POI</summary>
public class ApproveVendorRequest
{
    /// <summary>PoiId sẽ gán cho vendor này</summary>
    public int PoiPointId { get; set; }
}

/// <summary>Từ chối vendor</summary>
public class RejectVendorRequest
{
    public string Reason { get; set; } = string.Empty;
}

/// <summary>POI update chờ duyệt</summary>
public class PendingUpdateDto
{
    public int Id { get; set; }
    public int VendorId { get; set; }
    /// <summary>Tên vendor gửi</summary>
    public string VendorName { get; set; } = string.Empty;
    /// <summary>Tên vendor gửi (alias)</summary>
    public string BusinessName { get; set; } = string.Empty;
    public int PoiId { get; set; }
    public string PoiName { get; set; } = string.Empty;
    /// <summary>Mô tả ngắn POI</summary>
    public string? PoiShortDesc { get; set; }
    /// <summary>Người gửi</summary>
    public string? SubmittedBy { get; set; }
    /// <summary>Loại thay đổi: poi_created | poi_updated | script_added | script_updated | image_uploaded</summary>
    public string? ChangeType { get; set; }
    public string Status { get; set; } = "Pending";
    /// <summary>Thời gian gửi</summary>
    public DateTime SubmittedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    /// <summary>Mô tả ngắn gọn thay đổi</summary>
    public string? Summary { get; set; }
    /// <summary>Chi tiết thay đổi (deserialize từ Payload)</summary>
    public Dictionary<string, ChangeValueDto>? Changes { get; set; }
}

/// <summary>Chi tiết POI update</summary>
public class UpdateDetailDto
{
    public int Id { get; set; }
    public int VendorId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string PoiName { get; set; } = string.Empty;
    public string VendorEmail { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    /// <summary>JSON string để admin xem trước</summary>
    public string? Payload { get; set; }
    public string? ImagesPayload { get; set; }
    public string? ScriptsPayload { get; set; }
    /// <summary>Trạng thái hiện tại của update</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>Đường dẫn thay đổi (trước → sau) — deserialize từ Payload</summary>
    public Dictionary<string, ChangeValueDto>? Changes { get; set; }
}

/// <summary>Một trường thay đổi</summary>
public class ChangeValueDto
{
    public string? Before { get; set; }
    public string? After { get; set; }
}

/// <summary>Admin duyệt POI update</summary>
public class ApproveUpdateRequest
{
    /// <summary>Note khi duyệt (frontend gửi "reason")</summary>
    [JsonPropertyName("reason")]
    public string? AdminNote { get; set; }
}

/// <summary>Admin từ chối POI update</summary>
public class RejectUpdateRequest
{
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>Ảnh staging chờ duyệt</summary>
public class StagingImageDto
{
    public int Id { get; set; }
    public int VendorId { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public int PoiPointId { get; set; }
    public string PoiName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    /// <summary>Upload | Deletion</summary>
    public string StagingType { get; set; } = "Upload";
    /// <summary>URL preview: TempUrl (upload) hoặc ReferencedImageUrl (deletion)</summary>
    public string PreviewUrl { get; set; } = string.Empty;
    /// <summary>URL ảnh gốc trong RestaurantImages (dùng cho Deletion)</summary>
    public string? ReferencedImageUrl { get; set; }
    public string TempUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>Duyệt 1 staging image</summary>
public class ApproveStagingImageRequest
{
    [JsonPropertyName("poiPointId")]
    public int PoiPointId { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════════
// POI IMAGE MANAGEMENT DTOs (Admin)
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Toàn bộ ảnh của một POI (admin xem gallery)</summary>
public class PoiImageGalleryDto
{
    public int PoiId { get; set; }
    public string PoiName { get; set; } = string.Empty;
    public List<RestaurantImageDto> Images { get; set; } = new();
}

/// <summary>Vendor gửi yêu cầu xóa ảnh</summary>
public class DeleteImageRequestDto
{
    /// <summary>Id của ảnh trong RestaurantImages cần xóa</summary>
    public int ImageId { get; set; }
    /// <summary>POI mà ảnh này thuộc về</summary>
    public int PoiPointId { get; set; }
}

/// <summary>Duyệt yêu cầu xóa ảnh</summary>
public class ApproveDeletionRequestDto
{
    public string? AdminNote { get; set; }
}

/// <summary>Badge counts cho dashboard</summary>
public class AdminBadgeDto
{
    public int PendingVendors { get; set; }
    public int PendingUpdates { get; set; }
}

/// <summary>Danh sách POI chưa có vendor</summary>
public class UnassignedPoiDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>Stats cho trang pending-updates</summary>
public class PendingUpdatesStatsDto
{
    public int Pending { get; set; }
    public int ApprovedToday { get; set; }
    public int RejectedToday { get; set; }
    public int UniquePoiCount { get; set; }
}

/// <summary>Duyệt logo</summary>
public class ApproveLogoRequest
{
    [JsonPropertyName("poiPointId")]
    public int PoiPointId { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════════
// ANALYTICS — HEATMAP DTOs
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Một điểm trên heatmap — tọa độ + số lượt phát</summary>
public class HeatmapPointDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Weight { get; set; }
}

/// <summary>Mảng HeatmapPointDto — dùng cho Leaflet heat layer</summary>
public class HeatmapDataDto
{
    public List<HeatmapPointDto> Points { get; set; } = new();
    /// <summary>Tổng số điểm dữ liệu</summary>
    public int TotalCount { get; set; }
}

/// <summary>Tổng hợp heatmap theo khung giờ (giờ trong ngày)</summary>
public class HeatmapByHourDto
{
    /// <summary>0–23</summary>
    public int Hour { get; set; }
    public int Count { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════════
// ANALYTICS — USER USAGE HISTORY DTOs
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Một lượt nghe trong lịch sử</summary>
public class UsageHistoryItemDto
{
    public int Id { get; set; }
    public int PoiPointId { get; set; }
    public string PoiName { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = string.Empty;
    public string TriggerType { get; set; } = string.Empty;
    public double DistanceMeters { get; set; }
    public DateTime PlayedAt { get; set; }
    /// <summary>Anonymous device ID (nên ẩn/format để bảo mật)</summary>
    public string DeviceId { get; set; } = string.Empty;
}

/// <summary>Danh sách lịch sử phân trang</summary>
public class UsageHistoryResponseDto
{
    public List<UsageHistoryItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>Filter lịch sử người dùng</summary>
public class UsageHistoryFilterDto
{
    public int? PoiPointId { get; set; }
    public string? DeviceId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

/// <summary>Thống kê theo thiết bị</summary>
public class DeviceStatsDto
{
    public string DeviceId { get; set; } = string.Empty;
    public int TotalPlays { get; set; }
    public int UniquePois { get; set; }
    public DateTime? FirstPlay { get; set; }
    public DateTime? LastPlay { get; set; }
}

/// <summary>Top thiết bị hoạt động nhiều nhất</summary>
public class TopDeviceDto
{
    public string DeviceId { get; set; } = string.Empty;
    public int TotalPlays { get; set; }
    public int UniquePois { get; set; }
    public DateTime? FirstPlay { get; set; }
    public DateTime LastPlay { get; set; }
}
