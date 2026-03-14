namespace TasteVinhKhanh.Shared.Models;

/// <summary>
/// Điểm thuyết minh trên phố Vĩnh Khánh
/// </summary>
public class PoiPoint
{
    public int Id { get; set; }

    /// <summary>Tên điểm — ví dụ: "Bánh mì Cô Ba"</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Mô tả ngắn hiển thị trên bản đồ</summary>
    public string ShortDescription { get; set; } = string.Empty;

    /// <summary>Vĩ độ</summary>
    public double Latitude { get; set; }

    /// <summary>Kinh độ</summary>
    public double Longitude { get; set; }

    /// <summary>Bán kính kích hoạt tính bằng mét — mặc định 50m</summary>
    public double TriggerRadiusMeters { get; set; } = 50.0;

    /// <summary>Mức ưu tiên phát — số cao hơn sẽ được phát trước khi có nhiều POI trong tầm</summary>
    public int Priority { get; set; } = 1;

    /// <summary>Đang hoạt động hay bị ẩn</summary>
    public bool IsActive { get; set; } = true;

    public string? ImageUrl { get; set; }
    public string? MapUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Danh sách script thuyết minh theo từng ngôn ngữ</summary>
    public List<AudioScript> AudioScripts { get; set; } = new();
}
