using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TasteVinhKhanh.Api.Services;
using TasteVinhKhanh.Shared.DTOs;

namespace TasteVinhKhanh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PoiController : ControllerBase
{
    private readonly IPoiService _poi;

    public PoiController(IPoiService poi) => _poi = poi;

    /// <summary>Lấy danh sách POI — public, MauiApp gọi không cần token</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
        => Ok(await _poi.GetAllAsync(includeInactive));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _poi.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Tạo POI mới — chỉ Admin</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreatePoiRequest request)
    {
        var result = await _poi.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePoiRequest request)
    {
        var result = await _poi.UpdateAsync(id, request);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
        => await _poi.DeleteAsync(id) ? NoContent() : NotFound();

    /// <summary>Thêm hoặc cập nhật script theo ngôn ngữ</summary>
    [HttpPut("{poiId}/scripts")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpsertScript(int poiId, [FromBody] UpsertAudioScriptRequest request)
        => Ok(await _poi.UpsertScriptAsync(poiId, request));

    [HttpDelete("{poiId}/scripts/{lang}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteScript(int poiId, string lang)
        => await _poi.DeleteScriptAsync(poiId, lang) ? NoContent() : NotFound();
}
