namespace TasteVinhKhanh.Shared.Models;

/// <summary>
/// Ảnh của điểm thuyết minh (PoiPoint)
/// Mỗi quán có thể có nhiều ảnh
/// </summary>
public class RestaurantImage
{
    public int Id { get; set; }

    /// <summary>Id của PoiPoint mà ảnh này thuộc về</summary>
    public int PoiPointId { get; set; }

    /// <summary>Đường dẫn ảnh, ví dụ: /images/alo_quan_1.jpg</summary>
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>True = ảnh chính (ảnh đại diện của quán), chỉ 1 ảnh chính mỗi quán</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Thứ tự hiển thị ảnh (số nhỏ = hiển thị trước)</summary>
    public int SortOrder { get; set; }

    /// <summary>Ngày tạo</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Ngày cập nhật</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public PoiPoint? PoiPoint { get; set; }
}
