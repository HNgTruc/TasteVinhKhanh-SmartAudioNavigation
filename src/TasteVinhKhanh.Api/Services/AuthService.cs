using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using TasteVinhKhanh.Api.Data;
using TasteVinhKhanh.Shared.DTOs;

namespace TasteVinhKhanh.Api.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
}

public class AuthService : IAuthService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IConfiguration _config;

    public AuthService(UserManager<AppUser> userManager, IConfiguration config)
    {
        _userManager = userManager;
        _config = config;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        // Tìm user theo email
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null) return null;

        // Kiểm tra mật khẩu
        var isValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!isValid) return null;

        // Lấy danh sách role để đưa vào token
        var roles = await _userManager.GetRolesAsync(user);

        // Tạo claims
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.Name, user.FullName),
        };
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        // Ký JWT
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
            Email = user.Email!
        };
    }
}
