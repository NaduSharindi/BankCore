using BankCore.Application.Abstractions;
using BankCore.Domain.Common;
using BankCore.Domain.Entities;
using BankCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BankCore.Infrastructure.Persistence;

public sealed class DbInitializer(
    BankDbContext db,
    IPasswordHasher passwordHasher,
    IConfiguration configuration,
    ILogger<DbInitializer> logger)
{
    /// <summary>
    /// SQL Server + migrations present: apply migrations.
    /// Otherwise (first run, or SQLite for tests/demo): create the schema from the model.
    /// </summary>
    public async Task InitialiseAsync(CancellationToken cancellationToken = default)
    {
        if (db.Database.IsSqlServer() && db.Database.GetMigrations().Any())
        {
            logger.LogInformation("Applying EF Core migrations");
            await db.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            await db.Database.EnsureCreatedAsync(cancellationToken);
        }
    }

    /// <summary>Seeds demo staff users, customers and accounts. Never enable in production.</summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await db.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        var password = configuration["Seed:DefaultPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Seed:DefaultPassword is not configured - skipping demo data");
            return;
        }

        var now = DateTime.UtcNow;

        db.Users.AddRange(
            new AppUser("admin", passwordHasher.Hash(password), Roles.Admin, now),
            new AppUser("teller", passwordHasher.Hash(password), Roles.Teller, now),
            new AppUser("auditor", passwordHasher.Hash(password), Roles.Auditor, now));

        var kasun = new Customer(
            "CIF000000001", "Kasun Perera", "199025312345", "kasun.perera@example.com", "0771234567", now);
        var nimali = new Customer(
            "CIF000000002", "Nimali Fernando", "927531234V", "nimali.fernando@example.com", "0712345678", now);
        db.Customers.AddRange(kasun, nimali);

        // Deterministic, Luhn-valid demo account numbers: 001000000016, 001000000024, 001000000032
        var kasunSavings = new Account(kasun.Id, Luhn.AppendCheckDigit("00100000001"), AccountType.Savings, now);
        var nimaliSavings = new Account(nimali.Id, Luhn.AppendCheckDigit("00100000002"), AccountType.Savings, now);
        var kasunCurrent = new Account(kasun.Id, Luhn.AppendCheckDigit("00100000003"), AccountType.Current, now);
        db.Accounts.AddRange(kasunSavings, nimaliSavings, kasunCurrent);

        db.Transactions.AddRange(
            kasunSavings.Deposit(250_000m, "SEED000000001", "Opening balance", now),
            nimaliSavings.Deposit(75_000m, "SEED000000002", "Opening balance", now),
            kasunCurrent.Deposit(1_000_000m, "SEED000000003", "Opening balance", now));

        db.AuditLogs.Add(new AuditLog("system", "DEMO_DATA_SEEDED", "System", "seed", null, null, null, now));

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Demo data seeded (users: admin, teller, auditor)");
    }
}
