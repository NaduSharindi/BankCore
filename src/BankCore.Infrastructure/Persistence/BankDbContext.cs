using BankCore.Application.Abstractions;
using BankCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BankCore.Infrastructure.Persistence;

public sealed class BankDbContext(DbContextOptions<BankDbContext> options) : DbContext(options), IBankDbContext
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<AccountTransaction> Transactions => Set<AccountTransaction>();

    public DbSet<FundTransfer> Transfers => Set<FundTransfer>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BankDbContext).Assembly);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Money: DECIMAL(18,2) everywhere - never float/double for currency.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);

        // All timestamps are stored and returned as UTC.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }
}

/// <summary>Writes DateTime values as UTC and marks values read from the database as UTC.</summary>
internal sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : DateTime.SpecifyKind(v, DateTimeKind.Utc),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
{
}
