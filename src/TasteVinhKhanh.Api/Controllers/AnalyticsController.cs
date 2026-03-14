using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TasteVinhKhanh.Api.Services;
using TasteVinhKhanh.Shared.DTOs;

namespace TasteVinhKhanh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analytics;

    public AnalyticsController(IAnalyticsService analytics) => _analytics = analytics;

    /// <summary>MauiApp gửi batch log lên khi có mạng — không cần token</summary>
    [HttpPost("logs")]
    public async Task<IActionResult> BatchLog([FromBody] BatchPlaybackLogRequest request)
    {
        await _analytics.SaveLogsAsync(request.Logs);
        return Ok(new { saved = request.Logs.Count });
    }

    /// <summary>Tổng quan cho Dashboard Admin</summary>
    [HttpGet("summary")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Summary()
        => Ok(await _analytics.GetSummaryAsync());

    /// <summary>Top POI được nghe nhiều nhất</summary>
    [HttpGet("top-pois")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> TopPois([FromQuery] int top = 10)
        => Ok(await _analytics.GetTopPoisAsync(top));
}
