using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TasteVinhKhanh.Api.Data;
using TasteVinhKhanh.Api.Services;
using TasteVinhKhanh.Shared.Models;

namespace TasteVinhKhanh.Api.Controllers;

/// <summary>
/// Quản lý audio: upload, xóa, serve protected.
/// Audio file được lưu nội bộ, serve qua endpoint bảo vệ.
/// </summary>
[ApiController]
[Route("api/audio")]
public class AudioController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAudioStorageService _audio;
    private readonly ITtsGenerationService _tts;
    private readonly ILogger<AudioController> _log;

    public AudioController(
        AppDbContext db,
        IAudioStorageService audio,
        ITtsGenerationService tts,
        ILogger<AudioController> log)
    {
        _db = db;
        _audio = audio;
        _tts = tts;
        _log = log;
    }

    /// <summary>
    /// Serve audio file — requires JWT authentication.
    /// URL: /api/audio/{scriptId}
    /// </summary>
    [Authorize]
    [HttpGet("{scriptId}")]
    public async Task<IActionResult> GetAudio(int scriptId)
    {
        var stream = await _audio.GetAudioStreamAsync(scriptId);
        if (stream == null)
            return NotFound(new { message = "Audio không tồn tại hoặc chưa được tải lên." });

        var script = await _db.AudioScripts.FindAsync(scriptId);
        var contentType = GetContentType(script?.AudioFilePath ?? "");

        return File(stream, contentType, enableRangeProcessing: true);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ADMIN: Upload audio file cho POI
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Admin upload audio file cho script của POI</summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("admin/upload")]
    public async Task<IActionResult> AdminUploadAudio([FromForm] int poiId, [FromForm] string lang, [FromForm] IFormFile file)
    {
        return StatusCode(StatusCodes.Status403Forbidden, new
        {
            message = "Admin chỉ được duyệt. Chỉ Vendor mới được thêm/chỉnh sửa audio."
        });
    }

    /// <summary>Admin xóa audio file của script</summary>
    [Authorize(Roles = "Admin")]
    [HttpDelete("admin/{scriptId}")]
    public async Task<IActionResult> AdminDeleteAudio(int scriptId)
    {
        return StatusCode(StatusCodes.Status403Forbidden, new
        {
            message = "Admin chỉ được duyệt. Chỉ Vendor mới được thêm/chỉnh sửa audio."
        });
    }

    /// <summary>Admin tạo audio từ TTS script đã có trong DB</summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("admin/generate")]
    public async Task<IActionResult> AdminGenerateAudio([FromBody] GenerateAudioRequest req)
    {
        return StatusCode(StatusCodes.Status403Forbidden, new
        {
            message = "Admin chỉ được duyệt. Chỉ Vendor mới được thêm/chỉnh sửa audio."
        });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // VENDOR: Upload audio cho script của POI được gán
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Vendor upload audio file cho script POI được gán</summary>
    [Authorize(Roles = "Vendor")]
    [HttpPost("vendor/upload")]
    public async Task<IActionResult> VendorUploadAudio([FromForm] int poiId, [FromForm] string lang, [FromForm] IFormFile file)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);
        if (vendor == null) return Unauthorized();
        if (vendor.Status != "Approved") return BadRequest(new { message = "Tài khoản chưa được duyệt." });
        if (!vendor.PoiPointId.HasValue || vendor.PoiPointId.Value != poiId)
            return BadRequest(new { message = "Bạn không có quyền upload audio cho POI này." });

        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Không có file nào được chọn." });

        var maxSize = 10 * 1024 * 1024;
        if (file.Length > maxSize)
            return BadRequest(new { message = "File vượt quá 10MB." });

        var allowedExt = new[] { ".mp3", ".m4a", ".wav", ".ogg", ".webm" };
        var ext = Path.GetExtension(file.FileName).ToLower();
        if (!allowedExt.Contains(ext))
            return BadRequest(new { message = "Chỉ chấp nhận: mp3, m4a, wav, ogg, webm." });

        var result = await _audio.SaveAudioFileAsync(file, poiId, lang);
        if (result == null)
            return BadRequest(new { message = "Lưu audio thất bại." });

        return Ok(new { scriptId = result.Value.scriptId, message = $"Đã upload audio {lang.ToUpperInvariant()}" });
    }

    /// <summary>Vendor xóa audio file của script</summary>
    [Authorize(Roles = "Vendor")]
    [HttpDelete("vendor/{scriptId}")]
    public async Task<IActionResult> VendorDeleteAudio(int scriptId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);
        if (vendor == null) return Unauthorized();
        if (vendor.Status != "Approved") return BadRequest(new { message = "Tài khoản chưa được duyệt." });

        var script = await _db.AudioScripts.FindAsync(scriptId);
        if (script == null) return NotFound();

        if (!vendor.PoiPointId.HasValue || vendor.PoiPointId.Value != script.PoiPointId)
            return BadRequest(new { message = "Bạn không có quyền xóa audio của POI này." });

        await _audio.DeleteAudioFileAsync(scriptId);
        return Ok(new { message = "Đã xóa audio." });
    }

    /// <summary>Vendor tạo audio từ TTS script đã có trong DB</summary>
    [Authorize(Roles = "Vendor")]
    [HttpPost("vendor/generate")]
    public async Task<IActionResult> VendorGenerateAudio([FromBody] GenerateAudioRequest req)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);
        if (vendor == null) return Unauthorized();
        if (vendor.Status != "Approved") return BadRequest(new { message = "Tài khoản chưa được duyệt." });
        if (!vendor.PoiPointId.HasValue || vendor.PoiPointId.Value != req.PoiId)
            return BadRequest(new { message = "Bạn không có quyền generate audio cho POI này." });

        var script = await _db.AudioScripts
            .FirstOrDefaultAsync(s => s.PoiPointId == req.PoiId && s.LanguageCode == req.LanguageCode);

        if (script == null)
            return NotFound(new { message = $"Script {req.LanguageCode} chưa có." });

        if (string.IsNullOrWhiteSpace(script.TtsScript))
            return BadRequest(new { message = $"Script {req.LanguageCode} chưa có nội dung TTS." });

        var ttsResult = await _tts.GenerateFromTextAsync(script.TtsScript, req.LanguageCode);
        if (!ttsResult.Success || ttsResult.AudioBytes == null)
            return BadRequest(new
            {
                message = $"TTS generation failed: {ttsResult.ErrorMessage ?? "Unknown error"}",
                hint = "Vui lòng nhập nội dung TTS script trước khi generate."
            });

        var tmpPath = Path.Combine(Path.GetTempPath(), $"tts_{Guid.NewGuid()}.mp3");
        await System.IO.File.WriteAllBytesAsync(tmpPath, ttsResult.AudioBytes);

        await using var stream = new FileStream(tmpPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var formFile = new FormFile(stream, 0, stream.Length, "file", "tts.mp3")
        {
            Headers = new HeaderDictionary(),
            ContentType = "audio/mpeg"
        };

        var result = await _audio.SaveAudioFileAsync(formFile, req.PoiId, req.LanguageCode);
        await stream.DisposeAsync();
        try { System.IO.File.Delete(tmpPath); } catch { /* ignore — file có thể bị lock */ }

        if (result == null)
            return BadRequest(new { message = "Lưu audio thất bại." });

        return Ok(new { scriptId = result.Value.scriptId, message = $"TTS audio đã được tạo" });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // HELPER
    // ═══════════════════════════════════════════════════════════════════════════

    private static string GetContentType(string path)
    {
        var ext = Path.GetExtension(path).ToLower();
        return ext switch
        {
            ".mp3" => "audio/mpeg",
            ".m4a" => "audio/mp4",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            ".webm" => "audio/webm",
            _ => "application/octet-stream"
        };
    }
}

public class GenerateAudioRequest
{
    public int PoiId { get; set; }
    public string LanguageCode { get; set; } = "vi";
}
