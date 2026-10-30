using System.Globalization;
using System.Security.Cryptography;

namespace BankCore.Application.Common;

/// <summary>Creates human-readable transaction references, e.g. TRF261030143005123456.</summary>
public static class ReferenceGenerator
{
    public static string New(string prefix, DateTime nowUtc) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{prefix}{nowUtc:yyMMddHHmmss}{RandomNumberGenerator.GetInt32(0, 1_000_000):D6}");
}
