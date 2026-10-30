using BankCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BankCore.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedOnAdd();

        builder.Property(l => l.Actor).HasMaxLength(100).IsRequired();
        builder.Property(l => l.Action).HasMaxLength(50).IsRequired();
        builder.Property(l => l.EntityType).HasMaxLength(50).IsRequired();
        builder.Property(l => l.EntityId).HasMaxLength(100).IsRequired();
        builder.Property(l => l.Details).HasMaxLength(1000);
        builder.Property(l => l.IpAddress).HasMaxLength(45);
        builder.Property(l => l.CorrelationId).HasMaxLength(64);

        builder.HasIndex(l => l.TimestampUtc);
        builder.HasIndex(l => l.Action);
    }
}
