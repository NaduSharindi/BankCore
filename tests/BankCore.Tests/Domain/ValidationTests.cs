using BankCore.Domain.Common;
using BankCore.Infrastructure.Services;

namespace BankCore.Tests.Domain;

public sealed class SriLankanNicTests
{
    [Theory]
    [InlineData("199025312345")] // new format, male, day 253
    [InlineData("199075312345")] // new format, female (500 + 253)
    [InlineData("902531234V")]   // old format
    [InlineData("927531234v")]   // old format, female, lower-case v
    [InlineData("902531234X")]
    public void Valid_nics_are_accepted(string nic) => Assert.True(SriLankanNic.IsValid(nic));

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("902531234A")]   // wrong suffix
    [InlineData("199040012345")] // day 400 does not exist
    [InlineData("199000012345")] // day 000 does not exist
    [InlineData("189925312345")] // year before 1900
    [InlineData("90253123V")]    // too short
    public void Invalid_nics_are_rejected(string nic) => Assert.False(SriLankanNic.IsValid(nic));
}

public sealed class LuhnTests
{
    [Theory]
    [InlineData("00100000001", "001000000016")]
    [InlineData("00100000002", "001000000024")]
    [InlineData("00100000003", "001000000032")]
    public void Check_digit_is_appended(string body, string expected) =>
        Assert.Equal(expected, Luhn.AppendCheckDigit(body));

    [Fact]
    public void Single_digit_typo_is_detected()
    {
        Assert.True(Luhn.IsValid("001000000016"));
        Assert.False(Luhn.IsValid("001000000017"));
        Assert.False(Luhn.IsValid("001000000116"));
    }
}

public sealed class AccountNumberMaskTests
{
    [Theory]
    [InlineData("001000000016", "********0016")]
    [InlineData("123", "****")]
    [InlineData(null, "****")]
    public void Only_last_four_digits_are_visible(string? input, string expected) =>
        Assert.Equal(expected, AccountNumberMask.Mask(input));
}

public sealed class PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Correct_password_verifies_and_wrong_password_fails()
    {
        var hash = _hasher.Hash("S3cure!Pass");

        Assert.True(_hasher.Verify("S3cure!Pass", hash));
        Assert.False(_hasher.Verify("s3cure!pass", hash));
    }

    [Fact]
    public void Same_password_produces_different_hashes_because_of_salt()
    {
        Assert.NotEqual(_hasher.Hash("Password1!"), _hasher.Hash("Password1!"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("PBKDF2-SHA512$abc$xx$yy")]
    [InlineData("MD5$1$c2FsdA==$aGFzaA==")]
    public void Malformed_hashes_never_verify(string storedHash) =>
        Assert.False(_hasher.Verify("anything", storedHash));
}
