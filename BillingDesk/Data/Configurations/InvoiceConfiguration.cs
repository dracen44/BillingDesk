using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BillingDesk.Models;

namespace BillingDesk.Data.Configurations
{
    public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
    {
        public void Configure(EntityTypeBuilder<Invoice> builder)
        {
            builder.ToTable("Invoices");

            builder.HasKey(i => i.InvoiceId);

            builder.Property(i => i.InvoiceId).HasColumnName("InvoiceId");
            builder.Property(i => i.CustomerId).HasColumnName("CustomerId").IsRequired();
            builder.Property(i => i.InvoiceDate).HasColumnName("InvoiceDate").HasColumnType("date");
            builder.Property(i => i.DueDate).HasColumnName("DueDate").HasColumnType("date");
            builder.Property(i => i.Status).HasColumnName("Status").HasMaxLength(20).IsRequired();

            // Navigation properties
            builder.HasOne(i => i.Customer)
                .WithMany(c => c.Invoices)
                .HasForeignKey(i => i.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(i => i.InvoiceItems)
                .WithOne(ii => ii.Invoice)
                .HasForeignKey(ii => ii.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(i => i.Payments)
                .WithOne(p => p.Invoice)
                .HasForeignKey(p => p.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
