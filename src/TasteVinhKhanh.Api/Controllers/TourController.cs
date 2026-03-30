using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TasteVinhKhanh.Api.Services;
using TasteVinhKhanh.Shared.DTOs;

namespace TasteVinhKhanh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class TourController : ControllerBase
{
    private readonly ITourService _tour;

    public TourController(ITourService tour) => _tour = tour;

    /// <summary>Lấy danh sách tours — phân trang, tìm kiếm, lọc trạng thái</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] bool includeInactive = false)
    {
        var result = await _tour.GetAllPagedAsync(page, pageSize, search, includeInactive);
        return Ok(result);
    }

    /// <summary>Lấy chi tiết một tour kèm danh sách POI theo thứ tự</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _tour.GetByIdAsync(id);
        return result == null ? NotFound(new { error = "Tour không tồn tại." }) : Ok(result);
    }

    /// <summary>Tạo tour mới</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTourRequest request)
    {
        try
        {
            var email = User.FindFirstValue(ClaimTypes.Email) ?? "admin@system";
            var result = await _tour.CreateAsync(request, email);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Cập nhật tour (thông tin + POIs)</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTourRequest request)
    {
        try
        {
            var result = await _tour.UpdateAsync(id, request);
            return result == null ? NotFound(new { error = "Tour không tồn tại." }) : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Chỉ cập nhật thứ tự POI trong tour</summary>
    [HttpPut("{id}/reorder")]
    public async Task<IActionResult> Reorder(int id, [FromBody] ReorderTourRequest request)
    {
        try
        {
            var result = await _tour.ReorderAsync(id, request);
            return result == null ? NotFound(new { error = "Tour không tồn tại." }) : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Xóa tour (soft delete)</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
        => await _tour.DeleteAsync(id) ? NoContent() : NotFound(new { error = "Tour không tồn tại." });
}
