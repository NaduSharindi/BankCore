using BankCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BankCore.Infrastructure.Persistence.Configurations;

internal sealed class AccountTransactionConfiguration : IEntityTypeConfiguration<AccountTransaction>
{
    public void Configure(EntityTypeBuilder<AccountTransaction> builder)
    {
        builder.ToTable("AccountTransactions");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedOnAdd();

        builder.Property(t => t.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Amount).HasPrecision(18, 2);
        builder.Property(t => t.BalanceAfter).HasPrecision(18, 2);
        builder.Property(t => t.Reference).HasMaxLength(30).IsRequired();
        builder.Property(t => t.Narration).HasMaxLength(140);
        builder.Property(t => t.CounterpartyAccountNumber).HasMaxLength(12);
        builder.Ignore(t => t.IsCredit);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // Statement and daily-limit queries filter by account + date.
        builder.HasIndex(t => new { t.AccountId, t.CreatedAtUtc });
        builder.HasIndex(t => t.Reference);
    }
}
