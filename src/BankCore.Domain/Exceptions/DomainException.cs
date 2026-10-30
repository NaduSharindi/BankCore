using BankCore.Domain.Common;

namespace BankCore.Domain.Exceptions;

/// <summary>A business rule was violated. Mapped to HTTP 422 by the API.</summary>
public class DomainException(string errorCode, string message) : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
}

/// <summary>Mapped to HTTP 404.</summary>
public sealed class NotFoundException(string resource, string key)
    : DomainException("NOT_FOUND", $"{resource} '{key}' was not found.")
{
}

/// <summary>Mapped to HTTP 409.</summary>
public sealed class ConflictException(string errorCode, string message)
    : DomainException(errorCode, message)
{
}

/// <summary>Mapped to HTTP 422.</summary>
public sealed class InsufficientFundsException(string accountNumber)
    : DomainException("INSUFFICIENT_FUNDS", $"Account {AccountNumberMask.Mask(accountNumber)} has insufficient funds.")
{
}

/// <summary>Mapped to HTTP 401. The message is deliberately generic to avoid user enumeration.</summary>
public sealed class AuthenticationFailedException()
    : DomainException("AUTHENTICATION_FAILED", "Invalid username or password.")
{
}
