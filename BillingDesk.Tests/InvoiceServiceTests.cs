using BillingDesk.Data;
using BillingDesk.Models;
using BillingDesk.Services;

namespace BillingDesk.Tests;

public class InvoiceServiceTests
{
    private static readonly DateTime InvoiceDate = new(2025, 1, 10);

    [Fact]
    public async Task ZeroItemInvoice_IsListed_WithZeroTotals()
    {
        await using var db = new PaymentTestDatabase();
        await db.InitializeAsync();
        var invoiceId = await SeedInvoiceAsync(db, InvoiceDate, Array.Empty<(int, decimal)>(), Array.Empty<decimal>());

        var result = await new InvoiceService(new TestContextFactory(db.Options)).GetInvoicesAsync();

        Assert.Equal(1, result.TotalCount);
        var item = Assert.Single(result.Items);
        Assert.Equal(invoiceId, item.InvoiceId);
        Assert.Equal(0m, item.InvoiceTotal);
        Assert.Equal(0m, item.TotalPaid);
        Assert.Equal(0m, item.Balance);
    }

    [Fact]
    public async Task InvoiceList_TotalCountAndFinancials_AreCorrect()
    {
        await using var db = new PaymentTestDatabase();
        await db.InitializeAsync();
        var zeroItemId = await SeedInvoiceAsync(db, InvoiceDate.AddDays(3), Array.Empty<(int, decimal)>(), Array.Empty<decimal>());
        var multiId = await SeedInvoiceAsync(db, InvoiceDate.AddDays(2), [(2, 10m), (1, 5m)], [10m, 5m]);
        var partialId = await SeedInvoiceAsync(db, InvoiceDate.AddDays(1), [(1, 100m)], [30m]);
        var overpaidId = await SeedInvoiceAsync(db, InvoiceDate, [(1, 50m)], [60m]);

        var result = await new InvoiceService(new TestContextFactory(db.Options)).GetInvoicesAsync();

        Assert.Equal(4, result.TotalCount);
        Assert.Equal(new[] { zeroItemId, multiId, partialId, overpaidId }, result.Items.Select(i => i.InvoiceId).ToArray());

        var multi = result.Items.Single(i => i.InvoiceId == multiId);
        Assert.Equal(25m, multi.InvoiceTotal);
        Assert.Equal(15m, multi.TotalPaid);
        Assert.Equal(10m, multi.Balance);

        var partial = result.Items.Single(i => i.InvoiceId == partialId);
        Assert.Equal(100m, partial.InvoiceTotal);
        Assert.Equal(30m, partial.TotalPaid);
        Assert.Equal(70m, partial.Balance);

        var overpaid = result.Items.Single(i => i.InvoiceId == overpaidId);
        Assert.Equal(50m, overpaid.InvoiceTotal);
        Assert.Equal(60m, overpaid.TotalPaid);
        Assert.Equal(0m, overpaid.Balance);
    }

    [Fact]
    public async Task InvoicePaging_SharedDate_IsDeterministicAndComplete()
    {
        await using var db = new PaymentTestDatabase();
        await db.InitializeAsync();
        var seededIds = new List<int>();
        for (var i = 0; i < 5; i++)
        {
            seededIds.Add(await SeedInvoiceAsync(db, InvoiceDate, [(1, 10m)], Array.Empty<decimal>()));
        }
        var expectedOrder = seededIds.OrderByDescending(id => id).ToArray();
        var service = new InvoiceService(new TestContextFactory(db.Options));

        var firstPage = await service.GetInvoicesAsync(pageNumber: 1, pageSize: 2);
        var firstPageRepeat = await service.GetInvoicesAsync(pageNumber: 1, pageSize: 2);
        var secondPage = await service.GetInvoicesAsync(pageNumber: 2, pageSize: 2);
        var secondPageRepeat = await service.GetInvoicesAsync(pageNumber: 2, pageSize: 2);
        var thirdPage = await service.GetInvoicesAsync(pageNumber: 3, pageSize: 2);

        var firstIds = firstPage.Items.Select(i => i.InvoiceId).ToArray();
        var secondIds = secondPage.Items.Select(i => i.InvoiceId).ToArray();
        var thirdIds = thirdPage.Items.Select(i => i.InvoiceId).ToArray();

        Assert.Equal(firstIds, firstPageRepeat.Items.Select(i => i.InvoiceId).ToArray());
        Assert.Equal(secondIds, secondPageRepeat.Items.Select(i => i.InvoiceId).ToArray());
        Assert.Equal(expectedOrder, firstIds.Concat(secondIds).Concat(thirdIds).ToArray());
        Assert.All(new[] { firstPage, secondPage, thirdPage }, p => Assert.Equal(5, p.TotalCount));
    }

    private static async Task<int> SeedInvoiceAsync(
        PaymentTestDatabase db,
        DateTime invoiceDate,
        (int Quantity, decimal UnitPrice)[] items,
        decimal[] payments)
    {
        await using var context = new BillingDbContext(db.Options);

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

        foreach (var (quantity, unitPrice) in items)
        {
            context.InvoiceItems.Add(new InvoiceItem
            {
                InvoiceId = invoice.InvoiceId,
                Description = "Service",
                Quantity = quantity,
                UnitPrice = unitPrice
            });
        }

        foreach (var amount in payments)
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
}
