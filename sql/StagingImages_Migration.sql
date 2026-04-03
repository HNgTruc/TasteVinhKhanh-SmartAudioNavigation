-- Migration: Thêm bảng StagingImages (ảnh vendor chờ admin duyệt)
-- Database: TasteVinhKhanhDb
-- Chạy trong SQL Server Management Studio (SSMS)

USE TasteVinhKhanhDb;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StagingImages')
BEGIN
    CREATE TABLE [dbo].[StagingImages] (
        [Id]            INT IDENTITY(1,1) PRIMARY KEY,
        [VendorId]      INT NOT NULL,
        [PoiPointId]   INT NOT NULL,
        [FileName]     NVARCHAR(255) NOT NULL,
        [TempUrl]      NVARCHAR(500) NOT NULL,
        [ApprovedUrl]  NVARCHAR(500) NULL,
        [Status]       NVARCHAR(20) NOT NULL DEFAULT N'Pending',
        [AdminNote]     NVARCHAR(500) NULL,
        [ReviewedBy]    NVARCHAR(256) NULL,
        [ReviewedAt]    DATETIME2 NULL,
        [CreatedAt]     DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT [CK_StagingImages_Status]
            CHECK ([Status] IN (N'Pending', N'Approved', N'Rejected')),

        CONSTRAINT [FK_StagingImages_Vendors]
            FOREIGN KEY ([VendorId])
            REFERENCES [dbo].[Vendors]([Id])
            ON DELETE CASCADE,

        CONSTRAINT [FK_StagingImages_PoiPoints]
            FOREIGN KEY ([PoiPointId])
            REFERENCES [dbo].[PoiPoints]([Id])
            ON DELETE CASCADE
    );

    CREATE INDEX [IX_StagingImages_Status]
        ON [dbo].[StagingImages] ([Status]);

    CREATE INDEX [IX_StagingImages_VendorId]
        ON [dbo].[StagingImages] ([VendorId]);

    CREATE INDEX [IX_StagingImages_PoiPointId]
        ON [dbo].[StagingImages] ([PoiPointId]);

    PRINT N'✅ Bảng StagingImages đã được tạo với các chỉ mục.';
END
ELSE
    PRINT N'ℹ️  Bảng StagingImages đã tồn tại — bỏ qua.';
GO
