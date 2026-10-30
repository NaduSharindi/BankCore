using BankCore.Application.Abstractions;
using BankCore.Application.Common;
using BankCore.Application.Dtos;
using BankCore.Application.Mapping;
using Microsoft.EntityFrameworkCore;

namespace BankCore.Application.Services;

public sealed class AuditQueryService(IBankDbContext db)
{
    public async Task<PagedResult<AuditLogResponse>> SearchAsync(
        string? action,
        string? actor,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var query = db.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(action))
        {
            var normalizedAction = action.Trim().ToUpperInvariant();
            query = query.Where(l => l.Action == normalizedAction);
        }

        if (!string.IsNullOrWhiteSpace(actor))
        {
            var normalizedActor = actor.Trim().ToLowerInvariant();
            query = query.Where(l => l.Actor == normalizedActor);
        }

        if (fromUtc.HasValue)
        {
            var from = fromUtc.Value.ToUniversalTime();
            query = query.Where(l => l.TimestampUtc >= from);
        }

        if (toUtc.HasValue)
        {
            var to = toUtc.Value.ToUniversalTime();
            query = query.Where(l => l.TimestampUtc < to);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(l => l.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogResponse>(rows.Select(r => r.ToResponse()).ToList(), page, pageSize, total);
    }
}
