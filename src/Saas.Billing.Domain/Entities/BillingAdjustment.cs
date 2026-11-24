using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Domain.Entities;

public sealed class BillingAdjustment
{
    private BillingAdjustment()
    {
    }

    private BillingAdjustment(
        Guid id,
        string folio,
        Guid tenantId,
        AdjustmentTargetType targetType,
        Guid targetId,
        decimal deltaMxn,
        string reason,
        Guid actorAccountId,
        string actorName,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        Folio = folio;
        TenantId = tenantId;
        TargetType = targetType;
        TargetId = targetId;
        DeltaMxn = MoneyHelper.RoundMxn(deltaMxn);
        Reason = reason;
        ActorAccountId = actorAccountId;
        ActorName = actorName;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public string Folio { get; private set; } = string.Empty;
    public Guid TenantId { get; private set; }
    public AdjustmentTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public decimal DeltaMxn { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public Guid ActorAccountId { get; private set; }
    public string ActorName { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static BillingAdjustment Create(
        Guid id,
        string folio,
        Guid tenantId,
        AdjustmentTargetType targetType,
        Guid targetId,
        decimal deltaMxn,
        string reason,
        Guid actorAccountId,
        string actorName,
        DateTimeOffset createdAtUtc)
    {
        return new BillingAdjustment(
            id,
            folio.Trim(),
            tenantId,
            targetType,
            targetId,
            deltaMxn,
            reason.Trim(),
            actorAccountId,
            actorName.Trim(),
            createdAtUtc);
    }
}
