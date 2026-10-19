
USE [PortfolioBillingDemo];
GO

SET XACT_ABORT ON;
GO

BEGIN TRY
 -- Never seed a database that already contains business data.
    IF EXISTS (SELECT 1 FROM dbo.Customers)
       OR EXISTS (SELECT 1 FROM dbo.Invoices)
       OR EXISTS (SELECT 1 FROM dbo.InvoiceItems)
       OR EXISTS (SELECT 1 FROM dbo.Payments)
    BEGIN
        THROW 50002,
            'Database is not empty. Demo data was not inserted.',
            1;
    END;
BEGIN TRANSACTION;


    -- Prevent accidentally inserting duplicate demo data.
    IF EXISTS (
        SELECT 1
        FROM dbo.Customers
        WHERE Email = 'demo.customer01@example.com'
    )
    BEGIN
        THROW 50001, 'Demo data already exists. No records inserted.', 1;
    END;

    DECLARE @Customers TABLE
    (
        RowNum int PRIMARY KEY,
        CustomerId int NOT NULL
    );

    DECLARE @Invoices TABLE
    (
        RowNum int PRIMARY KEY,
        InvoiceId int NOT NULL
    );

    -- Create 10 sample customers.
    DECLARE @i int = 1;

    WHILE @i <= 10
    BEGIN
        INSERT INTO dbo.Customers
            (FirstName, LastName, Email, IsActive, CreatedDate)
        VALUES
            (
                CONCAT('Demo', @i),
                CONCAT('Customer', @i),
                CONCAT(
                    'demo.customer',
                    RIGHT(CONCAT('00', @i), 2),
                    '@example.com'
                ),
                1,
                DATEADD(DAY, -90, SYSUTCDATETIME())
            );

        INSERT INTO @Customers
        VALUES (@i, CONVERT(int, SCOPE_IDENTITY()));

        SET @i += 1;
    END;

    -- Create 15 invoices with a variety of statuses.
    SET @i = 1;

    WHILE @i <= 15
    BEGIN
        DECLARE @CustomerId int;
        DECLARE @InvoiceDate date;
        DECLARE @DueDate date;
        DECLARE @Status varchar(20);

        SELECT @CustomerId = CustomerId
        FROM @Customers
        WHERE RowNum = ((@i - 1) % 10) + 1;

        SET @InvoiceDate =
            DATEADD(DAY, -(@i * 7), CONVERT(date, GETDATE()));

        SET @DueDate = DATEADD(DAY, 30, @InvoiceDate);

        SET @Status =
            CASE
                WHEN @i <= 5 THEN 'Paid'
                ELSE 'Open'
            END;

        INSERT INTO dbo.Invoices
            (CustomerId, InvoiceDate, DueDate, Status)
        VALUES
            (@CustomerId, @InvoiceDate, @DueDate, @Status);

        INSERT INTO @Invoices
        VALUES (@i, CONVERT(int, SCOPE_IDENTITY()));

        SET @i += 1;
    END;

    -- Add two line items to each invoice.
    SET @i = 1;

    WHILE @i <= 15
    BEGIN
        DECLARE @InvoiceId int;

        SELECT @InvoiceId = InvoiceId
        FROM @Invoices
        WHERE RowNum = @i;

        INSERT INTO dbo.InvoiceItems
            (InvoiceId, Description, Quantity, UnitPrice)
        VALUES
            (@InvoiceId, 'Professional Services', 2, 125.00),
            (@InvoiceId, 'Support and Maintenance', 1, 75.00);

        SET @i += 1;
    END;

    -- Each invoice totals $325.00.
    -- Five fully paid invoices.
    INSERT INTO dbo.Payments
        (InvoiceId, PaymentDate, Amount)
    SELECT InvoiceId, CONVERT(date, GETDATE()), 325.00
    FROM @Invoices
    WHERE RowNum BETWEEN 1 AND 5;

    -- Three partially paid invoices.
    INSERT INTO dbo.Payments
        (InvoiceId, PaymentDate, Amount)
    SELECT InvoiceId, CONVERT(date, GETDATE()), 100.00
    FROM @Invoices
    WHERE RowNum BETWEEN 6 AND 8;

    COMMIT TRANSACTION;

    PRINT 'Demo data inserted successfully.';
    PRINT 'Customers: 10';
    PRINT 'Invoices: 15';
    PRINT 'InvoiceItems: 30';
    PRINT 'Payments: 8';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
    BEGIN
        ROLLBACK TRANSACTION;
    END;
    THROW;
END CATCH;
GO
