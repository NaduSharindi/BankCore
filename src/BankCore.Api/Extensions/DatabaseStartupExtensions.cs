using BankCore.Infrastructure.Persistence;

namespace BankCore.Api.Extensions;

public static class DatabaseStartupExtensions
{
    public static async Task InitialiseDatabaseAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue("Database:InitializeOnStartup", true))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
        await initializer.InitialiseAsync();

        if (app.Configuration.GetValue("Seed:Enabled", false))
        {
            await initializer.SeedAsync();
        }
    }
}
