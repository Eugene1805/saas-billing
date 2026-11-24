using Microsoft.Extensions.Options;
using Saas.Billing.Application.Ports;
using Saas.Billing.Domain.ValueObjects;
using Saas.Billing.Infrastructure.Persistence;

namespace Saas.Billing.Infrastructure.Clinical;

public sealed class ClinicalOptions
{
    public const string SectionName = "Clinical";

    public string Mode { get; init; } = "Local";
    public bool SimulateUnavailable { get; init; }
    public List<EncounterSeedEntry> Encounters { get; init; } = [];
}

public sealed class EncounterSeedEntry
{
    public Guid EncounterId { get; init; }
    public Guid TenantId { get; init; }
    public Guid RecordId { get; init; }
    public Guid PatientId { get; init; }
    public Guid SpecialistId { get; init; }
    public EncounterStatus Status { get; init; }
    public DateTimeOffset? EndedAtUtc { get; init; }
}

internal sealed class LocalClinicalEncounterGatewayAdapter(
    IOptions<ClinicalOptions> optionsAccessor) : IClinicalEncounterGatewayPort
{
    private static readonly IReadOnlyList<EncounterSeedEntry> DefaultEncounters =
    [
        new()
        {
            EncounterId = BillingDbInitializer.Enc0101_1Id,
            TenantId = BillingDbInitializer.NorteTenantId,
            RecordId = BillingDbInitializer.Rec0101Id,
            PatientId = BillingDbInitializer.Pat0101Id,
            SpecialistId = BillingDbInitializer.MendozaNorteAccountId,
            Status = EncounterStatus.FINALIZADA,
            EndedAtUtc = new DateTimeOffset(2026, 11, 14, 18, 25, 0, TimeSpan.Zero),
        },
        new()
        {
            EncounterId = BillingDbInitializer.Enc0101_2Id,
            TenantId = BillingDbInitializer.NorteTenantId,
            RecordId = BillingDbInitializer.Rec0101Id,
            PatientId = BillingDbInitializer.Pat0101Id,
            SpecialistId = BillingDbInitializer.MendozaNorteAccountId,
            Status = EncounterStatus.FINALIZADA,
            EndedAtUtc = new DateTimeOffset(2026, 11, 18, 17, 5, 0, TimeSpan.Zero),
        },
        new()
        {
            EncounterId = BillingDbInitializer.Enc0102_1Id,
            TenantId = BillingDbInitializer.NorteTenantId,
            RecordId = BillingDbInitializer.Rec0102Id,
            PatientId = BillingDbInitializer.Pat0102Id,
            SpecialistId = BillingDbInitializer.SalazarNorteAccountId,
            Status = EncounterStatus.FINALIZADA,
            EndedAtUtc = new DateTimeOffset(2026, 11, 15, 21, 55, 0, TimeSpan.Zero),
        },
        new()
        {
            EncounterId = BillingDbInitializer.Enc0104_1Id,
            TenantId = BillingDbInitializer.NorteTenantId,
            RecordId = BillingDbInitializer.Rec0104Id,
            PatientId = BillingDbInitializer.Pat0104Id,
            SpecialistId = BillingDbInitializer.SalazarNorteAccountId,
            Status = EncounterStatus.FINALIZADA,
            EndedAtUtc = new DateTimeOffset(2026, 11, 20, 19, 0, 0, TimeSpan.Zero),
        },
        new()
        {
            EncounterId = BillingDbInitializer.Enc0101_InProgressId,
            TenantId = BillingDbInitializer.NorteTenantId,
            RecordId = BillingDbInitializer.Rec0101Id,
            PatientId = BillingDbInitializer.Pat0101Id,
            SpecialistId = BillingDbInitializer.MendozaNorteAccountId,
            Status = EncounterStatus.EN_CURSO,
            EndedAtUtc = null,
        },
    ];

    public Task<EncounterLookupResult> GetEncounterAsync(
        Guid tenantId,
        Guid encounterId,
        CancellationToken cancellationToken = default)
    {
        var opts = optionsAccessor.Value;
        if (opts.SimulateUnavailable)
        {
            return Task.FromResult(EncounterLookupResult.Unavailable());
        }

        var catalog = opts.Encounters.Count > 0 ? opts.Encounters : DefaultEncounters;
        var match = catalog.FirstOrDefault(e =>
            e.TenantId == tenantId && e.EncounterId == encounterId);

        if (match is null)
        {
            return Task.FromResult(EncounterLookupResult.NotFound());
        }

        return Task.FromResult(EncounterLookupResult.WithEncounter(new EncounterSummary(
            match.EncounterId,
            match.TenantId,
            match.RecordId,
            match.PatientId,
            match.SpecialistId,
            match.Status,
            match.EndedAtUtc)));
    }
}
