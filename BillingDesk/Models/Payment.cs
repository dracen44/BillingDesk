namespace BillingDesk.Models
{
    public class Payment
    {
        public int PaymentId { get; set; }
        public int InvoiceId { get; set; }
        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }

        // Navigation properties
        public virtual Invoice Invoice { get; set; } = null!;
    }
}
