using Saas.Billing.Domain.Entities;
using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Application.Ports;

public sealed record ChargeSheetQueryFilter(
    Guid TenantId,
    Guid? EncounterId,
    Guid? PatientId,
    Guid? SpecialistId,
    ChargeSheetStatus? Status,
    IReadOnlySet<Guid>? RestrictToPatientIds);

public interface IChargeSheetRepository
{
    Task<EncounterChargeSheet?> GetByIdAsync(
        Guid tenantId,
        Guid chargeSheetId,
        bool includeDetails,
        CancellationToken cancellationToken = default);

    Task<EncounterChargeSheet?> GetByEncounterIdAsync(
        Guid tenantId,
        Guid encounterId,
        bool includeDetails,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EncounterChargeSheet>> ListAsync(
        ChargeSheetQueryFilter filter,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        EncounterChargeSheet sheet,
        CancellationToken cancellationToken = default);

    Task ReplaceItemsAsync(
        EncounterChargeSheet sheet,
        IReadOnlyList<ChargeLineItem> newItems,
        ChargeSheetAuditEvent auditEvent,
        CancellationToken cancellationToken = default);

    Task UpdateWithAuditAsync(
        EncounterChargeSheet sheet,
        ChargeSheetAuditEvent auditEvent,
        CancellationToken cancellationToken = default);
}
