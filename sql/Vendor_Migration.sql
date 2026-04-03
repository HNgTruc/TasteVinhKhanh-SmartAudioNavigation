-- ================================================================
-- MIGRATION: Hệ thống Vendor Management
-- Database: TasteVinhKhanhDb
-- CHỈ tạo bảng + Role. Tài khoản vendor sẽ được tạo
-- bởi API trong Program.cs
-- ================================================================
USE [TasteVinhKhanhDb]
GO

PRINT N'';
PRINT N'============================================================';
PRINT N'⚠️  Mật khẩu vendor: VinhKhanh123';
PRINT N'============================================================';
PRINT N'';

-- ================================================================
-- 1. Tạo bảng Vendors
-- ================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Vendors')
BEGIN
    CREATE TABLE [dbo].[Vendors] (
        [Id]              INT IDENTITY(1,1) PRIMARY KEY,
        [UserId]          NVARCHAR(450) NOT NULL,
        [PoiPointId]     INT NULL,
        [BusinessName]   NVARCHAR(200) NOT NULL,
        [OwnerName]      NVARCHAR(100) NOT NULL,
        [Phone]          NVARCHAR(20) NOT NULL,
        [Address]        NVARCHAR(500) NULL,
        [Status]         NVARCHAR(20) DEFAULT N'Pending',
        [RejectedReason] NVARCHAR(500) NULL,
        [CreatedAt]      DATETIME2 DEFAULT GETUTCDATE(),
        [UpdatedAt]      DATETIME2 DEFAULT GETUTCDATE(),
        CONSTRAINT [FK_Vendors_AspNetUsers]
            FOREIGN KEY ([UserId])
            REFERENCES [dbo].[AspNetUsers]([Id])
            ON DELETE CASCADE,
        CONSTRAINT [FK_Vendors_PoiPoints]
            FOREIGN KEY ([PoiPointId])
            REFERENCES [dbo].[PoiPoints]([Id])
            ON DELETE SET NULL,
        CONSTRAINT [CK_Vendors_Status]
            CHECK ([Status] IN (N'Pending', N'Approved', N'Rejected'))
    );
    PRINT N'✅ Bảng Vendors đã được tạo';
END
ELSE
    PRINT N'ℹ️  Bảng Vendors đã tồn tại — bỏ qua';
GO

-- ================================================================
-- 2. Tạo bảng PendingPOIUpdates
-- ================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PendingPOIUpdates')
BEGIN
    CREATE TABLE [dbo].[PendingPOIUpdates] (
        [Id]            INT IDENTITY(1,1) PRIMARY KEY,
        [VendorId]     INT NOT NULL,
        [PoiPointId]   INT NOT NULL,
        [Payload]       NVARCHAR(MAX) NOT NULL,
        [ImagesPayload] NVARCHAR(MAX) NULL,
        [ScriptsPayload] NVARCHAR(MAX) NULL,
        [Status]        NVARCHAR(20) DEFAULT N'Pending',
        [AdminNote]     NVARCHAR(500) NULL,
        [CreatedAt]     DATETIME2 DEFAULT GETUTCDATE(),
        [ReviewedAt]    DATETIME2 NULL,
        [ReviewedBy]    NVARCHAR(256) NULL,
        CONSTRAINT [FK_PendingPOIUpdates_Vendors]
            FOREIGN KEY ([VendorId])
            REFERENCES [dbo].[Vendors]([Id])
            ON DELETE CASCADE,
        CONSTRAINT [CK_PendingPOIUpdates_Status]
            CHECK ([Status] IN (N'Pending', N'Approved', N'Rejected'))
    );
    PRINT N'✅ Bảng PendingPOIUpdates đã được tạo';
END
ELSE
    PRINT N'ℹ️  Bảng PendingPOIUpdates đã tồn tại — bỏ qua';
GO

-- ================================================================
-- 3. Thêm cột IconUrl vào PoiPoints
-- ================================================================
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[PoiPoints]')
    AND name = 'IconUrl'
)
BEGIN
    ALTER TABLE [dbo].[PoiPoints] ADD [IconUrl] NVARCHAR(500) NULL;
    PRINT N'✅ Cột IconUrl đã được thêm vào PoiPoints';
