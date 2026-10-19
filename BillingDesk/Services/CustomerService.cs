using BillingDesk.Data;
using BillingDesk.Models;
using Microsoft.EntityFrameworkCore;

namespace BillingDesk.Services
{
    /// <summary>
    /// Service for customer operations.
    /// 
    /// CRITICAL: All financial aggregations are calculated correctly:
    /// - InvoiceItems totals are aggregated independently
    /// - Payments totals are aggregated independently
    /// - Only after independent aggregation are they joined
    /// This avoids the one-to-many × one-to-many multiplication bug.
    /// </summary>
    public class CustomerService : ICustomerService
    {
        private readonly IDbContextFactory<BillingDbContext> _dbContextFactory;

        public CustomerService(IDbContextFactory<BillingDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        /// <summary>
        /// Gets paged customer list with optional search filtering.
        /// Search happens in SQL via EF Core.
        /// Paging happens in SQL via Skip/Take.
        /// </summary>
        public async Task<PagedResult<CustomerListItemDto>> GetCustomersAsync(
            string? searchTerm = null,
            int pageNumber = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            // Aggregate invoice items by invoice ID
            var invoiceTotalsQuery = context.InvoiceItems
                .AsNoTracking()
                .GroupBy(ii => ii.InvoiceId)
                .Select(g => new
                {
                    InvoiceId = g.Key,
                    ItemsTotal = g.Sum(ii => ii.Quantity * ii.UnitPrice)
                });

            // Aggregate payments by invoice ID
            var paymentTotalsQuery = context.Payments
                .AsNoTracking()
                .GroupBy(p => p.InvoiceId)
                .Select(g => new
                {
                    InvoiceId = g.Key,
                    PaymentsTotal = g.Sum(p => p.Amount)
                });

            // Combine aggregations with invoice data (every invoice is kept, including invoices without line items)
            var invoiceFinancialsQuery = context.Invoices
                .AsNoTracking()
                .Select(inv => new
                {
                    inv.CustomerId,
                    inv.InvoiceId,
                    ItemsTotal = invoiceTotalsQuery
                        .Where(t => t.InvoiceId == inv.InvoiceId)
                        .Select(t => (decimal?)t.ItemsTotal)
                        .FirstOrDefault() ?? 0m,
                    PaymentsTotal = paymentTotalsQuery
                        .Where(p => p.InvoiceId == inv.InvoiceId)
                        .Select(p => (decimal?)p.PaymentsTotal)
                        .FirstOrDefault() ?? 0m
                });

            // Aggregate by customer
            var customerFinancialsQuery = invoiceFinancialsQuery
                .GroupBy(x => x.CustomerId)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    InvoiceCount = g.Count(),
                    TotalBilled = g.Sum(x => x.ItemsTotal),
                    TotalPaid = g.Sum(x => x.PaymentsTotal),
                    OutstandingBalance = g.Sum(x => x.ItemsTotal > x.PaymentsTotal ? x.ItemsTotal - x.PaymentsTotal : 0m)
                });

            // Build the main query with optional search
            var query = context.Customers
                .AsNoTracking()
                .Where(c =>
                    searchTerm == null ||
                    EF.Functions.Like(c.FirstName, $"%{searchTerm}%") ||
                    EF.Functions.Like(c.LastName, $"%{searchTerm}%") ||
                    EF.Functions.Like(c.Email, $"%{searchTerm}%")
                )
                .OrderBy(c => c.LastName)
                .ThenBy(c => c.FirstName)
                .ThenBy(c => c.CustomerId);

            // Get total count before pagination
            var totalCount = await query.CountAsync(cancellationToken);

            // Get paginated results
            var customers = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            // Get customer IDs for financial lookup
            var customerIds = customers.Select(c => c.CustomerId).ToList();

            // Get financial data for these customers
            var financials = await customerFinancialsQuery
                .Where(cf => customerIds.Contains(cf.CustomerId))
                .ToListAsync(cancellationToken);

            var financialDict = financials.ToDictionary(x => x.CustomerId);

            // Project to DTOs
            var dtos = customers.Select(c =>
            {
                var financial = financialDict.TryGetValue(c.CustomerId, out var f) ? f : null;

                return new CustomerListItemDto
                {
                    CustomerId = c.CustomerId,
                    FirstName = c.FirstName,
                    LastName = c.LastName,
                    Email = c.Email,
                    Status = c.IsActive ? "Active" : "Inactive",
                    InvoiceCount = financial?.InvoiceCount ?? 0,
                    TotalBilled = financial?.TotalBilled ?? 0,
                    TotalPaid = financial?.TotalPaid ?? 0,
                    OutstandingBalance = financial?.OutstandingBalance ?? 0
                };
            }).ToList();

            return new PagedResult<CustomerListItemDto>
            {
                Items = dtos,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        /// <summary>
        /// Gets detailed customer information with billing summary.
        /// </summary>
        public async Task<CustomerDetailDto?> GetCustomerDetailAsync(
            int customerId,
            CancellationToken cancellationToken = default)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            // Get customer
            var customer = await context.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);

            if (customer == null)
                return null;

