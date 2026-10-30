using BankCore.Application.Abstractions;
using BankCore.Domain.Common;
using BankCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace BankCore.Infrastructure.Services;

public sealed class UtcClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public sealed class AuditService(IBankDbContext db, ICurrentUser currentUser, IClock clock) : IAuditService
{
    private const int MaxDetailsLength = 1000;

    public void Record(string action, string entityType, string entityId, string? details = null, string? actor = null)
    {
        if (details is { Length: > MaxDetailsLength })
        {
            details = details[..MaxDetailsLength];
        }

        db.AuditLogs.Add(new AuditLog(
            actor ?? currentUser.UserName,
            action,
            entityType,
            entityId,
            details,
            currentUser.IpAddress,
            currentUser.CorrelationId,
            clock.UtcNow));
    }
}

/// <summary>
/// Account number = 3-digit branch code + 8 random digits + Luhn check digit (12 digits).
/// Customer number = "CIF" + 9 random digits.
/// </summary>
public sealed class AccountNumberGenerator(IBankDbContext db) : IAccountNumberGenerator
{
    private const string HeadOfficeBranchCode = "001";
    private const int MaxAttempts = 10;

    public async Task<string> NextAccountNumberAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var candidate = Luhn.AppendCheckDigit(HeadOfficeBranchCode + RandomDigits(8));
            if (!await db.Accounts.AnyAsync(a => a.AccountNumber == candidate, cancellationToken))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Could not generate a unique account number.");
    }

    public async Task<string> NextCustomerNumberAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var candidate = "CIF" + RandomDigits(9);
            if (!await db.Customers.AnyAsync(c => c.CustomerNumber == candidate, cancellationToken))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Could not generate a unique customer number.");
    }

    private static string RandomDigits(int length)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = (char)('0' + RandomNumberGenerator.GetInt32(10));
        }

        return new string(chars);
    }
}
