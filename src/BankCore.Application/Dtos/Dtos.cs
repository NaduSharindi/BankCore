using System.ComponentModel.DataAnnotations;
using BankCore.Application.Common;
using BankCore.Application.Validation;
using BankCore.Domain.Enums;

namespace BankCore.Application.Dtos;

// ---------- Auth ----------
public sealed record LoginRequest(
    [Required, StringLength(50)] string Username,
    [Required, StringLength(128)] string Password);

public sealed record AuthenticatedUser(Guid UserId, string Username, string Role);

// ---------- Customers ----------
public sealed record CreateCustomerRequest(
    [Required, StringLength(150, MinimumLength = 3)] string FullName,
    [Required, SriLankanNic] string Nic,
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, RegularExpression(@"^(?:\+94|0)7\d{8}$", ErrorMessage = "PhoneNumber must be a valid Sri Lankan mobile number, e.g. 0771234567 or +94771234567.")]
    string PhoneNumber);

public sealed record AccountSummary(
    string AccountNumber, AccountType Type, AccountStatus Status, decimal Balance, string Currency);

public sealed record CustomerResponse(
    Guid Id,
    string CustomerNumber,
    string FullName,
    string Nic,
    string Email,
    string PhoneNumber,
    DateTime CreatedAtUtc,
    IReadOnlyList<AccountSummary> Accounts);

// ---------- Accounts ----------
public sealed record OpenAccountRequest(
    Guid CustomerId,
    AccountType Type,
    [Range(typeof(decimal), "0", "100000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    decimal InitialDeposit = 0m);

public sealed record AccountResponse(
    Guid Id,
    string AccountNumber,
    Guid CustomerId,
    string CustomerName,
    AccountType Type,
    AccountStatus Status,
    string Currency,
    decimal Balance,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record CashTransactionRequest(
    [Range(typeof(decimal), "0.01", "10000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    decimal Amount,
    [StringLength(140)] string? Narration);

public sealed record TransactionResponse(
    long Id,
    string Reference,
    TransactionType Type,
    decimal Amount,
    decimal BalanceAfter,
    string? Narration,
    string? CounterpartyAccountNumber,
    DateTime CreatedAtUtc);

public sealed record AccountStatementResponse(
    string AccountNumber,
    string CustomerName,
    string Currency,
    DateOnly From,
    DateOnly To,
    decimal OpeningBalance,
    decimal TotalCredits,
    decimal TotalDebits,
    decimal ClosingBalance,
    PagedResult<TransactionResponse> Transactions);

// ---------- Transfers ----------
public sealed record TransferRequest(
    [Required, RegularExpression(@"^\d{12}$", ErrorMessage = "FromAccountNumber must be 12 digits.")] string FromAccountNumber,
    [Required, RegularExpression(@"^\d{12}$", ErrorMessage = "ToAccountNumber must be 12 digits.")] string ToAccountNumber,
    [Range(typeof(decimal), "0.01", "100000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    decimal Amount,
    [StringLength(140)] string? Narration);

public sealed record TransferResponse(
    Guid TransferId,
    string Reference,
    string FromAccountNumber,
    string ToAccountNumber,
    decimal Amount,
    string Currency,
    string? Narration,
    string InitiatedBy,
    DateTime CreatedAtUtc,
    bool IsReplay);

// ---------- Audit ----------
public sealed record AuditLogResponse(
    long Id,
    DateTime TimestampUtc,
    string Actor,
    string Action,
    string EntityType,
    string EntityId,
    string? Details,
    string? IpAddress,
    string? CorrelationId);
