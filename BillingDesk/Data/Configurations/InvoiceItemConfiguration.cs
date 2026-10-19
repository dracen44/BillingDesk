using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BillingDesk.Models;

namespace BillingDesk.Data.Configurations
{
    public class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
    {
        public void Configure(EntityTypeBuilder<InvoiceItem> builder)
        {
            builder.ToTable("InvoiceItems");

            builder.HasKey(ii => ii.InvoiceItemId);

            builder.Property(ii => ii.InvoiceItemId).HasColumnName("InvoiceItemId");
            builder.Property(ii => ii.InvoiceId).HasColumnName("InvoiceId").IsRequired();
            builder.Property(ii => ii.Description).HasColumnName("Description").HasMaxLength(150).IsRequired();
            builder.Property(ii => ii.Quantity).HasColumnName("Quantity");
            builder.Property(ii => ii.UnitPrice).HasColumnName("UnitPrice").HasPrecision(10, 2);

            // Navigation properties
            builder.HasOne(ii => ii.Invoice)
                .WithMany(i => i.InvoiceItems)
                .HasForeignKey(ii => ii.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
