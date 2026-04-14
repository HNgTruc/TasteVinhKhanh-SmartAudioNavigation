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
builder.Services.AddScoped<IAudioStorageService, AudioStorageService>();
builder.Services.AddScoped<ITtsGenerationService, TtsGenerationService>();

// TTS & audio HTTP clients
builder.Services.AddHttpClient("tts").ConfigureHttpClient(c =>
{
    c.Timeout = TimeSpan.FromSeconds(15);
    c.DefaultRequestHeaders.Add("User-Agent", "TasteVinhKhanh/1.0");
});
builder.Services.AddHttpClient("azure-tts").ConfigureHttpClient(c =>
{
    c.Timeout = TimeSpan.FromSeconds(20);
});

// ── CORS ───────────────────────────────────────────────────────────────────────
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

    // Thử migrate — nếu lỗi pending model thì bỏ qua (database tạo thủ công)
    try { await db.Database.MigrateAsync(); }
    catch (InvalidOperationException) { Console.WriteLine("⚠️  Bỏ qua migrate — database tạo thủ công trong SSMS"); }
    catch (Exception ex) { Console.WriteLine($"⚠️  Lỗi migrate (bỏ qua): {ex.Message}"); }

    // Tạo role Admin và Vendor nếu chưa có
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    if (!await roleManager.RoleExistsAsync("Admin"))
        await roleManager.CreateAsync(new IdentityRole("Admin") { NormalizedName = "ADMIN" });
    if (!await roleManager.RoleExistsAsync("Vendor"))
        await roleManager.CreateAsync(new IdentityRole("Vendor") { NormalizedName = "VENDOR" });

    // Tạo tài khoản admin mặc định
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var adminEmail = builder.Configuration["AdminSeed:Email"] ?? "admin@vinhkhanh.com";
    if (await userManager.FindByEmailAsync(adminEmail) == null)
    {
        var admin = new AppUser { UserName = adminEmail, Email = adminEmail, FullName = "Admin", EmailConfirmed = true };
        var result = await userManager.CreateAsync(admin, builder.Configuration["AdminSeed:Password"] ?? "Admin@12345");
        if (result.Succeeded)
            await userManager.AddToRoleAsync(admin, "Admin");
    }

    // Seed 12 tài khoản vendor
    await SeedVendorAccountsAsync(userManager, roleManager, db);
}

