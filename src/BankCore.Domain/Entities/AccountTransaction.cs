using BankCore.Domain.Enums;

namespace BankCore.Domain.Entities;

/// <summary>
/// Immutable ledger entry. Rows are only ever inserted, never updated or deleted.
/// </summary>
public class AccountTransaction
{
    // Required by EF Core.
    private AccountTransaction()
    {
    }

    internal AccountTransaction(
        Guid accountId,
        TransactionType type,
        decimal amount,
        decimal balanceAfter,
        string reference,
        string? narration,
        string? counterpartyAccountNumber,
        DateTime createdAtUtc)
    {
        AccountId = accountId;
        Type = type;
        Amount = amount;
        BalanceAfter = balanceAfter;
        Reference = reference;
        Narration = string.IsNullOrWhiteSpace(narration) ? null : narration.Trim();
        CounterpartyAccountNumber = counterpartyAccountNumber;
        CreatedAtUtc = createdAtUtc;
    }

    public long Id { get; private set; }

    public Guid AccountId { get; private set; }

    public TransactionType Type { get; private set; }

    public decimal Amount { get; private set; }

    public decimal BalanceAfter { get; private set; }

    public string Reference { get; private set; } = string.Empty;

    public string? Narration { get; private set; }

    public string? CounterpartyAccountNumber { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public bool IsCredit => Type is TransactionType.Deposit or TransactionType.TransferIn;
}
