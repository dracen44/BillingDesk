using BillingDesk.Services;

namespace BillingDesk.Tests;

public class DashboardServiceTests
{
    private static DashboardService CreateService(PaymentTestDatabase db) =>
        new(new TestContextFactory(db.Options));

    [Fact]
    public async Task OutstandingBalance_UnpaidInvoice_EqualsFullTotal()
    {
        await using var db = new PaymentTestDatabase();
        await db.InitializeAsync();
        var customerId = await FinancialSeedHelper.SeedCustomerAsync(db, "Unpaid");
        await FinancialSeedHelper.SeedInvoiceAsync(db, customerId, [(2, 50m)], []);

        var total = await CreateService(db).GetTotalOutstandingBalanceAsync();

        Assert.Equal(100m, total);
    }

    [Fact]
    public async Task OutstandingBalance_PartiallyPaidInvoice_EqualsRemainder()
    {
        await using var db = new PaymentTestDatabase();
        await db.InitializeAsync();
        var customerId = await FinancialSeedHelper.SeedCustomerAsync(db, "Partial");
        await FinancialSeedHelper.SeedInvoiceAsync(db, customerId, [(1, 100m)], [40m]);

        var total = await CreateService(db).GetTotalOutstandingBalanceAsync();

        Assert.Equal(60m, total);
    }

    [Fact]
    public async Task OutstandingBalance_FullyPaidInvoice_IsZero()
    {
        await using var db = new PaymentTestDatabase();
        await db.InitializeAsync();
        var customerId = await FinancialSeedHelper.SeedCustomerAsync(db, "Paid");
        await FinancialSeedHelper.SeedInvoiceAsync(db, customerId, [(1, 100m)], [100m]);

        var total = await CreateService(db).GetTotalOutstandingBalanceAsync();

        Assert.Equal(0m, total);
    }

    [Fact]
    public async Task OutstandingBalance_ZeroItemInvoice_ContributesZero()
    {
        await using var db = new PaymentTestDatabase();
        await db.InitializeAsync();
        var customerId = await FinancialSeedHelper.SeedCustomerAsync(db, "Empty");
        await FinancialSeedHelper.SeedInvoiceAsync(db, customerId, [], []);

        var total = await CreateService(db).GetTotalOutstandingBalanceAsync();

        Assert.Equal(0m, total);
    }

    [Fact]
    public async Task OutstandingBalance_OverpaidInvoice_IsClampedToZero()
    {
        await using var db = new PaymentTestDatabase();
        await db.InitializeAsync();
        var customerId = await FinancialSeedHelper.SeedCustomerAsync(db, "Overpaid");
        await FinancialSeedHelper.SeedInvoiceAsync(db, customerId, [(1, 100m)], [150m]);

        var total = await CreateService(db).GetTotalOutstandingBalanceAsync();

        Assert.Equal(0m, total);
    }

    [Fact]
    public async Task OutstandingBalance_Overpayment_DoesNotReduceAnotherInvoice()
    {
        await using var db = new PaymentTestDatabase();
        await db.InitializeAsync();
        var customerId = await FinancialSeedHelper.SeedCustomerAsync(db, "Isolation");
        await FinancialSeedHelper.SeedInvoiceAsync(db, customerId, [(1, 100m)], [150m]);
        await FinancialSeedHelper.SeedInvoiceAsync(db, customerId, [(1, 100m)], []);

        var total = await CreateService(db).GetTotalOutstandingBalanceAsync();

        Assert.Equal(100m, total);
    }

    [Fact]
    public async Task OutstandingBalance_MixedInvoices_SumsOnlyPositiveBalances()
    {
        await using var db = new PaymentTestDatabase();
        await db.InitializeAsync();
        var customerA = await FinancialSeedHelper.SeedCustomerAsync(db, "MixedA");
        var customerB = await FinancialSeedHelper.SeedCustomerAsync(db, "MixedB");
        await FinancialSeedHelper.SeedInvoiceAsync(db, customerA, [(1, 100m)], [40m]);
        await FinancialSeedHelper.SeedInvoiceAsync(db, customerA, [(1, 100m)], []);
        await FinancialSeedHelper.SeedInvoiceAsync(db, customerA, [(1, 100m)], [100m]);
        await FinancialSeedHelper.SeedInvoiceAsync(db, customerB, [], []);
        await FinancialSeedHelper.SeedInvoiceAsync(db, customerB, [(1, 50m)], [80m]);

        var total = await CreateService(db).GetTotalOutstandingBalanceAsync();

        Assert.Equal(160m, total);
    }

    [Fact]
    public async Task RecentPayments_SameDate_OrdersByPaymentIdDescending()
    {
        await using var db = new PaymentTestDatabase();
        await db.InitializeAsync();
        var customerId = await FinancialSeedHelper.SeedCustomerAsync(db, "Recent");
        var invoiceId = await FinancialSeedHelper.SeedInvoiceAsync(db, customerId, [(1, 100m)], [10m, 20m, 30m]);

        var payments = await CreateService(db).GetRecentPaymentsAsync(10);

        Assert.Equal(3, payments.Count);
        Assert.All(payments, p => Assert.Equal(invoiceId, p.InvoiceId));
        var ids = payments.Select(p => p.PaymentId).ToList();
        Assert.Equal(ids.OrderByDescending(id => id).ToList(), ids);
    }
}
