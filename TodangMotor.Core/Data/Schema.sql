-- ============================================================
-- TodangMotorDB — Schema
-- Loaded and executed by DatabaseInitializer on first launch.
-- Each GO-separated batch runs independently.
-- All statements are IF-NOT-EXISTS guarded so re-runs are safe.
-- ============================================================

-- ---------- 1. Categories ----------
IF OBJECT_ID('dbo.Categories', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Categories (
        CategoryId    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Categories PRIMARY KEY,
        CategoryName  NVARCHAR(100) NOT NULL CONSTRAINT UQ_Categories_CategoryName UNIQUE,
        IsActive      BIT NOT NULL CONSTRAINT DF_Categories_IsActive DEFAULT(1)
    );
END
GO

-- ---------- 2. Suppliers ----------
IF OBJECT_ID('dbo.Suppliers', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Suppliers (
        SupplierId     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Suppliers PRIMARY KEY,
        SupplierName   NVARCHAR(150) NOT NULL CONSTRAINT UQ_Suppliers_SupplierName UNIQUE,
        ContactNumber  NVARCHAR(30) NULL,
        Address        NVARCHAR(250) NULL,
        IsActive       BIT NOT NULL CONSTRAINT DF_Suppliers_IsActive DEFAULT(1)
    );
END
GO

-- ---------- 3. Users ----------
IF OBJECT_ID('dbo.Users', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users (
        UserId               INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
        Username             NVARCHAR(50) NOT NULL CONSTRAINT UQ_Users_Username UNIQUE,
        PasswordHash         NVARCHAR(200) NOT NULL,
        FullName             NVARCHAR(100) NOT NULL,
        Role                 NVARCHAR(20) NOT NULL,
        IsActive             BIT NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT(1),
        CreatedAt            DATETIME NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT(GETDATE()),
        FailedLoginAttempts  INT NOT NULL CONSTRAINT DF_Users_FailedLoginAttempts DEFAULT(0),
        LockoutUntil         DATETIME NULL
    );
END
GO

-- ---------- 4. Products ----------
IF OBJECT_ID('dbo.Products', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Products (
        ProductId       INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Products PRIMARY KEY,
        CategoryId      INT NOT NULL,
        ProductName     NVARCHAR(150) NOT NULL,
        Brand           NVARCHAR(100) NULL,
        Unit            NVARCHAR(20) NOT NULL CONSTRAINT DF_Products_Unit DEFAULT('pcs'),
        CostPrice       DECIMAL(18,2) NOT NULL CONSTRAINT DF_Products_CostPrice DEFAULT(0),
        SellingPrice    DECIMAL(18,2) NOT NULL CONSTRAINT DF_Products_SellingPrice DEFAULT(0),
        QuantityOnHand  INT NOT NULL CONSTRAINT DF_Products_QuantityOnHand DEFAULT(0),
        ReorderLevel    INT NOT NULL CONSTRAINT DF_Products_ReorderLevel DEFAULT(5),
        IsActive        BIT NOT NULL CONSTRAINT DF_Products_IsActive DEFAULT(1),
        CreatedAt       DATETIME NOT NULL CONSTRAINT DF_Products_CreatedAt DEFAULT(GETDATE()),
        UpdatedAt       DATETIME NOT NULL CONSTRAINT DF_Products_UpdatedAt DEFAULT(GETDATE()),
        Notes           NVARCHAR(500) NULL,
        SKU             NVARCHAR(50) NULL,
        ExpirationDate  DATETIME NULL,
        Description     NVARCHAR(500) NULL,
        SupplierId      INT NULL,

        CONSTRAINT FK_Products_Categories
            FOREIGN KEY (CategoryId) REFERENCES dbo.Categories(CategoryId),
        CONSTRAINT FK_Products_Suppliers
            FOREIGN KEY (SupplierId) REFERENCES dbo.Suppliers(SupplierId)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_Products_Name_Brand')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UQ_Products_Name_Brand
        ON dbo.Products(ProductName, Brand);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_Products_SKU')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UQ_Products_SKU
        ON dbo.Products(SKU)
        WHERE SKU IS NOT NULL;
END
GO

-- ---------- 5. ProductAlternateSuppliers ----------
IF OBJECT_ID('dbo.ProductAlternateSuppliers', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductAlternateSuppliers (
        ProductId   INT NOT NULL,
        SupplierId  INT NOT NULL,

        CONSTRAINT PK_ProductAlternateSuppliers PRIMARY KEY (ProductId, SupplierId),
        CONSTRAINT FK_PAS_Products
            FOREIGN KEY (ProductId)  REFERENCES dbo.Products(ProductId),
        CONSTRAINT FK_PAS_Suppliers
            FOREIGN KEY (SupplierId) REFERENCES dbo.Suppliers(SupplierId)
    );
END
GO

-- ---------- 6. Sales ----------
IF OBJECT_ID('dbo.Sales', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Sales (
        SaleId           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Sales PRIMARY KEY,
        InvoiceNo        NVARCHAR(30) NOT NULL CONSTRAINT UQ_Sales_InvoiceNo UNIQUE,
        UserId           INT NOT NULL,
        SaleDate         DATETIME NOT NULL CONSTRAINT DF_Sales_SaleDate DEFAULT(GETDATE()),
        CustomerName     NVARCHAR(100) NULL,
        PaymentMethod    NVARCHAR(20) NOT NULL,
        Subtotal         DECIMAL(18,2) NOT NULL,
        AmountTendered   DECIMAL(18,2) NOT NULL,
        ChangeAmount     DECIMAL(18,2) NOT NULL,
        Status           NVARCHAR(20) NOT NULL CONSTRAINT DF_Sales_Status DEFAULT('Completed'),
        VoidedByUserId   INT NULL,
        VoidedAt         DATETIME NULL,
        VoidReason       NVARCHAR(250) NULL,

        CONSTRAINT FK_Sales_Users
            FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_Sales_VoidedByUserId
            FOREIGN KEY (VoidedByUserId) REFERENCES dbo.Users(UserId)
    );
END
GO

-- ---------- 7. SaleItems ----------
IF OBJECT_ID('dbo.SaleItems', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SaleItems (
        SaleItemId  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SaleItems PRIMARY KEY,
        SaleId      INT NOT NULL,
        ProductId   INT NOT NULL,
        Quantity    INT NOT NULL,
        UnitPrice   DECIMAL(18,2) NOT NULL,
        LineTotal   DECIMAL(18,2) NOT NULL,
        UnitCost    DECIMAL(18,2) NULL,

        CONSTRAINT FK_SaleItems_Sales
            FOREIGN KEY (SaleId) REFERENCES dbo.Sales(SaleId),
        CONSTRAINT FK_SaleItems_Products
            FOREIGN KEY (ProductId) REFERENCES dbo.Products(ProductId)
    );
END
GO

-- ---------- 8. StockIns ----------
IF OBJECT_ID('dbo.StockIns', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.StockIns (
        StockInId      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StockIns PRIMARY KEY,
        SupplierId     INT NOT NULL,
        UserId         INT NOT NULL,
        DeliveryDate   DATETIME NOT NULL,
        ReferenceNo    NVARCHAR(50) NULL,
        Notes          NVARCHAR(250) NULL,
        CreatedAt      DATETIME NOT NULL CONSTRAINT DF_StockIns_CreatedAt DEFAULT(GETDATE()),

        CONSTRAINT FK_StockIns_Suppliers
            FOREIGN KEY (SupplierId) REFERENCES dbo.Suppliers(SupplierId),
        CONSTRAINT FK_StockIns_Users
            FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId)
    );
END
GO

-- ---------- 9. StockInItems ----------
IF OBJECT_ID('dbo.StockInItems', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.StockInItems (
        StockInItemId  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StockInItems PRIMARY KEY,
        StockInId      INT NOT NULL,
        ProductId      INT NOT NULL,
        Quantity       INT NOT NULL,
        UnitCost       DECIMAL(18,2) NOT NULL,
        LineTotal      DECIMAL(18,2) NOT NULL,

        CONSTRAINT FK_StockInItems_StockIns
            FOREIGN KEY (StockInId) REFERENCES dbo.StockIns(StockInId),
        CONSTRAINT FK_StockInItems_Products
            FOREIGN KEY (ProductId) REFERENCES dbo.Products(ProductId)
    );
END
GO

-- ---------- 10. StockMovements ----------
IF OBJECT_ID('dbo.StockMovements', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.StockMovements (
        MovementId      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StockMovements PRIMARY KEY,
        ProductId       INT NOT NULL,
        MovementType    NVARCHAR(20) NOT NULL,
        QuantityChange  INT NOT NULL,
        QuantityBefore  INT NOT NULL,
        QuantityAfter   INT NOT NULL,
        ReferenceId     INT NULL,
        UserId          INT NOT NULL,
        MovementDate    DATETIME NOT NULL CONSTRAINT DF_StockMovements_MovementDate DEFAULT(GETDATE()),
        Notes           NVARCHAR(500) NULL,

        CONSTRAINT FK_StockMovements_Products
            FOREIGN KEY (ProductId) REFERENCES dbo.Products(ProductId),
        CONSTRAINT FK_StockMovements_Users
            FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId)
    );
END
GO

-- ---------- 11. PurchaseOrders ----------
IF OBJECT_ID('dbo.PurchaseOrders', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PurchaseOrders (
        PurchaseOrderId  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PurchaseOrders PRIMARY KEY,
        PONumber         NVARCHAR(30) NOT NULL CONSTRAINT UQ_PurchaseOrders_PONumber UNIQUE,
        SupplierId       INT NOT NULL,
        UserId           INT NOT NULL,
        OrderDate        DATETIME NOT NULL,
        Notes            NVARCHAR(250) NULL,
        CreatedAt        DATETIME NOT NULL CONSTRAINT DF_PurchaseOrders_CreatedAt DEFAULT(GETDATE()),

        CONSTRAINT FK_PurchaseOrders_Suppliers
            FOREIGN KEY (SupplierId) REFERENCES dbo.Suppliers(SupplierId),
        CONSTRAINT FK_PurchaseOrders_Users
            FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId)
    );
END
GO

-- ---------- 12. PurchaseOrderItems ----------
IF OBJECT_ID('dbo.PurchaseOrderItems', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PurchaseOrderItems (
        PurchaseOrderItemId  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PurchaseOrderItems PRIMARY KEY,
        PurchaseOrderId      INT NOT NULL,
        ProductId            INT NOT NULL,
        Quantity             INT NOT NULL,

        CONSTRAINT FK_PurchaseOrderItems_PurchaseOrders
            FOREIGN KEY (PurchaseOrderId) REFERENCES dbo.PurchaseOrders(PurchaseOrderId),
        CONSTRAINT FK_PurchaseOrderItems_Products
            FOREIGN KEY (ProductId) REFERENCES dbo.Products(ProductId)
    );
END
GO

-- ---------- Indexes ----------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Sales_SaleDate')
    CREATE NONCLUSTERED INDEX IX_Sales_SaleDate
        ON dbo.Sales(SaleDate) INCLUDE (Status, Subtotal, PaymentMethod);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SaleItems_SaleId')
    CREATE NONCLUSTERED INDEX IX_SaleItems_SaleId
        ON dbo.SaleItems(SaleId) INCLUDE (ProductId, Quantity, LineTotal, UnitCost);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SaleItems_ProductId')
    CREATE NONCLUSTERED INDEX IX_SaleItems_ProductId
        ON dbo.SaleItems(ProductId) INCLUDE (SaleId, Quantity, LineTotal);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StockMovements_ProductId')
    CREATE NONCLUSTERED INDEX IX_StockMovements_ProductId
        ON dbo.StockMovements(ProductId, MovementDate);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StockMovements_MovementDate')
    CREATE NONCLUSTERED INDEX IX_StockMovements_MovementDate
        ON dbo.StockMovements(MovementDate);
GO

-- ---------- Seed: 5 fixed categories (locked business decision) ----------
IF NOT EXISTS (SELECT 1 FROM dbo.Categories)
BEGIN
    INSERT INTO dbo.Categories (CategoryName, IsActive) VALUES
        (N'Engine Parts', 1),
        (N'Lubricants',   1),
        (N'Tires',        1),
        (N'Electrical',   1),
        (N'Accessories',  1);
END
GO