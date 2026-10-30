using BankCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BankCore.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.CustomerNumber).HasMaxLength(20).IsRequired();
        builder.HasIndex(c => c.CustomerNumber).IsUnique();

        builder.Property(c => c.FullName).HasMaxLength(150).IsRequired();

        builder.Property(c => c.Nic).HasMaxLength(12).IsRequired();
        builder.HasIndex(c => c.Nic).IsUnique();

        builder.Property(c => c.Email).HasMaxLength(254).IsRequired();
        builder.Property(c => c.PhoneNumber).HasMaxLength(15).IsRequired();

        builder.HasMany(c => c.Accounts)
            .WithOne(a => a.Customer)
            .HasForeignKey(a => a.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(c => c.Accounts).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
