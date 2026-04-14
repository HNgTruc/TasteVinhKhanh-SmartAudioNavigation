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
        // Dùng UTC+7 (Việt Nam) làm mốc "hôm nay"
        var vnNow = DateTime.UtcNow.AddHours(7);
        var vnTodayStart = vnNow.Date; // 00:00 UTC+7 hôm nay = 17:00 UTC hôm qua

        // Pending: POI updates + StagingImages (upload + deletion)
        var poiPending = await _db.PendingPOIUpdates.CountAsync(x => x.Status == "Pending");
        var imgPending = await _db.StagingImages.CountAsync(x => x.Status == "Pending");
        var delPending = await _db.StagingImages.CountAsync(x => x.StagingType == "Deletion" && x.Status == "Pending");

        // Approved/Rejected hôm nay: POI updates + StagingImages
        var poiApprovedToday = await _db.PendingPOIUpdates
            .CountAsync(x => x.Status == "Approved" && x.ReviewedAt >= vnTodayStart);
        var poiRejectedToday = await _db.PendingPOIUpdates
            .CountAsync(x => x.Status == "Rejected" && x.ReviewedAt >= vnTodayStart);
        var imgApprovedToday = await _db.StagingImages
            .CountAsync(x => x.Status == "Approved" && x.ReviewedAt >= vnTodayStart);
        var imgRejectedToday = await _db.StagingImages
            .CountAsync(x => x.Status == "Rejected" && x.ReviewedAt >= vnTodayStart);

        var stats = new PendingUpdatesStatsDto
        {
            Pending = poiPending + imgPending + delPending,
            ApprovedToday = poiApprovedToday + imgApprovedToday,
            RejectedToday = poiRejectedToday + imgRejectedToday,
            UniquePoiCount = poiPending
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
            else if (status == "Suspended")
                query = query.Where(v => v.Status == "Suspended");
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
            ImageUrl = p.ImageUrl, IconUrl = p.IconUrl, MapUrl = p.MapUrl, LogoUrl = p.LogoUrl,
            UpdatedAt = p.UpdatedAt,
            AudioScripts = p.AudioScripts.Select(s => new AudioScriptDto
            {
                Id = s.Id, PoiPointId = s.PoiPointId,
                LanguageCode = s.LanguageCode,
                TtsScript = s.TtsScript,
                AudioFilePath = s.AudioFilePath, IsAudioUploaded = s.IsAudioUploaded, UpdatedAt = s.UpdatedAt
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

    /// <summary>Ngưng hợp tác vendor (không xóa tài khoản/dữ liệu)</summary>
    [HttpDelete("vendors/{vendorId}")]
    public async Task<IActionResult> DeleteVendor(int vendorId)
    {
        var vendor = await _db.Vendors.FindAsync(vendorId);
        if (vendor == null) return NotFound(new { message = "Vendor không tồn tại." });
        if (vendor.Status == "Suspended")
            return BadRequest(new { message = "Vendor đã ở trạng thái ngưng hợp tác." });

        vendor.Status = "Suspended";
        vendor.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = $"Đã ngưng hợp tác với vendor '{vendor.BusinessName}'." });
    }

    // ── POI UPDATE MANAGEMENT ─────────────────────────────────────────

    /// <summary>Danh sách POI updates chờ duyệt</summary>
    [HttpGet("pending-updates")]
    public async Task<IActionResult> GetPendingUpdates([FromQuery] string status = "Pending")
    {
        status = NormalizeStatusFilter(status, defaultStatus: "Pending");

        var query = _db.PendingPOIUpdates
            .Include(u => u.Vendor)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(u => u.Status == status);
        }
        // status == "" (rỗng) → trả về TẤT CẢ, không filter

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

        // Stats (cho badge) — dùng UTC+7 (Việt Nam) làm mốc "hôm nay"
        var vnNow = DateTime.UtcNow.AddHours(7);
        var vnTodayStart = vnNow.Date;

        // Đếm đủ 3 loại: POI updates + StagingImages upload + StagingImages deletion
        var poiPending = await _db.PendingPOIUpdates.CountAsync(x => x.Status == "Pending");
        var imgPending = await _db.StagingImages.CountAsync(x => x.Status == "Pending");
        var delPending = await _db.StagingImages.CountAsync(x => x.StagingType == "Deletion" && x.Status == "Pending");
        var poiApprovedToday = await _db.PendingPOIUpdates.CountAsync(x => x.Status == "Approved" && x.ReviewedAt >= vnTodayStart);
        var poiRejectedToday = await _db.PendingPOIUpdates.CountAsync(x => x.Status == "Rejected" && x.ReviewedAt >= vnTodayStart);
        var imgApprovedToday = await _db.StagingImages.CountAsync(x => x.Status == "Approved" && x.ReviewedAt >= vnTodayStart);
        var imgRejectedToday = await _db.StagingImages.CountAsync(x => x.Status == "Rejected" && x.ReviewedAt >= vnTodayStart);

        var stats = new PendingUpdatesStatsDto
        {
            Pending = poiPending + imgPending + delPending,
            ApprovedToday = poiApprovedToday + imgApprovedToday,
            RejectedToday = poiRejectedToday + imgRejectedToday,
            UniquePoiCount = poiPending
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

        Shared.Models.PoiPoint? poi;

        // Nếu PoiPointId = 0 → tạo POI mới
        if (update.PoiPointId == 0)
        {
            poi = new Shared.Models.PoiPoint();
            _db.PoiPoints.Add(poi);
            await _db.SaveChangesAsync(); // Lưu trước để lấy ID

            // Gán POI cho vendor để vendor thấy được POI mới
            var vendor = await _db.Vendors.FindAsync(update.VendorId);
            if (vendor != null)
            {
                vendor.PoiPointId = poi.Id;
            }

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

                // Try both camelCase (từ frontend) và PascalCase (từ backend serialize)
                var GetStringValue = (JsonElement el, string camelKey, string pascalKey) =>
                {
                    if (el.TryGetProperty(camelKey, out var val) && val.ValueKind == JsonValueKind.String)
                        return val.GetString();
                    if (el.TryGetProperty(pascalKey, out var val2) && val2.ValueKind == JsonValueKind.String)
                        return val2.GetString();
                    return null;
                };

                var GetNumberValue = (JsonElement el, string camelKey, string pascalKey) =>
                {
                    if (el.TryGetProperty(camelKey, out var val) && val.ValueKind == JsonValueKind.Number)
                        return (double?)val.GetDouble();
                    if (el.TryGetProperty(pascalKey, out var val2) && val2.ValueKind == JsonValueKind.Number)
                        return (double?)val2.GetDouble();
                    return null;
                };

                var GetIntValue = (JsonElement el, string camelKey, string pascalKey) =>
                {
                    if (el.TryGetProperty(camelKey, out var val) && val.ValueKind == JsonValueKind.Number)
                        return (int?)val.GetInt32();
                    if (el.TryGetProperty(pascalKey, out var val2) && val2.ValueKind == JsonValueKind.Number)
                        return (int?)val2.GetInt32();
                    return null;
                };

                var GetBoolValue = (JsonElement el, string camelKey, string pascalKey) =>
                {
                    if (el.TryGetProperty(camelKey, out var val) && (val.ValueKind == JsonValueKind.True || val.ValueKind == JsonValueKind.False))
                        return (bool?)val.GetBoolean();
                    if (el.TryGetProperty(pascalKey, out var val2) && (val2.ValueKind == JsonValueKind.True || val2.ValueKind == JsonValueKind.False))
                        return (bool?)val2.GetBoolean();
                    return null;
                };

                var nameVal = GetStringValue(root, "name", "Name");
                if (!string.IsNullOrEmpty(nameVal))
                    poi.Name = nameVal;

                var descVal = GetStringValue(root, "shortDescription", "ShortDescription");
                if (!string.IsNullOrEmpty(descVal))
                    poi.ShortDescription = descVal;

                var latVal = GetNumberValue(root, "latitude", "Latitude");
                if (latVal.HasValue)
                    poi.Latitude = latVal.Value;

                var lngVal = GetNumberValue(root, "longitude", "Longitude");
                if (lngVal.HasValue)
                    poi.Longitude = lngVal.Value;

                var radiusVal = GetNumberValue(root, "triggerRadiusMeters", "TriggerRadiusMeters");
                if (radiusVal.HasValue)
                    poi.TriggerRadiusMeters = radiusVal.Value;

                var prioVal = GetIntValue(root, "priority", "Priority");
                if (prioVal.HasValue)
                    poi.Priority = prioVal.Value;

                var imgVal = GetStringValue(root, "imageUrl", "ImageUrl");
                if (!string.IsNullOrEmpty(imgVal))
                    poi.ImageUrl = imgVal;

                var mapVal = GetStringValue(root, "mapUrl", "MapUrl");
                if (!string.IsNullOrEmpty(mapVal))
                    poi.MapUrl = mapVal;

                var activeVal = GetBoolValue(root, "isActive", "IsActive");
                if (activeVal.HasValue)
                    poi.IsActive = activeVal.Value;
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
                            existing.UpdatedAt = DateTime.UtcNow;
                        }
                        else
                        {
                            _db.AudioScripts.Add(new Shared.Models.AudioScript
                            {
                                PoiPointId = poi.Id,
                                LanguageCode = sc.LanguageCode,
                                TtsScript = sc.TtsScript,
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

    /// <summary>Danh sách ảnh staging chờ duyệt (chỉ Upload, không gộp Deletion)</summary>
    [HttpGet("staging-images")]
    public async Task<IActionResult> GetStagingImages([FromQuery] string status = "Pending")
    {
        status = NormalizeStatusFilter(status, defaultStatus: "Pending");

        var query = _db.StagingImages
            .Include(x => x.Vendor)
            .Include(x => x.PoiPoint)
            .Where(x => x.StagingType == "Upload")
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(x => x.Status == status);

        var items = (await query.ToListAsync()).Select(x => new StagingImageDto
        {
            Id = x.Id,
            VendorId = x.VendorId,
            VendorName = x.Vendor?.BusinessName ?? "",
            PoiPointId = x.PoiPointId,
            PoiName = x.PoiPoint?.Name ?? "",
            FileName = x.FileName,
            StagingType = x.StagingType,
            PreviewUrl = x.TempUrl,
            ReferencedImageUrl = x.ReferencedImageUrl,
            TempUrl = x.TempUrl,
            Status = x.Status,
            CreatedAt = x.CreatedAt
        }).ToList();

        var pendingCount = await _db.StagingImages.CountAsync(x => x.StagingType == "Upload" && x.Status == "Pending");
        return Ok(new { items, pendingCount });
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

        var relPath = img.TempUrl.TrimStart('/');
        var sourcePath = Path.Combine(WwwRoot, relPath);

        if (!System.IO.File.Exists(sourcePath))
            return BadRequest(new { message = "Khong tim thay file: " + img.TempUrl + " (tai: " + sourcePath + ")" });

        var destFolder = Path.Combine(WwwRoot, "images", $"poi_{poiId}");
        Directory.CreateDirectory(destFolder);

        var fileName = Path.GetFileName(sourcePath);
        var destPath = Path.Combine(destFolder, fileName);
        System.IO.File.Copy(sourcePath, destPath, overwrite: true);

        // ✅ Xóa file staging sau khi đã copy sang /images/
        if (System.IO.File.Exists(sourcePath))
            System.IO.File.Delete(sourcePath);

        var relativeUrl = $"/images/poi_{poiId}/{fileName}";
        img.Status = "Approved";
        img.ApprovedUrl = relativeUrl;
        img.ReviewedBy = adminEmail;
        img.ReviewedAt = DateTime.UtcNow;

        // Thêm ảnh mới vào gallery (KHÔNG xóa ảnh cũ — giữ gallery nhiều ảnh)
        var existingCount = await _db.RestaurantImages.CountAsync(i => i.PoiPointId == poiId);

        _db.RestaurantImages.Add(new Shared.Models.RestaurantImage
        {
            PoiPointId = poiId,
            ImageUrl = relativeUrl,
            IsPrimary = existingCount == 0,  // Chỉ ảnh đầu tiên mới làm ảnh chính
            SortOrder = existingCount + 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // Cập nhật ImageUrl của PoiPoint nếu là ảnh đầu tiên
        if (existingCount == 0)
        {
            var poi = await _db.PoiPoints.FindAsync(poiId);
            if (poi != null)
            {
                poi.ImageUrl = relativeUrl;
                poi.UpdatedAt = DateTime.UtcNow;
                _db.PoiPoints.Update(poi);
            }
        }

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

    // ═══════════════════════════════════════════════════════════════════════════
    // POI IMAGE MANAGEMENT (Admin xem gallery & xóa trực tiếp)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Lấy toàn bộ ảnh đã duyệt của một POI</summary>
    [HttpGet("pois/{poiId}/images")]
    public async Task<IActionResult> GetPoiImages(int poiId)
    {
        var poi = await _db.PoiPoints.FindAsync(poiId);
        if (poi == null) return NotFound(new { message = "POI không tồn tại." });

        var images = await _db.RestaurantImages
            .Where(i => i.PoiPointId == poiId)
            .OrderBy(i => i.SortOrder)
            .Select(i => new RestaurantImageDto
            {
                Id = i.Id,
                PoiPointId = i.PoiPointId,
                ImageUrl = i.ImageUrl,
                IsPrimary = i.IsPrimary,
                SortOrder = i.SortOrder
            })
            .ToListAsync();

        return Ok(new PoiImageGalleryDto
        {
            PoiId = poiId,
            PoiName = poi.Name,
            Images = images
        });
    }

    /// <summary>Xóa trực tiếp một ảnh (admin không cần vendor gửi yêu cầu)</summary>
    [HttpDelete("pois/{poiId}/images/{imageId}")]
    public async Task<IActionResult> DeletePoiImage(int poiId, int imageId)
    {
        var image = await _db.RestaurantImages
            .FirstOrDefaultAsync(i => i.Id == imageId && i.PoiPointId == poiId);

        if (image == null) return NotFound(new { message = "Ảnh không tồn tại." });

        var filePath = Path.Combine(WwwRoot, image.ImageUrl.TrimStart('/'));
        if (System.IO.File.Exists(filePath))
            System.IO.File.Delete(filePath);

        _db.RestaurantImages.Remove(image);

        if (image.IsPrimary)
        {
            var nextPrimary = await _db.RestaurantImages
                .Where(i => i.PoiPointId == poiId && i.Id != imageId)
                .OrderBy(i => i.SortOrder)
                .FirstOrDefaultAsync();

            if (nextPrimary != null)
            {
                nextPrimary.IsPrimary = true;
                _db.RestaurantImages.Update(nextPrimary);
            }

            var poi = await _db.PoiPoints.FindAsync(poiId);
            if (poi != null)
            {
                poi.UpdatedAt = DateTime.UtcNow;
                _db.PoiPoints.Update(poi);
            }
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "Đã xóa ảnh." });
    }

    /// <summary>Thêm ảnh mới vào gallery của POI (admin thêm trực tiếp bằng URL)</summary>
    [HttpPost("pois/{poiId}/images")]
    public async Task<IActionResult> AddPoiImage(int poiId, [FromBody] UpsertImageRequest req)
    {
        var poi = await _db.PoiPoints.FindAsync(poiId);
        if (poi == null) return NotFound(new { message = "POI không tồn tại." });

        if (string.IsNullOrWhiteSpace(req.ImageUrl))
            return BadRequest(new { message = "URL ảnh không được để trống." });

        // Validate: không cho lưu URL rỗng hoặc chỉ có khoảng trắng
        var cleanUrl = req.ImageUrl.Trim();
        if (string.IsNullOrWhiteSpace(cleanUrl) || cleanUrl.Length < 5)
            return BadRequest(new { message = "URL ảnh không hợp lệ." });

        var existingCount = await _db.RestaurantImages
            .CountAsync(i => i.PoiPointId == poiId);

        // Nếu là ảnh đầu tiên → tự động đặt làm ảnh chính
        var isFirst = existingCount == 0;
        var isPrimary = req.IsPrimary || isFirst;

        // Nếu đặt làm ảnh chính → bỏ IsPrimary của ảnh cũ
        if (isPrimary)
        {
            var currentPrimary = await _db.RestaurantImages
                .FirstOrDefaultAsync(i => i.PoiPointId == poiId && i.IsPrimary);
            if (currentPrimary != null)
            {
                currentPrimary.IsPrimary = false;
                _db.RestaurantImages.Update(currentPrimary);
            }
        }

        var newImage = new Shared.Models.RestaurantImage
        {
            PoiPointId = poiId,
            ImageUrl = cleanUrl,
            IsPrimary = isPrimary,
            SortOrder = req.SortOrder > 0 ? req.SortOrder : existingCount + 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.RestaurantImages.Add(newImage);

        // Cập nhật ImageUrl của PoiPoint nếu là ảnh đầu tiên
        if (isFirst)
        {
            poi.ImageUrl = cleanUrl;
            poi.UpdatedAt = DateTime.UtcNow;
            _db.PoiPoints.Update(poi);
        }

        await _db.SaveChangesAsync();

        return Ok(new RestaurantImageDto
        {
            Id = newImage.Id,
            PoiPointId = newImage.PoiPointId,
            ImageUrl = newImage.ImageUrl,
            IsPrimary = newImage.IsPrimary,
            SortOrder = newImage.SortOrder
        });
    }

    /// <summary>Đặt ảnh chính cho POI</summary>
    [HttpPut("pois/{poiId}/images/{imageId}/primary")]
    public async Task<IActionResult> SetPrimaryImage(int poiId, int imageId)
    {
        var poi = await _db.PoiPoints.FindAsync(poiId);
        if (poi == null) return NotFound(new { message = "POI không tồn tại." });

        var target = await _db.RestaurantImages
            .FirstOrDefaultAsync(i => i.Id == imageId && i.PoiPointId == poiId);
        if (target == null) return NotFound(new { message = "Ảnh không tồn tại." });

        // Bỏ IsPrimary của ảnh cũ
        var oldPrimary = await _db.RestaurantImages
            .FirstOrDefaultAsync(i => i.PoiPointId == poiId && i.IsPrimary && i.Id != imageId);
        if (oldPrimary != null)
        {
            oldPrimary.IsPrimary = false;
            _db.RestaurantImages.Update(oldPrimary);
        }

        // Đặt ảnh mới làm chính
        target.IsPrimary = true;
        _db.RestaurantImages.Update(target);

        // Cập nhật ImageUrl của PoiPoint
        poi.ImageUrl = target.ImageUrl;
        poi.UpdatedAt = DateTime.UtcNow;
        _db.PoiPoints.Update(poi);

        await _db.SaveChangesAsync();
        return Ok(new { message = "Đã đặt ảnh chính." });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // STAGING DELETION MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Danh sách yêu cầu xóa ảnh chờ duyệt</summary>
    [HttpGet("staging-images/deletion")]
    public async Task<IActionResult> GetDeletionRequests([FromQuery] string status = "Pending")
    {
        status = NormalizeStatusFilter(status, defaultStatus: "Pending");

        var query = _db.StagingImages
            .Include(x => x.Vendor)
            .Include(x => x.PoiPoint)
            .Where(x => x.StagingType == "Deletion")
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(x => x.Status == status);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new StagingImageDto
            {
                Id = x.Id,
                VendorId = x.VendorId,
                VendorName = x.Vendor != null ? x.Vendor.BusinessName : "",
                PoiPointId = x.PoiPointId,
                PoiName = x.PoiPoint != null ? x.PoiPoint.Name : "",
                FileName = x.FileName,
                StagingType = x.StagingType,
                PreviewUrl = x.ReferencedImageUrl ?? "",
                ReferencedImageUrl = x.ReferencedImageUrl,
                TempUrl = x.TempUrl,
                Status = x.Status,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync();

        var pendingCount = await _db.StagingImages
            .CountAsync(x => x.StagingType == "Deletion" && x.Status == "Pending");

        return Ok(new { items, pendingCount });
    }

    /// <summary>Duyệt yêu cầu xóa ảnh — xóa khỏi RestaurantImages</summary>
    [HttpPost("staging-images/{id}/approve-deletion")]
    public async Task<IActionResult> ApproveDeletionRequest(int id, [FromBody] ApproveDeletionRequestDto req)
    {
        var staging = await _db.StagingImages
            .Include(x => x.PoiPoint)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (staging == null) return NotFound(new { message = "Yêu cầu không tồn tại." });
        if (staging.StagingType != "Deletion")
            return BadRequest(new { message = "Yêu cầu này không phải là yêu cầu xóa ảnh." });
        if (staging.Status != "Pending")
            return BadRequest(new { message = "Yêu cầu đã được xử lý trước đó." });

        var adminEmail = User.FindFirstValue(ClaimTypes.Email) ?? "admin";

        var imageUrl = staging.ReferencedImageUrl;
        var image = await _db.RestaurantImages
            .FirstOrDefaultAsync(i => i.ImageUrl == imageUrl && i.PoiPointId == staging.PoiPointId);

        if (image != null)
        {
            var filePath = Path.Combine(WwwRoot, imageUrl!.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);

            var wasPrimary = image.IsPrimary;
            _db.RestaurantImages.Remove(image);

            if (wasPrimary)
            {
                var nextPrimary = await _db.RestaurantImages
                    .Where(i => i.PoiPointId == staging.PoiPointId)
                    .OrderBy(i => i.SortOrder)
                    .FirstOrDefaultAsync();

                if (nextPrimary != null)
                {
                    nextPrimary.IsPrimary = true;
                    _db.RestaurantImages.Update(nextPrimary);
                }
            }
        }

        staging.Status = "Approved";
        staging.ReviewedBy = adminEmail;
        staging.ReviewedAt = DateTime.UtcNow;
        staging.AdminNote = req?.AdminNote;

        if (staging.PoiPoint != null)
        {
            staging.PoiPoint.UpdatedAt = DateTime.UtcNow;
            _db.PoiPoints.Update(staging.PoiPoint);
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "Đã duyệt yêu cầu xóa ảnh." });
    }

    /// <summary>Từ chối yêu cầu xóa ảnh</summary>
    [HttpPost("staging-images/{id}/reject-deletion")]
    public async Task<IActionResult> RejectDeletionRequest(int id, [FromBody] RejectUpdateRequest req)
    {
        var staging = await _db.StagingImages.FindAsync(id);

        if (staging == null) return NotFound(new { message = "Yêu cầu không tồn tại." });
        if (staging.StagingType != "Deletion")
            return BadRequest(new { message = "Yêu cầu này không phải là yêu cầu xóa ảnh." });
        if (staging.Status != "Pending")
            return BadRequest(new { message = "Yêu cầu đã được xử lý trước đó." });

        var adminEmail = User.FindFirstValue(ClaimTypes.Email) ?? "admin";

        staging.Status = "Rejected";
        staging.AdminNote = req?.Reason ?? "";
        staging.ReviewedBy = adminEmail;
        staging.ReviewedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Đã từ chối yêu cầu xóa ảnh." });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // LOGO MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Admin upload logo trực tiếp (không qua staging) → lưu vào /staging/poi_X/logo
    /// rồi tự động tạo StagingImage để hiện trên trang Duyệt cập nhật.
    /// </summary>
    [HttpPost("staging-images/logo/upload")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> AdminUploadLogo([FromForm] IFormFile file, [FromForm] int poiId)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Không có tệp nào được chọn." });

        const long maxSize = 5 * 1024 * 1024;
        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        if (file.Length > maxSize)
            return BadRequest(new { message = "Logo vượt quá 5MB." });
        var ext = Path.GetExtension(file.FileName).ToLower();
        if (!allowed.Contains(ext))
            return BadRequest(new { message = "Chỉ chấp nhận: jpg, png, webp." });

        var poi = await _db.PoiPoints.FindAsync(poiId);
        if (poi == null)
            return NotFound(new { message = "POI không tồn tại." });

        var adminEmail = User.FindFirstValue(ClaimTypes.Email) ?? "admin";

        // Lưu file vào staging/logo
        var stagingFolder = Path.Combine(WwwRoot, "staging", $"poi_{poiId}", "logo");
        Directory.CreateDirectory(stagingFolder);

        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(stagingFolder, fileName);
        using (var stream = new FileStream(filePath, FileMode.Create))
            await file.CopyToAsync(stream);

        var tempUrl = $"/staging/poi_{poiId}/logo/{fileName}";

        // Xóa staging logo cũ của POI này nếu đang pending
        var oldPending = await _db.StagingImages
            .Where(s => s.PoiPointId == poiId && s.StagingType == "Logo" && s.Status == "Pending")
            .ToListAsync();
        foreach (var old in oldPending)
        {
            var oldPath = Path.Combine(WwwRoot, old.TempUrl.TrimStart('/'));
            if (System.IO.File.Exists(oldPath))
                System.IO.File.Delete(oldPath);
        }

        // 0 = admin upload (VendorId không nullable trong model)
        var staging = new Shared.Models.StagingImage
        {
            VendorId = 0,
            PoiPointId = poiId,
            FileName = file.FileName,
            TempUrl = tempUrl,
            StagingType = "Logo",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        _db.StagingImages.Add(staging);
        await _db.SaveChangesAsync();

        return Ok(new { success = true, stagingId = staging.Id, tempUrl, message = "Logo đã được gửi duyệt." });
    }

    /// <summary>Danh sách logo chờ duyệt (upload + xóa)</summary>
    [HttpGet("staging-images/logo")]
    public async Task<IActionResult> GetPendingLogos([FromQuery] string status = "Pending")
    {
        status = NormalizeStatusFilter(status, defaultStatus: "Pending");

        var query = _db.StagingImages
            .Include(x => x.Vendor)
            .Include(x => x.PoiPoint)
            .Where(x => x.StagingType == "Logo" || x.StagingType == "LogoDeletion")
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(x => x.Status == status);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new StagingImageDto
            {
                Id = x.Id,
                VendorId = x.VendorId,
                VendorName = x.Vendor != null ? x.Vendor.BusinessName : "",
                PoiPointId = x.PoiPointId,
                PoiName = x.PoiPoint != null ? x.PoiPoint.Name : "",
                FileName = x.FileName,
                StagingType = x.StagingType,
                PreviewUrl = x.ReferencedImageUrl ?? x.TempUrl,
                ReferencedImageUrl = x.ReferencedImageUrl,
                TempUrl = x.TempUrl,
                Status = x.Status,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync();

        var pendingCount = await _db.StagingImages
            .CountAsync(x => (x.StagingType == "Logo" || x.StagingType == "LogoDeletion") && x.Status == "Pending");

        return Ok(new { items, pendingCount });
    }

    private static string NormalizeStatusFilter(string? status, string defaultStatus = "Pending")
    {
        if (status is null) return defaultStatus;

        var value = status.Trim();
        if (value.Length == 0) return string.Empty; // empty => no filter (All)
        if (string.Equals(value, "all", StringComparison.OrdinalIgnoreCase)) return string.Empty;
        if (string.Equals(value, "pending", StringComparison.OrdinalIgnoreCase)) return "Pending";
        if (string.Equals(value, "approved", StringComparison.OrdinalIgnoreCase)) return "Approved";
        if (string.Equals(value, "rejected", StringComparison.OrdinalIgnoreCase)) return "Rejected";

        return value;
    }

    /// <summary>Duyệt logo — copy từ staging → /images/poi_X/logo và cập nhật PoiPoint.LogoUrl</summary>
    [HttpPost("staging-images/logo/{id}/approve")]
    public async Task<IActionResult> ApproveLogo(int id, [FromBody] ApproveLogoRequest req)
    {
        var staging = await _db.StagingImages
            .Include(x => x.PoiPoint)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (staging == null) return NotFound(new { message = "Yêu cầu không tồn tại." });
        if (staging.StagingType != "Logo")
            return BadRequest(new { message = "Yêu cầu này không phải là logo." });
        if (staging.Status != "Pending")
            return BadRequest(new { message = "Logo đã được xử lý trước đó." });

        var adminEmail = User.FindFirstValue(ClaimTypes.Email) ?? "admin";
        var poiId = req.PoiPointId > 0 ? req.PoiPointId : staging.PoiPointId;

        var sourcePath = Path.Combine(WwwRoot, staging.TempUrl.TrimStart('/'));
        if (!System.IO.File.Exists(sourcePath))
            return BadRequest(new { message = "Không tìm thấy file: " + staging.TempUrl });

        // Xóa logo cũ (nếu có) trước khi copy logo mới
        var poi = await _db.PoiPoints.FindAsync(poiId);
        if (poi == null) return NotFound(new { message = "POI không tồn tại." });
        if (!string.IsNullOrEmpty(poi.LogoUrl)) {
            var oldPath = Path.Combine(WwwRoot, poi.LogoUrl.TrimStart('/'));
            if (System.IO.File.Exists(oldPath))
                System.IO.File.Delete(oldPath);
        }

        // Copy sang thư mục logo chính thức
        var destFolder = Path.Combine(WwwRoot, "images", $"poi_{poiId}", "logo");
        Directory.CreateDirectory(destFolder);

        var fileName = Path.GetFileName(sourcePath);
        var destPath = Path.Combine(destFolder, fileName);
        System.IO.File.Copy(sourcePath, destPath, overwrite: true);

        // Xóa file staging sau khi copy
        System.IO.File.Delete(sourcePath);

        var relativeUrl = $"/images/poi_{poiId}/logo/{fileName}";

        // Cập nhật LogoUrl của POI
        poi.LogoUrl = relativeUrl;
        poi.UpdatedAt = DateTime.UtcNow;
        _db.PoiPoints.Update(poi);

        staging.Status = "Approved";
        staging.ApprovedUrl = relativeUrl;
        staging.ReviewedBy = adminEmail;
        staging.ReviewedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new { message = "Đã duyệt logo.", logoUrl = relativeUrl });
    }

    /// <summary>Từ chối logo</summary>
    [HttpPost("staging-images/logo/{id}/reject")]
    public async Task<IActionResult> RejectLogo(int id, [FromBody] RejectUpdateRequest req)
    {
        var staging = await _db.StagingImages
            .Include(x => x.PoiPoint)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (staging == null) return NotFound(new { message = "Yêu cầu không tồn tại." });
        if (staging.StagingType != "Logo")
            return BadRequest(new { message = "Yêu cầu này không phải là logo." });
        if (staging.Status != "Pending")
            return BadRequest(new { message = "Logo đã được xử lý trước đó." });

        var adminEmail = User.FindFirstValue(ClaimTypes.Email) ?? "admin";

        // Xóa file staging
        var filePath = Path.Combine(WwwRoot, staging.TempUrl.TrimStart('/'));
        if (System.IO.File.Exists(filePath))
            System.IO.File.Delete(filePath);

        staging.Status = "Rejected";
        staging.AdminNote = req?.Reason ?? "";
        staging.ReviewedBy = adminEmail;
        staging.ReviewedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new { message = "Đã từ chối logo." });
    }

    /// <summary>Duyệt yêu cầu xóa logo — xóa file logo khỏi disk và đặt LogoUrl = null</summary>
    [HttpPost("staging-images/logo/{id}/approve-deletion")]
    public async Task<IActionResult> ApproveLogoDeletion(int id)
    {
        var staging = await _db.StagingImages
            .Include(x => x.PoiPoint)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (staging == null) return NotFound(new { message = "Yêu cầu không tồn tại." });
        if (staging.StagingType != "LogoDeletion")
            return BadRequest(new { message = "Yêu cầu này không phải là yêu cầu xóa logo." });
        if (staging.Status != "Pending")
            return BadRequest(new { message = "Yêu cầu đã được xử lý trước đó." });

        var adminEmail = User.FindFirstValue(ClaimTypes.Email) ?? "admin";

        // Xóa file logo vật lý khỏi disk
        if (!string.IsNullOrEmpty(staging.ReferencedImageUrl)) {
            var filePath = Path.Combine(WwwRoot, staging.ReferencedImageUrl.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
        }

        // Cập nhật LogoUrl = null
        var poi = await _db.PoiPoints.FindAsync(staging.PoiPointId);
        if (poi != null) {
            poi.LogoUrl = null;
            poi.UpdatedAt = DateTime.UtcNow;
            _db.PoiPoints.Update(poi);
        }

        staging.Status = "Approved";
        staging.ReviewedBy = adminEmail;
        staging.ReviewedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new { message = "Đã xóa logo." });
    }

    /// <summary>Từ chối yêu cầu xóa logo — logo giữ nguyên</summary>
    [HttpPost("staging-images/logo/{id}/reject-deletion")]
    public async Task<IActionResult> RejectLogoDeletion(int id, [FromBody] RejectUpdateRequest req)
    {
        var staging = await _db.StagingImages
            .FirstOrDefaultAsync(x => x.Id == id);

        if (staging == null) return NotFound(new { message = "Yêu cầu không tồn tại." });
        if (staging.StagingType != "LogoDeletion")
            return BadRequest(new { message = "Yêu cầu này không phải là yêu cầu xóa logo." });
        if (staging.Status != "Pending")
            return BadRequest(new { message = "Yêu cầu đã được xử lý trước đó." });

        var adminEmail = User.FindFirstValue(ClaimTypes.Email) ?? "admin";

        staging.Status = "Rejected";
        staging.AdminNote = req?.Reason ?? "";
        staging.ReviewedBy = adminEmail;
        staging.ReviewedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new { message = "Đã từ chối yêu cầu xóa logo." });
    }
}
