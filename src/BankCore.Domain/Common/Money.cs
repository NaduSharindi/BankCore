using BankCore.Domain.Exceptions;

namespace BankCore.Domain.Common;

public static class Money
{
    /// <summary>LKR is handled with two decimal places (cents).</summary>
    public const int Scale = 2;

    public static void EnsureValidAmount(decimal amount)
    {
        if (amount <= 0)
        {
            throw new DomainException("INVALID_AMOUNT", "Amount must be greater than zero.");
        }

        if (decimal.Round(amount, Scale) != amount)
        {
            throw new DomainException("INVALID_AMOUNT", "Amount cannot have more than 2 decimal places.");
        }
    }
}
