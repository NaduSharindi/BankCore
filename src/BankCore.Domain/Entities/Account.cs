using BankCore.Domain.Common;
using BankCore.Domain.Enums;
using BankCore.Domain.Exceptions;

namespace BankCore.Domain.Entities;

/// <summary>
/// A customer deposit account. All balance changes go through methods on this class,
/// and every change produces an <see cref="AccountTransaction"/> ledger entry.
/// </summary>
public class Account
{
    public const string DefaultCurrency = "LKR";

    // Required by EF Core.
    private Account()
    {
    }

    public Account(
        Guid customerId,
        string accountNumber,
        AccountType type,
        DateTime createdAtUtc,
        string currency = DefaultCurrency)
    {
        if (customerId == Guid.Empty)
        {
            throw new DomainException("INVALID_CUSTOMER", "A customer is required to open an account.");
        }

        if (string.IsNullOrWhiteSpace(accountNumber)
            || accountNumber.Length != 12
            || !accountNumber.All(char.IsAsciiDigit))
        {
            throw new DomainException("INVALID_ACCOUNT_NUMBER", "Account number must contain exactly 12 digits.");
        }

        if (!Enum.IsDefined(type))
        {
            throw new DomainException("INVALID_ACCOUNT_TYPE", "Unsupported account type.");
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
        {
            throw new DomainException("INVALID_CURRENCY", "Currency must be a 3-letter ISO code.");
        }

        CustomerId = customerId;
        AccountNumber = accountNumber;
        Type = type;
        Currency = currency.ToUpperInvariant();
        Status = AccountStatus.Active;
        Balance = 0m;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public string AccountNumber { get; private set; } = string.Empty;

    public Guid CustomerId { get; private set; }

    public Customer? Customer { get; private set; }

    public AccountType Type { get; private set; }

    public string Currency { get; private set; } = DefaultCurrency;

    public decimal Balance { get; private set; }

    public AccountStatus Status { get; private set; }

    /// <summary>
    /// Optimistic concurrency token. Regenerated on every change so that two tellers
    /// updating the same account at the same time cannot silently overwrite each other.
    /// </summary>
    public Guid ConcurrencyStamp { get; private set; } = Guid.NewGuid();

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public AccountTransaction Deposit(decimal amount, string reference, string? narration, DateTime nowUtc)
    {
        EnsureCanCredit(amount);
        Balance += amount;
        Touch(nowUtc);
        return new AccountTransaction(Id, TransactionType.Deposit, amount, Balance, reference, narration, null, nowUtc);
    }

    public AccountTransaction Withdraw(decimal amount, string reference, string? narration, DateTime nowUtc)
    {
        EnsureCanDebit(amount);
        Balance -= amount;
        Touch(nowUtc);
        return new AccountTransaction(Id, TransactionType.Withdrawal, amount, Balance, reference, narration, null, nowUtc);
    }

    public AccountTransaction TransferOut(
        decimal amount, string reference, string? narration, string toAccountNumber, DateTime nowUtc)
    {
        EnsureCanDebit(amount);
        Balance -= amount;
        Touch(nowUtc);
        return new AccountTransaction(
            Id, TransactionType.TransferOut, amount, Balance, reference, narration, toAccountNumber, nowUtc);
    }

    public AccountTransaction TransferIn(
        decimal amount, string reference, string? narration, string fromAccountNumber, DateTime nowUtc)
    {
        EnsureCanCredit(amount);
        Balance += amount;
        Touch(nowUtc);
        return new AccountTransaction(
            Id, TransactionType.TransferIn, amount, Balance, reference, narration, fromAccountNumber, nowUtc);
    }

    public void EnsureCanDebit(decimal amount)
    {
        Money.EnsureValidAmount(amount);
        EnsureActive();

        if (Balance < amount)
        {
            throw new InsufficientFundsException(AccountNumber);
        }
    }

    public void EnsureCanCredit(decimal amount)
    {
        Money.EnsureValidAmount(amount);
        EnsureActive();
    }

    public void Freeze(DateTime nowUtc)
    {
        if (Status == AccountStatus.Closed)
        {
            throw new DomainException("ACCOUNT_CLOSED", "A closed account cannot be frozen.");
        }

        if (Status == AccountStatus.Frozen)
        {
            throw new ConflictException("ACCOUNT_ALREADY_FROZEN", "The account is already frozen.");
        }

        Status = AccountStatus.Frozen;
        Touch(nowUtc);
    }

    public void Unfreeze(DateTime nowUtc)
    {
        if (Status != AccountStatus.Frozen)
        {
            throw new ConflictException("ACCOUNT_NOT_FROZEN", "Only a frozen account can be unfrozen.");
        }

        Status = AccountStatus.Active;
        Touch(nowUtc);
    }

    public void Close(DateTime nowUtc)
    {
        if (Status == AccountStatus.Closed)
        {
            throw new ConflictException("ACCOUNT_ALREADY_CLOSED", "The account is already closed.");
        }

        if (Balance != 0m)
        {
            throw new DomainException("BALANCE_NOT_ZERO", "The account balance must be zero before it can be closed.");
        }

        Status = AccountStatus.Closed;
        Touch(nowUtc);
    }

    private void EnsureActive()
    {
        if (Status != AccountStatus.Active)
        {
            throw new DomainException(
                "ACCOUNT_NOT_ACTIVE",
                $"Account {AccountNumberMask.Mask(AccountNumber)} is {Status} and cannot be used for transactions.");
        }
    }

    private void Touch(DateTime nowUtc)
    {
        ConcurrencyStamp = Guid.NewGuid();
        UpdatedAtUtc = nowUtc;
    }
}
