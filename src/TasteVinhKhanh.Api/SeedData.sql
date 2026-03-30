-- ═══════════════════════════════════════════════════════════════
-- Script seed dữ liệu POI cho phố Ẩm thực Vĩnh Khánh, Q.4, TP.HCM
-- Chạy trong SQL Server Management Studio (SSMS)
-- Database: TasteVinhKhanhDb
-- ═══════════════════════════════════════════════════════════════

USE TasteVinhKhanhDb;
GO

-- ── 1. Tạo bảng PoiPoints nếu chưa có ────────────────────────
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PoiPoints')
BEGIN
    CREATE TABLE PoiPoints (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(200) NOT NULL,
        ShortDescription NVARCHAR(500),
        Latitude FLOAT NOT NULL,
        Longitude FLOAT NOT NULL,
        TriggerRadiusMeters FLOAT DEFAULT 50,
        Priority INT DEFAULT 1,
        IsActive BIT DEFAULT 1,
        ImageUrl NVARCHAR(500),
        MapUrl NVARCHAR(500),
        CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 DEFAULT GETUTCDATE()
    );
    PRINT '✅ Bảng PoiPoints đã được tạo';
END
ELSE
    PRINT 'ℹ️  Bảng PoiPoints đã tồn tại';
GO

-- ── 2. Tạo bảng AudioScripts nếu chưa có ───────────────────
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AudioScripts')
BEGIN
    CREATE TABLE AudioScripts (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        PoiPointId INT NOT NULL,
        LanguageCode NVARCHAR(10) NOT NULL,
        TtsScript NVARCHAR(2000),
        AudioFileUrl NVARCHAR(500),
        UpdatedAt DATETIME2 DEFAULT GETUTCDATE(),
        CONSTRAINT FK_AudioScripts_PoiPoints FOREIGN KEY (PoiPointId)
            REFERENCES PoiPoints(Id) ON DELETE CASCADE,
        CONSTRAINT UQ_AudioScripts_PoiPoint_Lang
            UNIQUE (PoiPointId, LanguageCode)
    );
    PRINT '✅ Bảng AudioScripts đã được tạo';
END
ELSE
    PRINT 'ℹ️  Bảng AudioScripts đã tồn tại';
GO

-- ── 3. Xóa dữ liệu cũ (nếu muốn seed lại) ──────────────────
-- Bỏ comment nếu muốn xóa và seed lại:
-- DELETE FROM AudioScripts;
-- DBCC CHECKIDENT ('AudioScripts', RESEED, 0);
-- DELETE FROM PoiPoints;
-- DBCC CHECKIDENT ('PoiPoints', RESEED, 0);
-- GO

-- ── 4. Seed POIs Vĩnh Khánh ──────────────────────────────────
-- Tọa độ thực tế: phố Ẩm thực Vĩnh Khánh, Quận 4, TP.HCM
-- (Dataly Vĩnh Khánh - con đường nổi tiếng ẩm thực Sài Gòn)

SET IDENTITY_INSERT PoiPoints ON;

MERGE INTO PoiPoints AS target
USING (VALUES
    (1, N'Bánh Mì Cô Ba', N'Quán bánh mì lâu đời nhất phố Vĩnh Khánh, giá từ 15.000đ',
        10.7567, 106.6997, 50, 5, 1, NULL, NULL, GETUTCDATE(), GETUTCDATE()),
    (2, N'Hủ Tiếu Nam Vang Số 1', N'Nước dùng đậm đà, topping phong phú, phục vụ hơn 30 năm',
        10.7570, 106.7002, 50, 4, 1, NULL, NULL, GETUTCDATE(), GETUTCDATE()),
    (3, N'Cà Phê Vợt Vĩnh Khánh', N'Lưu giữ hương vị cà phê truyền thống Sài Gòn, pha chế thủ công',
        10.7573, 106.7008, 40, 3, 1, NULL, NULL, GETUTCDATE(), GETUTCDATE()),
    (4, N'Bún Bò Huế Vĩnh Khánh', N'Bún bò dai sần sật, nước lèo thơm nồng gió heo',
        10.7576, 106.7013, 50, 3, 1, NULL, NULL, GETUTCDATE(), GETUTCDATE()),
    (5, N'Chè Long Thành', N'Chè các loại mát lạnh, topping đầy đặn, mở cửa đến 22h',
        10.7579, 106.7018, 40, 2, 1, NULL, NULL, GETUTCDATE(), GETUTCDATE())
) AS source (Id, Name, ShortDescription, Latitude, Longitude,
             TriggerRadiusMeters, Priority, IsActive, ImageUrl, MapUrl, CreatedAt, UpdatedAt)
ON target.Id = source.Id
WHEN MATCHED THEN
    UPDATE SET
        target.Name = source.Name,
        target.ShortDescription = source.ShortDescription,
        target.Latitude = source.Latitude,
        target.Longitude = source.Longitude,
        target.TriggerRadiusMeters = source.TriggerRadiusMeters,
        target.Priority = source.Priority,
        target.IsActive = source.IsActive,
        target.UpdatedAt = GETUTCDATE()
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Id, Name, ShortDescription, Latitude, Longitude,
            TriggerRadiusMeters, Priority, IsActive, ImageUrl, MapUrl, CreatedAt, UpdatedAt)
    VALUES (source.Id, source.Name, source.ShortDescription, source.Latitude, source.Longitude,
            source.TriggerRadiusMeters, source.Priority, source.IsActive, source.ImageUrl,
            source.MapUrl, source.CreatedAt, source.UpdatedAt);

