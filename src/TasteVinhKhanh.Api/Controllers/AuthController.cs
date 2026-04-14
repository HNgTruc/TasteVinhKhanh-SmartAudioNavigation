using Microsoft.AspNetCore.Mvc;
using TasteVinhKhanh.Api.Data;
using TasteVinhKhanh.Api.Services;
using TasteVinhKhanh.Shared.DTOs;

namespace TasteVinhKhanh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>Đăng nhập — trả JWT token</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _auth.LoginAsync(request);
        if (result == null)
            return Unauthorized(new { message = "Email hoặc mật khẩu không đúng" });
        return Ok(result);
    }

    /// <summary>Đăng ký tài khoản vendor (public)</summary>
    [HttpPost("vendor-register")]
    public async Task<IActionResult> VendorRegister([FromBody] VendorRegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.BusinessName) ||
            string.IsNullOrWhiteSpace(request.OwnerName) ||
            string.IsNullOrWhiteSpace(request.Phone))
            return BadRequest(new { message = "Vui lòng điền đầy đủ thông tin bắt buộc." });

        var result = await _auth.VendorRegisterAsync(request);
        if (!result.Success)
            return BadRequest(new { message = result.Message });

        return Ok(new { message = result.Message });
    }

    /// <summary>Đăng nhập vendor</summary>
    [HttpPost("vendor-login")]
    public async Task<IActionResult> VendorLogin([FromBody] VendorLoginRequest request)
    {
        var result = await _auth.LoginAsync(new LoginRequest
        {
            Email = request.Email,
            Password = request.Password
        });

        if (result == null)
            return Unauthorized(new { message = "Email hoặc mật khẩu không đúng." });

        if (result.Role != "Vendor")
            return StatusCode(403, new { message = "Tài khoản không có quyền truy cập vendor." });

        var vendorStatus = await _auth.GetVendorStatusByEmailAsync(request.Email);
        if (vendorStatus == "Suspended")
            return StatusCode(403, new { message = "Tài khoản vendor đã ngưng hợp tác. Vui lòng liên hệ quản trị viên." });

        // Kiểm tra vendor đã được admin duyệt chưa
        var vendorApproved = await _auth.IsVendorApprovedAsync(request.Email);
        if (!vendorApproved)
            return StatusCode(403, new { message = "Tài khoản của bạn đang chờ được duyệt. Vui lòng liên hệ quản trị viên." });

        return Ok(result);
    }

    /// <summary>Vendor quên mật khẩu — xác minh email + số điện thoại để đặt lại mật khẩu</summary>
    [HttpPost("vendor-forgot-password")]
    public async Task<IActionResult> VendorForgotPassword([FromBody] VendorForgotPasswordRequest request)
    {
        var result = await _auth.ResetVendorPasswordAsync(request);
        if (!result.Success)
            return BadRequest(new { message = result.Message });
        return Ok(new { message = result.Message });
    }

    /// <summary>
    /// Device đăng ký để lấy JWT token — dùng cho MAUI app tải audio.
    /// Device tự gửi deviceId (GUID) → server trả token không expiry.
    /// </summary>
    [HttpPost("device-register")]
    public async Task<IActionResult> DeviceRegister([FromBody] DeviceRegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId))
            return BadRequest(new { message = "DeviceId is required." });

        var result = await _auth.GetOrCreateDeviceTokenAsync(request.DeviceId);
        return Ok(result);
    }
}
