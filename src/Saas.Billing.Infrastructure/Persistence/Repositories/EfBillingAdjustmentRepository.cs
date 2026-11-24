using Microsoft.EntityFrameworkCore;
using Saas.Billing.Application.Ports;
using Saas.Billing.Domain.Entities;
using Saas.Billing.Domain.ValueObjects;
using Saas.Billing.Infrastructure.Persistence.Tenancy;

namespace Saas.Billing.Infrastructure.Persistence.Repositories;

internal sealed class EfBillingAdjustmentRepository(TenantBillingDbContextAccessor contextAccessor)
    : IBillingAdjustmentRepository
{
    public Task<IReadOnlyList<BillingAdjustment>> ListAsync(
        Guid tenantId,
        AdjustmentTargetType? targetType,
        Guid? targetId,
        CancellationToken cancellationToken = default)
    {
        return TenantPersistenceHelper.ExecuteAsync<IReadOnlyList<BillingAdjustment>>(
            contextAccessor,
            tenantId,
            async dbContext =>
            {
                var query = dbContext.BillingAdjustments
                    .AsNoTracking()
                    .Where(a => a.TenantId == tenantId);

                if (targetType is AdjustmentTargetType type)
                {
                    query = query.Where(a => a.TargetType == type);
                }

                if (targetId is Guid tid)
                {
                    query = query.Where(a => a.TargetId == tid);
                }

                return await query
                    .OrderByDescending(a => a.CreatedAtUtc)
                    .ThenBy(a => a.Id)
                    .ToListAsync(cancellationToken);
            },
            cancellationToken);
    }

    public Task<int> CountByTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return TenantPersistenceHelper.ExecuteAsync(
            contextAccessor,
            tenantId,
            dbContext => dbContext.BillingAdjustments
                .AsNoTracking()
                .CountAsync(a => a.TenantId == tenantId, cancellationToken),
            cancellationToken);
    }

    public Task AddAsync(
        BillingAdjustment adjustment,
        CancellationToken cancellationToken = default)
    {
        return TenantPersistenceHelper.ExecuteAsync(
            contextAccessor,
            adjustment.TenantId,
            async dbContext =>
            {
                dbContext.BillingAdjustments.Add(adjustment);
                await dbContext.SaveChangesAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
            },
            cancellationToken);
    }
}
