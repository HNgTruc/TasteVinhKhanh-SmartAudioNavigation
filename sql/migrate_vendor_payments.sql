-- Tạo bảng VendorPayments cho luồng vendor thanh toán duy trì hợp tác
IF OBJECT_ID(N'dbo.VendorPayments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VendorPayments
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        VendorId INT NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        BankName NVARCHAR(100) NOT NULL,
        TransactionId NVARCHAR(100) NOT NULL,
        ReceiverAccountNumber NVARCHAR(50) NOT NULL CONSTRAINT DF_VendorPayments_ReceiverAccountNumber DEFAULT N'',
        ReceiverAccountName NVARCHAR(120) NOT NULL CONSTRAINT DF_VendorPayments_ReceiverAccountName DEFAULT N'',
        ReceiverBankName NVARCHAR(120) NOT NULL CONSTRAINT DF_VendorPayments_ReceiverBankName DEFAULT N'',
        ReceiverBankType NVARCHAR(50) NOT NULL CONSTRAINT DF_VendorPayments_ReceiverBankType DEFAULT N'',
        ReceiptUrl NVARCHAR(500) NOT NULL,
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_VendorPayments_Status DEFAULT N'Unpaid',
        DueDate DATETIME2 NULL,
        Note NVARCHAR(500) NULL,
        AdminNote NVARCHAR(500) NULL,
        ReviewedBy NVARCHAR(256) NULL,
        ReviewedAt DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_VendorPayments_CreatedAt DEFAULT SYSUTCDATETIME()
    );
END
GO

IF COL_LENGTH('dbo.VendorPayments', 'DueDate') IS NULL
BEGIN
    ALTER TABLE dbo.VendorPayments ADD DueDate DATETIME2 NULL;
END
GO

IF COL_LENGTH('dbo.VendorPayments', 'ReceiverAccountNumber') IS NULL
BEGIN
    ALTER TABLE dbo.VendorPayments ADD ReceiverAccountNumber NVARCHAR(50) NOT NULL CONSTRAINT DF_VendorPayments_ReceiverAccountNumber_Alter DEFAULT N'';
END
GO

IF COL_LENGTH('dbo.VendorPayments', 'ReceiverAccountName') IS NULL
BEGIN
    ALTER TABLE dbo.VendorPayments ADD ReceiverAccountName NVARCHAR(120) NOT NULL CONSTRAINT DF_VendorPayments_ReceiverAccountName_Alter DEFAULT N'';
END
GO

IF COL_LENGTH('dbo.VendorPayments', 'ReceiverBankName') IS NULL
BEGIN
    ALTER TABLE dbo.VendorPayments ADD ReceiverBankName NVARCHAR(120) NOT NULL CONSTRAINT DF_VendorPayments_ReceiverBankName_Alter DEFAULT N'';
END
GO

IF COL_LENGTH('dbo.VendorPayments', 'ReceiverBankType') IS NULL
BEGIN
    ALTER TABLE dbo.VendorPayments ADD ReceiverBankType NVARCHAR(50) NOT NULL CONSTRAINT DF_VendorPayments_ReceiverBankType_Alter DEFAULT N'';
END
GO

IF EXISTS (
    SELECT 1
    FROM sys.default_constraints dc
    JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.VendorPayments')
      AND c.name = N'Status'
      AND dc.name = N'DF_VendorPayments_Status'
)
BEGIN
    ALTER TABLE dbo.VendorPayments DROP CONSTRAINT DF_VendorPayments_Status;
    ALTER TABLE dbo.VendorPayments ADD CONSTRAINT DF_VendorPayments_Status DEFAULT N'Unpaid' FOR Status;
END
GO

UPDATE dbo.VendorPayments
SET Status = CASE
    WHEN Status = 'Pending' THEN 'PendingVerification'
    WHEN Status = 'Approved' THEN 'Paid'
    WHEN Status = 'Rejected' THEN 'Unpaid'
    ELSE Status
END
WHERE Status IN ('Pending', 'Approved', 'Rejected');
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_VendorPayments_Vendors_VendorId'
)
BEGIN
    ALTER TABLE dbo.VendorPayments
    ADD CONSTRAINT FK_VendorPayments_Vendors_VendorId
        FOREIGN KEY (VendorId) REFERENCES dbo.Vendors(Id)
        ON DELETE CASCADE;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_VendorPayments_DueDate'
      AND object_id = OBJECT_ID(N'dbo.VendorPayments')
)
BEGIN
    CREATE INDEX IX_VendorPayments_DueDate ON dbo.VendorPayments(DueDate);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_VendorPayments_Status'
      AND object_id = OBJECT_ID(N'dbo.VendorPayments')
)
BEGIN
    CREATE INDEX IX_VendorPayments_Status ON dbo.VendorPayments(Status);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_VendorPayments_VendorId'
      AND object_id = OBJECT_ID(N'dbo.VendorPayments')
)
BEGIN
    CREATE INDEX IX_VendorPayments_VendorId ON dbo.VendorPayments(VendorId);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_VendorPayments_CreatedAt'
      AND object_id = OBJECT_ID(N'dbo.VendorPayments')
)
BEGIN
    CREATE INDEX IX_VendorPayments_CreatedAt ON dbo.VendorPayments(CreatedAt);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_VendorPayments_TransactionId'
      AND object_id = OBJECT_ID(N'dbo.VendorPayments')
)
BEGIN
    CREATE INDEX IX_VendorPayments_TransactionId ON dbo.VendorPayments(TransactionId);
END
GO