            // Aggregate invoice items by invoice ID
            var invoiceTotals = await context.InvoiceItems
                .AsNoTracking()
                .Where(ii => ii.Invoice.CustomerId == customerId)
                .GroupBy(ii => ii.InvoiceId)
                .Select(g => new
                {
                    InvoiceId = g.Key,
                    ItemsTotal = g.Sum(ii => ii.Quantity * ii.UnitPrice)
                })
                .ToListAsync(cancellationToken);

            // Aggregate payments by invoice ID
            var paymentTotals = await context.Payments
                .AsNoTracking()
                .Where(p => p.Invoice.CustomerId == customerId)
                .GroupBy(p => p.InvoiceId)
                .Select(g => new
                {
                    InvoiceId = g.Key,
                    PaymentsTotal = g.Sum(p => p.Amount)
                })
                .ToListAsync(cancellationToken);

            // Calculate totals
            var invoiceDict = invoiceTotals.ToDictionary(x => x.InvoiceId);
            var paymentDict = paymentTotals.ToDictionary(x => x.InvoiceId);

            var totalBilled = invoiceTotals.Sum(x => x.ItemsTotal);
            var totalPaid = paymentTotals.Sum(x => x.PaymentsTotal);

            // Get invoice statuses
            var invoices = await context.Invoices
                .AsNoTracking()
                .Where(i => i.CustomerId == customerId)
                .Select(i => new { i.InvoiceId, i.Status })
                .ToListAsync(cancellationToken);

            var invoiceDict2 = invoices.ToDictionary(x => x.InvoiceId);
            var openInvoiceCount = invoices.Count(i => i.Status == "Open");

            // Calculate outstanding balance: floor each invoice balance at 0, then sum
            var outstandingBalance = 0m;
            foreach (var invoiceId in invoiceDict.Keys.Union(paymentDict.Keys))
            {
                var invoiceAmount = invoiceDict.TryGetValue(invoiceId, out var it) ? it.ItemsTotal : 0m;
                var paidAmount = paymentDict.TryGetValue(invoiceId, out var pt) ? pt.PaymentsTotal : 0m;
                var invoiceBalance = Math.Max(invoiceAmount - paidAmount, 0m);
                outstandingBalance += invoiceBalance;
            }

            return new CustomerDetailDto
            {
                CustomerId = customer.CustomerId,
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                Email = customer.Email,
                Status = customer.IsActive ? "Active" : "Inactive",
                CreatedDate = customer.CreatedDate,
                TotalBilled = totalBilled,
                TotalPaid = totalPaid,
                OutstandingBalance = outstandingBalance,
                InvoiceCount = invoices.Count,
                OpenInvoiceCount = openInvoiceCount
            };
        }

        /// <summary>
        /// Gets customer's invoice history, newest first.
        /// </summary>
        public async Task<List<CustomerInvoiceDto>> GetCustomerInvoicesAsync(
            int customerId,
            CancellationToken cancellationToken = default)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            // Aggregate invoice items by invoice ID
            var invoiceTotals = await context.InvoiceItems
                .AsNoTracking()
                .Where(ii => ii.Invoice.CustomerId == customerId)
                .GroupBy(ii => ii.InvoiceId)
                .Select(g => new
                {
                    InvoiceId = g.Key,
                    ItemsTotal = g.Sum(ii => ii.Quantity * ii.UnitPrice)
                })
                .ToListAsync(cancellationToken);

            // Aggregate payments by invoice ID
            var paymentTotals = await context.Payments
                .AsNoTracking()
                .Where(p => p.Invoice.CustomerId == customerId)
                .GroupBy(p => p.InvoiceId)
                .Select(g => new
                {
                    InvoiceId = g.Key,
                    PaymentsTotal = g.Sum(p => p.Amount)
                })
                .ToListAsync(cancellationToken);

            var invoiceDict = invoiceTotals.ToDictionary(x => x.InvoiceId);
            var paymentDict = paymentTotals.ToDictionary(x => x.InvoiceId);

            // Get invoices
            var invoices = await context.Invoices
                .AsNoTracking()
                .Where(i => i.CustomerId == customerId)
                .OrderByDescending(i => i.InvoiceDate)
                .Select(i => new
                {
                    i.InvoiceId,
                    i.InvoiceDate,
                    i.DueDate,
                    i.Status
                })
                .ToListAsync(cancellationToken);

            // Project to DTOs with financial data
            return invoices.Select(i =>
            {
                var itemsTotal = invoiceDict.TryGetValue(i.InvoiceId, out var it) ? it.ItemsTotal : 0m;
                var paymentsTotal = paymentDict.TryGetValue(i.InvoiceId, out var pt) ? pt.PaymentsTotal : 0m;

                return new CustomerInvoiceDto
                {
                    InvoiceId = i.InvoiceId,
                    InvoiceDate = i.InvoiceDate,
                    DueDate = i.DueDate,
                    Status = i.Status,
                    InvoiceTotal = itemsTotal,
                    TotalPaid = paymentsTotal,
                    Balance = Math.Max(itemsTotal - paymentsTotal, 0m)
                };
            }).ToList();
        }
    }
}
