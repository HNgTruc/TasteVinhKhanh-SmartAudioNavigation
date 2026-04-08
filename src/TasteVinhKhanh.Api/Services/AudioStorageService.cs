using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using TasteVinhKhanh.Api.Data;
using TasteVinhKhanh.Shared.Models;

namespace TasteVinhKhanh.Api.Services;

/// <summary>
/// Quản lý audio file: lưu file, serve protected, delete.
/// Audio được lưu trong wwwroot/audio/poi_{id}/{lang}.mp3
/// </summary>
public interface IAudioStorageService
{
    /// <summary>Lấy đường dẫn file audio (nội bộ) từ script ID</summary>
    Task<string?> GetAudioFilePathAsync(int scriptId);

    /// <summary>Lưu audio file vào wwwroot, cập nhật AudioScript.AudioFilePath</summary>
    Task<(string path, int scriptId)?> SaveAudioFileAsync(IFormFile file, int poiId, string lang);

    /// <summary>Xóa audio file khỏi disk + reset AudioScript.AudioFilePath</summary>
    Task<bool> DeleteAudioFileAsync(int scriptId);

    /// <summary>Serve file audio theo scriptId</summary>
    Task<Stream?> GetAudioStreamAsync(int scriptId);
}

public class AudioStorageService : IAudioStorageService
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AudioStorageService> _log;

    public AudioStorageService(AppDbContext db, IWebHostEnvironment env, ILogger<AudioStorageService> log)
    {
        _db = db;
        _env = env;
        _log = log;
    }

    private string WwwRoot => Path.Combine(_env.ContentRootPath, "wwwroot");

    public async Task<string?> GetAudioFilePathAsync(int scriptId)
    {
        var script = await _db.AudioScripts.FindAsync(scriptId);
        return script?.AudioFilePath;
    }

    public async Task<(string path, int scriptId)?> SaveAudioFileAsync(IFormFile file, int poiId, string lang)
    {
        if (file == null || file.Length == 0)
            return null;

        var script = await _db.AudioScripts
            .FirstOrDefaultAsync(s => s.PoiPointId == poiId && s.LanguageCode == lang);

        if (script == null)
        {
            // Tạo script mới nếu chưa có
            script = new AudioScript { PoiPointId = poiId, LanguageCode = lang };
            _db.AudioScripts.Add(script);
            await _db.SaveChangesAsync();
        }

        // Tạo thư mục audio
        var audioDir = Path.Combine(WwwRoot, "audio", $"poi_{poiId}");
        Directory.CreateDirectory(audioDir);

        // Xóa file cũ nếu có
        if (!string.IsNullOrEmpty(script.AudioFilePath))
        {
            var oldPath = Path.Combine(WwwRoot, script.AudioFilePath);
            if (File.Exists(oldPath))
            {
                try { File.Delete(oldPath); } catch { /* ignore */ }
            }
        }

        // Lưu file mới
        var ext = Path.GetExtension(file.FileName).ToLower();
        if (string.IsNullOrEmpty(ext) || ext == ".") ext = ".mp3";
        var fileName = $"{lang}{ext}";
        var relPath = Path.Combine("audio", $"poi_{poiId}", fileName);
        var fullPath = Path.Combine(WwwRoot, relPath);

        await using var stream = new FileStream(fullPath, FileMode.Create);
        await file.CopyToAsync(stream);

        // Cập nhật DB
        script.AudioFilePath = relPath;
        script.IsAudioUploaded = true;
        script.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _log.LogInformation("Audio saved: POI={PoiId} lang={Lang} path={Path}", poiId, lang, relPath);
        return (relPath, script.Id);
    }

    public async Task<bool> DeleteAudioFileAsync(int scriptId)
    {
        var script = await _db.AudioScripts.FindAsync(scriptId);
        if (script == null) return false;

        if (!string.IsNullOrEmpty(script.AudioFilePath))
        {
            var fullPath = Path.Combine(WwwRoot, script.AudioFilePath);
            if (File.Exists(fullPath))
            {
                try { File.Delete(fullPath); } catch { /* ignore */ }
            }
        }

        script.AudioFilePath = null;
        script.IsAudioUploaded = false;
        script.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _log.LogInformation("Audio deleted: scriptId={ScriptId}", scriptId);
        return true;
    }

    public async Task<Stream?> GetAudioStreamAsync(int scriptId)
    {
        var script = await _db.AudioScripts.FindAsync(scriptId);
        if (script == null || string.IsNullOrEmpty(script.AudioFilePath))
            return null;

        var fullPath = Path.Combine(WwwRoot, script.AudioFilePath);
        if (!File.Exists(fullPath))
            return null;

        return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }
}
