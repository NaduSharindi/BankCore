using System.Threading.RateLimiting;

namespace BankCore.Api.Extensions;

public static class RateLimitingExtensions
{
    public const string LoginPolicy = "login";

    /// <summary>Throttles login attempts per client IP to slow down credential stuffing.</summary>
    public static IServiceCollection AddLoginRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(LoginPolicy, httpContext =>
            {
                var permitLimit = httpContext.RequestServices
                    .GetRequiredService<IConfiguration>()
                    .GetValue("RateLimiting:LoginPermitLimitPerMinute", 5);

                return RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    });
            });
        });

        return services;
    }
}
