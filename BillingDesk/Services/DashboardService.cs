using BillingDesk.Data;
using BillingDesk.Models;
using Microsoft.EntityFrameworkCore;

namespace BillingDesk.Services
{
    /// <summary>
    /// Service for retrieving dashboard metrics and billing information.
    /// Optimized for large datasets: 10k+ customers, 100k+ invoices, 300k+ items.
    /// 
    /// All queries:
    /// - Use AsNoTracking() for read-only operations
    /// - Execute aggregations in SQL Server (not in-memory)
    /// - Use appropriate projections to limit data transfer
    /// - Handle one-to-many joins carefully (Invoice has both Items and Payments)
    /// </summary>
    public class DashboardService
    {
        private readonly IDbContextFactory<BillingDbContext> _dbContextFactory;

        public DashboardService(IDbContextFactory<BillingDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        /// <summary>
        /// Gets the total outstanding balance across all invoices.
        /// 
        /// Calculates:
        /// - InvoiceItems sum by invoice ID
        /// - Payments sum by invoice ID
        /// - Balance = ItemsTotal - PaymentTotal
        /// - Sum all balances
        /// 
        /// Query pattern:
        /// 1. Group InvoiceItems by InvoiceId, calculate total per invoice
        /// 2. Group Payments by InvoiceId, calculate total per invoice
        /// 3. Join resultsets and calculate balance
        /// 4. Sum all outstanding balances
        /// 
        /// This avoids the N+1 problem and the row multiplication issue
        /// when joining InvoiceItems and Payments directly.
        /// </summary>
        public async Task<decimal> GetTotalOutstandingBalanceAsync()
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();

            var invoiceIds = await context.Invoices
                .AsNoTracking()
                .Select(inv => inv.InvoiceId)
                .ToListAsync();

            var itemTotals = await context.InvoiceItems
                .AsNoTracking()
                .GroupBy(ii => ii.InvoiceId)
                .Select(g => new
                {
                    InvoiceId = g.Key,
                    ItemsTotal = g.Sum(ii => ii.Quantity * ii.UnitPrice)
                })
                .ToDictionaryAsync(x => x.InvoiceId, x => x.ItemsTotal);

            var paymentTotals = await context.Payments
                .AsNoTracking()
                .GroupBy(p => p.InvoiceId)
                .Select(g => new
                {
                    InvoiceId = g.Key,
                    PaymentsTotal = g.Sum(p => p.Amount)
                })
                .ToDictionaryAsync(x => x.InvoiceId, x => x.PaymentsTotal);

            return invoiceIds.Sum(id =>
            {
                var balance = (itemTotals.GetValueOrDefault(id) - paymentTotals.GetValueOrDefault(id));
                return Math.Max(0m, balance);
            });
        }

        /// <summary>
        /// Gets the count of open invoices (not "Paid" or "Cancelled").
        /// </summary>
        public async Task<int> GetOpenInvoiceCountAsync()
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();

            return await context.Invoices
                .AsNoTracking()
                .Where(i => i.Status != "Paid" && i.Status != "Cancelled")
                .CountAsync();
        }

        /// <summary>
        /// Gets the count of overdue invoices (due date passed, not paid).
        /// Assumes Status values include "Paid" or "Cancelled" to indicate closed invoices.
        /// </summary>
        public async Task<int> GetOverdueInvoiceCountAsync()
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();

            var today = DateTime.Today;

            return await context.Invoices
                .AsNoTracking()
                .Where(i => i.DueDate < today && i.Status != "Paid" && i.Status != "Cancelled")
                .CountAsync();
        }

        /// <summary>
        /// Gets the sum of payments received in the last 30 days.
        /// </summary>
        public async Task<decimal> GetRecentPaymentsTotalAsync(int days = 30)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();

            var startDate = DateTime.Today.AddDays(-days);

            return await context.Payments
                .AsNoTracking()
                .Where(p => p.PaymentDate >= startDate)
                .SumAsync(p => p.Amount);
        }

        /// <summary>
        /// Gets the most recent invoices with calculated totals and balance.
        /// 
        /// Uses projection to calculate totals per invoice in SQL,
        /// avoiding N+1 queries and unnecessary object tracking.
        /// </summary>
        public async Task<List<RecentInvoiceDto>> GetRecentInvoicesAsync(int count = 10)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();

            // Get recent invoices with projected totals
            var recentInvoices = await context.Invoices
                .AsNoTracking()
                .OrderByDescending(i => i.InvoiceDate)
                .ThenByDescending(i => i.InvoiceId)
                .Take(count)
                .Select(i => new
                {
                    i.InvoiceId,
                    i.InvoiceDate,
                    i.DueDate,
                    i.Status,
                    CustomerFirstName = i.Customer.FirstName,
                    CustomerLastName = i.Customer.LastName,
                    ItemsTotal = i.InvoiceItems.Sum(ii => ii.Quantity * ii.UnitPrice),
                    PaymentsTotal = i.Payments.Sum(p => p.Amount)
                })
                .ToListAsync();

            // Map to DTO
            return recentInvoices
                .Select(i => new RecentInvoiceDto
                {
                    InvoiceId = i.InvoiceId,
                    CustomerName = $"{i.CustomerFirstName} {i.CustomerLastName}",
                    InvoiceDate = i.InvoiceDate,
                    DueDate = i.DueDate,
                    Status = i.Status,
                    Total = i.ItemsTotal,
                    AmountPaid = i.PaymentsTotal,
                    Balance = Math.Max(i.ItemsTotal - i.PaymentsTotal, 0m)
                })
                .ToList();
        }

        /// <summary>
        /// Gets the most recent payments.
        /// </summary>
        public async Task<List<RecentPaymentDto>> GetRecentPaymentsAsync(int count = 10)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();

            return await context.Payments
                .AsNoTracking()
                .OrderByDescending(p => p.PaymentDate)
                .ThenByDescending(p => p.PaymentId)
                .Take(count)
                .Select(p => new RecentPaymentDto
                {
                    PaymentId = p.PaymentId,
                    PaymentDate = p.PaymentDate,
                    InvoiceId = p.InvoiceId,
                    CustomerName = $"{p.Invoice.Customer.FirstName} {p.Invoice.Customer.LastName}",
                    Amount = p.Amount
                })
                .ToListAsync();
        }
    }

    /// <summary>DTO for recent invoice display.</summary>
    public class RecentInvoiceDto
    {
        public int InvoiceId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal Balance { get; set; }
    }

    /// <summary>DTO for recent payment display.</summary>
    public class RecentPaymentDto
    {
        public int PaymentId { get; set; }
        public DateTime PaymentDate { get; set; }
        public int InvoiceId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
}
