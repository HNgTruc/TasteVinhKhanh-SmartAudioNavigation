namespace TasteVinhKhanh.Shared.Models;

/// <summary>
/// Nội dung thuyết minh cho 1 POI theo 1 ngôn ngữ cụ thể.
/// App sẽ ưu tiên phát AudioFileUrl nếu có, nếu không thì dùng TtsScript.
/// </summary>
public class AudioScript
{
    public int Id { get; set; }

    /// <summary>Thuộc POI nào</summary>
    public int PoiPointId { get; set; }

    /// <summary>Mã ngôn ngữ: "vi" | "en" | "zh" | "ko" | "ja"</summary>
    public string LanguageCode { get; set; } = "vi";

    /// <summary>Nội dung text đưa vào TTS khi không có file audio</summary>
    public string TtsScript { get; set; } = string.Empty;

    /// <summary>URL file audio thu sẵn trên server — chất lượng cao hơn TTS</summary>
    public string? AudioFileUrl { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public PoiPoint? PoiPoint { get; set; }
}
