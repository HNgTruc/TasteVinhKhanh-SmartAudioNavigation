namespace TasteVinhKhanh.Shared.Models;

/// <summary>
/// Một điểm dừng trong lộ trình tour — liên kết Tour với POI và thứ tự.
/// </summary>
public class TourStop
{
    public int Id { get; set; }

    /// <summary>Tour cha</summary>
    public int TourId { get; set; }
    public Tour Tour { get; set; } = null!;

    /// <summary>POI được thêm vào tour</summary>
    public int PoiPointId { get; set; }
    public PoiPoint PoiPoint { get; set; } = null!;

    /// <summary>Thứ tự trong lộ trình (1-based)</summary>
    public int StopOrder { get; set; }

    /// <summary>Thời điểm thêm vào tour (UTC)</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Thời điểm cập nhật cuối (UTC)</summary>
    public DateTime? UpdatedAt { get; set; }
}
