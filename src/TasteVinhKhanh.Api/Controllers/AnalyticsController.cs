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

    /// <summary>Bản đồ nhiệt — tọa độ + weight</summary>
    [HttpGet("heatmap")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Heatmap([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await _analytics.GetHeatmapDataAsync(from, to));

    /// <summary>Heatmap theo giờ trong ngày (0–23)</summary>
    [HttpGet("heatmap/by-hour")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> HeatmapByHour([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(await _analytics.GetHeatmapByHourAsync(from, to));

    /// <summary>Lịch sử nghe chi tiết — có phân trang + filter</summary>
    [HttpGet("history")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> History(
        [FromQuery] int? poiPointId,
        [FromQuery] string? deviceId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var filter = new UsageHistoryFilterDto
        {
            PoiPointId = poiPointId,
            DeviceId = deviceId,
            FromDate = fromDate,
            ToDate = toDate
        };
        return Ok(await _analytics.GetUsageHistoryAsync(filter, page, pageSize));
    }

    /// <summary>Top thiết bị hoạt động nhiều nhất</summary>
    [HttpGet("devices")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> TopDevices([FromQuery] int top = 20)
        => Ok(await _analytics.GetTopDevicesAsync(top));

    /// <summary>Số user (device) đang truy cập trong N phút gần nhất.</summary>
    [HttpGet("active-users")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ActiveUsers([FromQuery] int windowMinutes = 5)
        => Ok(await _analytics.GetActiveUsersAsync(windowMinutes));
}