END
ELSE
    PRINT N'ℹ️  Cột IconUrl đã tồn tại — bỏ qua';
GO

-- ================================================================
-- 4. Tạo Role "Vendor" trong AspNetRoles
-- ================================================================
IF NOT EXISTS (SELECT * FROM [dbo].[AspNetRoles] WHERE [Name] = N'Vendor')
BEGIN
    DECLARE @vendorRoleId NVARCHAR(450) = NEWID();
    INSERT INTO [dbo].[AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
    VALUES (@vendorRoleId, N'Vendor', UPPER(N'Vendor'), NEWID());
    PRINT N'✅ Role "Vendor" đã được tạo';
END
ELSE
    PRINT N'ℹ️  Role "Vendor" đã tồn tại — bỏ qua';
GO

-- ================================================================
-- 5. Xóa dữ liệu cũ (reset sạch)
-- ================================================================
DELETE FROM [dbo].[PendingPOIUpdates];
DELETE FROM [dbo].[Vendors];
PRINT N'ℹ️  Đã xóa dữ liệu vendor cũ (nếu có)';
GO

-- ================================================================
-- 6. Kiểm tra bảng AspNetUsers có những cột nào
-- ================================================================
SELECT '=== Cấu trúc AspNetUsers ===' AS Info;
SELECT COLUMN_NAME, DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'AspNetUsers'
ORDER BY ORDINAL_POSITION;
GO

-- ================================================================
-- TỔNG KẾT
-- ================================================================
SELECT '=== Tổng kết ===' AS Info;
SELECT
    (SELECT COUNT(*) FROM [dbo].[Vendors]) AS TotalVendors,
    (SELECT COUNT(*) FROM [dbo].[Vendors] WHERE Status = N'Approved') AS Approved,
    (SELECT COUNT(*) FROM [dbo].[Vendors] WHERE Status = N'Pending') AS Pending,
    (SELECT COUNT(*) FROM [dbo].[PendingPOIUpdates]) AS TotalPendingUpdates,
    (SELECT COUNT(*) FROM [dbo].[AspNetRoles] WHERE [Name] = N'Vendor') AS HasVendorRole;
GO

PRINT N'';
PRINT N'============================================================';
PRINT N'✅ Migration BẢNG thành công!';
PRINT N'   Bảng + Role đã sẵn sàng.';
PRINT N'   Tài khoản vendor sẽ được tạo bởi API trong Program.cs';
PRINT N'   Mật khẩu: VinhKhanh123';
PRINT N'============================================================';
PRINT N'';
PRINT N'📋 12 VENDORS SẼ ĐƯỢC TẠO BỞI API:';
PRINT N'   1. vendor_aloquan@vinhkhanh.com      → Alo Quán (POI #1)';
PRINT N'   2. vendor_yakiniku@vinhkhanh.com    → THÈM NƯỚNG YAKINIKU (POI #2)';
PRINT N'   3. vendor_chilli@vinhkhanh.com      → Chilli Lẩu Nướng (POI #3)';
PRINT N'   4. vendor_afat@vinhkhanh.com       → A FAT HOT POT (POI #4)';
PRINT N'   5. vendor_langquan@vinhkhanh.com   → Lãng Quán (POI #5)';
PRINT N'   6. vendor_thuanviet@vinhkhanh.com  → Lẩu Nướng Thuận Việt (POI #6)';
PRINT N'   7. vendor_ochockieu@vinhkhanh.com  → Ốc Hoa Kiều (POI #7)';
PRINT N'   8. vendor_rongbuffet@vinhkhanh.com  → RONGbuffet (POI #8)';
PRINT N'   9. vendor_shaokao@vinhkhanh.com    → SHAOKAO (POI #9)';
PRINT N'  10. vendor_laugaga@vinhkhanh.com    → Lẩu Gà Lá É (POI #10)';
PRINT N'  11. vendor_bona@vinhkhanh.com      → BONA Food and Beer (POI #11)';
PRINT N'  12. vendor_sinzien@vinhkhanh.com   → Quán Nước SINZIEN (POI #12)';
PRINT N'============================================================';
GO
