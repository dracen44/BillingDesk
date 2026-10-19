# BillingDesk — Database Setup

BillingDesk is a WPF application built with .NET, Entity Framework Core, and Microsoft SQL Server.

These instructions create a local demonstration database for running the application.

## Prerequisites

- Visual Studio with .NET desktop development support
- .NET 10 SDK
- SQL Server Express LocalDB or a compatible SQL Server instance
- SQL Server Management Studio (SSMS)

## 1. Create the database

Open SSMS and connect to your SQL Server instance.

Run:

`Database/00_CreateDatabase.sql`

This creates the `PortfolioBillingDemo` database if it does not already exist.

**Important:** Use a new, empty database. Do not run the schema or seed scripts against an existing populated database.

## 2. Create the tables

Execute these scripts in the following order:

1. `Database/customers_create.sql`
2. `Database/invoices_create.sql`
3. `Database/invoice items create.sql`
4. `Database/payments_create.sql`

These scripts create the Customers, Invoices, InvoiceItems, and Payments tables.

## 3. Insert sample data

Execute:

`Database/05_SeedDemoData.sql`

The script populates an empty database with:

| Table | Records |
|---|---:|
| Customers | 10 |
| Invoices | 15 |
| InvoiceItems | 30 |
| Payments | 8 |

The sample data includes paid, unpaid, and partially paid invoices.

Expected financial totals:

| Metric | Amount |
|---|---:|
| Total invoiced | $4,875.00 |
| Total paid | $1,925.00 |
| Outstanding balance | $2,950.00 |

The seed script refuses to insert data if any of the four business tables already contain records.

## 4. Configure the connection

Configure BillingDesk to connect to your SQL Server instance and the `PortfolioBillingDemo` database.

Use the application's existing configuration mechanism or .NET User Secrets. Do not commit credentials or private connection strings to GitHub.

## 5. Run BillingDesk

Open `BillingDesk.slnx` in Visual Studio.

Restore NuGet packages, build the solution, and run the `BillingDesk` application.

## 6. Run the tests

Use Visual Studio's Test Explorer to run the tests in `BillingDesk.Tests`.

Some integration tests require a working local SQL Server instance.

## Troubleshooting

**Database connection failure:** Verify the SQL Server instance name, connection string, and authentication settings.

**Missing table error:** Verify that all four schema scripts were executed in the correct order.

**Seed script refuses to run:** The database already contains records. Use a new, empty database rather than deleting existing data.

**Build failure:** Confirm that the .NET 10 SDK and required Visual Studio workloads are installed.