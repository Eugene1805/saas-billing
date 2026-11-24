using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Application.Ports;

public sealed record EncounterLookupResult(
    bool Found,
    bool DependencyUnavailable,
    EncounterSummary? Encounter)
{
    public static EncounterLookupResult WithEncounter(EncounterSummary encounter) => new(true, false, encounter);
    public static EncounterLookupResult NotFound() => new(false, false, null);
    public static EncounterLookupResult Unavailable() => new(false, true, null);
}

public interface IClinicalEncounterGatewayPort
{
    Task<EncounterLookupResult> GetEncounterAsync(
        Guid tenantId,
        Guid encounterId,
        CancellationToken cancellationToken = default);
}
