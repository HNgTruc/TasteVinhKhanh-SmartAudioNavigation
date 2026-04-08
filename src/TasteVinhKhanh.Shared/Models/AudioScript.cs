namespace TasteVinhKhanh.Shared.Models;

/// <summary>
/// Nội dung thuyết minh cho 1 POI theo 1 ngôn ngữ cụ thể.
/// App ưu tiên phát audio file (AudioFilePath) nếu có,
/// nếu không thì dùng TTS (TtsScript).
/// Audio file được serve qua endpoint bảo vệ /api/audio/{id}
/// — app lấy file qua protected URL thay vì public URL.
/// </summary>
public class AudioScript
{
    public int Id { get; set; }

    /// <summary>Thuộc POI nào</summary>
    public int PoiPointId { get; set; }

    /// <summary>Mã ngôn ngữ: "vi" | "en" | "zh" | "ko" | "ja"</summary>
    public string LanguageCode { get; set; } = "vi";

    /// <summary>Nội dung text đưa vào TTS</summary>
    public string TtsScript { get; set; } = string.Empty;

    /// <summary>
    /// Đường dẫn file audio đã generate/upload lên server.
    /// Đặt trong wwwroot/audio/poi_X/ — không dùng public URL.
    /// </summary>
    public string? AudioFilePath { get; set; }

    /// <summary>File đã tải về device chưa (app dùng)</summary>
    public bool IsAudioUploaded { get; set; } = false;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public PoiPoint? PoiPoint { get; set; }
}
