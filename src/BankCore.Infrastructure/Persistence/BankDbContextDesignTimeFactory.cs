using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BankCore.Infrastructure.Persistence;

/// <summary>
/// Used only by the `dotnet ef` CLI when creating migrations (SQL Server).
/// Override the connection with the BANKCORE_DESIGN_CONNECTION environment variable.
/// </summary>
public sealed class BankDbContextDesignTimeFactory : IDesignTimeDbContextFactory<BankDbContext>
{
    private const string DefaultConnection =
        "Server=(localdb)\\MSSQLLocalDB;Database=BankCoreDb;Trusted_Connection=True;TrustServerCertificate=True";

    public BankDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("BANKCORE_DESIGN_CONNECTION") ?? DefaultConnection;
        var options = new DbContextOptionsBuilder<BankDbContext>().UseSqlServer(connectionString).Options;
        return new BankDbContext(options);
    }
}
