using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BillingDesk.Models;

namespace BillingDesk.Data.Configurations
{
    public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
    {
        public void Configure(EntityTypeBuilder<Payment> builder)
        {
            builder.ToTable("Payments");

            builder.HasKey(p => p.PaymentId);

            builder.Property(p => p.PaymentId).HasColumnName("PaymentId");
            builder.Property(p => p.InvoiceId).HasColumnName("InvoiceId").IsRequired();
            builder.Property(p => p.PaymentDate).HasColumnName("PaymentDate").HasColumnType("date");
            builder.Property(p => p.Amount).HasColumnName("Amount").HasPrecision(10, 2);

            // Navigation properties
            builder.HasOne(p => p.Invoice)
                .WithMany(i => i.Payments)
                .HasForeignKey(p => p.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
