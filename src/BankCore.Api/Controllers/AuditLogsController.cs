using BankCore.Api.Authentication;
using BankCore.Application.Common;
using BankCore.Application.Dtos;
using BankCore.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankCore.Api.Controllers;

[ApiController]
[Route("api/v1/audit-logs")]
[Produces("application/json")]
public sealed class AuditLogsController(AuditQueryService auditQuery) : ControllerBase
{
    /// <summary>Searches the audit trail (newest first). Read-only, for Auditors and Admins.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.ViewAudit)]
    [ProducesResponseType<PagedResult<AuditLogResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AuditLogResponse>>> Search(
        [FromQuery] string? action,
        [FromQuery] string? actor,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await auditQuery.SearchAsync(action, actor, fromUtc, toUtc, page, pageSize, cancellationToken));
}
