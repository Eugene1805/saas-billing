namespace Saas.Billing.Domain.ValueObjects;

public sealed record BillingActorContext(
    Guid AccountId,
    Guid? TenantId,
    OrgRole Role,
    string FullName);
