namespace BankCore.Application.Options;

public sealed class TransferLimitsOptions
{
    public const string SectionName = "TransferLimits";

    /// <summary>Maximum value of a single transfer (LKR).</summary>
    public decimal SingleTransferLimit { get; set; } = 500_000m;

    /// <summary>Maximum total outgoing transfers per account per business day (LKR).</summary>
    public decimal DailyTransferLimit { get; set; } = 1_000_000m;
}

public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    public int MaxFailedLoginAttempts { get; set; } = 5;

    public int LockoutMinutes { get; set; } = 15;
}
