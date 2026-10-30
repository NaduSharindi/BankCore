using BankCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BankCore.Infrastructure.Persistence.Configurations;

internal sealed class FundTransferConfiguration : IEntityTypeConfiguration<FundTransfer>
{
    public void Configure(EntityTypeBuilder<FundTransfer> builder)
    {
        builder.ToTable("FundTransfers");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Reference).HasMaxLength(30).IsRequired();
        builder.HasIndex(t => t.Reference).IsUnique();

        // The unique index is the final safety net for idempotency under concurrent retries.
        builder.Property(t => t.IdempotencyKey).HasMaxLength(64).IsRequired();
        builder.HasIndex(t => t.IdempotencyKey).IsUnique();

        builder.Property(t => t.FromAccountNumber).HasMaxLength(12).IsRequired();
        builder.Property(t => t.ToAccountNumber).HasMaxLength(12).IsRequired();
        builder.Property(t => t.Amount).HasPrecision(18, 2);
        builder.Property(t => t.Currency).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(t => t.Narration).HasMaxLength(140);
        builder.Property(t => t.InitiatedBy).HasMaxLength(50).IsRequired();

        builder.HasOne<Account>().WithMany().HasForeignKey(t => t.FromAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(t => t.ToAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