// ── SEED 12 POI MẶC ĐỊNH (nếu bảng trống) ─────────────────────────────
using (var scope2 = app.Services.CreateScope())
{
    var db = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
    if (!await db.PoiPoints.AnyAsync())
    {
        var pois = new List<PoiPoint>
        {
            new() { Id = 1,  Name = "Alo Quán",                   ShortDescription = "333 Vĩnh Khánh – Quán ăn đa dạng: hải sản tươi, nướng, lẩu. Không gian rộng rãi, thực đơn phong phú hơn 50 món ăn. Bắt buộc thử: bò cuốn kimchi, ba chỉ cuộn giòn và hải sản nướng.", Latitude = 10.7607671, Longitude = 106.7036279, TriggerRadiusMeters = 50, Priority = 10, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = 2,  Name = "THÈM NƯỚNG YAKINIKU",      ShortDescription = "122 Vĩnh Khánh – Quán nướng Nhật cao cấp, thịt bò Wagyu tươi sống nướng ngay tại bàn. Không gian hiện đại, phù hợp nhóm bạn và gia đình. Trải nghiệm ẩm thực Nhật chính hiệu tại trung tâm phố Vĩnh Khánh.", Latitude = 10.7607671, Longitude = 106.7036279, TriggerRadiusMeters = 50, Priority = 9, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = 3,  Name = "Chilli Lẩu Nướng Quán",      ShortDescription = "232 Vĩnh Khánh – Quán lẩu Thái, lẩu Hàn, hơn 50 món tươi mỗi ngày. Đa dạng nước dùng: lẩu Thái chua cay, lẩu kimchi Hàn Quốc. Không gian rộng rãi, giá cả phải chăng.", Latitude = 10.7606591, Longitude = 106.7037663, TriggerRadiusMeters = 50, Priority = 9, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = 4,  Name = "A FAT HOT POT",               ShortDescription = "668 Vĩnh Khánh – Quán lẩu hot nhất phố, phong cách Hong Kong độc đáo. Thực đơn: lẩu Tứ Xuyên cay nồng, lẩu Tomyum chua thơm và lẩu sữa bơ. Nguyên liệu hải sản tươi sống cập nhật mỗi ngày.", Latitude = 10.7606578, Longitude = 106.7037689, TriggerRadiusMeters = 50, Priority = 8, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = 5,  Name = "Lãng Quán",                  ShortDescription = "531 Vĩnh Khánh – Quán nướng lẩu hơn 40 món từ giòn rụm đến hải sản nướng. Không gian rộng, phục vụ khuya, cuối tuần luôn đông khách. Địa điểm quen thuộc của dân Sài Gòn mê ẩm thực.", Latitude = 10.7610569, Longitude = 106.7053027, TriggerRadiusMeters = 50, Priority = 8, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = 6,  Name = "Lẩu Nướng Thuận Việt",       ShortDescription = "424 Vĩnh Khánh – Quán lẩu miền Trung với hương vị đậm đà. Thực đơn đa dạng, món từ 30,000đ. Điểm nhấn là nước dùng đậm đà và công thức nước chấm gia truyền. Lựa chọn tuyệt vời cho ngân sách tiết kiệm.", Latitude = 10.7615, Longitude = 106.7060, TriggerRadiusMeters = 50, Priority = 7, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = 7,  Name = "Ốc Hoa Kiều",                ShortDescription = "598 Vĩnh Khánh – Quán ốc hơn 30 loại tươi: hấp, xào, nướng, lẩu. Hải sản nhập từ biển mỗi sáng. Bắt buộc thử: ốc bươu rang muối, ốc len xào dừa và càng cua rang me. Quán lâu đời trên phố Vĩnh Khánh.", Latitude = 10.7620, Longitude = 106.7065, TriggerRadiusMeters = 50, Priority = 7, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = 8,  Name = "RONGbuffet",                  ShortDescription = "122 Vĩnh Khánh – Quán buffet hải sản cao cấp hơn 80 món tươi, chỉ từ 199,000đ. Khu vực nướng trong nhà tiện nghi. Hải sản tươi: tôm, cua, ghẹ, nghêu và ốc đặc biệt. Trải nghiệm buffet hải sản ngon nhất phố Vĩnh Khánh.", Latitude = 10.7625, Longitude = 106.7070, TriggerRadiusMeters = 50, Priority = 6, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = 9,  Name = "SHAOKAO",                    ShortDescription = "424 Vĩnh Khánh – Quán nướng Trung-Việt độc đáo, kết hợp tinh hoa ẩm thực hai nền ẩm thực. Các món nướng được ướp theo công thức gia truyền. Điểm nhấn là không gian ngoài trời thoáng mát, phù hợp tiệc lớn.", Latitude = 10.7630, Longitude = 106.7075, TriggerRadiusMeters = 50, Priority = 6, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = 10, Name = "Lẩu Gà Lá É",                ShortDescription = "18 Vĩnh Khánh – Quán chuyên lẩu gà nấu với nước dùng từ lá thảo mộc đặc trưng. Gà ta tự nhiên, thịt dai ngon. Ngoài ra có gà nướng, gà xào và các món gà miền Trung đặc sắc. Không gian rộng phù hợp gia đình và nhóm bạn.", Latitude = 10.7635, Longitude = 106.7080, TriggerRadiusMeters = 50, Priority = 5, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = 11, Name = "BONA Food and Beer",          ShortDescription = "122 Vĩnh Khánh – Quán ăn địa phương đa dạng từ hải sản đến các món Việt cổ điển. Điểm nhấn: không gian thoáng mát, giá cả hợp lý, phục vụ khuya. Các món ốc và hải sản cập nhật mỗi ngày. Điểm dừng chân lý tưởng khuya trên phố Vĩnh Khánh.", Latitude = 10.7640, Longitude = 106.7085, TriggerRadiusMeters = 50, Priority = 4, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = 12, Name = "SINZIEN Quán Nước",           ShortDescription = "375 Vĩnh Khánh – Quán nước giải khát nằm dọc phố ẩm thực, là điểm dừng chân lý tưởng sau bữa ăn. Thực đơn đa dạng từ sinh tố, nước ép trái cây đến các loại trà và cà phê. Không gian mát mẻ, phục vụ nhanh chóng.", Latitude = 10.7617, Longitude = 106.7022, TriggerRadiusMeters = 40, Priority = 3, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };
        db.PoiPoints.AddRange(pois);
        await db.SaveChangesAsync();
        Console.WriteLine($"✅ Đã seed {pois.Count} POIs mặc định vào database");
    }
    else
    {
        Console.WriteLine($"ℹ️  Database đã có POIs — bỏ qua seed POI mặc định");
    }
}

// ── MIDDLEWARE ────────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Cho phép serve file ảnh từ wwwroot (staging images + approved images)
app.UseStaticFiles();

app.UseCors("AllowAll");

// ⚠️ CORS preflight (OPTIONS) cần được trả lời TRƯỚC khi đi qua Authentication.
// Nếu không có middleware này, browser gửi Authorization header
// sẽ trigger preflight OPTIONS → server 401 → GET không bao giờ được gửi.
app.Use(async (context, next) =>
{
    if (context.Request.Method == "OPTIONS")
    {
        context.Response.StatusCode = 200;
        context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
        context.Response.Headers.Append("Access-Control-Allow-Methods", "GET,POST,PUT,DELETE,OPTIONS");
        context.Response.Headers.Append("Access-Control-Allow-Headers", "Content-Type,Authorization");
        await context.Response.CompleteAsync();
        return;
    }
    await next();
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// ═══════════════════════════════════════════════════════════════════════════════
// HELPER: Seed 12 tài khoản vendor
// ═══════════════════════════════════════════════════════════════════════════════
static async Task SeedVendorAccountsAsync(
    UserManager<AppUser> userManager,
    RoleManager<IdentityRole> roleManager,
    AppDbContext db)
{
    // Nếu Vendors table trống → tạo 12 vendor
    if (await db.Vendors.AnyAsync())
    {
        Console.WriteLine("ℹ️  Vendor accounts đã tồn tại — bỏ qua seed vendor");
        return;
    }

    const string defaultPwd = "VinhKhanh123";

    var vendors = new[]
    {
        (user: "vendor_aloquan@vinhkhanh.com",     business: "Alo Quán",                   owner: "Nguyễn Văn A",  phone: "0901234561", poiId: 1),
        (user: "vendor_yakiniku@vinhkhanh.com",    business: "THÈM NƯỚNG YAKINIKU",       owner: "Nguyễn Văn B",  phone: "0901234562", poiId: 2),
        (user: "vendor_chilli@vinhkhanh.com",       business: "Chilli Lẩu Nướng Quán",        owner: "Nguyễn Văn C",  phone: "0901234563", poiId: 3),
        (user: "vendor_afat@vinhkhanh.com",         business: "A FAT HOT POT",               owner: "Nguyễn Văn D",  phone: "0901234564", poiId: 4),
        (user: "vendor_langquan@vinhkhanh.com",     business: "Lãng Quán",                  owner: "Nguyễn Văn E",  phone: "0901234565", poiId: 5),
        (user: "vendor_thuanviet@vinhkhanh.com",    business: "Lẩu Nướng Thuận Việt",       owner: "Nguyễn Văn F",  phone: "0901234566", poiId: 6),
        (user: "vendor_ochockieu@vinhkhanh.com",     business: "Ốc Hoa Kiều",                owner: "Nguyễn Thị G",  phone: "0901234567", poiId: 7),
        (user: "vendor_rongbuffet@vinhkhanh.com",   business: "RONGbuffet",                  owner: "Nguyễn Thị H",  phone: "0901234568", poiId: 8),
        (user: "vendor_shaokao@vinhkhanh.com",      business: "中越友谊烧烤 SHAOKAO",          owner: "Nguyễn Văn I",  phone: "0901234569", poiId: 9),
        (user: "vendor_laugaga@vinhkhanh.com",      business: "Lẩu Gà Lá É Con Gà Trống",    owner: "Nguyễn Văn J",  phone: "0901234570", poiId: 10),
        (user: "vendor_bona@vinhkhanh.com",         business: "BONA Food and Beer",           owner: "Nguyễn Thị K",  phone: "0901234571", poiId: 11),
        (user: "vendor_sinzien@vinhkhanh.com",      business: "Quán Nước SINZIEN",           owner: "Nguyễn Thị L",  phone: "0901234572", poiId: 12),
    };

    foreach (var v in vendors)
    {
        if (await userManager.FindByEmailAsync(v.user) != null) continue;

        var appUser = new AppUser
        {
            UserName = v.user.Replace("@vinhkhanh.com", ""),
            Email = v.user,
            EmailConfirmed = true,
            FullName = v.owner
        };
        var result = await userManager.CreateAsync(appUser, defaultPwd);
        if (!result.Succeeded)
        {
            Console.WriteLine($"⚠️  Lỗi tạo vendor {v.user}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            continue;
        }

        await userManager.AddToRoleAsync(appUser, "Vendor");

        // Lấy UserId vừa tạo
        var createdUser = await userManager.FindByEmailAsync(v.user);
        if (createdUser == null) continue;

        var vendor = new Vendor
        {
            UserId = createdUser.Id,
            PoiPointId = v.poiId,
            BusinessName = v.business,
            OwnerName = v.owner,
            Phone = v.phone,
            Address = $"Đ. Vĩnh Khánh, Phường 8, Quận 4, TP.HCM",
            Status = "Approved",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Vendors.Add(vendor);

        Console.WriteLine($"✅ Vendor: {v.user} (POI #{v.poiId})");
    }

    await db.SaveChangesAsync();
    Console.WriteLine($"✅ Đã seed 12 tài khoản vendor — Mật khẩu: {defaultPwd}");
}
