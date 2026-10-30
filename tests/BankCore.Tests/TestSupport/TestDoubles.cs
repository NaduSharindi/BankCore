using BankCore.Application.Abstractions;
using BankCore.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BankCore.Tests.TestSupport;

public sealed class FixedClock(DateTime utcNow) : IClock
{
    public DateTime UtcNow { get; set; } = utcNow;
}

public sealed class FakeCurrentUser(string userName = "test.teller") : ICurrentUser
{
    public string UserName { get; } = userName;

    public string? IpAddress => "127.0.0.1";

    public string? CorrelationId => "test-correlation-id";
}

/// <summary>
/// A real relational database (SQLite in-memory) so tests exercise actual SQL, unique indexes,
/// concurrency tokens and transactions - unlike the EF InMemory provider.
/// </summary>
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteTestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    public BankDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<BankDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}
