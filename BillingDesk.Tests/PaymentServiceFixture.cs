using BillingDesk.Data;
using BillingDesk.Models;
using BillingDesk.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BillingDesk.Tests;

/// <summary>
/// Creates an isolated SQL Server database per test so the demo database is never touched.
/// </summary>
public sealed class PaymentTestDatabase : IAsyncDisposable
{
    private const string MasterConnection = "Server=(localdb)\\MSSQLLocalDB;Database=master;Integrated Security=true;Encrypt=false;TrustServerCertificate=true;";

    public string DatabaseName { get; } = $"BillingDesk_Tests_{Guid.NewGuid():N}";

    public string ConnectionString =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={DatabaseName};Integrated Security=true;Encrypt=false;TrustServerCertificate=true;";

    public DbContextOptions<BillingDbContext> Options =>
        new DbContextOptionsBuilder<BillingDbContext>().UseSqlServer(ConnectionString).Options;

    public async Task InitializeAsync()
    {
        await using (var connection = new Microsoft.Data.SqlClient.SqlConnection(MasterConnection))
        {
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE [{DatabaseName}]";
            await cmd.ExecuteNonQueryAsync();
        }

        await using var context = new BillingDbContext(Options);
        await context.Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// Seeds one customer and one invoice with the given line total. Returns the invoice id.
    /// </summary>
    public async Task<int> SeedInvoiceAsync(decimal unitPrice, DateTime invoiceDate, params decimal[] existingPayments)
    {
        await using var context = new BillingDbContext(Options);

        var customer = new Customer
        {
            FirstName = "Test",
            LastName = "Customer",
            Email = "test@example.com",
            IsActive = true,
            CreatedDate = DateTime.Today
        };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var invoice = new Invoice
        {
            CustomerId = customer.CustomerId,
            InvoiceDate = invoiceDate.Date,
            DueDate = invoiceDate.Date.AddDays(30),
            Status = "Open"
        };
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();

        context.InvoiceItems.Add(new InvoiceItem
        {
            InvoiceId = invoice.InvoiceId,
            Description = "Service",
            Quantity = 1,
            UnitPrice = unitPrice
        });

        foreach (var amount in existingPayments)
        {
            context.Payments.Add(new Payment
            {
                InvoiceId = invoice.InvoiceId,
                PaymentDate = invoiceDate.Date,
                Amount = amount
            });
        }

        await context.SaveChangesAsync();
        return invoice.InvoiceId;
    }

    public async Task<(string Status, decimal Paid, int PaymentCount)> ReadInvoiceAsync(int invoiceId)
    {
        await using var context = new BillingDbContext(Options);
        var status = await context.Invoices.AsNoTracking()
            .Where(i => i.InvoiceId == invoiceId)
            .Select(i => i.Status)
            .SingleAsync();
        var paid = await context.Payments.AsNoTracking()
            .Where(p => p.InvoiceId == invoiceId)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;
        var count = await context.Payments.AsNoTracking()
            .CountAsync(p => p.InvoiceId == invoiceId);
        return (status, paid, count);
    }

    public PaymentService CreateService(IDbContextFactory<BillingDbContext>? factory = null) =>
        new(factory ?? new TestContextFactory(Options), NullLogger<PaymentService>.Instance);

    public async ValueTask DisposeAsync()
    {
        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(MasterConnection);
        await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{DatabaseName}];";
        await cmd.ExecuteNonQueryAsync();
    }
}

internal sealed class TestContextFactory : IDbContextFactory<BillingDbContext>
{
    private readonly DbContextOptions<BillingDbContext> _options;

    public TestContextFactory(DbContextOptions<BillingDbContext> options) => _options = options;

    public BillingDbContext CreateDbContext() => new(_options);

    public Task<BillingDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new BillingDbContext(_options));
}
