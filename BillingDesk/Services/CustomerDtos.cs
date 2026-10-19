namespace BillingDesk.Services
{
    /// <summary>
    /// DTO for customer list display.
    /// Contains aggregated financial data calculated efficiently in SQL.
    /// </summary>
    public class CustomerListItemDto
    {
        public int CustomerId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;  // "Active" or "Inactive"

        // Financial metrics (aggregated in SQL, not loaded in memory)
        public int InvoiceCount { get; set; }
        public decimal TotalBilled { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal OutstandingBalance { get; set; }

        // Computed properties for display
        public string FullName => $"{FirstName} {LastName}";
    }

    /// <summary>
    /// DTO for customer detail view.
    /// </summary>
    public class CustomerDetailDto
    {
        public int CustomerId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }

        // Billing summary
        public decimal TotalBilled { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal OutstandingBalance { get; set; }
        public int InvoiceCount { get; set; }
        public int OpenInvoiceCount { get; set; }

        // Computed properties
        public string FullName => $"{FirstName} {LastName}";
        public string StatusDisplay => Status == "Active" ? "Active" : "Inactive";
    }

    /// <summary>
    /// DTO for invoice history in customer detail.
    /// </summary>
    public class CustomerInvoiceDto
    {
        public int InvoiceId { get; set; }
        public DateTime InvoiceDate { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal InvoiceTotal { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal Balance { get; set; }
    }

    /// <summary>
    /// Paging metadata for customer list.
    /// </summary>
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (TotalCount + PageSize - 1) / PageSize;
    }

    /// <summary>
    /// DTO for invoice list display.
    /// Contains invoice summary with financial totals aggregated independently.
    /// </summary>
    public class InvoiceListItemDto
    {
        public int InvoiceId { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal InvoiceTotal { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal Balance { get; set; }

        // Computed properties
        public string StatusDisplay => Status == "Paid" ? "Paid" : Status == "Open" ? "Open" : "Overdue";
    }

    /// <summary>
    /// DTO for invoice detail display.
    /// Contains complete invoice information with line items and payments.
    /// </summary>
    public class InvoiceDetailDto
    {
        public int InvoiceId { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerStatus { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal InvoiceTotal { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal Balance { get; set; }

        // Line items and payments
        public List<InvoiceLineItemDto> LineItems { get; set; } = new();
        public List<InvoicePaymentDto> Payments { get; set; } = new();

        // Computed properties
        public string StatusDisplay => Status == "Paid" ? "Paid" : Status == "Open" ? "Open" : "Overdue";
        public bool IsOverdue => DateTime.Today > DueDate && Status != "Paid";
        public string OverdueDisplay => IsOverdue ? "Overdue" : "Not overdue";
    }

    /// <summary>
    /// DTO for invoice line item.
    /// </summary>
    public class InvoiceLineItemDto
    {
        public int InvoiceItemId { get; set; }
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal => Quantity * UnitPrice;
    }

    /// <summary>
    /// DTO for invoice payment.
    /// </summary>
    public class InvoicePaymentDto
    {
        public int PaymentId { get; set; }
        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }
    }
}
