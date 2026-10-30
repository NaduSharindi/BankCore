using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace BankCore.Tests.Integration;

/// <summary>
/// Boots the real API in-memory (full middleware pipeline, JWT auth, policies, EF Core)
/// against a throw-away SQLite file database.
/// </summary>
public sealed class BankCoreApiFactory : WebApplicationFactory<Program>
{
    public const string Password = "Test@Pass12345";

    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"bankcore-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:BankCore"] = $"Data Source={_databasePath}",
            ["Database:Provider"] = "Sqlite",
            ["Database:InitializeOnStartup"] = "true",
            ["Jwt:Issuer"] = "BankCore.Tests",
            ["Jwt:Audience"] = "BankCore.Tests",
            ["Jwt:SigningKey"] = "integration-tests-only-signing-key-0123456789abcdef",
            ["Seed:Enabled"] = "true",
            ["Seed:DefaultPassword"] = Password,
            ["RateLimiting:LoginPermitLimitPerMinute"] = "1000",
        }));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();

        try
        {
            File.Delete(_databasePath);
        }
        catch (IOException)
        {
            // Best effort clean-up of the temp database.
        }
    }
}
