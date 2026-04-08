-- ============================================================
-- Migration: Thêm AudioFilePath + IsAudioUploaded vào AudioScripts
-- Chạy trên database TasteVinhKhanh trong SSMS
-- ============================================================
USE [TasteVinhKhanhDb]
GO

-- 1. Thêm cột AudioFilePath (NVARCHAR, nullable)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AudioScripts') AND name = 'AudioFilePath')
BEGIN
    ALTER TABLE [dbo].[AudioScripts] ADD [AudioFilePath] NVARCHAR(500) NULL;
    PRINT 'Added AudioFilePath column';
END
ELSE
    PRINT 'AudioFilePath already exists';
GO

-- 2. Thêm cột IsAudioUploaded (BIT, default = 0)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AudioScripts') AND name = 'IsAudioUploaded')
BEGIN
    ALTER TABLE [dbo].[AudioScripts] ADD [IsAudioUploaded] BIT NOT NULL DEFAULT 0;
    PRINT 'Added IsAudioUploaded column';
END
ELSE
    PRINT 'IsAudioUploaded already exists';
GO

-- 3. Xóa cột AudioFileUrl cũ (nếu muốn dọn)
-- IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AudioScripts') AND name = 'AudioFileUrl')
-- BEGIN
--     ALTER TABLE [dbo].[AudioScripts] DROP COLUMN [AudioFileUrl];
--     PRINT 'Dropped AudioFileUrl column';
-- END
GO

-- Verify
SELECT name, system_type_name, is_nullable
FROM sys.dm_exec_describe_first_result_set('SELECT * FROM AudioScripts WHERE 1=0', NULL, 1);
GO
