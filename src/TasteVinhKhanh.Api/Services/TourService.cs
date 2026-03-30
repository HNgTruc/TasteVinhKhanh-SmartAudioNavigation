using Microsoft.EntityFrameworkCore;
using TasteVinhKhanh.Api.Data;
using TasteVinhKhanh.Shared.DTOs;
using TasteVinhKhanh.Shared.Models;

namespace TasteVinhKhanh.Api.Services;

public interface ITourService
{
    Task<TourPagedDto> GetAllPagedAsync(int page, int pageSize, string? search, bool includeInactive);
    Task<TourDetailDto?> GetByIdAsync(int id);
    Task<TourDetailDto> CreateAsync(CreateTourRequest request, string createdBy);
    Task<TourDetailDto?> UpdateAsync(int id, UpdateTourRequest request);
    Task<bool> DeleteAsync(int id);
    Task<TourDetailDto?> ReorderAsync(int id, ReorderTourRequest request);
}

public class TourService : ITourService
{
    private readonly AppDbContext _db;

    public TourService(AppDbContext db) => _db = db;

    public async Task<TourPagedDto> GetAllPagedAsync(int page, int pageSize, string? search, bool includeInactive)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _db.Tours
            .Include(t => t.TourStops)
            .AsQueryable();

        if (!includeInactive)
            query = query.Where(t => t.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t => t.Name.ToLower().Contains(search.ToLower()));

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TourListItemDto
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                PoiCount = t.TourStops.Count,
                IsActive = t.IsActive,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            })
            .ToListAsync();

        return new TourPagedDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

    public async Task<TourDetailDto?> GetByIdAsync(int id)
    {
        var tour = await _db.Tours
            .Include(t => t.TourStops)
                .ThenInclude(s => s.PoiPoint)
            .FirstOrDefaultAsync(t => t.Id == id);

        return tour == null ? null : ToDetailDto(tour);
    }

    public async Task<TourDetailDto> CreateAsync(CreateTourRequest r, string createdBy)
    {
        // Validate: tên không rỗng
        if (string.IsNullOrWhiteSpace(r.Name))
            throw new ArgumentException("Tên tour không được để trống.");

        if (r.Name.Length > 200)
            throw new ArgumentException("Tên tour không được vượt quá 200 ký tự.");

        if (r.Description?.Length > 1000)
            throw new ArgumentException("Mô tả không được vượt quá 1000 ký tự.");

        // Validate: POI phải tồn tại và đang active
        if (r.PoiIds.Count > 50)
            throw new ArgumentException("Tour không được chứa quá 50 điểm.");

        var validPois = await _db.PoiPoints
            .Where(p => r.PoiIds.Contains(p.Id) && p.IsActive)
            .ToListAsync();

        if (validPois.Count != r.PoiIds.Distinct().Count())
        {
            var missing = r.PoiIds.Except(validPois.Select(p => p.Id)).ToList();
            throw new ArgumentException($"POI không hợp lệ hoặc đã bị ẩn: {string.Join(", ", missing)}");
        }

        var tour = new Tour
        {
            Name = r.Name.Trim(),
            Description = r.Description?.Trim(),
            IsActive = true,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        _db.Tours.Add(tour);
        await _db.SaveChangesAsync();

        // Thêm các điểm dừng theo thứ tự
        for (int i = 0; i < r.PoiIds.Count; i++)
        {
            _db.TourStops.Add(new TourStop
            {
                TourId = tour.Id,
                PoiPointId = r.PoiIds[i],
                StopOrder = i + 1,
                CreatedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();

        // Load lại với navigation
        return (await GetByIdAsync(tour.Id))!;
    }

    public async Task<TourDetailDto?> UpdateAsync(int id, UpdateTourRequest r)
    {
        var tour = await _db.Tours
            .Include(t => t.TourStops)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tour == null) return null;

        if (string.IsNullOrWhiteSpace(r.Name))
            throw new ArgumentException("Tên tour không được để trống.");

        if (r.Name.Length > 200)
            throw new ArgumentException("Tên tour không được vượt quá 200 ký tự.");

        if (r.Description?.Length > 1000)
            throw new ArgumentException("Mô tả không được vượt quá 1000 ký tự.");

        if (r.PoiIds.Count > 50)
            throw new ArgumentException("Tour không được chứa quá 50 điểm.");

        // Validate POIs
        if (r.PoiIds.Count > 0)
        {
            var validPois = await _db.PoiPoints
                .Where(p => r.PoiIds.Contains(p.Id) && p.IsActive)
                .ToListAsync();

            if (validPois.Count != r.PoiIds.Distinct().Count())
                throw new ArgumentException("Một hoặc nhiều POI không hợp lệ hoặc đã bị ẩn.");
        }

        tour.Name = r.Name.Trim();
        tour.Description = r.Description?.Trim();
        tour.UpdatedAt = DateTime.UtcNow;

        // Xóa stops cũ, tạo lại theo thứ tự mới
        _db.TourStops.RemoveRange(tour.TourStops);
        for (int i = 0; i < r.PoiIds.Count; i++)
        {
            _db.TourStops.Add(new TourStop
            {
                TourId = tour.Id,
                PoiPointId = r.PoiIds[i],
                StopOrder = i + 1,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        return await GetByIdAsync(tour.Id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var tour = await _db.Tours.FindAsync(id);
        if (tour == null) return false;

        tour.IsActive = false;
        tour.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<TourDetailDto?> ReorderAsync(int id, ReorderTourRequest r)
    {
        var tour = await _db.Tours
            .Include(t => t.TourStops)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tour == null) return null;

        var currentPoiIds = tour.TourStops.OrderBy(s => s.StopOrder).Select(s => s.PoiPointId).ToList();
        var newPoiIds = r.PoiIds.Distinct().ToList();

        if (!currentPoiIds.OrderBy(x => x).SequenceEqual(newPoiIds.OrderBy(x => x)))
            throw new ArgumentException("Danh sách POI không khớp với các điểm hiện có trong tour.");

        // Cập nhật StopOrder theo thứ tự mới
        foreach (var stop in tour.TourStops)
        {
            var newOrder = newPoiIds.IndexOf(stop.PoiPointId) + 1;
            stop.StopOrder = newOrder;
            stop.UpdatedAt = DateTime.UtcNow;
        }

        tour.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return await GetByIdAsync(tour.Id);
    }

    // ── Mappers ───────────────────────────────────────────────────────────────

    private static TourDetailDto ToDetailDto(Tour t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Description = t.Description,
        IsActive = t.IsActive,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
        Pois = t.TourStops
            .OrderBy(s => s.StopOrder)
            .Select(s => new TourPoiDto
            {
                PoiId = s.PoiPointId,
                PoiName = s.PoiPoint.Name,
                PoiIsActive = s.PoiPoint.IsActive,
                StopOrder = s.StopOrder
            })
            .ToList()
    };
}
