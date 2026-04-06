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
/// Vendor endpoint — yêu cầu Role = Vendor
/// </summary>
[Authorize(Roles = "Vendor")]
[ApiController]
[Route("api/vendor")]
public class VendorController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public VendorController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    private string WwwRoot => Path.Combine(_env.ContentRootPath, "wwwroot");

    /// <summary>Lấy thông tin vendor đang đăng nhập</summary>
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var vendor = await _db.Vendors
            .Include(v => v.PoiPoint)
            .FirstOrDefaultAsync(v => v.UserId == userId);

        if (vendor == null)
            return NotFound(new { message = "Vendor không tồn tại." });

        var user = await _db.Users.FindAsync(userId);

        return Ok(new VendorProfileDto
        {
            Id = vendor.Id,
            BusinessName = vendor.BusinessName,
            OwnerName = vendor.OwnerName,
            Email = user?.Email ?? "",
            Phone = vendor.Phone,
            Address = vendor.Address,
            Status = vendor.Status,
            RejectedReason = vendor.RejectedReason,
            CreatedAt = vendor.CreatedAt,
            Poi = vendor.PoiPointId.HasValue ? new VendorPoiDto
            {
                Id = vendor.PoiPoint!.Id,
                Name = vendor.PoiPoint.Name,
                IconUrl = vendor.PoiPoint.IconUrl,
                ImageUrl = vendor.PoiPoint.ImageUrl
            } : null
        });
    }

    /// <summary>Cập nhật thông tin cá nhân vendor (không cần admin duyệt)</summary>
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateVendorProfileRequest req)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);
        if (vendor == null) return NotFound(new { message = "Vendor không tồn tại." });

        // Cập nhật Vendor record (không cần duyệt)
        vendor.OwnerName = req.OwnerName;
        vendor.Phone = req.Phone;
        vendor.Address = req.Address;
        vendor.UpdatedAt = DateTime.UtcNow;

        // Cập nhật User (Email, FullName) qua UserManager
        var userManager = HttpContext.RequestServices.GetRequiredService<UserManager<AppUser>>();
        var user = await userManager.FindByIdAsync(userId);
        if (user != null)
        {
            // Kiểm tra email mới đã dùng chưa (nếu đổi email)
            if (!string.Equals(user.Email, req.Email, StringComparison.OrdinalIgnoreCase))
            {
                var existingUser = await userManager.FindByEmailAsync(req.Email);
                if (existingUser != null)
                    return BadRequest(new { message = "Email này đã được sử dụng bởi tài khoản khác." });

                await userManager.SetEmailAsync(user, req.Email);
                await userManager.SetUserNameAsync(user, req.Email.Split('@')[0]);
            }
            user.FullName = req.OwnerName;
            await userManager.UpdateAsync(user);
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "Cập nhật thông tin thành công." });
    }

    /// <summary>Gửi yêu cầu cập nhật POI</summary>
    [HttpPost("poi/update")]
    public async Task<IActionResult> SubmitPOIUpdate([FromBody] SubmitPOIUpdateRequest req)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);
        if (vendor == null) return NotFound(new { message = "Vendor không tồn tại." });

        if (vendor.Status != "Approved")
            return BadRequest(new { message = "Tài khoản chưa được duyệt." });

        if (!vendor.PoiPointId.HasValue)
            return BadRequest(new { message = "Bạn chưa được gán quán nào." });

        // Tạo pending update
        var payload = new
        {
            req.Name,
            req.ShortDescription,
            req.IconUrl,
            req.TriggerRadiusMeters,
            req.Priority
        };

        var update = new Shared.Models.PendingPOIUpdate
        {
            VendorId = vendor.Id,
            PoiPointId = vendor.PoiPointId.Value,
            Payload = JsonSerializer.Serialize(payload),
            ImagesPayload = req.Images != null ? JsonSerializer.Serialize(req.Images) : null,
            ScriptsPayload = req.Scripts != null ? JsonSerializer.Serialize(req.Scripts) : null,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        _db.PendingPOIUpdates.Add(update);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Đã gửi yêu cầu cập nhật. Admin sẽ duyệt trong thời gian sớm nhất." });
    }

    /// <summary>Gửi yêu cầu thêm POI mới chờ admin duyệt</summary>
    [HttpPost("pois")]
    public async Task<IActionResult> SubmitNewPOI([FromBody] CreatePoiRequest req)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);
            if (vendor == null) return NotFound(new { message = "Vendor không tồn tại." });

            if (vendor.Status != "Approved")
                return BadRequest(new { message = "Tài khoản chưa được duyệt." });

            // Tạo yêu cầu thêm POI mới, dùng PoiPointId = 0 để đánh dấu là "tạo mới"
            var payload = new
            {
                req.Name,
                req.ShortDescription,
                req.Latitude,
                req.Longitude,
                req.TriggerRadiusMeters,
                req.Priority,
                req.ImageUrl,
                req.MapUrl,
                req.IsActive
            };

            var update = new Shared.Models.PendingPOIUpdate
            {
                VendorId = vendor.Id,
                PoiPointId = 0, // 0 = yêu cầu tạo POI mới
                Payload = JsonSerializer.Serialize(payload),
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _db.PendingPOIUpdates.Add(update);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Đã gửi yêu cầu thêm điểm mới. Admin sẽ xem xét trong thời gian sớm nhất." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = "Lỗi khi xử lý yêu cầu: " + ex.Message });
        }
    }

    /// <summary>Lấy POI mà vendor này được gán</summary>
    [HttpGet("pois")]
    public async Task<IActionResult> GetMyPois()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var vendor = await _db.Vendors
            .Include(v => v.PoiPoint)
            .ThenInclude(p => p!.AudioScripts)
            .Include(v => v.PoiPoint)
            .ThenInclude(p => p!.Images)
            .FirstOrDefaultAsync(v => v.UserId == userId);

        if (vendor == null) return NotFound(new { message = "Vendor không tồn tại." });

        // Vendor chưa được gán POI → trả mảng rỗng
        if (!vendor.PoiPointId.HasValue || vendor.PoiPoint == null)
            return Ok(new List<PoiDto>());

        var poi = vendor.PoiPoint;
        return Ok(new List<PoiDto>
        {
            new PoiDto
            {
                Id = poi.Id,
                Name = poi.Name,
                ShortDescription = poi.ShortDescription,
                Latitude = poi.Latitude,
                Longitude = poi.Longitude,
                TriggerRadiusMeters = poi.TriggerRadiusMeters,
                Priority = poi.Priority,
                IsActive = poi.IsActive,
                ImageUrl = poi.ImageUrl,
                IconUrl = poi.IconUrl,
                MapUrl = poi.MapUrl,
                UpdatedAt = poi.UpdatedAt,
                AudioScripts = poi.AudioScripts.Select(s => new AudioScriptDto
                {
                    Id = s.Id,
                    PoiPointId = s.PoiPointId,
                    LanguageCode = s.LanguageCode,
                    TtsScript = s.TtsScript,
                    AudioFileUrl = s.AudioFileUrl,
                    UpdatedAt = s.UpdatedAt
                }).ToList(),
                Images = poi.Images.Select(i => new RestaurantImageDto
                {
                    Id = i.Id,
                    PoiPointId = i.PoiPointId,
                    ImageUrl = i.ImageUrl,
                    IsPrimary = i.IsPrimary,
                    SortOrder = i.SortOrder
                }).ToList()
            }
        });
    }

    /// <summary>Lấy chi tiết 1 POI của vendor</summary>
    [HttpGet("pois/{id}")]
    public async Task<IActionResult> GetMyPoi(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);
        if (vendor == null) return NotFound();

        // Chỉ cho phép xem POI mà vendor được gán
        if (!vendor.PoiPointId.HasValue || vendor.PoiPointId.Value != id)
            return Forbid();

        var poi = await _db.PoiPoints
            .Include(p => p.AudioScripts)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (poi == null) return NotFound();

        return Ok(new PoiDto
        {
            Id = poi.Id,
            Name = poi.Name,
            ShortDescription = poi.ShortDescription,
            Latitude = poi.Latitude,
            Longitude = poi.Longitude,
            TriggerRadiusMeters = poi.TriggerRadiusMeters,
            Priority = poi.Priority,
            IsActive = poi.IsActive,
            ImageUrl = poi.ImageUrl,
            IconUrl = poi.IconUrl,
            MapUrl = poi.MapUrl,
            UpdatedAt = poi.UpdatedAt,
            AudioScripts = poi.AudioScripts.Select(s => new AudioScriptDto
            {
                Id = s.Id, PoiPointId = s.PoiPointId,
                LanguageCode = s.LanguageCode, TtsScript = s.TtsScript,
                AudioFileUrl = s.AudioFileUrl, UpdatedAt = s.UpdatedAt
            }).ToList(),
            Images = poi.Images.Select(i => new RestaurantImageDto
            {
                Id = i.Id, PoiPointId = i.PoiPointId,
                ImageUrl = i.ImageUrl, IsPrimary = i.IsPrimary, SortOrder = i.SortOrder
            }).ToList()
        });
    }

    /// <summary>Gửi yêu cầu cập nhật POI được gán — chờ admin duyệt</summary>
    [HttpPut("pois/{id}")]
    public async Task<IActionResult> SubmitUpdateMyPoi(int id, [FromBody] UpdatePoiRequest req)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);
            if (vendor == null) return NotFound(new { message = "Vendor không tồn tại." });

            if (vendor.Status != "Approved")
                return BadRequest(new { message = "Tài khoản chưa được duyệt." });

            // Kiểm tra vendor chỉ update POI được gán
            if (!vendor.PoiPointId.HasValue || vendor.PoiPointId.Value != id)
                return BadRequest(new { message = "Bạn không có quyền cập nhật POI này." });

            // Tạo pending update
            var payload = new
            {
                req.Name,
                req.ShortDescription,
                req.Latitude,
                req.Longitude,
                req.TriggerRadiusMeters,
                req.Priority,
                req.ImageUrl,
                req.MapUrl,
                req.IsActive
            };

            var update = new Shared.Models.PendingPOIUpdate
            {
                VendorId = vendor.Id,
                PoiPointId = id,
                Payload = JsonSerializer.Serialize(payload),
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _db.PendingPOIUpdates.Add(update);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Đã gửi yêu cầu cập nhật. Admin sẽ duyệt trong thời gian sớm nhất." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = "Lỗi khi xử lý yêu cầu: " + ex.Message });
        }
    }

    /// <summary>Xem lịch sử submit của mình</summary>
    [HttpGet("updates")]
    public async Task<IActionResult> GetMyUpdates()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);
        if (vendor == null) return NotFound();

        var updates = await _db.PendingPOIUpdates
            .Include(u => u.Vendor)
            .Where(u => u.VendorId == vendor.Id)
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new VendorUpdateHistoryDto
            {
                Id = u.Id,
                PoiName = _db.PoiPoints.Where(p => p.Id == u.PoiPointId).Select(p => p.Name).FirstOrDefault() ?? "",
                Status = u.Status,
                AdminNote = u.AdminNote,
                CreatedAt = u.CreatedAt,
                ReviewedAt = u.ReviewedAt
            })
            .ToListAsync();

        return Ok(updates);
    }

    /// <summary>Lấy danh sách ảnh của 1 POI</summary>
    [HttpGet("pois/{poiId}/images")]
    public async Task<IActionResult> GetPoiImages(int poiId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);
        if (vendor == null) return NotFound();

        // Chỉ cho xem ảnh POI được gán
        if (!vendor.PoiPointId.HasValue || vendor.PoiPointId.Value != poiId)
            return Forbid();

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

        return Ok(images);
    }

    /// <summary>Cập nhật hoặc thêm mới audio script — chỉ cho POI được gán</summary>
    [HttpPut("pois/{poiId}/scripts")]
    public async Task<IActionResult> UpsertMyScript(int poiId, [FromBody] UpsertAudioScriptRequest req)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);
            if (vendor == null) return NotFound(new { message = "Vendor không tồn tại." });

            // Chỉ cho update script POI được gán
            if (!vendor.PoiPointId.HasValue || vendor.PoiPointId.Value != poiId)
                return BadRequest(new { message = "Bạn không có quyền cập nhật script POI này." });

            var poi = await _db.PoiPoints
                .Include(p => p.AudioScripts)
                .FirstOrDefaultAsync(p => p.Id == poiId);

            if (poi == null) return NotFound(new { message = "POI không tồn tại." });

            // Tìm script hiện tại
            var script = poi.AudioScripts.FirstOrDefault(s => s.LanguageCode == req.LanguageCode);

            if (script == null)
            {
                // Tạo mới
                script = new Shared.Models.AudioScript
                {
                    PoiPointId = poiId,
                    LanguageCode = req.LanguageCode,
                    TtsScript = req.TtsScript,
                    AudioFileUrl = req.AudioFileUrl,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.AudioScripts.Add(script);
            }
            else
            {
                // Cập nhật
                script.TtsScript = req.TtsScript;
                script.AudioFileUrl = req.AudioFileUrl;
                script.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            return Ok(new AudioScriptDto
            {
                Id = script.Id,
                PoiPointId = script.PoiPointId,
                LanguageCode = script.LanguageCode,
                TtsScript = script.TtsScript,
                AudioFileUrl = script.AudioFileUrl,
                UpdatedAt = script.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = "Lỗi khi xử lý yêu cầu: " + ex.Message });
        }
    }

    /// <summary>Upload ảnh quán</summary>
    [HttpPost("images/upload")]
    [RequestSizeLimit(20 * 1024 * 1024)] // 20MB
    public async Task<IActionResult> UploadImages([FromForm] int poiId, [FromForm] List<IFormFile> files)
    {
        if (files == null || files.Count == 0)
            return BadRequest(new { message = "Không có ảnh nào được chọn." });

        // Giới hạn 5MB/ảnh
        const long maxSize = 5 * 1024 * 1024;
        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };

        var urls = new List<string>();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);
        if (vendor == null)
            return Unauthorized();

        // Vendor chỉ được upload vào POI được gán
        if (!vendor.PoiPointId.HasValue || vendor.PoiPointId.Value != poiId)
            return BadRequest(new { message = "Bạn không có quyền upload ảnh cho POI này." });

        var poiFolder = Path.Combine(WwwRoot, "images", $"poi_{poiId}");

        foreach (var file in files)
        {
            if (file.Length > maxSize)
                return BadRequest(new { message = $"Ảnh {file.FileName} vượt quá 5MB." });

            var ext = Path.GetExtension(file.FileName).ToLower();
            if (!allowed.Contains(ext))
                return BadRequest(new { message = $"Chỉ chấp nhận: jpg, png, webp." });

            Directory.CreateDirectory(poiFolder);
            var fileName = $"{Guid.NewGuid()}{ext}";
            var path = Path.Combine(poiFolder, fileName);
            await using var stream = new FileStream(path, FileMode.Create);
            await file.CopyToAsync(stream);

            // URL tương đối
            var relativePath = $"/images/poi_{poiId}/{fileName}";

            // Lưu vào bảng RestaurantImages
            var sortOrder = urls.Count + 1;
            var isPrimary = sortOrder == 1;
            _db.RestaurantImages.Add(new Shared.Models.RestaurantImage
            {
                PoiPointId = poiId,
                ImageUrl = relativePath,
                IsPrimary = isPrimary,
                SortOrder = sortOrder,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            urls.Add(relativePath);
        }

        await _db.SaveChangesAsync();
        return Ok(new { urls, message = $"Đã upload {urls.Count} ảnh thành công." });
    }

    /// <summary>Upload ảnh vào staging — chờ admin duyệt mới hiển thị</summary>
    [HttpPost("images/staging")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> UploadStaging([FromForm] int poiId, [FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Không có ảnh nào được chọn." });

        var maxSize = 5 * 1024 * 1024L;
        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };

        if (file.Length > maxSize)
            return BadRequest(new { message = "Ảnh vượt quá 5MB." });

        var ext = Path.GetExtension(file.FileName).ToLower();
        if (!allowed.Contains(ext))
            return BadRequest(new { message = "Chỉ chấp nhận: jpg, png, webp." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);
        if (vendor == null) return Unauthorized();

        if (vendor.Status != "Approved")
            return BadRequest(new { message = "Tài khoản chưa được duyệt." });

        if (!vendor.PoiPointId.HasValue || vendor.PoiPointId.Value != poiId)
            return BadRequest(new { message = "Bạn không có quyền upload ảnh cho POI này." });

        // Lưu vào thư mục staging
        var stagingFolder = Path.Combine(WwwRoot, "staging", $"poi_{poiId}");
        Directory.CreateDirectory(stagingFolder);

        var fileName = $"{Guid.NewGuid()}{ext}";
        var path = Path.Combine(stagingFolder, fileName);
        await using var stream = new FileStream(path, FileMode.Create);
        await file.CopyToAsync(stream);

        var tempUrl = $"/staging/poi_{poiId}/{fileName}";

        // Lưu record vào StagingImages
        var staging = new Shared.Models.StagingImage
        {
            VendorId = vendor.Id,
            PoiPointId = poiId,
            FileName = file.FileName,
            TempUrl = tempUrl,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        _db.StagingImages.Add(staging);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            stagingId = staging.Id,
            tempUrl,
            message = "Ảnh đã được tải lên, đang chờ quản trị viên duyệt."
        });
    }

    /// <summary>Gửi yêu cầu xóa ảnh — chờ admin duyệt mới xóa thật</summary>
    [HttpPost("images/delete-request")]
    public async Task<IActionResult> RequestDeleteImage([FromBody] DeleteImageRequestDto req)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.UserId == userId);
        if (vendor == null) return NotFound();

        if (vendor.Status != "Approved")
            return BadRequest(new { message = "Tài khoản chưa được duyệt." });

        if (!vendor.PoiPointId.HasValue || vendor.PoiPointId.Value != req.PoiPointId)
            return BadRequest(new { message = "Bạn không có quyền xóa ảnh của POI này." });

        var image = await _db.RestaurantImages
            .FirstOrDefaultAsync(i => i.Id == req.ImageId && i.PoiPointId == req.PoiPointId);

        if (image == null)
            return NotFound(new { message = "Ảnh không tồn tại." });

        var existingRequest = await _db.StagingImages
            .AnyAsync(s => s.StagingType == "Deletion"
                        && s.ReferencedImageUrl == image.ImageUrl
                        && s.PoiPointId == req.PoiPointId
                        && s.Status == "Pending");

        if (existingRequest)
            return BadRequest(new { message = "Đã có yêu cầu xóa ảnh này đang chờ duyệt." });

        var staging = new Shared.Models.StagingImage
        {
            VendorId = vendor.Id,
            PoiPointId = req.PoiPointId,
            FileName = Path.GetFileName(image.ImageUrl),
            TempUrl = image.ImageUrl,
            ReferencedImageUrl = image.ImageUrl,
            StagingType = "Deletion",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        _db.StagingImages.Add(staging);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            stagingId = staging.Id,
            message = "Đã gửi yêu cầu xóa ảnh. Quản trị viên sẽ duyệt trong thời gian sớm nhất."
        });
    }
}
