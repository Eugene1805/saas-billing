namespace Saas.Billing.Domain.ValueObjects;

public sealed record SpecialistSummary(
    Guid AccountId,
    Guid TenantId,
    bool Active,
    string FullName,
    string Specialty);
