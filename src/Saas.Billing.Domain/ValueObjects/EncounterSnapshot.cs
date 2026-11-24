namespace Saas.Billing.Domain.ValueObjects;

public enum EncounterStatus
{
    EN_CURSO = 0,
    FINALIZADA = 1,
}

public sealed record EncounterSummary(
    Guid EncounterId,
    Guid TenantId,
    Guid RecordId,
    Guid PatientId,
    Guid SpecialistId,
    EncounterStatus Status,
    DateTimeOffset? EndedAtUtc);
