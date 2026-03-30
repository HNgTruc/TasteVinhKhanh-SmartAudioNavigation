using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TasteVinhKhanh.Api.Data;
using TasteVinhKhanh.Api.Services;
using TasteVinhKhanh.Shared.Models;

var builder = WebApplication.CreateBuilder(args);

// ── DATABASE ──────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));

    // Bỏ qua cảnh báo "pending model changes" vì database được tạo thủ công trong SSMS
    opt.ConfigureWarnings(w => w.Ignore(
        Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

// ── IDENTITY ──────────────────────────────────────────────────────────────────
builder.Services.AddIdentity<AppUser, IdentityRole>(opt =>
{
    opt.Password.RequireDigit = true;
    opt.Password.RequiredLength = 8;
    opt.Password.RequireUppercase = false;
    opt.Password.RequireNonAlphanumeric = false;
    opt.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// ── JWT ───────────────────────────────────────────────────────────────────────
var jwt = builder.Configuration.GetSection("JwtSettings");
builder.Services.AddAuthentication(opt =>
{
    opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(opt =>
{
    opt.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwt["Issuer"],
        ValidAudience = jwt["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["SecretKey"]!))
    };
});

builder.Services.AddAuthorization();

// ── SERVICES ──────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPoiService, PoiService>();
builder.Services.AddScoped<ITourService, TourService>();
builder.Services.AddScoped<ISyncService, SyncService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();

// ── CORS — cho phép Admin Blazor và MauiApp gọi ───────────────────────────────
builder.Services.AddCors(opt => opt.AddPolicy("AllowAll",
    p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

// ── SWAGGER ───────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "TasteVinhKhanh API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Nhập: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {{
        new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        },
        Array.Empty<string>()
    }});
});

var app = builder.Build();

// ── INIT DATABASE + SEED ───────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Thử migrate trước — nếu lỗi pending model thì bỏ qua (database tạo thủ công)
    try
    {
        await db.Database.MigrateAsync();
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("PendingModelChangesWarning"))
    {
        Console.WriteLine("⚠️  Bỏ qua migrate — database được tạo thủ công trong SSMS");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️  Lỗi migrate (bỏ qua): {ex.Message}");
    }

    // Tạo role Admin nếu chưa có
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    if (!await roleManager.RoleExistsAsync("Admin"))
        await roleManager.CreateAsync(new IdentityRole("Admin"));

    // Tạo tài khoản admin mặc định nếu chưa có
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var adminEmail = builder.Configuration["AdminSeed:Email"] ?? "admin@vinhkhanh.com";
    if (await userManager.FindByEmailAsync(adminEmail) == null)
    {
        var admin = new AppUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FullName = "Admin",
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(admin,
            builder.Configuration["AdminSeed:Password"] ?? "Admin@12345");
        if (result.Succeeded)
            await userManager.AddToRoleAsync(admin, "Admin");
    }

    // ── SEED POI DATA (nếu bảng trống) ─────────────────────────────────────
    if (!await db.PoiPoints.AnyAsync())
    {
        var pois = new List<PoiPoint>
        {
            new() {
                Name = "Bánh Mì Cô Ba",
                ShortDescription = "Quán bánh mì lâu đời nhất phố Vĩnh Khánh, giá từ 15.000đ",
                Latitude = 10.7567, Longitude = 106.6997,
                TriggerRadiusMeters = 50, Priority = 5,
                IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
                AudioScripts = new List<AudioScript>
                {
                    new() { LanguageCode = "vi", TtsScript = "Chào mừng bạn đến với tiệm Bánh Mì Cô Ba. Đây là một trong những tiệm bánh mì lâu đời và nổi tiếng nhất trên phố Ẩm thực Vĩnh Khánh, Quận 4, Thành phố Hồ Chí Minh." },
                    new() { LanguageCode = "en", TtsScript = "Welcome to Banh Mi Co Ba. This is one of the oldest and most famous banh mi shops on Vinh Khanh Food Street, District 4, Ho Chi Minh City." }
                }
            },
            new() {
                Name = "Hủ Tiếu Nam Vang Số 1",
                ShortDescription = "Nước dùng đậm đà, topping phong phú, phục vụ hơn 30 năm",
                Latitude = 10.7570, Longitude = 106.7002,
                TriggerRadiusMeters = 50, Priority = 4,
                IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
                AudioScripts = new List<AudioScript>
                {
                    new() { LanguageCode = "vi", TtsScript = "Đây là quán Hủ Tiếu Nam Vang số 1, nổi tiếng với nước dùng đậm đà và topping phong phú. Quán đã phục vụ thực khách hơn 30 năm tại con phố Vĩnh Khánh." },
                    new() { LanguageCode = "en", TtsScript = "This is Hu Tieu Nam Vang Number 1, famous for its rich broth and abundant toppings. The shop has served customers for over 30 years on Vinh Khanh street." }
                }
            },
            new() {
                Name = "Cà Phê Vợt Vĩnh Khánh",
                ShortDescription = "Lưu giữ hương vị cà phê truyền thống Sài Gòn, pha chế thủ công",
                Latitude = 10.7573, Longitude = 106.7008,
                TriggerRadiusMeters = 40, Priority = 3,
                IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
                AudioScripts = new List<AudioScript>
                {
                    new() { LanguageCode = "vi", TtsScript = "Quán cà phê vợt Vĩnh Khánh, nơi lưu giữ hương vị cà phê truyền thống Sài Gòn với cách pha chế thủ công độc đáo." },
                    new() { LanguageCode = "en", TtsScript = "Vinh Khanh Coffee Filter stall, preserving the traditional Saigon coffee flavor with a unique handmade brewing method." }
                }
            },
            new() {
                Name = "Bún Bò Huế Vĩnh Khánh",
                ShortDescription = "Bún bò dai sần sật, nước lèo thơm nồng gió heo",
                Latitude = 10.7576, Longitude = 106.7013,
                TriggerRadiusMeters = 50, Priority = 3,
                IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
                AudioScripts = new List<AudioScript>
                {
                    new() { LanguageCode = "vi", TtsScript = "Quán bún bò Huế Vĩnh Khánh với tô bún dai sần sật, nước lèo thơm nồng mùi gió heo đặc trưng." }
                }
            },
            new() {
                Name = "Chè Long Thành",
                ShortDescription = "Chè các loại mát lạnh, topping đầy đặn, mở cửa đến 22h",
                Latitude = 10.7579, Longitude = 106.7018,
                TriggerRadiusMeters = 40, Priority = 2,
                IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
                AudioScripts = new List<AudioScript>
                {
                    new() { LanguageCode = "vi", TtsScript = "Chè Long Thành, quán chè nổi tiếng trên phố Vĩnh Khánh với nhiều loại chè mát lạnh." }
                }
            }
        };

        db.PoiPoints.AddRange(pois);
        await db.SaveChangesAsync();
        Console.WriteLine($"✅ Đã seed {pois.Count} POIs vào database");
    }
    else
    {
        var count = await db.PoiPoints.CountAsync();
        Console.WriteLine($"ℹ️  Database đã có {count} POIs — bỏ qua seed");
    }
}

// ── MIDDLEWARE ────────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
