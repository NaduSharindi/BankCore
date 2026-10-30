using System.Text.RegularExpressions;

namespace BankCore.Domain.Common;

/// <summary>
/// Validates Sri Lankan National Identity Card numbers.
/// Old format: 9 digits + V/X (e.g. 901234567V). New format: 12 digits (e.g. 199012345678).
/// The day-of-year segment is 001-366 for males and 501-866 for females.
/// </summary>
public static partial class SriLankanNic
{
    [GeneratedRegex(@"^\d{9}[VvXx]$")]
    private static partial Regex OldFormat();

    [GeneratedRegex(@"^\d{12}$")]
    private static partial Regex NewFormat();

    public static bool IsValid(string? nic)
    {
        if (string.IsNullOrWhiteSpace(nic))
        {
            return false;
        }

        nic = nic.Trim();
        int dayValue;

        if (OldFormat().IsMatch(nic))
        {
            dayValue = int.Parse(nic.AsSpan(2, 3));
        }
        else if (NewFormat().IsMatch(nic))
        {
            var year = int.Parse(nic.AsSpan(0, 4));
            if (year < 1900 || year > DateTime.UtcNow.Year)
            {
                return false;
            }

            dayValue = int.Parse(nic.AsSpan(4, 3));
        }
        else
        {
            return false;
        }

        if (dayValue > 500)
        {
            dayValue -= 500;
        }

        return dayValue is >= 1 and <= 366;
    }

    public static string Normalize(string nic) => nic.Trim().ToUpperInvariant();
}
