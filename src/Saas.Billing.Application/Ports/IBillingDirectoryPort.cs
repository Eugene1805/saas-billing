using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Application.Ports;

public sealed record SpecialistLookupResult(
    bool Found,
    bool DependencyUnavailable,
    SpecialistSummary? Specialist)
{
    public static SpecialistLookupResult WithSpecialist(SpecialistSummary specialist) => new(true, false, specialist);
    public static SpecialistLookupResult NotFound() => new(false, false, null);
    public static SpecialistLookupResult Unavailable() => new(false, true, null);
}

public interface IBillingDirectoryPort
{
    Task<SpecialistLookupResult> GetSpecialistAsync(
        Guid tenantId,
        Guid specialistId,
        CancellationToken cancellationToken = default);
}
