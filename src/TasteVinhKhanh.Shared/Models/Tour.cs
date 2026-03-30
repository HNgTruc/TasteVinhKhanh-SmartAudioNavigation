namespace TasteVinhKhanh.Shared.Models;

/// <summary>
/// Một tuyến tham quan gồm nhiều điểm POI đã được thiết lập thứ tự lộ trình.
/// </summary>
public class Tour
{
    public int Id { get; set; }

    /// <summary>Tên tour — ví dụ: "Tour Miền Tây 1 Ngày"</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Mô tả ngắn về tour</summary>
    public string? Description { get; set; }

    /// <summary>Đang hoạt động hay đã bị xóa (soft delete)</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Email admin tạo tour</summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>Thời điểm tạo (UTC)</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Thời điểm cập nhật cuối (UTC)</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Các điểm dừng trong tour, theo thứ tự lộ trình</summary>
    public List<TourStop> TourStops { get; set; } = new();
}
