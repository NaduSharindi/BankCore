namespace BankCore.Domain.Enums;

public enum AccountType
{
    Savings = 1,
    Current = 2,
}

public enum AccountStatus
{
    Active = 1,
    Frozen = 2,
    Closed = 3,
}

public enum TransactionType
{
    Deposit = 1,
    Withdrawal = 2,
    TransferIn = 3,
    TransferOut = 4,
}
