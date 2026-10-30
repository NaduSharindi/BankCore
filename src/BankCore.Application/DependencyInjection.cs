using BankCore.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BankCore.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<CustomerService>();
        services.AddScoped<AccountService>();
        services.AddScoped<TransferService>();
        services.AddScoped<AuditQueryService>();
        return services;
    }
}
