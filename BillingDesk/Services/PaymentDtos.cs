namespace BillingDesk.Services
{
    /// <summary>
    /// DTO for payment list display.
    /// Invoice totals and paid amounts are aggregated independently in SQL per invoice.
    /// </summary>
    public class PaymentListItemDto
    {
        public int PaymentId { get; set; }
        public DateTime PaymentDate { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int InvoiceId { get; set; }
        public decimal InvoiceTotal { get; set; }
        public decimal PaymentAmount { get; set; }
        public decimal TotalPaid { get; set; }

        // Computed properties
        public decimal RemainingBalance => Math.Max(InvoiceTotal - TotalPaid, 0m);
    }

    /// <summary>
    /// Result of recording a payment. ErrorMessage is user-safe; technical details are logged only.
    /// </summary>
    public class RecordPaymentResult
    {
        public bool IsSuccess { get; init; }
        public string? ErrorMessage { get; init; }
        public int? PaymentId { get; init; }
        public string? InvoiceStatus { get; init; }
        public decimal RemainingBalance { get; init; }

        public static RecordPaymentResult Failure(string message) => new() { IsSuccess = false, ErrorMessage = message };
    }

    /// <summary>
    /// DTO for payment detail display.
    /// </summary>
    public class PaymentDetailDto
    {
        public int PaymentId { get; set; }
        public DateTime PaymentDate { get; set; }
        public decimal PaymentAmount { get; set; }
        public int InvoiceId { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string InvoiceStatus { get; set; } = string.Empty;
        public decimal InvoiceTotal { get; set; }
        public decimal TotalPaid { get; set; }

        // Computed properties
        public decimal RemainingBalance => Math.Max(InvoiceTotal - TotalPaid, 0m);
        public string StatusDisplay => InvoiceStatus == "Paid" ? "Paid" : InvoiceStatus == "Open" ? "Open" : "Overdue";
    }
}
