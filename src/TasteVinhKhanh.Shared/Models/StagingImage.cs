namespace TasteVinhKhanh.Shared.Models;

/// <summary>
/// Ảnh vendor tải lên — chờ admin duyệt trước khi áp dụng
/// </summary>
public class StagingImage
{
    public int Id { get; set; }

    /// <summary>Vendor tải ảnh lên</summary>
    public int VendorId { get; set; }

    /// <summary>POI mà ảnh này thuộc về</summary>
    public int PoiPointId { get; set; }

    /// <summary>Tên gốc của file</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Đường dẫn tạm trên server (trong wwwroot/staging)</summary>
    public string TempUrl { get; set; } = string.Empty;

    /// <summary>Đường dẫn sẽ áp dụng khi admin duyệt (trong wwwroot/images)</summary>
    public string? ApprovedUrl { get; set; }

    /// <summary>Trạng thái: Pending | Approved | Rejected</summary>
    public string Status { get; set; } = "Pending";

    /// <summary>Admin ghi chú khi duyệt/từ chối</summary>
    public string? AdminNote { get; set; }

    /// <summary>Ai đã duyệt / từ chối</summary>
    public string? ReviewedBy { get; set; }

    /// <summary>Thời điểm duyệt/từ chối</summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>Thời điểm vendor tải lên</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Loại staging: "Upload" = vendor upload ảnh mới, "Deletion" = vendor xin xóa ảnh cũ.
    /// Mặc định "Upload" để backward-compatible.
    /// </summary>
    public string StagingType { get; set; } = "Upload";

    /// <summary>
    /// Khi StagingType = "Deletion": URL ảnh cần xóa (từ bảng RestaurantImages).
    /// Khi StagingType = "Upload": chứa TempUrl (path trong staging/).
    /// </summary>
    public string? ReferencedImageUrl { get; set; }

    // Navigation
    public Vendor Vendor { get; set; } = null!;
    public PoiPoint PoiPoint { get; set; } = null!;
}
