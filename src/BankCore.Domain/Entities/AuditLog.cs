namespace BankCore.Domain.Entities;

/// <summary>
/// Append-only audit trail entry (who did what, when, from where).
/// </summary>
public class AuditLog
{
    // Required by EF Core.
    private AuditLog()
    {
    }

    public AuditLog(
        string actor,
        string action,
        string entityType,
        string entityId,
        string? details,
        string? ipAddress,
        string? correlationId,
        DateTime timestampUtc)
    {
        Actor = actor;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        Details = details;
        IpAddress = ipAddress;
        CorrelationId = correlationId;
        TimestampUtc = timestampUtc;
    }

    public long Id { get; private set; }

    public DateTime TimestampUtc { get; private set; }

    public string Actor { get; private set; } = string.Empty;

    public string Action { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public string EntityId { get; private set; } = string.Empty;

    public string? Details { get; private set; }

    public string? IpAddress { get; private set; }

    public string? CorrelationId { get; private set; }
}
