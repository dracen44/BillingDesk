# BillingDesk

**A C#/.NET desktop billing management application built with WPF, Entity Framework Core, and Microsoft SQL Server.**

BillingDesk is a portfolio application demonstrating enterprise software development practices, financial business logic, relational database design, and transactional payment processing.

## Features

- **Dashboard:** Financial summaries, outstanding balances, overdue invoices, and recent activity.
- **Customer Management:** Searchable, paginated customer list with financial summaries and invoice history.
- **Invoice Management:** Invoice browsing, detailed line items, payment history, and balance calculations.
- **Payment Management:** Payment search, payment details, and navigation between related records.
- **Payment Entry:** Record partial or full payments with validation, database transactions, and concurrency protection.
- **Automated Testing:** Integration tests for financial calculations, payment validation, transaction rollback, and concurrent submissions.

## Technology Stack

| Component | Technology |
|---|---|
| Language | C# |
| Framework | .NET 10 |
| Desktop UI | WPF |
| Architecture | MVVM |
| MVVM Toolkit | CommunityToolkit.Mvvm |
| Database | Microsoft SQL Server |
| Data Access | Entity Framework Core |
| Dependency Injection | Microsoft.Extensions.Hosting |
| Testing | xUnit, SQL Server LocalDB |

## Architecture

BillingDesk separates responsibilities into:

- **Views:** WPF user interfaces and navigation.
- **ViewModels:** Presentation state, commands, and user interaction.
- **Services:** Database queries, financial calculations, and payment operations.
- **Data:** Entity Framework Core database context and entity configurations.
- **Models:** Customers, invoices, invoice items, and payments.
- **Tests:** Integration tests using isolated SQL Server LocalDB databases.

## Financial Business Rules

Invoice totals are calculated from their line items rather than stored as redundant values.

**Invoice Total = Sum of (Quantity × Unit Price)**

**Amount Paid = Sum of recorded payments**

**Outstanding Balance = Maximum of (Invoice Total − Amount Paid, 0)**

Outstanding balances are calculated per invoice before aggregation, preventing an overpayment on one invoice from reducing the outstanding balance of another.

Payment entry validates amounts and dates, recalculates balances within a database transaction, and uses SQL Server locking to protect against concurrent overpayment attempts.

## Testing

The current automated test suite contains **26 passing tests** covering payment processing, concurrency, rollback behavior, financial aggregation, and edge cases such as zero-item and overpaid invoices.

Tests use isolated SQL Server LocalDB databases rather than the demonstration database.

## Project Status

Core application functionality is complete. Repository documentation and reproducible setup instructions are being finalized.

## Screenshots

Application screenshots will be added before publication.

## Local Setup

Detailed prerequisites, database initialization, and configuration instructions will be added after verifying the repository's SQL setup scripts.

## Purpose

This project was developed as a professional portfolio demonstration of C#/.NET application development, SQL Server integration, enterprise business logic, and software testing.