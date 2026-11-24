using Saas.Billing.Domain.Entities;
using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Application.Ports;

public sealed record PaymentQueryFilter(
    Guid TenantId,
    Guid? SpecialistId,
    Guid? PatientId,
    PaymentMethod? Method,
    bool? UnassignedOnly,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToExclusiveUtc,
    IReadOnlySet<Guid>? RestrictToPatientIds);

public enum PaymentWriteOutcome
{
    Succeeded = 0,
    ChargeSheetAlreadyPaid = 1,
}

public interface IPaymentRepository
{
    Task<PatientPayment?> GetByIdAsync(
        Guid tenantId,
        Guid paymentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PatientPayment>> ListAsync(
        PaymentQueryFilter filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PatientPayment>> ListByIdsTrackedAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> paymentIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PatientPayment>> ListByCutIdAsync(
        Guid tenantId,
        Guid cutId,
        bool tracked,
        CancellationToken cancellationToken = default);

    Task<int> CountByTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<PaymentWriteOutcome> RecordFullPaymentAsync(
        PatientPayment payment,
        EncounterChargeSheet sheet,
        ChargeSheetAuditEvent paidAuditEvent,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PatientPayment>> ListForMetricsAsync(
        Guid tenantId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toExclusiveUtc,
        CancellationToken cancellationToken = default);
}
