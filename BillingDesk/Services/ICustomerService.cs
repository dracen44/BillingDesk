namespace BillingDesk.Services
{
    /// <summary>
    /// Service interface for customer operations.
    /// All queries use AsNoTracking and aggregate data independently in SQL.
    /// </summary>
    public interface ICustomerService
    {
        /// <summary>
        /// Gets a paged list of customers with search filtering.
        /// </summary>
        /// <param name="searchTerm">Optional search term for FirstName, LastName, or Email (case-insensitive)</param>
        /// <param name="pageNumber">Page number (1-based)</param>
        /// <param name="pageSize">Number of customers per page</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Paged result with customer list and total count</returns>
        Task<PagedResult<CustomerListItemDto>> GetCustomersAsync(
            string? searchTerm = null,
            int pageNumber = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets detailed information for a specific customer.
        /// </summary>
        /// <param name="customerId">Customer ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Customer detail DTO or null if not found</returns>
        Task<CustomerDetailDto?> GetCustomerDetailAsync(
            int customerId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets invoice history for a specific customer.
        /// </summary>
        /// <param name="customerId">Customer ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of invoices ordered newest first</returns>
        Task<List<CustomerInvoiceDto>> GetCustomerInvoicesAsync(
            int customerId,
            CancellationToken cancellationToken = default);
    }
}
