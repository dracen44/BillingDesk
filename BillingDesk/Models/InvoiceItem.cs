namespace BillingDesk.Models
{
    public class InvoiceItem
    {
        public int InvoiceItemId { get; set; }
        public int InvoiceId { get; set; }
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }

        // Navigation properties
        public virtual Invoice Invoice { get; set; } = null!;

        // Calculated property
        public decimal LineTotal => Quantity * UnitPrice;
    }
}
