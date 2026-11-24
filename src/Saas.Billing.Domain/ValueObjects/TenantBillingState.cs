namespace Saas.Billing.Domain.ValueObjects;

public sealed record TenantBillingState(
    Guid TenantId,
    string Slug,
    string Name,
    TenantOperationalState State,
    bool UsesBilling,
    bool MayOperate,
    string EvaluatedAtGmt6);
