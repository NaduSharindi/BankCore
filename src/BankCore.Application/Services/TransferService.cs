using BankCore.Application.Abstractions;
using BankCore.Application.Common;
using BankCore.Application.Dtos;
using BankCore.Application.Mapping;
using BankCore.Application.Options;
using BankCore.Domain.Common;
using BankCore.Domain.Entities;
using BankCore.Domain.Enums;
using BankCore.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BankCore.Application.Services;

/// <summary>
/// Internal fund transfers between two accounts of the bank.
///
/// Guarantees:
///  * Atomicity   - debit, credit, both ledger entries, the transfer header and the audit row
///                  are persisted by ONE SaveChanges call, i.e. one database transaction.
///  * Idempotency - every request carries an Idempotency-Key; a retry with the same key returns
///                  the original result instead of moving money twice (unique DB index as backstop).
///  * Consistency - optimistic concurrency on accounts prevents lost updates under parallel load.
///  * Limits      - single-transfer and daily (Sri Lanka business day) limits are enforced.
/// </summary>
public sealed class TransferService(
    IBankDbContext db,
    IAuditService audit,
    IClock clock,
    ICurrentUser currentUser,
    IOptions<TransferLimitsOptions> limits,
    ILogger<TransferService> logger)
{
    public const int MaxIdempotencyKeyLength = 64;

    public async Task<TransferResponse> TransferAsync(
        TransferRequest request, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > MaxIdempotencyKeyLength)
        {
            throw new DomainException(
                "INVALID_IDEMPOTENCY_KEY",
                $"An Idempotency-Key of 1-{MaxIdempotencyKeyLength} characters is required.");
        }

        var existing = await db.Transfers
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return Replay(existing, request);
        }

        Money.EnsureValidAmount(request.Amount);

        if (request.FromAccountNumber == request.ToAccountNumber)
        {
            throw new DomainException("SAME_ACCOUNT", "Source and destination accounts must be different.");
        }

        var options = limits.Value;
        if (request.Amount > options.SingleTransferLimit)
        {
            throw new DomainException(
                "SINGLE_LIMIT_EXCEEDED",
                FormattableString.Invariant($"The maximum amount for a single transfer is {options.SingleTransferLimit:N2}."));
        }

        var accounts = await db.Accounts
            .Where(a => a.AccountNumber == request.FromAccountNumber || a.AccountNumber == request.ToAccountNumber)
            .ToListAsync(cancellationToken);

        var from = accounts.FirstOrDefault(a => a.AccountNumber == request.FromAccountNumber)
            ?? throw new NotFoundException("Account", request.FromAccountNumber);
        var to = accounts.FirstOrDefault(a => a.AccountNumber == request.ToAccountNumber)
            ?? throw new NotFoundException("Account", request.ToAccountNumber);

        if (from.Currency != to.Currency)
        {
            throw new DomainException("CURRENCY_MISMATCH", "Cross-currency transfers are not supported.");
        }

        // Validate BOTH legs before mutating anything.
        from.EnsureCanDebit(request.Amount);
        to.EnsureCanCredit(request.Amount);

        var now = clock.UtcNow;
        await EnsureWithinDailyLimitAsync(from, request.Amount, options.DailyTransferLimit, now, cancellationToken);

        var reference = ReferenceGenerator.New("TRF", now);
        db.Transactions.Add(from.TransferOut(request.Amount, reference, request.Narration, to.AccountNumber, now));
        db.Transactions.Add(to.TransferIn(request.Amount, reference, request.Narration, from.AccountNumber, now));

        var transfer = new FundTransfer(
            reference,
            idempotencyKey,
            from.Id,
            to.Id,
            from.AccountNumber,
            to.AccountNumber,
            request.Amount,
            from.Currency,
            request.Narration,
            currentUser.UserName,
            now);
        db.Transfers.Add(transfer);

        audit.Record(
            "FUND_TRANSFER",
            nameof(FundTransfer),
            transfer.Id.ToString(),
            FormattableString.Invariant(
                $"Ref={reference}; From={AccountNumberMask.Mask(from.AccountNumber)}; To={AccountNumberMask.Mask(to.AccountNumber)}; Amount={request.Amount:N2}"));

        try
        {
            // One SaveChanges == one database transaction: all rows commit, or none do.
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "CONCURRENCY_CONFLICT",
                "One of the accounts was updated by another operation. Please retry with the same Idempotency-Key.");
        }
        catch (DbUpdateException)
        {
            // A parallel request with the same Idempotency-Key may have won the race and
            // tripped the unique index. If so, return its result; otherwise re-throw.
            var winner = await db.Transfers
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey, cancellationToken);

            if (winner is null)
            {
                throw;
            }

            return Replay(winner, request);
        }

        logger.LogInformation(
            "Transfer {Reference}: {Amount} {Currency} from {From} to {To} by {User}",
            reference,
            request.Amount,
            from.Currency,
            AccountNumberMask.Mask(from.AccountNumber),
            AccountNumberMask.Mask(to.AccountNumber),
            currentUser.UserName);

        return transfer.ToResponse(isReplay: false);
    }

    private async Task EnsureWithinDailyLimitAsync(
        Account from, decimal amount, decimal dailyLimit, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var startOfDayUtc = BusinessCalendar.StartOfBusinessDayUtc(nowUtc);

        var transferredToday = (await db.Transactions
                .AsNoTracking()
                .Where(t => t.AccountId == from.Id
                            && t.Type == TransactionType.TransferOut
                            && t.CreatedAtUtc >= startOfDayUtc)
                .Select(t => t.Amount)
                .ToListAsync(cancellationToken))
            .Sum();

        if (transferredToday + amount > dailyLimit)
        {
            throw new DomainException(
                "DAILY_LIMIT_EXCEEDED",
                FormattableString.Invariant(
                    $"Daily transfer limit of {dailyLimit:N2} would be exceeded. Remaining today: {Math.Max(0, dailyLimit - transferredToday):N2}."));
        }
    }

    private static TransferResponse Replay(FundTransfer existing, TransferRequest request)
    {
        var samePayload = existing.FromAccountNumber == request.FromAccountNumber
                          && existing.ToAccountNumber == request.ToAccountNumber
                          && existing.Amount == request.Amount;

        if (!samePayload)
        {
            throw new ConflictException(
                "IDEMPOTENCY_KEY_REUSED",
                "This Idempotency-Key was already used for a different transfer request.");
        }

        return existing.ToResponse(isReplay: true);
    }
}
