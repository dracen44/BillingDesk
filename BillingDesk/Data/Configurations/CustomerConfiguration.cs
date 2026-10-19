using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BillingDesk.Models;

namespace BillingDesk.Data.Configurations
{
    public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
    {
        public void Configure(EntityTypeBuilder<Customer> builder)
        {
            builder.ToTable("Customers");

            builder.HasKey(c => c.CustomerId);

            builder.Property(c => c.CustomerId).HasColumnName("CustomerId");
            builder.Property(c => c.FirstName).HasColumnName("FirstName").HasMaxLength(50).IsRequired();
            builder.Property(c => c.LastName).HasColumnName("LastName").HasMaxLength(50).IsRequired();
            builder.Property(c => c.Email).HasColumnName("Email").HasMaxLength(150).IsRequired();
            builder.Property(c => c.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            builder.Property(c => c.CreatedDate).HasColumnName("CreatedDate").HasDefaultValueSql("GETDATE()");

            // Navigation properties
            builder.HasMany(c => c.Invoices)
                .WithOne(i => i.Customer)
                .HasForeignKey(i => i.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
