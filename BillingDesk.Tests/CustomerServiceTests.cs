using BillingDesk.Data;
using BillingDesk.Models;
using BillingDesk.Services;

namespace BillingDesk.Tests;

public class CustomerServiceTests
{
    [Fact]
    public async Task CustomerWithZeroItemInvoice_IsCountedWithZeroTotals()
    {
        await using var db = new PaymentTestDatabase();
        await db.InitializeAsync();
        var customerId = await SeedCustomerWithInvoiceAsync(db, [], []);
        var service = new CustomerService(new TestContextFactory(db.Options));

        var result = await service.GetCustomersAsync();

        var customer = Assert.Single(result.Items);
        Assert.Equal(customerId, customer.CustomerId);
        Assert.Equal(1, customer.InvoiceCount);
        Assert.Equal(0m, customer.TotalBilled);
        Assert.Equal(0m, customer.TotalPaid);
        Assert.Equal(0m, customer.OutstandingBalance);
    }

    [Fact]
    public async Task CustomerInvoices_OnlyReturnsInvoicesForRequestedCustomer()
    {
        await using var db = new PaymentTestDatabase();
        await db.InitializeAsync();
        var customerA = await FinancialSeedHelper.SeedCustomerAsync(db, "IsolationA");
        var customerB = await FinancialSeedHelper.SeedCustomerAsync(db, "IsolationB");
        var invoiceA = await FinancialSeedHelper.SeedInvoiceAsync(db, customerA, [(1, 100m)], [25m]);
        await FinancialSeedHelper.SeedInvoiceAsync(db, customerB, [(1, 999m)], [1m]);
        var service = new CustomerService(new TestContextFactory(db.Options));

        var result = await service.GetCustomerInvoicesAsync(customerA);

        var invoice = Assert.Single(result);
        Assert.Equal(invoiceA, invoice.InvoiceId);
        Assert.Equal(100m, invoice.InvoiceTotal);
        Assert.Equal(25m, invoice.TotalPaid);
        Assert.Equal(75m, invoice.Balance);
    }

    [Fact]
    public async Task CustomerInvoices_ZeroItemInvoice_HasZeroTotalsAndBalance()
    {
        await using var db = new PaymentTestDatabase();
        await db.InitializeAsync();
        var customerId = await FinancialSeedHelper.SeedCustomerAsync(db, "ZeroItems");
        var invoiceId = await FinancialSeedHelper.SeedInvoiceAsync(db, customerId, [], []);
        var service = new CustomerService(new TestContextFactory(db.Options));

        var result = await service.GetCustomerInvoicesAsync(customerId);

        var invoice = Assert.Single(result);
        Assert.Equal(invoiceId, invoice.InvoiceId);
        Assert.Equal(0m, invoice.InvoiceTotal);
        Assert.Equal(0m, invoice.TotalPaid);
        Assert.Equal(0m, invoice.Balance);
    }

    [Fact]
    public async Task CustomerInvoices_Overpayment_ClampsBalanceToZero()
    {
        await using var db = new PaymentTestDatabase();
        await db.InitializeAsync();
        var customerId = await FinancialSeedHelper.SeedCustomerAsync(db, "Overpaid");
        await FinancialSeedHelper.SeedInvoiceAsync(db, customerId, [(1, 50m)], [80m]);
        var service = new CustomerService(new TestContextFactory(db.Options));

        var result = await service.GetCustomerInvoicesAsync(customerId);

        var invoice = Assert.Single(result);
        Assert.Equal(50m, invoice.InvoiceTotal);
        Assert.Equal(80m, invoice.TotalPaid);
        Assert.Equal(0m, invoice.Balance);
    }

    [Fact]
    public async Task CustomerListAndDetail_FinancialTotals_MatchEachOther()
    {
        await using var db = new PaymentTestDatabase();
        await db.InitializeAsync();
        var customerId = await FinancialSeedHelper.SeedCustomerAsync(db, "Consistent");
        await FinancialSeedHelper.SeedInvoiceAsync(db, customerId, [(1, 100m)], [150m]);
        await FinancialSeedHelper.SeedInvoiceAsync(db, customerId, [(1, 100m)], [40m]);
        await FinancialSeedHelper.SeedInvoiceAsync(db, customerId, [], []);
        var service = new CustomerService(new TestContextFactory(db.Options));
        var dashboard = new DashboardService(new TestContextFactory(db.Options));

        var list = await service.GetCustomersAsync();
        var listItem = Assert.Single(list.Items, c => c.CustomerId == customerId);
        var detail = await service.GetCustomerDetailAsync(customerId);
        var invoices = await service.GetCustomerInvoicesAsync(customerId);
        var dashboardOutstanding = await dashboard.GetTotalOutstandingBalanceAsync();

        Assert.NotNull(detail);
        Assert.Equal(detail.TotalBilled, listItem.TotalBilled);
        Assert.Equal(detail.TotalPaid, listItem.TotalPaid);
        Assert.Equal(detail.OutstandingBalance, listItem.OutstandingBalance);
        Assert.Equal(detail.InvoiceCount, listItem.InvoiceCount);

        Assert.Equal(200m, detail.TotalBilled);
        Assert.Equal(190m, detail.TotalPaid);
        Assert.Equal(60m, detail.OutstandingBalance);

        Assert.Equal(detail.OutstandingBalance, invoices.Sum(i => i.Balance));
        Assert.Equal(detail.OutstandingBalance, dashboardOutstanding);
    }

    private static async Task<int> SeedCustomerWithInvoiceAsync(
        PaymentTestDatabase db,
        (int Quantity, decimal UnitPrice)[] items,
        decimal[] payments)
    {
        await using var context = new BillingDbContext(db.Options);

        var customer = new Customer
        {
            FirstName = "Zero",
            LastName = "Items",
            Email = "zero@example.com",
            IsActive = true,
            CreatedDate = DateTime.Today
        };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var invoice = new Invoice
        {
            CustomerId = customer.CustomerId,
            InvoiceDate = DateTime.Today,
            DueDate = DateTime.Today.AddDays(30),
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
                PaymentDate = DateTime.Today,
                Amount = amount
            });
        }

        await context.SaveChangesAsync();
        return customer.CustomerId;
    }
}
