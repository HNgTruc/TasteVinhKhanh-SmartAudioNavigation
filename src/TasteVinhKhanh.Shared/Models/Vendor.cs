namespace TasteVinhKhanh.Shared.Models;

/// <summary>
/// Vendor — tài khoản quản lý quán ăn (1 vendor = 1 quán)
/// </summary>
public class Vendor
{
    public int Id { get; set; }

    /// <summary>AspNetUsers.Id</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>POI mà vendor này quản lý (null khi chưa gán)</summary>
    public int? PoiPointId { get; set; }

    /// <summary>Tên quán</summary>
    public string BusinessName { get; set; } = string.Empty;

    /// <summary>Tên chủ quán</summary>
    public string OwnerName { get; set; } = string.Empty;

    /// <summary>Số điện thoại</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>Địa chỉ</summary>
    public string? Address { get; set; }

    /// <summary>Trạng thái: Pending | Approved | Rejected</summary>
    public string Status { get; set; } = "Pending";

    /// <summary>Lý do từ chối (nếu có)</summary>
    public string? RejectedReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public PoiPoint? PoiPoint { get; set; }
}
