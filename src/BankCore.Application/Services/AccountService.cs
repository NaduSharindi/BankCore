using BankCore.Application.Abstractions;
using BankCore.Application.Common;
using BankCore.Application.Dtos;
using BankCore.Application.Mapping;
using BankCore.Domain.Common;
using BankCore.Domain.Entities;
using BankCore.Domain.Enums;
using BankCore.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BankCore.Application.Services;

public sealed class AccountService(
    IBankDbContext db,
    IAccountNumberGenerator numberGenerator,
    IAuditService audit,
    IClock clock,
    ILogger<AccountService> logger)
{
    private const int MaxStatementDays = 366;

    public async Task<AccountResponse> OpenAsync(OpenAccountRequest request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException("Customer", request.CustomerId.ToString());

        var now = clock.UtcNow;
        var accountNumber = await numberGenerator.NextAccountNumberAsync(cancellationToken);
        var account = new Account(customer.Id, accountNumber, request.Type, now);
        db.Accounts.Add(account);

        if (request.InitialDeposit > 0)
        {
            db.Transactions.Add(account.Deposit(
                request.InitialDeposit, ReferenceGenerator.New("DEP", now), "Initial deposit", now));
        }

        audit.Record(
            "ACCOUNT_OPENED",
            nameof(Account),
            account.AccountNumber,
            FormattableString.Invariant($"Type={request.Type}; InitialDeposit={request.InitialDeposit:N2}"));

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Account {AccountNumber} opened for customer {CustomerNumber}",
            AccountNumberMask.Mask(accountNumber),
            customer.CustomerNumber);

        return account.ToResponse(customer.FullName);
    }

    public async Task<AccountResponse> GetAsync(string accountNumber, CancellationToken cancellationToken)
    {
        var account = await db.Accounts
            .AsNoTracking()
            .Include(a => a.Customer)
            .FirstOrDefaultAsync(a => a.AccountNumber == accountNumber, cancellationToken)
            ?? throw new NotFoundException("Account", accountNumber);

        return account.ToResponse(account.Customer?.FullName ?? string.Empty);
    }

    public async Task<TransactionResponse> DepositAsync(
        string accountNumber, CashTransactionRequest request, CancellationToken cancellationToken)
    {
        var account = await FindForUpdateAsync(accountNumber, cancellationToken);
        var now = clock.UtcNow;

        var transaction = account.Deposit(request.Amount, ReferenceGenerator.New("DEP", now), request.Narration, now);
        db.Transactions.Add(transaction);
        audit.Record(
            "CASH_DEPOSIT",
            nameof(Account),
            account.AccountNumber,
            FormattableString.Invariant($"Ref={transaction.Reference}; Amount={request.Amount:N2}"));

        await db.SaveChangesWithConcurrencyCheckAsync(cancellationToken);
        logger.LogInformation(
            "Deposit {Reference} of {Amount} to {AccountNumber}",
            transaction.Reference, request.Amount, AccountNumberMask.Mask(accountNumber));

        return transaction.ToResponse();
    }

    public async Task<TransactionResponse> WithdrawAsync(
        string accountNumber, CashTransactionRequest request, CancellationToken cancellationToken)
    {
        var account = await FindForUpdateAsync(accountNumber, cancellationToken);
        var now = clock.UtcNow;

        var transaction = account.Withdraw(request.Amount, ReferenceGenerator.New("WDL", now), request.Narration, now);
        db.Transactions.Add(transaction);
        audit.Record(
            "CASH_WITHDRAWAL",
            nameof(Account),
            account.AccountNumber,
            FormattableString.Invariant($"Ref={transaction.Reference}; Amount={request.Amount:N2}"));

        await db.SaveChangesWithConcurrencyCheckAsync(cancellationToken);
        logger.LogInformation(
            "Withdrawal {Reference} of {Amount} from {AccountNumber}",
            transaction.Reference, request.Amount, AccountNumberMask.Mask(accountNumber));

        return transaction.ToResponse();
    }

    public Task FreezeAsync(string accountNumber, CancellationToken cancellationToken) =>
        ChangeStatusAsync(accountNumber, "ACCOUNT_FROZEN", (a, now) => a.Freeze(now), cancellationToken);

    public Task UnfreezeAsync(string accountNumber, CancellationToken cancellationToken) =>
        ChangeStatusAsync(accountNumber, "ACCOUNT_UNFROZEN", (a, now) => a.Unfreeze(now), cancellationToken);

    public Task CloseAsync(string accountNumber, CancellationToken cancellationToken) =>
        ChangeStatusAsync(accountNumber, "ACCOUNT_CLOSED", (a, now) => a.Close(now), cancellationToken);

    public async Task<AccountStatementResponse> GetStatementAsync(
        string accountNumber,
        DateOnly? from,
        DateOnly? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var account = await db.Accounts
            .AsNoTracking()
            .Include(a => a.Customer)
            .FirstOrDefaultAsync(a => a.AccountNumber == accountNumber, cancellationToken)
            ?? throw new NotFoundException("Account", accountNumber);

        var toDate = to ?? BusinessCalendar.Today(clock.UtcNow);
        var fromDate = from ?? toDate.AddDays(-30);

        if (fromDate > toDate)
        {
            throw new DomainException("INVALID_DATE_RANGE", "'from' must be on or before 'to'.");
        }

        if (toDate.DayNumber - fromDate.DayNumber > MaxStatementDays)
        {
            throw new DomainException("INVALID_DATE_RANGE", "A statement period cannot exceed one year.");
        }

        (page, pageSize) = Paging.Normalize(page, pageSize);
        var fromUtc = BusinessCalendar.ToUtcStart(fromDate);
        var toUtcExclusive = BusinessCalendar.ToUtcStart(toDate.AddDays(1));

        // Opening balance = running balance of the last entry before the period.
        var openingBalance = await db.Transactions
            .AsNoTracking()
            .Where(t => t.AccountId == account.Id && t.CreatedAtUtc < fromUtc)
            .OrderByDescending(t => t.Id)
            .Select(t => (decimal?)t.BalanceAfter)
            .FirstOrDefaultAsync(cancellationToken) ?? 0m;

        var periodQuery = db.Transactions
            .AsNoTracking()
            .Where(t => t.AccountId == account.Id && t.CreatedAtUtc >= fromUtc && t.CreatedAtUtc < toUtcExclusive);

        // Totals are aggregated in memory so the same code runs on SQL Server and SQLite
        // (SQLite cannot SUM decimal columns). The production equivalent is the
        // dbo.usp_GetAccountStatement stored procedure in /database.
        var movements = await periodQuery.Select(t => new { t.Type, t.Amount }).ToListAsync(cancellationToken);
        var totalCredits = movements
            .Where(m => m.Type is TransactionType.Deposit or TransactionType.TransferIn)
            .Sum(m => m.Amount);
        var totalDebits = movements
            .Where(m => m.Type is TransactionType.Withdrawal or TransactionType.TransferOut)
            .Sum(m => m.Amount);

        var pageItems = await periodQuery
            .OrderBy(t => t.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new AccountStatementResponse(
            account.AccountNumber,
            account.Customer?.FullName ?? string.Empty,
            account.Currency,
            fromDate,
            toDate,
            openingBalance,
            totalCredits,
            totalDebits,
            openingBalance + totalCredits - totalDebits,
            new PagedResult<TransactionResponse>(
                pageItems.Select(t => t.ToResponse()).ToList(), page, pageSize, movements.Count));
    }

    private async Task ChangeStatusAsync(
        string accountNumber, string auditAction, Action<Account, DateTime> change, CancellationToken cancellationToken)
    {
        var account = await FindForUpdateAsync(accountNumber, cancellationToken);
        change(account, clock.UtcNow);
        audit.Record(auditAction, nameof(Account), account.AccountNumber, $"NewStatus={account.Status}");
        await db.SaveChangesWithConcurrencyCheckAsync(cancellationToken);
        logger.LogInformation(
            "Account {AccountNumber} status changed to {Status}", AccountNumberMask.Mask(accountNumber), account.Status);
    }

    private async Task<Account> FindForUpdateAsync(string accountNumber, CancellationToken cancellationToken) =>
        await db.Accounts.FirstOrDefaultAsync(a => a.AccountNumber == accountNumber, cancellationToken)
        ?? throw new NotFoundException("Account", accountNumber);
}
