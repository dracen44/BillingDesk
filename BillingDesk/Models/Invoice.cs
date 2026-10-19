namespace BillingDesk.Models
{
    public class Invoice
    {
        public int InvoiceId { get; set; }
        public int CustomerId { get; set; }
        public DateTime InvoiceDate { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = string.Empty;

        // Navigation properties
        public virtual Customer Customer { get; set; } = null!;
        public virtual ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

        // Calculated properties
        public decimal InvoiceTotal => InvoiceItems.Sum(ii => ii.Quantity * ii.UnitPrice);
        public decimal AmountPaid => Payments.Sum(p => p.Amount);
        public decimal Balance => InvoiceTotal - AmountPaid;
        public bool IsOverdue => DateTime.Today > DueDate && Status != "Paid";
    }
}
