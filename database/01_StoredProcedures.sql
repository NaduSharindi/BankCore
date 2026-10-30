/* =============================================================================
   BankCore - SQL Server reporting objects (T-SQL)
   Run against the BankCoreDb database AFTER the application has created the schema.
   These complement the EF Core code path and are intended for reporting / batch use.
   ============================================================================= */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* -----------------------------------------------------------------------------
   Account statement for a period.
   Result set 1: header with opening/closing balance and totals.
   Result set 2: line items with debit / credit columns.
   NVARCHAR(12) parameter matches the column type so the unique index on
   AccountNumber is used (no implicit conversion => sargable seek).
   ----------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_GetAccountStatement
    @AccountNumber NVARCHAR(12),
    @FromUtc       DATETIME2(7),
    @ToUtc         DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @AccountId UNIQUEIDENTIFIER;
    SELECT @AccountId = Id FROM dbo.Accounts WHERE AccountNumber = @AccountNumber;

    IF @AccountId IS NULL
        THROW 50001, 'Account not found.', 1;

    IF @FromUtc >= @ToUtc
        THROW 50002, '@FromUtc must be earlier than @ToUtc.', 1;

    DECLARE @Opening DECIMAL(18, 2) = ISNULL((
        SELECT TOP (1) BalanceAfter
        FROM dbo.AccountTransactions
        WHERE AccountId = @AccountId AND CreatedAtUtc < @FromUtc
        ORDER BY Id DESC), 0);

    ;WITH Period AS
    (
        SELECT
            Credit = CASE WHEN [Type] IN (N'Deposit', N'TransferIn')     THEN Amount ELSE 0 END,
            Debit  = CASE WHEN [Type] IN (N'Withdrawal', N'TransferOut') THEN Amount ELSE 0 END
        FROM dbo.AccountTransactions
        WHERE AccountId = @AccountId
          AND CreatedAtUtc >= @FromUtc
          AND CreatedAtUtc <  @ToUtc
    )
    SELECT
        AccountNumber  = @AccountNumber,
        OpeningBalance = @Opening,
        TotalCredits   = ISNULL(SUM(Credit), 0),
        TotalDebits    = ISNULL(SUM(Debit), 0),
        ClosingBalance = @Opening + ISNULL(SUM(Credit), 0) - ISNULL(SUM(Debit), 0),
        LineCount      = COUNT(*)
    FROM Period;

    SELECT
        t.Id,
        t.Reference,
        t.[Type],
        Credit = CASE WHEN t.[Type] IN (N'Deposit', N'TransferIn')     THEN t.Amount END,
        Debit  = CASE WHEN t.[Type] IN (N'Withdrawal', N'TransferOut') THEN t.Amount END,
        t.BalanceAfter,
        t.Narration,
        t.CounterpartyAccountNumber,
        t.CreatedAtUtc
    FROM dbo.AccountTransactions AS t
    WHERE t.AccountId = @AccountId
      AND t.CreatedAtUtc >= @FromUtc
      AND t.CreatedAtUtc <  @ToUtc
    ORDER BY t.Id;
END
GO

/* -----------------------------------------------------------------------------
   Daily transaction summary for one Sri Lanka business date (UTC+05:30).
   Typical end-of-day (EOD) operations report.
   ----------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_GetDailyTransactionSummary
    @BusinessDate DATE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @FromUtc DATETIME2(7) = DATEADD(MINUTE, -330, CAST(@BusinessDate AS DATETIME2(7)));
    DECLARE @ToUtc   DATETIME2(7) = DATEADD(DAY, 1, @FromUtc);

    SELECT
        BusinessDate     = @BusinessDate,
        TransactionType  = t.[Type],
        TransactionCount = COUNT(*),
        TotalAmount      = SUM(t.Amount),
        LargestAmount    = MAX(t.Amount)
    FROM dbo.AccountTransactions AS t
    WHERE t.CreatedAtUtc >= @FromUtc
      AND t.CreatedAtUtc <  @ToUtc
    GROUP BY t.[Type]
    ORDER BY t.[Type];
END
GO

/* -----------------------------------------------------------------------------
   Dormant accounts: active accounts with no customer transaction for @Days days.
   Used for dormancy classification / CBSL reporting workflows.
   ----------------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_GetDormantAccounts
    @Days INT = 365
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CutoffUtc DATETIME2(7) = DATEADD(DAY, -@Days, SYSUTCDATETIME());

    SELECT
        a.AccountNumber,
        c.CustomerNumber,
        c.FullName,
        a.[Type],
        a.Balance,
        LastTransactionUtc = lt.LastTransactionUtc
    FROM dbo.Accounts AS a
    INNER JOIN dbo.Customers AS c ON c.Id = a.CustomerId
    OUTER APPLY
    (
        SELECT MAX(t.CreatedAtUtc) AS LastTransactionUtc
        FROM dbo.AccountTransactions AS t
        WHERE t.AccountId = a.Id
    ) AS lt
    WHERE a.[Status] = N'Active'
      AND (lt.LastTransactionUtc IS NULL OR lt.LastTransactionUtc < @CutoffUtc)
    ORDER BY lt.LastTransactionUtc;
END
GO

/* -----------------------------------------------------------------------------
   Reconciliation check: every account balance must equal the running balance of
   its latest ledger entry. Any row returned indicates a data integrity issue.
   ----------------------------------------------------------------------------- */
CREATE OR ALTER VIEW dbo.vw_BalanceReconciliation
AS
SELECT
    a.AccountNumber,
    AccountBalance = a.Balance,
    LedgerBalance  = ISNULL(last.BalanceAfter, 0),
    Difference     = a.Balance - ISNULL(last.BalanceAfter, 0)
FROM dbo.Accounts AS a
OUTER APPLY
(
    SELECT TOP (1) t.BalanceAfter
    FROM dbo.AccountTransactions AS t
    WHERE t.AccountId = a.Id
    ORDER BY t.Id DESC
) AS last
WHERE a.Balance <> ISNULL(last.BalanceAfter, 0);
GO
