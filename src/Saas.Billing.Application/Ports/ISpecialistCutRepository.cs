using Saas.Billing.Domain.Entities;
using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Application.Ports;

public sealed record CutQueryFilter(
    Guid TenantId,
    Guid? SpecialistId,
    SpecialistCutStatus? Status,
    DateOnly? FromDateGmt6,
    DateOnly? ToDateGmt6);

public interface ISpecialistCutRepository
{
    Task<SpecialistCut?> GetByIdAsync(
        Guid tenantId,
        Guid cutId,
        bool includeHistory,
        bool tracked,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SpecialistCut>> ListAsync(
        CutQueryFilter filter,
        CancellationToken cancellationToken = default);

    Task<int> CountByTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        SpecialistCut cut,
        CancellationToken cancellationToken = default);

    Task UpdateWithEventAsync(
        SpecialistCut cut,
        SpecialistCutEvent historyEvent,
        CancellationToken cancellationToken = default);
}
