using BankCore.Application.Dtos;
using BankCore.Domain.Entities;

namespace BankCore.Application.Mapping;

public static class MappingExtensions
{
    public static CustomerResponse ToResponse(this Customer customer) =>
        new(
            customer.Id,
            customer.CustomerNumber,
            customer.FullName,
            customer.Nic,
            customer.Email,
            customer.PhoneNumber,
            customer.CreatedAtUtc,
            customer.Accounts
                .OrderBy(a => a.CreatedAtUtc)
                .Select(a => new AccountSummary(a.AccountNumber, a.Type, a.Status, a.Balance, a.Currency))
                .ToList());

    public static AccountResponse ToResponse(this Account account, string customerName) =>
        new(
            account.Id,
            account.AccountNumber,
            account.CustomerId,
            customerName,
            account.Type,
            account.Status,
            account.Currency,
            account.Balance,
            account.CreatedAtUtc,
            account.UpdatedAtUtc);

    public static TransactionResponse ToResponse(this AccountTransaction transaction) =>
        new(
            transaction.Id,
            transaction.Reference,
            transaction.Type,
            transaction.Amount,
            transaction.BalanceAfter,
            transaction.Narration,
            transaction.CounterpartyAccountNumber,
            transaction.CreatedAtUtc);

    public static TransferResponse ToResponse(this FundTransfer transfer, bool isReplay) =>
        new(
            transfer.Id,
            transfer.Reference,
            transfer.FromAccountNumber,
            transfer.ToAccountNumber,
            transfer.Amount,
            transfer.Currency,
            transfer.Narration,
            transfer.InitiatedBy,
            transfer.CreatedAtUtc,
            isReplay);

    public static AuditLogResponse ToResponse(this AuditLog log) =>
        new(
            log.Id,
            log.TimestampUtc,
            log.Actor,
            log.Action,
            log.EntityType,
            log.EntityId,
            log.Details,
            log.IpAddress,
            log.CorrelationId);
}
