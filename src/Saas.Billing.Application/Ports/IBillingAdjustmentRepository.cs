using Saas.Billing.Domain.Entities;
using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Application.Ports;

public interface IBillingAdjustmentRepository
{
    Task<IReadOnlyList<BillingAdjustment>> ListAsync(
        Guid tenantId,
        AdjustmentTargetType? targetType,
        Guid? targetId,
        CancellationToken cancellationToken = default);

    Task<int> CountByTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        BillingAdjustment adjustment,
        CancellationToken cancellationToken = default);
}
