namespace BillingDesk.Services
{
    /// <summary>
    /// Service interface for payment operations.
    /// All queries use AsNoTracking and aggregate data independently in SQL.
    /// </summary>
    public interface IPaymentService
    {
        /// <summary>
        /// Gets a paged list of payments with optional search and payment date range filtering.
        /// </summary>
        /// <param name="searchTerm">Optional search term for payment ID, invoice ID, customer name, or email</param>
        /// <param name="fromDate">Optional inclusive start of payment date range</param>
        /// <param name="toDate">Optional inclusive end of payment date range</param>
        /// <param name="pageNumber">Page number (1-based)</param>
        /// <param name="pageSize">Number of payments per page</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Paged result with payment list and total count</returns>
        Task<PagedResult<PaymentListItemDto>> GetPaymentsAsync(
            string? searchTerm = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int pageNumber = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets detailed information for a specific payment.
        /// </summary>
        /// <param name="paymentId">Payment ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Payment detail DTO or null if not found</returns>
        Task<PaymentDetailDto?> GetPaymentDetailAsync(
            int paymentId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Records a payment against an invoice inside a transaction. The invoice row is locked
        /// before the balance is recalculated, so concurrent payments for the same invoice are serialized.
        /// </summary>
        /// <param name="invoiceId">Invoice ID</param>
        /// <param name="amount">Payment amount (must be greater than zero and not exceed the remaining balance)</param>
        /// <param name="paymentDate">Payment date (cannot precede the invoice date)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result with success flag, user-safe error message, and updated balance/status</returns>
        Task<RecordPaymentResult> RecordPaymentAsync(
            int invoiceId,
            decimal amount,
            DateTime paymentDate,
            CancellationToken cancellationToken = default);
    }
}
