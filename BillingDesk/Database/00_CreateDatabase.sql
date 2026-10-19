
USE [master];
GO

IF DB_ID(N'PortfolioBillingDemo') IS NULL
BEGIN
    CREATE DATABASE [PortfolioBillingDemo];
END
GO

USE [PortfolioBillingDemo];
GO

PRINT 'PortfolioBillingDemo database is ready.';
GO
