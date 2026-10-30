using BankCore.Api.Authentication;
using BankCore.Application.Dtos;
using BankCore.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankCore.Api.Controllers;

[ApiController]
[Route("api/v1/accounts")]
[Produces("application/json")]
public sealed class AccountsController(AccountService accounts) : ControllerBase
{
    /// <summary>Opens a Savings or Current account for an existing customer.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.Onboard)]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountResponse>> Open(OpenAccountRequest request, CancellationToken cancellationToken)
    {
        var account = await accounts.OpenAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetByNumber), new { accountNumber = account.AccountNumber }, account);
    }

    /// <summary>Gets account details and current balance.</summary>
    [HttpGet("{accountNumber}")]
    [Authorize(Policy = Policies.ViewAccounts)]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountResponse>> GetByNumber(string accountNumber, CancellationToken cancellationToken) =>
        Ok(await accounts.GetAsync(accountNumber, cancellationToken));

    /// <summary>Cash deposit at the counter.</summary>
    [HttpPost("{accountNumber}/deposits")]
    [Authorize(Policy = Policies.Transact)]
    [ProducesResponseType<TransactionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TransactionResponse>> Deposit(
        string accountNumber, CashTransactionRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await accounts.DepositAsync(accountNumber, request, cancellationToken));

    /// <summary>Cash withdrawal at the counter.</summary>
    [HttpPost("{accountNumber}/withdrawals")]
    [Authorize(Policy = Policies.Transact)]
    [ProducesResponseType<TransactionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TransactionResponse>> Withdraw(
        string accountNumber, CashTransactionRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await accounts.WithdrawAsync(accountNumber, request, cancellationToken));

    /// <summary>Freezes an account (e.g. on an AML or court order). Blocks all transactions.</summary>
    [HttpPost("{accountNumber}/freeze")]
    [Authorize(Policy = Policies.ManageAccounts)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Freeze(string accountNumber, CancellationToken cancellationToken)
    {
        await accounts.FreezeAsync(accountNumber, cancellationToken);
        return NoContent();
    }

    /// <summary>Lifts a freeze.</summary>
    [HttpPost("{accountNumber}/unfreeze")]
    [Authorize(Policy = Policies.ManageAccounts)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Unfreeze(string accountNumber, CancellationToken cancellationToken)
    {
        await accounts.UnfreezeAsync(accountNumber, cancellationToken);
        return NoContent();
    }

    /// <summary>Closes an account with a zero balance.</summary>
    [HttpPost("{accountNumber}/close")]
    [Authorize(Policy = Policies.ManageAccounts)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Close(string accountNumber, CancellationToken cancellationToken)
    {
        await accounts.CloseAsync(accountNumber, cancellationToken);
        return NoContent();
    }

    /// <summary>Account statement with opening/closing balance for a period (dates in Sri Lanka time, yyyy-MM-dd).</summary>
    [HttpGet("{accountNumber}/statement")]
    [Authorize(Policy = Policies.ViewAccounts)]
    [ProducesResponseType<AccountStatementResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AccountStatementResponse>> Statement(
        string accountNumber,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await accounts.GetStatementAsync(accountNumber, from, to, page, pageSize, cancellationToken));
}
