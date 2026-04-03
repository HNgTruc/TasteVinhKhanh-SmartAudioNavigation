namespace TasteVinhKhanh.Shared.Models;

/// <summary>
/// Yêu cầu cập nhật POI từ vendor — chờ admin duyệt
/// </summary>
public class PendingPOIUpdate
{
    public int Id { get; set; }

    /// <summary>Vendor gửi yêu cầu</summary>
    public int VendorId { get; set; }

    /// <summary>POI đang được cập nhật</summary>
    public int PoiPointId { get; set; }

    /// <summary>JSON: name, shortDescription, lat, lng, radius, priority, iconUrl</summary>
    public string Payload { get; set; } = "{}";

    /// <summary>JSON array: [{ imageUrl, isPrimary, sortOrder }]</summary>
    public string? ImagesPayload { get; set; }

    /// <summary>JSON array: [{ languageCode, ttsScript, audioFileUrl }]</summary>
    public string? ScriptsPayload { get; set; }

    /// <summary>Trạng thái: Pending | Approved | Rejected</summary>
    public string Status { get; set; } = "Pending";

    /// <summary>Ghi chú của admin khi duyệt/từ chối</summary>
    public string? AdminNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }

    // Navigation
    public Vendor? Vendor { get; set; }
}
