using BankCore.Api.Authentication;
using BankCore.Application.Dtos;
using BankCore.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankCore.Api.Controllers;

[ApiController]
[Route("api/v1/transfers")]
[Produces("application/json")]
public sealed class TransfersController(TransferService transfers) : ControllerBase
{
    public const string IdempotencyHeader = "Idempotency-Key";

    /// <summary>
    /// Internal fund transfer. Requires an Idempotency-Key header (e.g. a GUID).
    /// Retrying with the same key returns the original result (200) instead of moving money twice.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.Transact)]
    [ProducesResponseType<TransferResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<TransferResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TransferResponse>> Create(
        TransferRequest request,
        [FromHeader(Name = IdempotencyHeader)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Missing Idempotency-Key header",
                detail: "Send a unique Idempotency-Key header (e.g. a GUID) with every transfer request.");
        }

        var result = await transfers.TransferAsync(request, idempotencyKey.Trim(), cancellationToken);
        return result.IsReplay ? Ok(result) : StatusCode(StatusCodes.Status201Created, result);
    }
}
