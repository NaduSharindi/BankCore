using System.Globalization;
using System.Security.Cryptography;
using BankCore.Application.Abstractions;

namespace BankCore.Infrastructure.Services;

/// <summary>
/// PBKDF2-HMAC-SHA512 with a per-password random salt and 210,000 iterations
/// (OWASP Password Storage Cheat Sheet). Format: PBKDF2-SHA512$iterations$salt$hash
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string Prefix = "PBKDF2-SHA512";
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 210_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA512;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySize);

        return string.Join(
            '$',
            Prefix,
            Iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(key));
    }

    public bool Verify(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(passwordHash))
        {
            return false;
        }

        var parts = passwordHash.Split('$');
        if (parts.Length != 4
            || parts[0] != Prefix
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations)
            || iterations <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expected.Length);

        // Constant-time comparison prevents timing attacks.
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
