using BankCore.Application.Abstractions;
using BankCore.Infrastructure.Persistence;
using BankCore.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BankCore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Configuration is resolved lazily so test hosts can override it.
        services.AddDbContext<BankDbContext>((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("BankCore");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Connection string 'BankCore' is not configured. See README.md > Getting started.");
            }

            var provider = configuration["Database:Provider"] ?? "SqlServer";
            if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlite(connectionString);
            }
            else
            {
                options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(maxRetryCount: 3));
            }
        });

        services.AddScoped<IBankDbContext>(sp => sp.GetRequiredService<BankDbContext>());
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAccountNumberGenerator, AccountNumberGenerator>();
        services.AddScoped<DbInitializer>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IClock, UtcClock>();

        return services;
    }
}
