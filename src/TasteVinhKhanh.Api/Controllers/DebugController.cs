using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TasteVinhKhanh.Api.Data;

namespace TasteVinhKhanh.Api.Controllers;

/// <summary>
/// Debug endpoint — chỉ dùng khi phát triển.
/// Xóa hoặc disable khi deploy production.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DebugController : ControllerBase
{
    private readonly AppDbContext _db;

    public DebugController(AppDbContext db) => _db = db;

    /// <summary>Trả về toàn bộ POI đang có trong SQL Server</summary>
    [HttpGet("pois")]
    public async Task<IActionResult> GetPois()
    {
        var pois = await _db.PoiPoints
            .Include(p => p.AudioScripts)
            .OrderByDescending(p => p.Priority)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.ShortDescription,
                p.Latitude,
                p.Longitude,
                p.TriggerRadiusMeters,
                p.Priority,
                p.IsActive,
                ScriptCount = p.AudioScripts.Count,
                Languages = p.AudioScripts.Select(s => s.LanguageCode).ToList()
            })
            .ToListAsync();

        return Ok(new
        {
            count = pois.Count,
            pois,
            serverTime = DateTime.UtcNow,
            note = "Nếu count=0 → chạy lại API để seed tự động, hoặc chạy SeedData.sql trong SSMS"
        });
    }

    /// <summary>Trả về dữ liệu sync y hệt MauiApp nhận được</summary>
    [HttpGet("sync-preview")]
    public async Task<IActionResult> SyncPreview()
    {
        var pois = await _db.PoiPoints
            .Include(p => p.AudioScripts)
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.Priority)
            .ToListAsync();

        return Ok(new
        {
            hasChanges = pois.Any(),
            syncedAt = DateTime.UtcNow,
            pois = pois.Select(p => new
            {
                p.Id,
                p.Name,
                p.ShortDescription,
                p.Latitude,
                p.Longitude,
                p.TriggerRadiusMeters,
                p.Priority,
                p.IsActive,
                p.ImageUrl,
                p.MapUrl,
                p.UpdatedAt,
                AudioScripts = p.AudioScripts.Select(s => new
                {
                    s.Id,
                    s.PoiPointId,
                    s.LanguageCode,
                    s.TtsScript,
                    s.AudioFileUrl,
                    s.UpdatedAt
                })
            })
        });
    }

    /// <summary>Reset dữ liệu — xóa hết POI để seed lại</summary>
    [HttpPost("reset-pois")]
    public async Task<IActionResult> ResetPois()
    {
        var scripts = await _db.AudioScripts.ToListAsync();
        var pois = await _db.PoiPoints.ToListAsync();

        _db.AudioScripts.RemoveRange(scripts);
        _db.PoiPoints.RemoveRange(pois);
        await _db.SaveChangesAsync();

        return Ok(new { deletedPois = pois.Count, deletedScripts = scripts.Count });
    }
}
