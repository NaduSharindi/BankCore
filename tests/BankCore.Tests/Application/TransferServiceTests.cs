using BankCore.Application.Dtos;
using BankCore.Application.Options;
using BankCore.Application.Services;
using BankCore.Domain.Entities;
using BankCore.Domain.Enums;
using BankCore.Domain.Exceptions;
using BankCore.Infrastructure.Persistence;
using BankCore.Infrastructure.Services;
using BankCore.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BankCore.Tests.Application;

public sealed class TransferServiceTests : IDisposable
{
    private const string Savings = "001000000016";
    private const string Current = "001000000024";

    private readonly SqliteTestDatabase _database = new();
    private readonly FixedClock _clock = new(new DateTime(2026, 10, 30, 4, 30, 0, DateTimeKind.Utc));

    public TransferServiceTests()
    {
        using var db = _database.CreateContext();
        var customer = new Customer(
            "CIF000000001", "Test Customer", "199025312345", "test@example.com", "0771234567", _clock.UtcNow);
        var savings = new Account(customer.Id, Savings, AccountType.Savings, _clock.UtcNow);
        var current = new Account(customer.Id, Current, AccountType.Current, _clock.UtcNow);

        db.Customers.Add(customer);
        db.Accounts.AddRange(savings, current);
        db.Transactions.Add(savings.Deposit(100_000m, "SEED1", "Opening balance", _clock.UtcNow));
        db.SaveChanges();
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Transfer_moves_funds_and_writes_double_entry_ledger()
    {
        await using var db = _database.CreateContext();

        var result = await CreateService(db).TransferAsync(Request(25_000.50m), "key-1", CancellationToken.None);

        Assert.False(result.IsReplay);
        Assert.Equal(74_999.50m, await BalanceOf(Savings));
        Assert.Equal(25_000.50m, await BalanceOf(Current));

        await using var verify = _database.CreateContext();
        var legs = await verify.Transactions.Where(t => t.Reference == result.Reference).ToListAsync();
        Assert.Equal(2, legs.Count);
        Assert.Contains(legs, t => t.Type == TransactionType.TransferOut && t.Amount == 25_000.50m);
        Assert.Contains(legs, t => t.Type == TransactionType.TransferIn && t.Amount == 25_000.50m);
        Assert.True(await verify.AuditLogs.AnyAsync(a => a.Action == "FUND_TRANSFER"));
    }

    [Fact]
    public async Task Retry_with_same_idempotency_key_does_not_debit_twice()
    {
        await using (var db = _database.CreateContext())
        {
            await CreateService(db).TransferAsync(Request(10_000m), "retry-key", CancellationToken.None);
        }

        await using (var db = _database.CreateContext())
        {
            var replay = await CreateService(db).TransferAsync(Request(10_000m), "retry-key", CancellationToken.None);
            Assert.True(replay.IsReplay);
        }

        Assert.Equal(90_000m, await BalanceOf(Savings));
        await using var verify = _database.CreateContext();
        Assert.Equal(1, await verify.Transfers.CountAsync());
    }

    [Fact]
    public async Task Reusing_idempotency_key_for_a_different_amount_is_a_conflict()
    {
        await using (var db = _database.CreateContext())
        {
            await CreateService(db).TransferAsync(Request(1_000m), "same-key", CancellationToken.None);
        }

        await using var db2 = _database.CreateContext();
        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => CreateService(db2).TransferAsync(Request(2_000m), "same-key", CancellationToken.None));
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", ex.ErrorCode);
    }

    [Fact]
    public async Task Insufficient_funds_rejects_transfer_and_leaves_balances_untouched()
    {
        await using var db = _database.CreateContext();

        await Assert.ThrowsAsync<InsufficientFundsException>(
            () => CreateService(db).TransferAsync(Request(100_000.01m), "big", CancellationToken.None));

        Assert.Equal(100_000m, await BalanceOf(Savings));
        Assert.Equal(0m, await BalanceOf(Current));
    }

    [Fact]
    public async Task Daily_limit_is_enforced_across_multiple_transfers()
    {
        var limits = new TransferLimitsOptions { SingleTransferLimit = 60_000m, DailyTransferLimit = 80_000m };

        await using (var db = _database.CreateContext())
        {
            await CreateService(db, limits).TransferAsync(Request(50_000m), "d1", CancellationToken.None);
        }

        await using var db2 = _database.CreateContext();
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => CreateService(db2, limits).TransferAsync(Request(40_000m), "d2", CancellationToken.None));
        Assert.Equal("DAILY_LIMIT_EXCEEDED", ex.ErrorCode);
    }

    [Fact]
    public async Task Daily_limit_resets_on_the_next_Sri_Lanka_business_day()
    {
        var limits = new TransferLimitsOptions { SingleTransferLimit = 60_000m, DailyTransferLimit = 60_000m };

        await using (var db = _database.CreateContext())
        {
            await CreateService(db, limits).TransferAsync(Request(30_000m), "day1", CancellationToken.None);
        }

        // 18:30 UTC == 00:00 next day in Sri Lanka (UTC+05:30)
        _clock.UtcNow = new DateTime(2026, 10, 30, 18, 30, 0, DateTimeKind.Utc);

        await using var db2 = _database.CreateContext();
        var result = await CreateService(db2, limits).TransferAsync(Request(40_000m), "day2", CancellationToken.None);
        Assert.False(result.IsReplay);
    }

    [Fact]
    public async Task Transfer_to_the_same_account_is_rejected()
    {
        await using var db = _database.CreateContext();
        var request = new TransferRequest(Savings, Savings, 100m, null);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => CreateService(db).TransferAsync(request, "self", CancellationToken.None));
        Assert.Equal("SAME_ACCOUNT", ex.ErrorCode);
    }

    [Fact]
    public async Task Transfer_into_a_frozen_account_is_rejected()
    {
        await using (var db = _database.CreateContext())
        {
            var current = await db.Accounts.SingleAsync(a => a.AccountNumber == Current);
            current.Freeze(_clock.UtcNow);
            await db.SaveChangesAsync();
        }

        await using var db2 = _database.CreateContext();
        var ex = await Assert.ThrowsAsync<DomainException>(
            () => CreateService(db2).TransferAsync(Request(100m), "frozen", CancellationToken.None));
        Assert.Equal("ACCOUNT_NOT_ACTIVE", ex.ErrorCode);
        Assert.Equal(100_000m, await BalanceOf(Savings));
    }

    [Fact]
    public async Task Unknown_destination_account_returns_not_found()
    {
        await using var db = _database.CreateContext();
        var request = new TransferRequest(Savings, "001999999990", 100m, null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => CreateService(db).TransferAsync(request, "nf", CancellationToken.None));
    }

    private static TransferRequest Request(decimal amount) => new(Savings, Current, amount, "Unit test");

    private TransferService CreateService(BankDbContext db, TransferLimitsOptions? limits = null)
    {
        var user = new FakeCurrentUser();
        return new TransferService(
            db,
            new AuditService(db, user, _clock),
            _clock,
            user,
            Options.Create(limits ?? new TransferLimitsOptions()),
            NullLogger<TransferService>.Instance);
    }

    private async Task<decimal> BalanceOf(string accountNumber)
    {
        await using var db = _database.CreateContext();
        return await db.Accounts.AsNoTracking()
            .Where(a => a.AccountNumber == accountNumber)
            .Select(a => a.Balance)
            .SingleAsync();
    }
}
