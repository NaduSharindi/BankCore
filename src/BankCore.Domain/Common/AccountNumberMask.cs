namespace BankCore.Domain.Common;

/// <summary>
/// Masks account numbers before they are written to logs (data-protection good practice).
/// </summary>
public static class AccountNumberMask
{
    public static string Mask(string? accountNumber)
    {
        if (string.IsNullOrEmpty(accountNumber) || accountNumber.Length <= 4)
        {
            return "****";
        }

        return new string('*', accountNumber.Length - 4) + accountNumber[^4..];
    }
}
