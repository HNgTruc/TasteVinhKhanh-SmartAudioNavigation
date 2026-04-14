using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using TasteVinhKhanh.Api.Data;
using TasteVinhKhanh.Shared.DTOs;
using TasteVinhKhanh.Shared.Models;

namespace TasteVinhKhanh.Api.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    Task<(bool Success, string Message)> VendorRegisterAsync(VendorRegisterRequest request);
    Task<bool> IsVendorApprovedAsync(string email);
    Task<string?> GetVendorStatusByEmailAsync(string email);
    Task<(bool Success, string Message)> ResetVendorPasswordAsync(VendorForgotPasswordRequest request);
    Task<DeviceTokenResponse> GetOrCreateDeviceTokenAsync(string deviceId);
}

public class AuthService : IAuthService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;

    public AuthService(UserManager<AppUser> userManager, IConfiguration config, AppDbContext db)
    {
        _userManager = userManager;
        _config = config;
        _db = db;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null) return null;

        var isValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!isValid) return null;

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "User";

        // Lấy VendorId nếu là Vendor
        int? vendorId = null;
        if (role == "Vendor")
        {
            var vendor = _db.Vendors.FirstOrDefault(v => v.UserId == user.Id);
            vendorId = vendor?.Id;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, role),
        };
        if (vendorId.HasValue)
            claims.Add(new Claim("VendorId", vendorId.Value.ToString()));

        var jwt = _config.GetSection("JwtSettings");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["SecretKey"]!));
        var expires = DateTime.UtcNow.AddMinutes(int.Parse(jwt["ExpiresInMinutes"]!));

        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );

        return new LoginResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAt = expires,
            UserName = user.FullName,
            Email = user.Email!,
            Role = role,
            VendorId = vendorId
        };
    }

    public async Task<(bool Success, string Message)> VendorRegisterAsync(VendorRegisterRequest request)
    {
        // Kiểm tra email đã tồn tại chưa
        if (await _userManager.FindByEmailAsync(request.Email) != null)
            return (false, "Email đã được sử dụng.");

        // Tạo AspNetUsers
        var user = new AppUser
        {
            UserName = request.Email.Split('@')[0],
            Email = request.Email,
            EmailConfirmed = true,
            FullName = request.OwnerName
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var msg = string.Join("; ", result.Errors
                .Select(e => e.Description)
                .Select(desc => desc switch
                {
                    var d when d.Contains("Password") || d.Contains("password") || d.Contains("digit") || d.Contains("length")
                        => "Mật khẩu phải có ít nhất 8 kí tự và chứa ít nhất 1 chữ số.",
                    var d when d.Contains("email", StringComparison.OrdinalIgnoreCase) || d.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                        => "Email đã được sử dụng.",
                    _ => desc
                }));
            return (false, msg);
        }

        // Gán role Vendor
        await _userManager.AddToRoleAsync(user, "Vendor");

        // Tạo Vendor record (Status = Pending — chờ admin duyệt)
        var vendor = new Vendor
        {
            UserId = user.Id,
            BusinessName = request.BusinessName,
            OwnerName = request.OwnerName,
            Phone = request.Phone,
            Address = request.Address,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Vendors.Add(vendor);
        await _db.SaveChangesAsync();

        return (true, "Đăng ký thành công! Tài khoản của bạn đang chờ được duyệt.");
    }

    public async Task<bool> IsVendorApprovedAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null) return false;
        var vendor = _db.Vendors.FirstOrDefault(v => v.UserId == user.Id);
        return vendor?.Status == "Approved";
    }

    public async Task<string?> GetVendorStatusByEmailAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null) return null;
        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.UserId == user.Id);
        return vendor?.Status;
    }

    public async Task<(bool Success, string Message)> ResetVendorPasswordAsync(VendorForgotPasswordRequest request)
    {
        var email = request.Email?.Trim() ?? string.Empty;
        var phone = request.Phone?.Trim() ?? string.Empty;
        var newPassword = request.NewPassword ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(newPassword))
            return (false, "Vui lòng nhập đầy đủ Email, Số điện thoại và Mật khẩu mới.");

        if (newPassword.Length < 8 || !newPassword.Any(char.IsDigit))
            return (false, "Mật khẩu mới phải có ít nhất 8 kí tự và chứa ít nhất 1 chữ số.");

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
            return (false, "Không tìm thấy tài khoản với email này.");

        var vendor = _db.Vendors.FirstOrDefault(v => v.UserId == user.Id);
        if (vendor == null)
            return (false, "Tài khoản không phải vendor.");

        if (!string.Equals(vendor.Phone?.Trim(), phone, StringComparison.Ordinal))
            return (false, "Số điện thoại xác minh không đúng.");

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            var msg = string.Join("; ", result.Errors
                .Select(e => e.Description)
                .Select(desc => desc switch
                {
                    var d when d.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
                               d.Contains("digit", StringComparison.OrdinalIgnoreCase) ||
                               d.Contains("length", StringComparison.OrdinalIgnoreCase)
                        => "Mật khẩu mới phải có ít nhất 8 kí tự và chứa ít nhất 1 chữ số.",
                    _ => desc
                }));
            return (false, msg);
        }

        return (true, "Khôi phục mật khẩu thành công. Bạn có thể đăng nhập lại.");
    }

    /// <summary>
    /// Device tự động đăng ký để lấy JWT token.
    /// Tạo tài khoản ảo "device_{deviceId}@system" không có password.
    /// Token không có expiry (hoặc expiry rất dài).
    /// </summary>
    public async Task<DeviceTokenResponse> GetOrCreateDeviceTokenAsync(string deviceId)
    {
        // Tìm user device hoặc tạo mới
        var deviceEmail = $"device_{deviceId}@tastevinhkhanh.local";
        var user = await _userManager.FindByEmailAsync(deviceEmail);

        if (user == null)
        {
            user = new AppUser
            {
                UserName = $"device_{deviceId}",
                Email = deviceEmail,
                EmailConfirmed = true,
                FullName = $"Device {deviceId[..8]}"
            };
            var result = await _userManager.CreateAsync(user);
            if (!result.Succeeded)
            {
                // Thử tìm lại (có thể race condition)
                user = await _userManager.FindByEmailAsync(deviceEmail);
                if (user == null)
                    throw new InvalidOperationException("Cannot create device user: " +
                        string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        // Tạo token với role = Device
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.Role, "Device"),
            new("DeviceId", deviceId)
        };

        var jwt = _config.GetSection("JwtSettings");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["SecretKey"]!));
        // Device token: expiry 1 năm
        var expires = DateTime.UtcNow.AddDays(365);

        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );

        return new DeviceTokenResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAt = expires
        };
    }
}
