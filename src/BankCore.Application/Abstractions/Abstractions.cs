using BankCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BankCore.Application.Abstractions;

/// <summary>Unit of work over the banking database. Implemented by the Infrastructure layer.</summary>
public interface IBankDbContext
{
    DbSet<Customer> Customers { get; }

    DbSet<Account> Accounts { get; }

    DbSet<AccountTransaction> Transactions { get; }

    DbSet<FundTransfer> Transfers { get; }

    DbSet<AuditLog> AuditLogs { get; }

    DbSet<AppUser> Users { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>The authenticated staff member making the current request.</summary>
public interface ICurrentUser
{
    string UserName { get; }

    string? IpAddress { get; }

    string? CorrelationId { get; }
}

/// <summary>Abstracted clock so that time-based rules (daily limits, lockouts) are testable.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}

/// <summary>
/// Adds audit entries to the current unit of work. They are committed in the SAME database
/// transaction as the business change, so an audit row can never be lost or orphaned.
/// </summary>
public interface IAuditService
{
    void Record(string action, string entityType, string entityId, string? details = null, string? actor = null);
}

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}

public interface IAccountNumberGenerator
{
    Task<string> NextAccountNumberAsync(CancellationToken cancellationToken);

    Task<string> NextCustomerNumberAsync(CancellationToken cancellationToken);
}