SET IDENTITY_INSERT PoiPoints OFF;
GO

-- ── 5. Seed Audio Scripts (tiếng Việt + tiếng Anh) ──────────
SET IDENTITY_INSERT AudioScripts ON;

MERGE INTO AudioScripts AS target
USING (VALUES
    -- Bánh Mì Cô Ba (PoiId=1)
    (1,  1, 'vi', N'Chào mừng bạn đến với tiệm Bánh Mì Cô Ba. Đây là một trong những tiệm bánh mì lâu đời và nổi tiếng nhất trên phố Ẩm thực Vĩnh Khánh, Quận 4, Thành phố Hồ Chí Minh. Với hơn 40 năm phục vụ, Cô Ba luôn giữ vững hương vị truyền thống Sài Gòn. Thử ngay bánh mì thịt, pate và đừng quên chấm với nước sốt đặc biệt.',
         NULL, GETUTCDATE()),
    (2,  1, 'en', 'Welcome to Banh Mi Co Ba. This is one of the oldest and most famous banh mi shops on Vinh Khanh Food Street, District 4, Ho Chi Minh City. With over 40 years of service, Co Ba has maintained its authentic Saigon flavor. Try the classic pork and pate banh mi, and do not forget to dip it in our special sauce.',
         NULL, GETUTCDATE()),

    -- Hủ Tiếu Nam Vang Số 1 (PoiId=2)
    (3,  2, 'vi', N'Đây là quán Hủ Tiếu Nam Vang số 1, nổi tiếng với nước dùng đậm đà và topping phong phú. Quán đã phục vụ thực khách hơn 30 năm tại con phố Vĩnh Khánh. Điểm đặc biệt là nước dùng được ninh từ xương heo và da heo giòn rụm.',
         NULL, GETUTCDATE()),
    (4,  2, 'en', 'This is Hu Tieu Nam Vang Number 1, famous for its rich broth and abundant toppings. The shop has served customers for over 30 years on Vinh Khanh street. The highlight is the broth simmered from pork bones, topped with crispy pork skin.',
         NULL, GETUTCDATE()),

    -- Cà Phê Vợt Vĩnh Khánh (PoiId=3)
    (5,  3, 'vi', N'Quán cà phê vợt Vĩnh Khánh, nơi lưu giữ hương vị cà phê truyền thống Sài Gòn với cách pha chế thủ công độc đáo. Ngồi ghế nhựa thấp, nhâm nhi ly cà phê sữa đá và ngắm nhìn phố Vĩnh Khánh nhộn nhịp.',
         NULL, GETUTCDATE()),
    (6,  3, 'en', 'Vinh Khanh Coffee Filter stall, preserving the traditional Saigon coffee flavor with a unique handmade brewing method. Sit on low plastic chairs, sip an iced milk coffee, and watch the bustling Vinh Khanh street.',
         NULL, GETUTCDATE()),

    -- Bún Bò Huế Vĩnh Khánh (PoiId=4)
    (7,  4, 'vi', N'Quán bún bò Huế Vĩnh Khánh với tô bún dai sần sật, nước lèo thơm nồng mùi gió heo đặc trưng. Đây là món ăn mang đậm hương vị miền Trung, được người Sài Gòn yêu thích suốt nhiều thập kỷ.',
         NULL, GETUTCDATE()),

    -- Chè Long Thành (PoiId=5)
    (8,  5, 'vi', N'Chè Long Thành, quán chè nổi tiếng trên phố Vĩnh Khánh với nhiều loại chè mát lạnh. Topping đầy đặn từ đậu xanh, đậu đỏ, thạch, nước cốt dừa béo ngậy. Quán mở cửa đến 22 giờ mỗi ngày.',
         NULL, GETUTCDATE())
) AS source (Id, PoiPointId, LanguageCode, TtsScript, AudioFileUrl, UpdatedAt)
ON target.Id = source.Id
WHEN MATCHED THEN
    UPDATE SET
        target.TtsScript = source.TtsScript,
        target.UpdatedAt = GETUTCDATE()
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Id, PoiPointId, LanguageCode, TtsScript, AudioFileUrl, UpdatedAt)
    VALUES (source.Id, source.PoiPointId, source.LanguageCode,
            source.TtsScript, source.AudioFileUrl, source.UpdatedAt);

SET IDENTITY_INSERT AudioScripts OFF;
GO

-- ── 6. Kiểm tra dữ liệu ─────────────────────────────────────
SELECT '=== PoiPoints ===' AS Info;
SELECT Id, Name, Latitude, Longitude, TriggerRadiusMeters, Priority, IsActive
FROM PoiPoints
ORDER BY Priority DESC;

SELECT '=== AudioScripts ===' AS Info;
SELECT s.Id, p.Name AS PoiName, s.LanguageCode, LEN(s.TtsScript) AS ScriptLen
FROM AudioScripts s
JOIN PoiPoints p ON s.PoiPointId = p.Id
ORDER BY p.Priority DESC, s.LanguageCode;

SELECT '=== Tổng kết ===' AS Info;
SELECT
    (SELECT COUNT(*) FROM PoiPoints WHERE IsActive = 1) AS ActivePois,
    (SELECT COUNT(*) FROM AudioScripts) AS TotalScripts,
    (SELECT COUNT(DISTINCT LanguageCode) FROM AudioScripts) AS Languages;

PRINT '✅ Seed hoàn tất! Chạy API và mở MauiApp để xem bản đồ.';
GO
