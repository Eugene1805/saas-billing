namespace Saas.Billing.Domain.Entities;

public sealed class ChargeSheetAuditEvent
{
    private ChargeSheetAuditEvent()
    {
    }

    internal ChargeSheetAuditEvent(
        Guid id,
        Guid chargeSheetId,
        Guid tenantId,
        string actorName,
        DateTimeOffset occurredAtUtc,
        string action,
        string detail)
    {
        Id = id;
        ChargeSheetId = chargeSheetId;
        TenantId = tenantId;
        ActorName = actorName;
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
        Action = action;
        Detail = detail;
    }

    public Guid Id { get; private set; }
    public Guid ChargeSheetId { get; private set; }
    public Guid TenantId { get; private set; }
    public string ActorName { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string Detail { get; private set; } = string.Empty;
}
