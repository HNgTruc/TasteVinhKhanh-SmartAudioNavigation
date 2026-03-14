using Microsoft.AspNetCore.Mvc;
using TasteVinhKhanh.Api.Services;

namespace TasteVinhKhanh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    private readonly ISyncService _sync;

    public SyncController(ISyncService sync) => _sync = sync;

    /// <summary>
    /// MauiApp gọi khi có mạng để cập nhật SQLite local.
    /// lastSyncAt = null  → lấy toàn bộ POI (lần đầu cài app)
    /// lastSyncAt có giá trị → chỉ lấy POI thay đổi sau thời điểm đó
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Sync([FromQuery] DateTime? lastSyncAt)
        => Ok(await _sync.GetChangesAsync(lastSyncAt));
}
