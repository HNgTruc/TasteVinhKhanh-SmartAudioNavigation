namespace TasteVinhKhanh.Shared.Models;

/// <summary>
/// Giao dịch thanh toán phí hợp tác của vendor.
/// </summary>
public class VendorPayment
{
    public int Id { get; set; }

    public int VendorId { get; set; }

    /// <summary>Số tiền vendor chuyển khoản.</summary>
    public decimal Amount { get; set; }

    /// <summary>Tên ngân hàng chuyển.</summary>
    public string BankName { get; set; } = string.Empty;

    /// <summary>Mã giao dịch / nội dung đối soát.</summary>
    public string TransactionId { get; set; } = string.Empty;

    /// <summary>Số tài khoản nhận tiền (phía admin) trong đơn thanh toán.</summary>
    public string ReceiverAccountNumber { get; set; } = string.Empty;

    /// <summary>Tên chủ tài khoản nhận tiền (phía admin).</summary>
    public string ReceiverAccountName { get; set; } = string.Empty;

    /// <summary>Tên ngân hàng nhận tiền (phía admin).</summary>
    public string ReceiverBankName { get; set; } = string.Empty;

    /// <summary>Loại ngân hàng nhận tiền: Nội địa/Quốc tế/Ngân hàng số...</summary>
    public string ReceiverBankType { get; set; } = string.Empty;

    /// <summary>Đường dẫn biên lai trong wwwroot/payments.</summary>
    public string ReceiptUrl { get; set; } = string.Empty;

    /// <summary>Trạng thái: Unpaid | PendingVerification | Paid.</summary>
    public string Status { get; set; } = "Unpaid";

    /// <summary>Hạn thanh toán của đơn (nếu có).</summary>
    public DateTime? DueDate { get; set; }

    public string? Note { get; set; }
    public string? AdminNote { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Vendor Vendor { get; set; } = null!;
}
