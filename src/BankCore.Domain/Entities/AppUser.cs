using BankCore.Domain.Common;
using BankCore.Domain.Exceptions;

namespace BankCore.Domain.Entities;

/// <summary>
/// Internal bank staff user (Admin / Teller / Auditor) with brute-force lockout support.
/// </summary>
public class AppUser
{
    // Required by EF Core.
    private AppUser()
    {
    }

    public AppUser(string username, string passwordHash, string role, DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new DomainException("INVALID_USER", "Username is required.");
        }

        if (!Roles.All.Contains(role))
        {
            throw new DomainException("INVALID_ROLE", $"Role '{role}' is not supported.");
        }

        Username = username.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
        Role = role;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public string Username { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public string Role { get; private set; } = string.Empty;

    public bool IsActive { get; private set; } = true;

    public int FailedLoginAttempts { get; private set; }

    public DateTime? LockoutEndUtc { get; private set; }

    public DateTime? LastLoginUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public bool IsLockedOut(DateTime nowUtc) => LockoutEndUtc.HasValue && LockoutEndUtc.Value > nowUtc;

    public void RegisterFailedLogin(int maxAttempts, TimeSpan lockoutDuration, DateTime nowUtc)
    {
        FailedLoginAttempts++;

        if (FailedLoginAttempts >= maxAttempts)
        {
            LockoutEndUtc = nowUtc.Add(lockoutDuration);
            FailedLoginAttempts = 0;
        }
    }

    public void RegisterSuccessfulLogin(DateTime nowUtc)
    {
        FailedLoginAttempts = 0;
        LockoutEndUtc = null;
        LastLoginUtc = nowUtc;
    }

    public void Deactivate() => IsActive = false;
}
