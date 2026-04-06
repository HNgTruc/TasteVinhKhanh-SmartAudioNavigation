# TasteVinhKhanh - Smart Audio Navigation

## WHAT

### Tech Stack
- **Backend**: ASP.NET Core 10 Web API (.NET 10)
- **Frontend**: Static HTML/JS (Admin & Vendor portals)
- **Mobile**: .NET MAUI (iOS/Android app)
- **Database**: SQL Server via Entity Framework Core 10
- **Auth**: JWT Bearer + ASP.NET Identity
- **API Docs**: Swagger (v1)

### Directory Map
```
TasteVinhKhanh-SmartAudioNavigation/
├── src/
│   ├── TasteVinhKhanh.Api/         # Web API (ASP.NET Core)
│   │   ├── Controllers/            # API endpoints
│   │   ├── Services/               # Business logic (IAuthService, IPoiService, ITourService...)
│   │   ├── Data/                   # AppDbContext (EF Core)
│   │   ├── Migrations/             # EF Core migrations
│   │   └── Program.cs              # Startup + seed data
│   ├── TasteVinhKhanh.Admin/       # Admin dashboard (HTML/JS)
│   ├── TasteVinhKhanh.Vendor/     # Vendor portal (HTML/JS)
│   ├── TasteVinhKhanh.MauiApp/    # MAUI mobile app
│   └── TasteVinhKhanh.Shared/      # Shared Models + DTOs
│       ├── Models/                 # EF Core entities
│       └── DTOs/                   # Request/Response DTOs
├── sql/                            # SQL scripts
├── docs/                           # Documentation
└── TasteVinhKhanh.sln             # Solution file
```

### Architecture
- **API-first**: Backend cung cấp REST API, frontend mobile/desktop là client
- **Service Layer**: Logic nghiệp vụ tách biệt trong Services (không viết trong Controller)
- **Shared models**: TasteVinhKhanh.Shared chứa Models + DTOs dùng chung cho Api và MauiApp
- **Identity**: ASP.NET Identity quản lý Users/Roles; có 2 roles: Admin, Vendor

---

## WHY

### Purpose of each module
| Module | Purpose |
|---|---|
| `Api` | REST API backend — xác thực, quản lý POI/Tour/Vendor, audio playback, sync |
| `Admin` | Dashboard quản trị (HTML/JS) — quản lý POI, duyệt ảnh, phê duyệt vendor |
| `Vendor` | Cổng thông tin nhà hàng — upload ảnh, chỉnh sửa thông tin, theo dõi analytics |
| `MauiApp` | Ứng dụng di động — phát audio khi đến gần POI, xem tour, điều hướng |
| `Shared` | Chứa entity models và DTO — tránh trùng lặp giữa Api và MauiApp |

### Design Decisions
- Database tạo **thủ công trong SSMS** → `ConfigureWarnings` bỏ qua `PendingModelChangesWarning`
- CORS policy `AllowAll` vì Admin/Vendor là static files trên cùng server
- CORS OPTIONS preflight xử lý bằng inline middleware trước `UseAuthentication`
- Ảnh upload qua Staging → Admin duyệt → Approved: 2 bước để kiểm soát chất lượng
- Audio script lưu TTS script text + URL file audio đã synthesize

---

## HOW

### Build / Test Commands
```bash
# Build toàn solution
dotnet build

# Build chỉ API
dotnet build src/TasteVinhKhanh.Api

# Chạy API (development)
dotnet run --project src/TasteVinhKhanh.Api

# Xóa cache + build (fix build lỗi do cache hỏng)
Remove-Item -Recurse -Force "src/TasteVinhKhanh.Api/obj"
Remove-Item -Recurse -Force "src/TasteVinhKhanh.Api/bin"
dotnet build
```

### Database
- Connection string: `appsettings.json` → `ConnectionStrings:DefaultConnection`
- Database được tạo thủ công trong SSMS, không dùng `dotnet ef migrations`
- Migrations folder vẫn tồn tại nhưng chỉ dùng khi cần

### Seed Data (tự động khi khởi động API)
- **Admin**: `admin@vinhkhanh.com` / `Admin@12345` (configurable in appsettings)
- **12 Vendor accounts**: `vendor_aloquan@vinhkhanh.com` → `vendor_sinzien@vinhkhanh.com` / `VinhKhanh123`
- **12 POI mặc định**: quán ăn dọc đường Vĩnh Khánh, Q.4, TP.HCM

### Roles & Access
| Role | Truy cập |
|---|---|
| `Admin` | Toàn bộ API (Authorize Roles="Admin") |
| `Vendor` | API vendor (Authorize Roles="Vendor") |
| Anonymous | GET /api/poi, GET /api/tours, POST /api/sync/playback (device-based) |

### Gotchas
- ⚠️ File `obj/project.nuget.cache` dễ bị hỏng nếu tắt máy đột ngột khi đang build → xóa `obj/` là cách fix
- ⚠️ CORS preflight OPTIONS phải trả lời TRƯỚC `UseAuthentication` (middleware thủ công trong `Program.cs`)
- ⚠️ Nullable reference: các property string trong Models phải có giá trị mặc định `= string.Empty` hoặc `?`
- ⚠️ `UseStaticWebAssets=false` trong `.csproj` để tránh lỗi Static Web Assets khi không có frontend bundler

---

## Workflows

### Khi bắt đầu session mới
1. Đọc `CLAUDE.md` để hiểu kiến trúc
2. `dotnet build` để kiểm tra trạng thái
3. Nếu lỗi → xóa `obj/` trước

### Trước khi push code
1. `dotnet build` — đảm bảo không lỗi
2. Kiểm tra nullable warnings (CS8600, CS8602...)
3. Commit message theo format: `[module] mô tả` (vd: `[Api] thêm endpoint analytics`)

### Thêm feature mới
1. Thêm DTO trong `TasteVinhKhanh.Shared/DTOs/`
2. Thêm Service interface + implementation trong `TasteVinhKhanh.Api/Services/`
3. Đăng ký DI trong `Program.cs`
4. Thêm Controller endpoint
5. Nếu cần DB → cập nhật `AppDbContext` + migration
6. Update `CLAUDE.md` nếu có thay đổi lớn về kiến trúc
