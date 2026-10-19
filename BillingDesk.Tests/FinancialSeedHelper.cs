using BillingDesk.Data;
using BillingDesk.Models;

namespace BillingDesk.Tests;

/// <summary>
/// Seeds customers, invoices, line items, and payments into an isolated test database.
/// </summary>
internal static class FinancialSeedHelper
{
    public static async Task<int> SeedCustomerAsync(PaymentTestDatabase db, string firstName)
    {
        await using var context = new BillingDbContext(db.Options);

        var customer = new Customer
        {
            FirstName = firstName,
            LastName = "Test",
            Email = $"{firstName.ToLowerInvariant()}.{Guid.NewGuid():N}@example.com",
            IsActive = true,
            CreatedDate = DateTime.Today
        };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        return customer.CustomerId;
    }

    public static async Task<int> SeedInvoiceAsync(
        PaymentTestDatabase db,
        int customerId,
        (int Quantity, decimal UnitPrice)[] items,
        decimal[] payments,
        DateTime? invoiceDate = null)
    {
        await using var context = new BillingDbContext(db.Options);

        var date = (invoiceDate ?? DateTime.Today).Date;
        var invoice = new Invoice
        {
            CustomerId = customerId,
            InvoiceDate = date,
            DueDate = date.AddDays(30),
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
                PaymentDate = date,
                Amount = amount
            });
        }

        await context.SaveChangesAsync();
        return invoice.InvoiceId;
    }
}
