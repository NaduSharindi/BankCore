namespace BankCore.Domain.Entities;

/// <summary>
/// Header record for an internal fund transfer. The unique <see cref="IdempotencyKey"/>
/// guarantees that a retried request can never move money twice.
/// </summary>
public class FundTransfer
{
    // Required by EF Core.
    private FundTransfer()
    {
    }

    public FundTransfer(
        string reference,
        string idempotencyKey,
        Guid fromAccountId,
        Guid toAccountId,
        string fromAccountNumber,
        string toAccountNumber,
        decimal amount,
        string currency,
        string? narration,
        string initiatedBy,
        DateTime createdAtUtc)
    {
        Reference = reference;
        IdempotencyKey = idempotencyKey;
        FromAccountId = fromAccountId;
        ToAccountId = toAccountId;
        FromAccountNumber = fromAccountNumber;
        ToAccountNumber = toAccountNumber;
        Amount = amount;
        Currency = currency;
        Narration = string.IsNullOrWhiteSpace(narration) ? null : narration.Trim();
        InitiatedBy = initiatedBy;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public string Reference { get; private set; } = string.Empty;

    public string IdempotencyKey { get; private set; } = string.Empty;

    public Guid FromAccountId { get; private set; }

    public Guid ToAccountId { get; private set; }

    public string FromAccountNumber { get; private set; } = string.Empty;

    public string ToAccountNumber { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = Account.DefaultCurrency;

    public string? Narration { get; private set; }

    public string InitiatedBy { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }
}
