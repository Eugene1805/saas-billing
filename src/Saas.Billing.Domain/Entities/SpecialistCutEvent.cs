using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Domain.Entities;

public sealed class SpecialistCutEvent
{
    private SpecialistCutEvent()
    {
    }

    internal SpecialistCutEvent(
        Guid id,
        Guid specialistCutId,
        Guid tenantId,
        SpecialistCutStatus status,
        string actorName,
        DateTimeOffset occurredAtUtc,
        string note)
    {
        Id = id;
        SpecialistCutId = specialistCutId;
        TenantId = tenantId;
        Status = status;
        ActorName = actorName;
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
        Note = note;
    }

    public Guid Id { get; private set; }
    public Guid SpecialistCutId { get; private set; }
    public Guid TenantId { get; private set; }
    public SpecialistCutStatus Status { get; private set; }
    public string ActorName { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public string Note { get; private set; } = string.Empty;
}
