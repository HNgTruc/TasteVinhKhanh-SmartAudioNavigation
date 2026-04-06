-- ================================================================
-- MIGRATION: Thêm bảng RestaurantImages cho 12 quán ăn
-- Database: TasteVinhKhanhDb
-- Chạy trong SQL Server Management Studio (SSMS)
-- ================================================================
USE [TasteVinhKhanhDb]
GO

-- ================================================================
-- 0. Thêm 2 cột mới cho bảng StagingImages
--    Hỗ trợ yêu cầu xóa ảnh từ Vendor (cần Admin duyệt)
-- ================================================================
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = 'StagingImages' AND COLUMN_NAME = 'StagingType')
BEGIN
    ALTER TABLE [dbo].[StagingImages]
        ADD [StagingType] NVARCHAR(20) NOT NULL DEFAULT 'Upload';
    PRINT N'✅ Đã thêm cột StagingType vào StagingImages';
END
ELSE
    PRINT N'ℹ️  Cột StagingType đã tồn tại — bỏ qua';

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = 'StagingImages' AND COLUMN_NAME = 'ReferencedImageUrl')
BEGIN
    ALTER TABLE [dbo].[StagingImages]
        ADD [ReferencedImageUrl] NVARCHAR(500) NULL;
    PRINT N'✅ Đã thêm cột ReferencedImageUrl vào StagingImages';
END
ELSE
    PRINT N'ℹ️  Cột ReferencedImageUrl đã tồn tại — bỏ qua';
GO

-- ================================================================
-- 1. Tạo bảng RestaurantImages
-- ================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RestaurantImages')
BEGIN
    CREATE TABLE [dbo].[RestaurantImages] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [PoiPointId] INT NOT NULL,
        [ImageUrl] NVARCHAR(500) NOT NULL,
        [IsPrimary] BIT DEFAULT 0,
        [SortOrder] INT DEFAULT 0,
        [CreatedAt] DATETIME2 DEFAULT GETUTCDATE(),
        [UpdatedAt] DATETIME2 DEFAULT GETUTCDATE(),
        CONSTRAINT [FK_RestaurantImages_PoiPoints]
            FOREIGN KEY ([PoiPointId])
            REFERENCES [dbo].[PoiPoints]([Id])
            ON DELETE CASCADE
    );
    PRINT N'✅ Bảng RestaurantImages đã được tạo';
END
ELSE
    PRINT N'ℹ️  Bảng RestaurantImages đã tồn tại — bỏ qua';
GO

-- ================================================================
-- 2. Reset identity (nếu cần)
-- ================================================================
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'RestaurantImages')
BEGIN
    DELETE FROM [dbo].[RestaurantImages];
    DBCC CHECKIDENT ('[dbo].[RestaurantImages]', RESEED, 0);
    PRINT N'ℹ️  Đã reset RestaurantImages';
END
GO

-- ================================================================
-- 3. Insert ảnh cho 12 quán
-- Copy ảnh thật vào: src/TasteVinhKhanh.Api/wwwroot/images/
-- ================================================================
SET IDENTITY_INSERT [dbo].[RestaurantImages] ON;

INSERT [dbo].[RestaurantImages] ([Id], [PoiPointId], [ImageUrl], [IsPrimary], [SortOrder], [CreatedAt], [UpdatedAt]) VALUES

-- 1. Alo Quán (PoiPointId = 1)
(1,  1, N'/images/alo_quan_1.jpg',  1, 1, CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2), CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2)),
(2,  1, N'/images/alo_quan_2.jpg',  0, 2, CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2), CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2)),
(3,  1, N'/images/alo_quan_3.jpg',  0, 3, CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2), CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2)),

-- 2. THÈM NƯỚNG YAKINIKU (PoiPointId = 2)
(4,  2, N'/images/yakiniku_1.jpg',   1, 1, CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2), CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2)),
(5,  2, N'/images/yakiniku_2.jpg',   0, 2, CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2), CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2)),

-- 3. Chilli Lẩu Nướng Quán (PoiPointId = 3)
(6,  3, N'/images/chilli_1.jpg',     1, 1, CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2), CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2)),
(7,  3, N'/images/chilli_2.jpg',     0, 2, CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2), CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2)),

-- 4. A FAT HOT POT (PoiPointId = 4)
(8,  4, N'/images/afat_1.jpg',       1, 1, CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2), CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2)),
(9,  4, N'/images/afat_2.jpg',       0, 2, CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2), CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2)),

-- 5. Lãng Quán (PoiPointId = 5)
(10, 5, N'/images/lang_quan_1.jpg',  1, 1, CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2), CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2)),

-- 6. Lẩu Nướng Thuận Việt (PoiPointId = 6)
(11, 6, N'/images/thuan_viet_1.jpg', 1, 1, CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2), CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2)),

-- 7. Ốc Hoa Kiều (PoiPointId = 7)
(12, 7, N'/images/oc_hoa_kieu_1.jpg', 1, 1, CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2), CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2)),
(13, 7, N'/images/oc_hoa_kieu_2.jpg', 0, 2, CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2), CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2)),

-- 8. RONGbuffet (PoiPointId = 8)
(14, 8, N'/images/rongbuffet_1.jpg',  1, 1, CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2), CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2)),
(15, 8, N'/images/rongbuffet_2.jpg',  0, 2, CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2), CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2)),

-- 9. 中越友谊烧烤 SHAOKAO (PoiPointId = 9)
(16, 9, N'/images/shaokao_1.jpg',    1, 1, CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2), CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2)),

-- 10. Lẩu Gà Lá É Con Gà Trống (PoiPointId = 10)
(17, 10, N'/images/lau_ga_1.jpg',    1, 1, CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2), CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2)),

-- 11. BONA Food and Beer (PoiPointId = 11)
(18, 11, N'/images/bona_1.jpg',      1, 1, CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2), CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2)),

-- 12. Quán Nước SINZIEN (PoiPointId = 12)
(19, 12, N'/images/sinzien_1.jpg',    1, 1, CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2), CAST(N'2026-03-30T00:00:00.0000000' AS DateTime2))

SET IDENTITY_INSERT [dbo].[RestaurantImages] OFF;
GO

-- ================================================================
-- 4. Kiểm tra
-- ================================================================
SELECT '=== RestaurantImages ===' AS Info;
SELECT i.Id, p.Name AS RestaurantName, i.ImageUrl, i.IsPrimary, i.SortOrder
FROM [dbo].[RestaurantImages] i
JOIN [dbo].[PoiPoints] p ON i.PoiPointId = p.Id
ORDER BY i.PoiPointId, i.SortOrder;

SELECT '=== Tổng kết ===' AS Info;
SELECT
    (SELECT COUNT(*) FROM [dbo].[RestaurantImages]) AS TotalImages,
    (SELECT COUNT(DISTINCT PoiPointId) FROM [dbo].[RestaurantImages]) AS TotalRestaurants;
GO

PRINT N'✅ Migration RestaurantImages hoàn tất!';
PRINT N'📁 Copy ảnh vào: src/TasteVinhKhanh.Api/wwwroot/images/';
GO
