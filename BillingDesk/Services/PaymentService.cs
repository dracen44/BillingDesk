using BillingDesk.Data;
using BillingDesk.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BillingDesk.Services
{
    /// <summary>
    /// Service for payment operations.
    ///
    /// Financial figures follow the same rules as InvoiceService:
    /// - Invoice total = SUM(InvoiceItems.Quantity * UnitPrice), aggregated per invoice
    /// - Total paid = SUM(Payments.Amount), aggregated per invoice
    /// Each aggregate is an independent correlated subquery, so InvoiceItems and Payments
    /// are never joined together and rows are not multiplied.
    /// </summary>
    public class PaymentService : IPaymentService
    {
        private readonly IDbContextFactory<BillingDbContext> _dbContextFactory;
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(
            IDbContextFactory<BillingDbContext> dbContextFactory,
            ILogger<PaymentService> logger)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
        }

        /// <summary>
        /// Gets paged payment list with optional search and payment date filtering.
        /// Search, date filtering, sorting, and paging all happen in SQL.
        /// </summary>
        public async Task<PagedResult<PaymentListItemDto>> GetPaymentsAsync(
            string? searchTerm = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int pageNumber = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var term = string.IsNullOrWhiteSpace(searchTerm) ? null : $"%{searchTerm.Trim()}%";
            var from = fromDate?.Date;
            var toExclusive = toDate?.Date.AddDays(1);

            var query = context.Payments
                .AsNoTracking()
                .Where(p =>
                    (from == null || p.PaymentDate >= from) &&
                    (toExclusive == null || p.PaymentDate < toExclusive) &&
                    (term == null ||
                     p.PaymentId.ToString().Contains(searchTerm!.Trim()) ||
                     p.InvoiceId.ToString().Contains(searchTerm!.Trim()) ||
                     EF.Functions.Like(p.Invoice.Customer.FirstName, term) ||
                     EF.Functions.Like(p.Invoice.Customer.LastName, term) ||
                     EF.Functions.Like(p.Invoice.Customer.FirstName + " " + p.Invoice.Customer.LastName, term) ||
                     EF.Functions.Like(p.Invoice.Customer.Email, term)))
                .OrderByDescending(p => p.PaymentDate)
                .ThenByDescending(p => p.PaymentId);

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PaymentListItemDto
                {
                    PaymentId = p.PaymentId,
                    PaymentDate = p.PaymentDate,
                    CustomerName = p.Invoice.Customer.FirstName + " " + p.Invoice.Customer.LastName,
                    InvoiceId = p.InvoiceId,
                    InvoiceTotal = context.InvoiceItems
                        .Where(ii => ii.InvoiceId == p.InvoiceId)
                        .Sum(ii => (decimal?)(ii.Quantity * ii.UnitPrice)) ?? 0m,
                    PaymentAmount = p.Amount,
                    TotalPaid = context.Payments
                        .Where(pp => pp.InvoiceId == p.InvoiceId)
                        .Sum(pp => (decimal?)pp.Amount) ?? 0m
                })
                .ToListAsync(cancellationToken);

            return new PagedResult<PaymentListItemDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        /// <summary>
        /// Gets payment detail with related invoice and customer data in a single query.
        /// </summary>
        public async Task<PaymentDetailDto?> GetPaymentDetailAsync(
            int paymentId,
            CancellationToken cancellationToken = default)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            return await context.Payments
                .AsNoTracking()
                .Where(p => p.PaymentId == paymentId)
                .Select(p => new PaymentDetailDto
                {
                    PaymentId = p.PaymentId,
                    PaymentDate = p.PaymentDate,
                    PaymentAmount = p.Amount,
                    InvoiceId = p.InvoiceId,
                    CustomerId = p.Invoice.CustomerId,
                    CustomerName = p.Invoice.Customer.FirstName + " " + p.Invoice.Customer.LastName,
                    CustomerEmail = p.Invoice.Customer.Email,
                    InvoiceStatus = p.Invoice.Status,
                    InvoiceTotal = context.InvoiceItems
                        .Where(ii => ii.InvoiceId == p.InvoiceId)
                        .Sum(ii => (decimal?)(ii.Quantity * ii.UnitPrice)) ?? 0m,
                    TotalPaid = context.Payments
                        .Where(pp => pp.InvoiceId == p.InvoiceId)
                        .Sum(pp => (decimal?)pp.Amount) ?? 0m
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        /// <summary>
        /// Records a payment in a single transaction.
        /// The invoice row is locked with UPDLOCK, ROWLOCK so a second submission for the same invoice
        /// waits until the first commits, then recalculates the balance against committed data.
        /// Item and payment totals are aggregated separately and never joined.
        /// </summary>
        public async Task<RecordPaymentResult> RecordPaymentAsync(
            int invoiceId,
            decimal amount,
            DateTime paymentDate,
            CancellationToken cancellationToken = default)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var invoice = await context.Invoices
                    .FromSqlInterpolated($"SELECT InvoiceId, CustomerId, InvoiceDate, DueDate, Status FROM dbo.Invoices WITH (UPDLOCK, ROWLOCK) WHERE InvoiceId = {invoiceId}")
                    .FirstOrDefaultAsync(cancellationToken);

                if (invoice == null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return RecordPaymentResult.Failure("Invoice was not found.");
                }

                var invoiceTotal = await context.InvoiceItems
                    .Where(ii => ii.InvoiceId == invoiceId)
                    .SumAsync(ii => (decimal?)(ii.Quantity * ii.UnitPrice), cancellationToken) ?? 0m;

                var totalPaid = await context.Payments
                    .Where(p => p.InvoiceId == invoiceId)
                    .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

                var remainingBalance = invoiceTotal - totalPaid;

                if (remainingBalance <= 0m)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return RecordPaymentResult.Failure("This invoice has no remaining balance.");
                }

                if (amount <= 0m)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return RecordPaymentResult.Failure("Payment amount must be greater than zero.");
                }

                if (decimal.Round(amount, 2) != amount)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return RecordPaymentResult.Failure("Payment amount can have at most two decimal places.");
                }

                if (amount > remainingBalance)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return RecordPaymentResult.Failure($"Payment amount cannot exceed the remaining balance of {remainingBalance:C2}.");
                }

                if (paymentDate.Date < invoice.InvoiceDate.Date)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return RecordPaymentResult.Failure($"Payment date cannot be before the invoice date ({invoice.InvoiceDate:yyyy-MM-dd}).");
                }

                var payment = new Payment
                {
                    InvoiceId = invoiceId,
                    PaymentDate = paymentDate.Date,
                    Amount = amount
                };

                context.Payments.Add(payment);

                var newBalance = remainingBalance - amount;
                if (newBalance == 0m)
                {
                    invoice.Status = "Paid";
                }

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new RecordPaymentResult
                {
                    IsSuccess = true,
                    PaymentId = payment.PaymentId,
                    InvoiceStatus = invoice.Status,
                    RemainingBalance = newBalance
                };
            }
            catch (OperationCanceledException)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _logger.LogError(ex, "Failed to record payment of {Amount} for invoice {InvoiceId}", amount, invoiceId);
                return RecordPaymentResult.Failure("The payment could not be saved. No changes were made. Please try again.");
            }
        }
    }
}
