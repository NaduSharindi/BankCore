using BankCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BankCore.Infrastructure.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.AccountNumber).HasMaxLength(12).IsRequired();
        builder.HasIndex(a => a.AccountNumber).IsUnique();

        builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Currency).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(a => a.Balance).HasPrecision(18, 2);

        // Optimistic concurrency: UPDATE ... WHERE ConcurrencyStamp = @original
        builder.Property(a => a.ConcurrencyStamp).IsConcurrencyToken();

        builder.HasIndex(a => a.CustomerId);
    }
}
