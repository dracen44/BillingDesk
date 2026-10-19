namespace BillingDesk.Services
{
    /// <summary>
    /// Service interface for invoice operations.
    /// All queries use AsNoTracking and aggregate data independently in SQL.
    /// </summary>
    public interface IInvoiceService
    {
        /// <summary>
        /// Gets a paged list of invoices with optional search and filtering.
        /// </summary>
        /// <param name="searchTerm">Optional search term for invoice ID, customer name, or email</param>
        /// <param name="statusFilter">Optional status filter ("Open", "Paid", "Overdue")</param>
        /// <param name="pageNumber">Page number (1-based)</param>
        /// <param name="pageSize">Number of invoices per page</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Paged result with invoice list and total count</returns>
        Task<PagedResult<InvoiceListItemDto>> GetInvoicesAsync(
            string? searchTerm = null,
            string? statusFilter = null,
            int pageNumber = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets detailed information for a specific invoice.
        /// </summary>
        /// <param name="invoiceId">Invoice ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Invoice detail DTO or null if not found</returns>
        Task<InvoiceDetailDto?> GetInvoiceDetailAsync(
            int invoiceId,
            CancellationToken cancellationToken = default);
    }
}
