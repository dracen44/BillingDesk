using BillingDesk.Data;
using BillingDesk.Models;
using Microsoft.EntityFrameworkCore;

namespace BillingDesk.Services
{
    /// <summary>
    /// Service for invoice operations.
    /// 
    /// CRITICAL: All financial aggregations are calculated correctly:
    /// - InvoiceItems totals are aggregated independently
    /// - Payments totals are aggregated independently
    /// - Only after independent aggregation are they joined
    /// This avoids the one-to-many × one-to-many multiplication bug.
    /// </summary>
    public class InvoiceService : IInvoiceService
    {
        private readonly IDbContextFactory<BillingDbContext> _dbContextFactory;

        public InvoiceService(IDbContextFactory<BillingDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        /// <summary>
        /// Gets paged invoice list with optional search filtering and status filtering.
        /// Search happens in SQL via EF Core.
        /// Filtering and paging happen in SQL via Skip/Take.
        /// </summary>
        public async Task<PagedResult<InvoiceListItemDto>> GetInvoicesAsync(
            string? searchTerm = null,
            string? statusFilter = null,
            int pageNumber = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            // Each invoice is kept even with no items or payments; item and payment totals
            // are aggregated independently per invoice (correlated subqueries) and default to 0.
            var invoiceFinancialsQuery = context.Invoices
                .AsNoTracking()
                .Select(inv => new
                {
                    inv.InvoiceId,
                    inv.CustomerId,
                    inv.InvoiceDate,
                    inv.DueDate,
                    inv.Status,
                    ItemsTotal = context.InvoiceItems
                        .Where(ii => ii.InvoiceId == inv.InvoiceId)
                        .Sum(ii => (decimal?)(ii.Quantity * ii.UnitPrice)) ?? 0m,
                    PaymentsTotal = context.Payments
                        .Where(p => p.InvoiceId == inv.InvoiceId)
                        .Sum(p => (decimal?)p.Amount) ?? 0m,
                    Customer = inv.Customer
                });

            // Build the main query with optional search and filtering
            var query = invoiceFinancialsQuery
                .Where(x =>
                    (searchTerm == null ||
                     x.InvoiceId.ToString().Contains(searchTerm) ||
                     EF.Functions.Like(x.Customer.FirstName, $"%{searchTerm}%") ||
                     EF.Functions.Like(x.Customer.LastName, $"%{searchTerm}%") ||
                     EF.Functions.Like(x.Customer.Email, $"%{searchTerm}%")
                    ) &&
                    (statusFilter == null || x.Status == statusFilter)
                )
                .OrderByDescending(x => x.InvoiceDate)
                .ThenByDescending(x => x.InvoiceId);

            // Get total count before pagination
            var totalCount = await query.CountAsync(cancellationToken);

            // Get paginated results
            var invoices = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            // Project to DTOs
            var dtos = invoices.Select(x => new InvoiceListItemDto
            {
                InvoiceId = x.InvoiceId,
                CustomerId = x.CustomerId,
                CustomerName = $"{x.Customer.FirstName} {x.Customer.LastName}",
                CustomerEmail = x.Customer.Email,
                InvoiceDate = x.InvoiceDate,
                DueDate = x.DueDate,
                Status = x.Status,
                InvoiceTotal = x.ItemsTotal,
                TotalPaid = x.PaymentsTotal,
                Balance = Math.Max(x.ItemsTotal - x.PaymentsTotal, 0m)
            }).ToList();

            return new PagedResult<InvoiceListItemDto>
            {
                Items = dtos,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        /// <summary>
        /// Gets detailed invoice information with line items and payments.
        /// </summary>
        public async Task<InvoiceDetailDto?> GetInvoiceDetailAsync(
            int invoiceId,
            CancellationToken cancellationToken = default)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            // Get invoice with customer
            var invoice = await context.Invoices
                .AsNoTracking()
                .Include(i => i.Customer)
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, cancellationToken);

            if (invoice == null)
                return null;

            // Get invoice items
            var lineItems = await context.InvoiceItems
                .AsNoTracking()
                .Where(ii => ii.InvoiceId == invoiceId)
                .Select(ii => new InvoiceLineItemDto
                {
                    InvoiceItemId = ii.InvoiceItemId,
                    Description = ii.Description,
                    Quantity = ii.Quantity,
                    UnitPrice = ii.UnitPrice
                })
                .OrderBy(ii => ii.InvoiceItemId)
                .ToListAsync(cancellationToken);

            // Get payments
            var payments = await context.Payments
                .AsNoTracking()
                .Where(p => p.InvoiceId == invoiceId)
                .Select(p => new InvoicePaymentDto
                {
                    PaymentId = p.PaymentId,
                    PaymentDate = p.PaymentDate,
                    Amount = p.Amount
                })
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync(cancellationToken);

            // Calculate totals
            var invoiceTotal = lineItems.Sum(li => li.LineTotal);
            var totalPaid = payments.Sum(p => p.Amount);
            var balance = invoiceTotal - totalPaid;

            return new InvoiceDetailDto
            {
                InvoiceId = invoice.InvoiceId,
                CustomerId = invoice.CustomerId,
                CustomerName = $"{invoice.Customer.FirstName} {invoice.Customer.LastName}",
                CustomerEmail = invoice.Customer.Email,
                CustomerStatus = invoice.Customer.IsActive ? "Active" : "Inactive",
                InvoiceDate = invoice.InvoiceDate,
                DueDate = invoice.DueDate,
                Status = invoice.Status,
                InvoiceTotal = invoiceTotal,
                TotalPaid = totalPaid,
                Balance = Math.Max(balance, 0m),
                LineItems = lineItems,
                Payments = payments
            };
        }
    }
}
