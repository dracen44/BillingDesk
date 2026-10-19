using BillingDesk.Data;
using BillingDesk.Services;
using Microsoft.EntityFrameworkCore;

namespace BillingDesk.Tests;

public class PaymentServiceTests
{
    private static readonly DateTime InvoiceDate = new(2025, 1, 10);

    [Fact]
    public async Task ValidPartialPayment_InsertsPayment_LeavesInvoiceOpen()
    {
        await using var db = new PaymentTestDatabase();
        await db.InitializeAsync();
        var invoiceId = await db.SeedInvoiceAsync(100m, InvoiceDate);

        var result = await db.CreateService().RecordPaymentAsync(invoiceId, 40m, InvoiceDate.AddDays(1));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.PaymentId);
        Assert.Equal("Open", result.InvoiceStatus);
        Assert.Equal(60m, result.RemainingBalance);

        var state = await db.ReadInvoiceAsync(invoiceId);
        Assert.Equal("Open", state.Status);
        Assert.Equal(40m, state.Paid);
        Assert.Equal(1, state.PaymentCount);
    }

    [Fact]
    public async Task ValidFullPayment_InsertsPayment_MarksInvoicePaid()
    {
        await using var db = new PaymentTestDatabase();
        await db.InitializeAsync();
        var invoiceId = await db.SeedInvoiceAsync(100m, InvoiceDate, 30m);

        var result = await db.CreateService().RecordPaymentAsync(invoiceId, 70m, InvoiceDate.AddDays(2));

        Assert.True(result.IsSuccess);
        Assert.Equal("Paid", result.InvoiceStatus);
        Assert.Equal(0m, result.RemainingBalance);

        var state = await db.ReadInvoiceAsync(invoiceId);
        Assert.Equal("Paid", state.Status);
        Assert.Equal(100m, state.Paid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task ZeroOrNegativeAmount_IsRejected_NoPaymentWritten(decimal amount)
    {
        await using var db = new PaymentTestDatabase();
        await db.InitializeAsync();
        var invoiceId = await db.SeedInvoiceAsync(100m, InvoiceDate);

        var result = await db.CreateService().RecordPaymentAsync(invoiceId, amount, InvoiceDate);

        Assert.False(result.IsSuccess);
        Assert.Equal("Payment amount must be greater than zero.", result.ErrorMessage);
        Assert.Equal(0, (await db.ReadInvoiceAsync(invoiceId)).PaymentCount);
    }

    [Fact]
    public async Task AmountGreaterThanBalance_IsRejected_NoPaymentWritten()
    {
        await using var db = new PaymentTestDatabase();
        await db.InitializeAsync();
        var invoiceId = await db.SeedInvoiceAsync(100m, InvoiceDate, 30m);

        var result = await db.CreateService().RecordPaymentAsync(invoiceId, 70.01m, InvoiceDate);

        Assert.False(result.IsSuccess);
        Assert.Contains("cannot exceed the remaining balance", result.ErrorMessage);
        var state = await db.ReadInvoiceAsync(invoiceId);
        Assert.Equal(1, state.PaymentCount);
        Assert.Equal("Open", state.Status);
    }

    [Fact]
    public async Task PaymentDateBeforeInvoiceDate_IsRejected()
    {
        await using var db = new PaymentTestDatabase();
        await db.InitializeAsync();
        var invoiceId = await db.SeedInvoiceAsync(100m, InvoiceDate);

        var result = await db.CreateService().RecordPaymentAsync(invoiceId, 10m, InvoiceDate.AddDays(-1));

        Assert.False(result.IsSuccess);
        Assert.Contains("cannot be before the invoice date", result.ErrorMessage);
        Assert.Equal(0, (await db.ReadInvoiceAsync(invoiceId)).PaymentCount);
    }

    [Fact]
    public async Task FullyPaidInvoice_IsRejected()
    {
        await using var db = new PaymentTestDatabase();
        await db.InitializeAsync();
        var invoiceId = await db.SeedInvoiceAsync(100m, InvoiceDate, 100m);

        var result = await db.CreateService().RecordPaymentAsync(invoiceId, 1m, InvoiceDate);

        Assert.False(result.IsSuccess);
        Assert.Equal("This invoice has no remaining balance.", result.ErrorMessage);
    }

    [Fact]
    public async Task DuplicateSubmission_SecondAttemptIsRejected_BalanceNotOverpaid()
    {
        await using var db = new PaymentTestDatabase();
        await db.InitializeAsync();
        var invoiceId = await db.SeedInvoiceAsync(100m, InvoiceDate);
        var service = db.CreateService();

        var first = await service.RecordPaymentAsync(invoiceId, 100m, InvoiceDate);
        var second = await service.RecordPaymentAsync(invoiceId, 100m, InvoiceDate);

        Assert.True(first.IsSuccess);
        Assert.False(second.IsSuccess);
        var state = await db.ReadInvoiceAsync(invoiceId);
        Assert.Equal(100m, state.Paid);
        Assert.Equal(1, state.PaymentCount);
    }

    [Fact]
    public async Task ConcurrentSubmissions_NeverOverpay()
    {
        await using var db = new PaymentTestDatabase();
        await db.InitializeAsync();
        var invoiceId = await db.SeedInvoiceAsync(100m, InvoiceDate);

        var factory = new TestContextFactory(db.Options);
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => new PaymentService(factory, Microsoft.Extensions.Logging.Abstractions.NullLogger<PaymentService>.Instance)
                .RecordPaymentAsync(invoiceId, 60m, InvoiceDate))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        var successes = results.Count(r => r.IsSuccess);
        Assert.Equal(1, successes);

        var state = await db.ReadInvoiceAsync(invoiceId);
        Assert.Equal(60m, state.Paid);
        Assert.True(state.Paid <= 100m);
        Assert.Equal(1, state.PaymentCount);
    }

    [Fact]
    public async Task DatabaseFailure_RollsBack_ReturnsSafeMessage_NoPaymentWritten()
    {
        await using var db = new PaymentTestDatabase();
        await db.InitializeAsync();
        var invoiceId = await db.SeedInvoiceAsync(100m, InvoiceDate);

        var service = db.CreateService(new FailingContextFactory(db.Options));

        var result = await service.RecordPaymentAsync(invoiceId, 25m, InvoiceDate);

        Assert.False(result.IsSuccess);
        Assert.Equal("The payment could not be saved. No changes were made. Please try again.", result.ErrorMessage);
        Assert.DoesNotContain("SqlException", result.ErrorMessage);

        var state = await db.ReadInvoiceAsync(invoiceId);
        Assert.Equal(0, state.PaymentCount);
        Assert.Equal("Open", state.Status);
    }

    /// <summary>
    /// Returns a context whose SaveChanges throws after validation, to exercise rollback on a database failure.
    /// </summary>
    private sealed class FailingContextFactory : IDbContextFactory<BillingDbContext>
    {
        private readonly DbContextOptions<BillingDbContext> _options;

        public FailingContextFactory(DbContextOptions<BillingDbContext> options) => _options = options;

        public BillingDbContext CreateDbContext() => new FailingContext(_options);

        public Task<BillingDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<BillingDbContext>(new FailingContext(_options));
    }

    private sealed class FailingContext : BillingDbContext
    {
        public FailingContext(DbContextOptions<BillingDbContext> options) : base(options)
        {
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated database failure");
    }
}
