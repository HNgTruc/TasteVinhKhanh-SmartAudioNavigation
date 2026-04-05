using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TasteVinhKhanh.Api.Data;
using TasteVinhKhanh.Shared.DTOs;

namespace TasteVinhKhanh.Api.Controllers;

/// <summary>
/// Admin endpoint — quản lý vendor và duyệt POI updates
/// </summary>
[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin")]
public class AdminVendorController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public AdminVendorController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    private string WwwRoot => Path.Combine(_env.ContentRootPath, "wwwroot");

    /// <summary>Badge count cho dashboard</summary>
    [HttpGet("badges")]
    public async Task<IActionResult> GetBadges()
    {
        var pendingVendors = await _db.Vendors.CountAsync(v => v.Status == "Pending");
        var pendingUpdates = await _db.PendingPOIUpdates.CountAsync(u => u.Status == "Pending");

        return Ok(new AdminBadgeDto
        {
            PendingVendors = pendingVendors,
            PendingUpdates = pendingUpdates
        });
    }

    /// <summary>Stats cho trang pending-updates (badge counts)</summary>
    [HttpGet("pending-updates/stats")]
    public async Task<IActionResult> GetPendingStats()
    {
        var stats = new PendingUpdatesStatsDto
        {
            Pending = await _db.PendingPOIUpdates.CountAsync(x => x.Status == "Pending"),
            ApprovedToday = await _db.PendingPOIUpdates
                .CountAsync(x => x.Status == "Approved" && x.ReviewedAt >= DateTime.UtcNow.Date),
            RejectedToday = await _db.PendingPOIUpdates
                .CountAsync(x => x.Status == "Rejected" && x.ReviewedAt >= DateTime.UtcNow.Date),
            UniquePoiCount = await _db.PendingPOIUpdates
                .Where(x => x.Status == "Pending")
                .Select(x => x.PoiPointId)
                .Distinct()
                .CountAsync()
        };
        return Ok(stats);
    }

    // ── VENDOR MANAGEMENT ───────────────────────────────────────────

    /// <summary>Danh sách vendor theo trạng thái</summary>
    [HttpGet("vendors")]
    public async Task<IActionResult> GetVendors([FromQuery] string status = "")
    {
        var query = _db.Vendors
            .Include(v => v.PoiPoint)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
        {
            if (status == "Pending")
                query = query.Where(v => v.Status == "Pending");
            else if (status == "Approved")
                query = query.Where(v => v.Status == "Approved");
            else if (status == "Rejected")
                query = query.Where(v => v.Status == "Rejected");
        }

        var vendors = await query
            .Include(v => v.PoiPoint)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();

        var userIds = vendors.Select(v => v.UserId).ToList();
        var userEmails = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email ?? "");

        var poiIds = vendors.Where(v => v.PoiPointId.HasValue).Select(v => v.PoiPointId!.Value).ToList();
        var playCounts = await _db.PlaybackLogs
            .Where(l => poiIds.Contains(l.PoiPointId))
            .GroupBy(l => l.PoiPointId)
            .Select(g => new { PoiPointId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PoiPointId, x => x.Count);

        var items = vendors.Select(v => new VendorListDto
        {
            Id = v.Id,
            Name = v.BusinessName,
            Email = userEmails.GetValueOrDefault(v.UserId, ""),
            BusinessName = v.BusinessName,
            IsActive = v.Status == "Approved",
            Status = v.Status,
            PoiCount = v.PoiPoint != null ? 1 : 0,
            TotalPlays = v.PoiPointId.HasValue && playCounts.ContainsKey(v.PoiPointId.Value) ? playCounts[v.PoiPointId.Value] : 0,
            CreatedAt = v.CreatedAt
        }).ToList();

        return Ok(new VendorListResponseDto
        {
            Items = items,
            TotalCount = items.Count,
            TotalPoiCount = items.Sum(x => x.PoiCount)
        });
    }

    /// <summary>Chi tiết 1 vendor</summary>
    [HttpGet("vendors/{id}")]
    public async Task<IActionResult> GetVendor(int id)
    {
        var v = await _db.Vendors
            .Include(v => v.PoiPoint)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (v == null) return NotFound();

        var user = await _db.Users.FindAsync(v.UserId);
        var poiCount = v.PoiPoint != null ? 1 : 0;
        var totalPlays = await _db.PlaybackLogs.CountAsync(l => l.PoiPointId == v.PoiPointId);

        return Ok(new VendorListDto
        {
            Id = v.Id,
            Name = v.BusinessName,
            Email = user?.Email ?? "",
            BusinessName = v.BusinessName,
            IsActive = v.Status == "Approved",
            Status = v.Status,
            PoiCount = poiCount,
            CreatedAt = v.CreatedAt,
            TotalPlays = totalPlays
        });
    }

    /// <summary>Danh sách POI của vendor</summary>
    [HttpGet("vendors/{id}/pois")]
    public async Task<IActionResult> GetVendorPois(int id)
    {
        var v = await _db.Vendors.FindAsync(id);
        if (v == null) return NotFound();

        if (!v.PoiPointId.HasValue)
            return Ok(new List<PoiDto>());

        var pois = await _db.PoiPoints
            .Include(p => p.AudioScripts)
            .Include(p => p.Images)
            .Where(p => p.Id == v.PoiPointId.Value)
            .ToListAsync();

        return Ok(pois.Select(p => new PoiDto
        {
            Id = p.Id, Name = p.Name,
            ShortDescription = p.ShortDescription,
            Latitude = p.Latitude, Longitude = p.Longitude,
            TriggerRadiusMeters = p.TriggerRadiusMeters,
            Priority = p.Priority, IsActive = p.IsActive,
            ImageUrl = p.ImageUrl, IconUrl = p.IconUrl, MapUrl = p.MapUrl,
            UpdatedAt = p.UpdatedAt,
            AudioScripts = p.AudioScripts.Select(s => new AudioScriptDto
            {
                Id = s.Id, PoiPointId = s.PoiPointId,
                LanguageCode = s.LanguageCode,
                TtsScript = s.TtsScript,
                AudioFileUrl = s.AudioFileUrl, UpdatedAt = s.UpdatedAt
            }).ToList(),
            Images = p.Images.Select(i => new RestaurantImageDto
            {
                Id = i.Id, PoiPointId = i.PoiPointId,
                ImageUrl = i.ImageUrl, IsPrimary = i.IsPrimary, SortOrder = i.SortOrder
            }).ToList()
        }).ToList());
    }

    /// <summary>Danh sách POIs chưa có vendor</summary>
    [HttpGet("pois/unassigned")]
    public async Task<IActionResult> GetUnassignedPois()
    {
        var assignedIds = await _db.Vendors
            .Where(v => v.PoiPointId.HasValue)
            .Select(v => v.PoiPointId!.Value)
            .ToListAsync();

        var pois = await _db.PoiPoints
            .Where(p => !assignedIds.Contains(p.Id))
            .Select(p => new UnassignedPoiDto { Id = p.Id, Name = p.Name })
            .ToListAsync();

        return Ok(pois);
    }

    /// <summary>Duyệt vendor + gán POI</summary>
    [HttpPut("vendors/{vendorId}/approve")]
    public async Task<IActionResult> ApproveVendor(int vendorId, [FromBody] ApproveVendorRequest req)
    {
        var vendor = await _db.Vendors.FindAsync(vendorId);
        if (vendor == null) return NotFound(new { message = "Vendor không tồn tại." });
        if (vendor.Status == "Approved") return BadRequest(new { message = "Vendor đã được duyệt trước đó." });

        // Kiểm tra POI đã có vendor chưa
        if (req.PoiPointId > 0)
        {
            var poiTaken = await _db.Vendors
                .AnyAsync(v => v.PoiPointId == req.PoiPointId && v.Id != vendorId);
            if (poiTaken)
                return BadRequest(new { message = "POI này đã được gán cho vendor khác." });

            vendor.PoiPointId = req.PoiPointId;
        }

        vendor.Status = "Approved";
        vendor.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Đã duyệt vendor và gán POI thành công." });
    }

    /// <summary>Từ chối vendor</summary>
    [HttpPut("vendors/{vendorId}/reject")]
    public async Task<IActionResult> RejectVendor(int vendorId, [FromBody] RejectVendorRequest req)
    {
        var vendor = await _db.Vendors.FindAsync(vendorId);
        if (vendor == null) return NotFound();

        vendor.Status = "Rejected";
        vendor.RejectedReason = req.Reason;
        vendor.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Đã từ chối vendor." });
    }

    /// <summary>Xoá vendor + tài khoản User</summary>
    [HttpDelete("vendors/{vendorId}")]
    public async Task<IActionResult> DeleteVendor(int vendorId)
    {
        var vendor = await _db.Vendors.FindAsync(vendorId);
        if (vendor == null) return NotFound(new { message = "Vendor không tồn tại." });

        var userId = vendor.UserId;
        var vendorName = vendor.BusinessName;

        // 1. Xoá PendingPOIUpdates trước
        var pendingUpdates = await _db.PendingPOIUpdates
            .Where(u => u.VendorId == vendorId)
            .ToListAsync();
        _db.PendingPOIUpdates.RemoveRange(pendingUpdates);

        // 2. Xoá Vendor record
        _db.Vendors.Remove(vendor);
        await _db.SaveChangesAsync(); // commit Vendors + PendingPOIUpdates

        // 3. Xoá User account qua UserManager (tự xử lý Identity tables)
        var userManager = HttpContext.RequestServices
            .GetRequiredService<UserManager<AppUser>>();
        var user = await userManager.FindByIdAsync(userId);
        if (user != null)
        {
            var result = await userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return StatusCode(500, new { message = $"Không thể xoá user: {errors}" });
            }
        }

        return Ok(new { message = $"Đã xoá vendor '{vendorName}' và tài khoản." });
    }

    // ── POI UPDATE MANAGEMENT ─────────────────────────────────────────

    /// <summary>Danh sách POI updates chờ duyệt</summary>
    [HttpGet("pending-updates")]
    public async Task<IActionResult> GetPendingUpdates([FromQuery] string status = "Pending")
    {
        var query = _db.PendingPOIUpdates
            .Include(u => u.Vendor)
            .AsQueryable();

        if (status == "Pending")
            query = query.Where(u => u.Status == "Pending");
        else if (status == "Approved")
            query = query.Where(u => u.Status == "Approved");
        else if (status == "Rejected")
            query = query.Where(u => u.Status == "Rejected");

        var updates = await query
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new
            {
                u.Id,
                u.VendorId,
                VendorName = u.Vendor!.BusinessName,
                u.PoiPointId,
                PoiName = _db.PoiPoints.Where(p => p.Id == u.PoiPointId).Select(p => p.Name).FirstOrDefault() ?? "",
                PoiShortDesc = _db.PoiPoints.Where(p => p.Id == u.PoiPointId).Select(p => p.ShortDescription).FirstOrDefault(),
                u.Status,
                u.CreatedAt,
                u.Payload,
                u.ImagesPayload,
                u.ScriptsPayload,
                SubmittedBy = u.Vendor.OwnerName,
                ChangeType = DetectChangeType(u.PoiPointId, u.Payload, u.ImagesPayload, u.ScriptsPayload)
            })
            .ToListAsync();

        var result = updates.Select(u => new PendingUpdateDto
        {
            Id = u.Id,
            VendorId = u.VendorId,
            VendorName = u.VendorName,
            BusinessName = u.VendorName,
            PoiId = u.PoiPointId,
            PoiName = u.PoiName,
            PoiShortDesc = u.PoiShortDesc,
            SubmittedBy = u.SubmittedBy,
            ChangeType = u.ChangeType,
            Status = u.Status,
            SubmittedAt = u.CreatedAt,
            CreatedAt = u.CreatedAt,
            Summary = BuildSummary(u.Payload, u.ImagesPayload, u.ScriptsPayload),
            Changes = ParsePayloadChanges(u.Payload)
        }).ToList();

        // Stats (cho badge)
        var stats = new PendingUpdatesStatsDto
        {
            Pending = await _db.PendingPOIUpdates.CountAsync(x => x.Status == "Pending"),
            ApprovedToday = await _db.PendingPOIUpdates
                .CountAsync(x => x.Status == "Approved" && x.ReviewedAt >= DateTime.UtcNow.Date),
            RejectedToday = await _db.PendingPOIUpdates
                .CountAsync(x => x.Status == "Rejected" && x.ReviewedAt >= DateTime.UtcNow.Date),
            UniquePoiCount = await _db.PendingPOIUpdates
                .Where(x => x.Status == "Pending")
                .Select(x => x.PoiPointId)
                .Distinct()
                .CountAsync()
        };

        return Ok(new { items = result, stats });
    }

    private static string DetectChangeType(int poiPointId, string? payload, string? imagesPayload, string? scriptsPayload)
    {
        // PoiPointId = 0 → Thêm POI mới
        if (poiPointId == 0) return "poi_created";
        
        // Còn lại → Cập nhật
        if (!string.IsNullOrEmpty(imagesPayload)) return "image_uploaded";
        if (!string.IsNullOrEmpty(scriptsPayload)) return "script_updated";
        if (!string.IsNullOrEmpty(payload)) return "poi_updated";
        return "poi_updated";
    }

    private static string? BuildSummary(string? payload, string? imagesPayload, string? scriptsPayload)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(imagesPayload)) parts.Add("📷 Ảnh mới");
        if (!string.IsNullOrEmpty(scriptsPayload)) parts.Add("🎤 Script mới");
        if (!string.IsNullOrEmpty(payload)) parts.Add("✏️ Thông tin POI");
        return parts.Count > 0 ? string.Join(" • ", parts) : null;
    }

    /// <summary>Chi tiết một POI update</summary>
    [HttpGet("pending-updates/{id}")]
    public async Task<IActionResult> GetUpdateDetail(int id)
    {
        var update = await _db.PendingPOIUpdates
            .Include(u => u.Vendor)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (update == null) return NotFound();

        var vendorUser = await _db.Users.FindAsync(update.Vendor!.UserId);

        return Ok(new UpdateDetailDto
        {
            Id = update.Id,
            VendorId = update.VendorId,
            BusinessName = update.Vendor.BusinessName,
            PoiName = _db.PoiPoints.Where(p => p.Id == update.PoiPointId).Select(p => p.Name).FirstOrDefault() ?? "",
            VendorEmail = vendorUser?.Email ?? "",
            CreatedAt = update.CreatedAt,
            Payload = update.Payload,
            ImagesPayload = update.ImagesPayload,
            ScriptsPayload = update.ScriptsPayload,
            Status = update.Status,
            Changes = ParsePayloadChanges(update.Payload)
        });
    }

    private static Dictionary<string, ChangeValueDto>? ParsePayloadChanges(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return null;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            // Lấy thông tin POI hiện tại để so sánh "trước"
            var changes = new Dictionary<string, ChangeValueDto>();

            // Các trường có thể thay đổi
            string[] fields = { "name", "shortDescription", "iconUrl", "triggerRadiusMeters", "priority", "isActive" };

            foreach (var field in fields)
            {
                if (root.TryGetProperty(field, out var afterElement) && afterElement.ValueKind != JsonValueKind.Null)
                {
                    var afterVal = afterElement.ValueKind == JsonValueKind.String
                        ? afterElement.GetString() ?? ""
                        : afterElement.ToString();

                    changes[field] = new ChangeValueDto
                    {
                        Before = null, // Admin có thể tự xem trong Payload
                        After  = afterVal
                    };
                }
            }

            return changes.Count > 0 ? changes : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Duyệt POI update — apply lên PoiPoint thật hoặc tạo mới</summary>
    [HttpPost("pending-updates/{id}/approve")]
    public async Task<IActionResult> ApproveUpdate(int id, [FromBody] ApproveUpdateRequest req)
    {
        var update = await _db.PendingPOIUpdates.FindAsync(id);
        if (update == null) return NotFound();
        if (update.Status != "Pending")
            return BadRequest(new { message = "Update đã được xử lý trước đó." });

        var adminEmail = User.FindFirstValue(ClaimTypes.Email) ?? "admin";

        Shared.Models.PoiPoint poi;

        // Nếu PoiPointId = 0 → tạo POI mới
        if (update.PoiPointId == 0)
        {
            poi = new Shared.Models.PoiPoint();
            _db.PoiPoints.Add(poi);
            await _db.SaveChangesAsync(); // Lưu trước để lấy ID

            update.PoiPointId = poi.Id; // Update PoiPointId trong PendingPOIUpdates
        }
        else
        {
            // PoiPointId > 0 → tìm POI hiện có
            poi = await _db.PoiPoints.FindAsync(update.PoiPointId);
            if (poi == null) return NotFound(new { message = "POI không tồn tại." });
        }

        // Apply Payload lên POI
        if (!string.IsNullOrEmpty(update.Payload))
        {
            try
            {
                var payload = JsonDocument.Parse(update.Payload);
                var root = payload.RootElement;

                if (root.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                    poi.Name = name.GetString()!;
                if (root.TryGetProperty("shortDescription", out var desc) && desc.ValueKind == JsonValueKind.String)
                    poi.ShortDescription = desc.GetString()!;
                if (root.TryGetProperty("latitude", out var lat) && lat.ValueKind == JsonValueKind.Number)
                    poi.Latitude = lat.GetDouble();
                if (root.TryGetProperty("longitude", out var lng) && lng.ValueKind == JsonValueKind.Number)
                    poi.Longitude = lng.GetDouble();
                if (root.TryGetProperty("triggerRadiusMeters", out var radius) && radius.ValueKind == JsonValueKind.Number)
                    poi.TriggerRadiusMeters = radius.GetDouble();
                if (root.TryGetProperty("priority", out var prio) && prio.ValueKind == JsonValueKind.Number)
                    poi.Priority = prio.GetInt32();
                if (root.TryGetProperty("imageUrl", out var img) && img.ValueKind == JsonValueKind.String)
                    poi.ImageUrl = img.GetString();
                if (root.TryGetProperty("mapUrl", out var mapUrl) && mapUrl.ValueKind == JsonValueKind.String)
                    poi.MapUrl = mapUrl.GetString();
                if (root.TryGetProperty("isActive", out var active) && active.ValueKind == JsonValueKind.True)
                    poi.IsActive = active.GetBoolean();
            }
            catch { /* payload lỗi → bỏ qua */ }
        }

        poi.UpdatedAt = DateTime.UtcNow;

        // Apply ảnh mới
        if (!string.IsNullOrEmpty(update.ImagesPayload))
        {
            // Xóa ảnh cũ
            var oldImages = await _db.RestaurantImages
                .Where(i => i.PoiPointId == poi.Id)
                .ToListAsync();
            _db.RestaurantImages.RemoveRange(oldImages);

            try
            {
                var images = JsonSerializer.Deserialize<List<ImagePayloadDto>>(update.ImagesPayload);
                if (images != null)
                {
                    foreach (var img in images)
                    {
                        _db.RestaurantImages.Add(new Shared.Models.RestaurantImage
                        {
                            PoiPointId = poi.Id,
                            ImageUrl = img.ImageUrl,
                            IsPrimary = img.IsPrimary,
                            SortOrder = img.SortOrder,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }
            }
            catch { /* images lỗi → bỏ qua */ }
        }

        // Apply scripts mới
        if (!string.IsNullOrEmpty(update.ScriptsPayload))
        {
            try
            {
                var scripts = JsonSerializer.Deserialize<List<ScriptPayloadDto>>(update.ScriptsPayload);
                if (scripts != null)
                {
                    foreach (var sc in scripts)
                    {
                        var existing = await _db.AudioScripts
                            .FirstOrDefaultAsync(a => a.PoiPointId == poi.Id && a.LanguageCode == sc.LanguageCode);

                        if (existing != null)
                        {
                            existing.TtsScript = sc.TtsScript;
                            existing.AudioFileUrl = sc.AudioFileUrl;
                            existing.UpdatedAt = DateTime.UtcNow;
                        }
                        else
                        {
                            _db.AudioScripts.Add(new Shared.Models.AudioScript
                            {
                                PoiPointId = poi.Id,
                                LanguageCode = sc.LanguageCode,
                                TtsScript = sc.TtsScript,
                                AudioFileUrl = sc.AudioFileUrl,
                                UpdatedAt = DateTime.UtcNow
                            });
                        }
                    }
                }
            }
            catch { /* scripts lỗi → bỏ qua */ }
        }

        // Đánh dấu update đã duyệt
        update.Status = "Approved";
        update.AdminNote = req.AdminNote;
        update.ReviewedAt = DateTime.UtcNow;
        update.ReviewedBy = adminEmail;

        await _db.SaveChangesAsync();

        return Ok(new { message = "Đã duyệt và áp dụng thay đổi lên POI.", poiId = poi.Id });
    }

    /// <summary>Từ chối POI update</summary>
    [HttpPost("pending-updates/{id}/reject")]
    public async Task<IActionResult> RejectUpdate(int id, [FromBody] RejectUpdateRequest req)
    {
        var update = await _db.PendingPOIUpdates.FindAsync(id);
        if (update == null) return NotFound();

        var adminEmail = User.FindFirstValue(ClaimTypes.Email) ?? "admin";

        update.Status = "Rejected";
        update.AdminNote = req?.Reason ?? "";
        update.ReviewedAt = DateTime.UtcNow;
        update.ReviewedBy = adminEmail;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Đã từ chối update." });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // STAGING IMAGE MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Danh sách ảnh staging chờ duyệt</summary>
    [HttpGet("staging-images")]
    public async Task<IActionResult> GetStagingImages([FromQuery] string status = "Pending")
    {
        // NOTE: EF Core silently ignores .Include() when followed by .Select() (projection).
        // Fix: force client evaluation with .ToList() BEFORE the .Select() so navigation
        // properties are actually loaded. An alternative would be subquery projections,
        // but .ToList() is simpler and guarantees the Include is honoured.
        var query = _db.StagingImages
            .Include(x => x.Vendor)
            .Include(x => x.PoiPoint)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(x => x.Status == status);

        var stagingList = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        var items = stagingList.Select(x => new StagingImageDto
        {
            Id = x.Id,
            VendorId = x.VendorId,
            VendorName = x.Vendor?.BusinessName ?? "",
            PoiPointId = x.PoiPointId,
            PoiName = x.PoiPoint?.Name ?? "",
            FileName = x.FileName,
            TempUrl = x.TempUrl,
            Status = x.Status,
            CreatedAt = x.CreatedAt
        }).ToList();

        var pendingCount = await _db.StagingImages.CountAsync(x => x.Status == "Pending");
        var approvedTodayCount = await _db.StagingImages.CountAsync(x => 
            x.Status == "Approved" && x.ReviewedAt >= DateTime.UtcNow.Date);
        var rejectedTodayCount = await _db.StagingImages.CountAsync(x => 
            x.Status == "Rejected" && x.ReviewedAt >= DateTime.UtcNow.Date);
        return Ok(new { items, pendingCount, stats = new { approvedToday = approvedTodayCount, rejectedToday = rejectedTodayCount } });
    }

        /// <summary>Duyệt 1 ảnh staging — copy file từ staging → wwwroot/images</summary>
    [HttpPost("staging-images/{id}/approve")]
    public async Task<IActionResult> ApproveStagingImage(int id, [FromBody] ApproveStagingImageRequest req)
    {
        var img = await _db.StagingImages
            .Include(x => x.PoiPoint)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (img == null) return NotFound(new { message = "Anh khong ton tai." });
        if (img.Status != "Pending")
            return BadRequest(new { message = "Anh da duoc xu ly truoc do." });

        var adminEmail = User.FindFirstValue(ClaimTypes.Email) ?? "admin";
        var poiId = req.PoiPointId > 0 ? req.PoiPointId : img.PoiPointId;

        // Tách TempUrl="/staging/poi_1/file.jpg" → lấy phần path sau "/staging/"
        var relPath = img.TempUrl.TrimStart('/');
        var sourcePath = Path.Combine(WwwRoot, relPath);

        if (!System.IO.File.Exists(sourcePath))
            return BadRequest(new { message = "Khong tim thay file: " + img.TempUrl + " (tai: " + sourcePath + ")" });

        var destFolder = Path.Combine(WwwRoot, "images", $"poi_{poiId}");
        Directory.CreateDirectory(destFolder);

        var fileName = Path.GetFileName(sourcePath);
        var destPath = Path.Combine(destFolder, fileName);
        System.IO.File.Copy(sourcePath, destPath, overwrite: true);

        var relativeUrl = $"/images/poi_{poiId}/{fileName}";
        img.Status = "Approved";
        img.ApprovedUrl = relativeUrl;
        img.ReviewedBy = adminEmail;
        img.ReviewedAt = DateTime.UtcNow;

        var isFirst = !await _db.RestaurantImages.AnyAsync(i => i.PoiPointId == poiId);
        _db.RestaurantImages.Add(new Shared.Models.RestaurantImage
        {
            PoiPointId = poiId,
            ImageUrl = relativeUrl,
            IsPrimary = isFirst,
            SortOrder = await _db.RestaurantImages.CountAsync(i => i.PoiPointId == poiId) + 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return Ok(new { message = "Da duyet anh. File da luu vao " + relativeUrl });
    }

    /// <summary>Từ chối 1 ảnh staging</summary>
    [HttpPost("staging-images/{id}/reject")]
    public async Task<IActionResult> RejectStagingImage(int id, [FromBody] RejectUpdateRequest req)
    {
        var img = await _db.StagingImages.FindAsync(id);
        if (img == null) return NotFound(new { message = "Anh khong ton tai." });

        var adminEmail = User.FindFirstValue(ClaimTypes.Email) ?? "admin";
        var filePath = Path.Combine(WwwRoot, img.TempUrl.TrimStart('/'));
        if (System.IO.File.Exists(filePath))
            System.IO.File.Delete(filePath);

        img.Status = "Rejected";
        img.AdminNote = req?.Reason ?? "";
        img.ReviewedBy = adminEmail;
        img.ReviewedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Da tu choi va xoa anh." });
    }
}
