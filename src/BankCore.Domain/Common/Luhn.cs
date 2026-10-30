namespace BankCore.Domain.Common;

/// <summary>
/// Luhn (mod 10) check digit, used so that mistyped account numbers can be detected early.
/// </summary>
public static class Luhn
{
    public static int ComputeCheckDigit(string digits)
    {
        ArgumentException.ThrowIfNullOrEmpty(digits);

        var sum = 0;
        var doubleDigit = true;

        for (var i = digits.Length - 1; i >= 0; i--)
        {
            if (!char.IsAsciiDigit(digits[i]))
            {
                throw new ArgumentException("Only digits are allowed.", nameof(digits));
            }

            var value = digits[i] - '0';
            if (doubleDigit)
            {
                value *= 2;
                if (value > 9)
                {
                    value -= 9;
                }
            }

            sum += value;
            doubleDigit = !doubleDigit;
        }

        return (10 - (sum % 10)) % 10;
    }

    public static string AppendCheckDigit(string digits) => digits + ComputeCheckDigit(digits);

    public static bool IsValid(string? number) =>
        !string.IsNullOrEmpty(number)
        && number.Length > 1
        && number.All(char.IsAsciiDigit)
        && ComputeCheckDigit(number[..^1]) == number[^1] - '0';
}
