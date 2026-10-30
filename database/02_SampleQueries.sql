/* Example usage of the BankCore reporting objects. */

-- Statement for October 2026 (Sri Lanka dates converted to UTC)
EXEC dbo.usp_GetAccountStatement
     @AccountNumber = N'001000000016',
     @FromUtc       = '2026-09-30T18:30:00',
     @ToUtc         = '2026-10-31T18:30:00';

-- End-of-day summary
EXEC dbo.usp_GetDailyTransactionSummary @BusinessDate = '2026-10-30';

-- Dormant accounts (no activity for 1 year)
EXEC dbo.usp_GetDormantAccounts @Days = 365;

-- Must return zero rows
SELECT * FROM dbo.vw_BalanceReconciliation;

-- Top 10 accounts by outgoing transfer value this month
SELECT TOP (10)
    a.AccountNumber,
    TotalOut = SUM(t.Amount),
    Transfers = COUNT(*)
FROM dbo.AccountTransactions AS t
JOIN dbo.Accounts AS a ON a.Id = t.AccountId
WHERE t.[Type] = N'TransferOut'
  AND t.CreatedAtUtc >= DATEADD(MONTH, DATEDIFF(MONTH, 0, SYSUTCDATETIME()), 0)
GROUP BY a.AccountNumber
ORDER BY TotalOut DESC;
