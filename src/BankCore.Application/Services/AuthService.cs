using BankCore.Application.Abstractions;
using BankCore.Application.Dtos;
using BankCore.Application.Options;
using BankCore.Domain.Entities;
using BankCore.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BankCore.Application.Services;

public sealed class AuthService(
    IBankDbContext db,
    IPasswordHasher passwordHasher,
    IAuditService audit,
    IClock clock,
    IOptions<SecurityOptions> securityOptions,
    ILogger<AuthService> logger)
{
    public async Task<AuthenticatedUser> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var username = request.Username.Trim().ToLowerInvariant();
        var now = clock.UtcNow;
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

        if (user is null || !user.IsActive)
        {
            // Spend the same CPU time as a real verification so response timing
            // does not reveal whether the username exists (OWASP: user enumeration).
            _ = passwordHasher.Hash(request.Password);
            await FailAsync(username, "Unknown or inactive user", cancellationToken);
        }
        else if (user.IsLockedOut(now))
        {
            await FailAsync(username, $"Login blocked: locked out until {user.LockoutEndUtc:O}", cancellationToken);
        }
        else if (!passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            var options = securityOptions.Value;
            user.RegisterFailedLogin(options.MaxFailedLoginAttempts, TimeSpan.FromMinutes(options.LockoutMinutes), now);
            var details = user.IsLockedOut(now) ? "Wrong password - account locked" : "Wrong password";
            await FailAsync(username, details, cancellationToken);
        }

        user!.RegisterSuccessfulLogin(now);
        audit.Record("LOGIN_SUCCEEDED", nameof(AppUser), user.Id.ToString(), actor: user.Username);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {Username} signed in with role {Role}", user.Username, user.Role);
        return new AuthenticatedUser(user.Id, user.Username, user.Role);
    }

    private async Task FailAsync(string username, string reason, CancellationToken cancellationToken)
    {
        audit.Record("LOGIN_FAILED", nameof(AppUser), username, reason, actor: username);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogWarning("Failed login for {Username}: {Reason}", username, reason);
        throw new AuthenticationFailedException();
    }
}
