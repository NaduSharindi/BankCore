using BankCore.Application.Abstractions;
using BankCore.Api.Middleware;
using BankCore.Domain.Common;
using Microsoft.AspNetCore.Authorization;

namespace BankCore.Api.Authentication;

/// <summary>
/// Policy-based authorization enforcing segregation of duties:
/// Tellers move money, Admins manage accounts, Auditors can only read.
/// </summary>
public static class Policies
{
    public const string Onboard = nameof(Onboard);
    public const string Transact = nameof(Transact);
    public const string ManageAccounts = nameof(ManageAccounts);
    public const string ViewAccounts = nameof(ViewAccounts);
    public const string ViewAudit = nameof(ViewAudit);
}

public static class AuthorizationExtensions
{
    public static IServiceCollection AddBankAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Onboard, p => p.RequireRole(Roles.Admin, Roles.Teller))
            .AddPolicy(Policies.Transact, p => p.RequireRole(Roles.Teller))
            .AddPolicy(Policies.ManageAccounts, p => p.RequireRole(Roles.Admin))
            .AddPolicy(Policies.ViewAccounts, p => p.RequireRole(Roles.Admin, Roles.Teller, Roles.Auditor))
            .AddPolicy(Policies.ViewAudit, p => p.RequireRole(Roles.Admin, Roles.Auditor))
            // Secure by default: every endpoint requires a valid token unless marked [AllowAnonymous].
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }
}

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string UserName
    {
        get
        {
            var identity = accessor.HttpContext?.User.Identity;
            return identity is { IsAuthenticated: true, Name: not null } ? identity.Name : "anonymous";
        }
    }

    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? CorrelationId => accessor.HttpContext?.Items[CorrelationIdMiddleware.ItemKey] as string;
}
