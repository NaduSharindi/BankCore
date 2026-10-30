using BankCore.Domain.Entities;
using BankCore.Domain.Enums;
using BankCore.Domain.Exceptions;

namespace BankCore.Tests.Domain;

public sealed class AccountTests
{
    private static readonly DateTime Now = new(2026, 10, 30, 4, 30, 0, DateTimeKind.Utc);

    private static Account NewAccount(decimal openingBalance = 0m)
    {
        var account = new Account(Guid.NewGuid(), "001000000016", AccountType.Savings, Now);
        if (openingBalance > 0)
        {
            account.Deposit(openingBalance, "SEED", null, Now);
        }

        return account;
    }

    [Fact]
    public void New_account_is_active_with_zero_LKR_balance()
    {
        var account = NewAccount();

        Assert.Equal(AccountStatus.Active, account.Status);
        Assert.Equal(0m, account.Balance);
        Assert.Equal("LKR", account.Currency);
    }

    [Fact]
    public void Deposit_increases_balance_and_records_running_balance()
    {
        var account = NewAccount(1_000m);

        var transaction = account.Deposit(500.25m, "DEP1", "Cash", Now);

        Assert.Equal(1_500.25m, account.Balance);
        Assert.Equal(TransactionType.Deposit, transaction.Type);
        Assert.Equal(1_500.25m, transaction.BalanceAfter);
        Assert.True(transaction.IsCredit);
    }

    [Fact]
    public void Withdraw_more_than_balance_throws_and_leaves_balance_unchanged()
    {
        var account = NewAccount(1_000m);

        Assert.Throws<InsufficientFundsException>(() => account.Withdraw(1_000.01m, "WDL1", null, Now));
        Assert.Equal(1_000m, account.Balance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Non_positive_amounts_are_rejected(decimal amount)
    {
        var account = NewAccount(1_000m);

        var ex = Assert.Throws<DomainException>(() => account.Deposit(amount, "X", null, Now));
        Assert.Equal("INVALID_AMOUNT", ex.ErrorCode);
    }

    [Fact]
    public void Amounts_with_more_than_two_decimals_are_rejected()
    {
        var account = NewAccount();

        var ex = Assert.Throws<DomainException>(() => account.Deposit(10.005m, "X", null, Now));
        Assert.Equal("INVALID_AMOUNT", ex.ErrorCode);
    }

    [Fact]
    public void Frozen_account_cannot_transact_until_unfrozen()
    {
        var account = NewAccount(5_000m);
        account.Freeze(Now);

        var ex = Assert.Throws<DomainException>(() => account.Withdraw(100m, "WDL", null, Now));
        Assert.Equal("ACCOUNT_NOT_ACTIVE", ex.ErrorCode);

        account.Unfreeze(Now);
        account.Withdraw(100m, "WDL", null, Now);
        Assert.Equal(4_900m, account.Balance);
    }

    [Fact]
    public void Account_with_balance_cannot_be_closed()
    {
        var account = NewAccount(10m);

        var ex = Assert.Throws<DomainException>(() => account.Close(Now));
        Assert.Equal("BALANCE_NOT_ZERO", ex.ErrorCode);

        account.Withdraw(10m, "WDL", null, Now);
        account.Close(Now);
        Assert.Equal(AccountStatus.Closed, account.Status);
    }

    [Fact]
    public void Every_change_rotates_the_concurrency_stamp()
    {
        var account = NewAccount();
        var before = account.ConcurrencyStamp;

        account.Deposit(1m, "DEP", null, Now);

        Assert.NotEqual(before, account.ConcurrencyStamp);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("00100000001A")]
    [InlineData("0010000000161")]
    public void Invalid_account_numbers_are_rejected(string accountNumber)
    {
        Assert.Throws<DomainException>(() => new Account(Guid.NewGuid(), accountNumber, AccountType.Savings, Now));
    }
}
