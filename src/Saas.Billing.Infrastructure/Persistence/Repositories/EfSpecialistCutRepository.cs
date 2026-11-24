using Microsoft.EntityFrameworkCore;
using Saas.Billing.Application.Ports;
using Saas.Billing.Domain.Entities;
using Saas.Billing.Domain.ValueObjects;
using Saas.Billing.Infrastructure.Persistence.Tenancy;

namespace Saas.Billing.Infrastructure.Persistence.Repositories;

internal sealed class EfSpecialistCutRepository(TenantBillingDbContextAccessor contextAccessor)
    : ISpecialistCutRepository
{
    public Task<SpecialistCut?> GetByIdAsync(
        Guid tenantId,
        Guid cutId,
        bool includeHistory,
        bool tracked,
        CancellationToken cancellationToken = default)
    {
        return TenantPersistenceHelper.ExecuteAsync(
            contextAccessor,
            tenantId,
            async dbContext =>
            {
                IQueryable<SpecialistCut> query = dbContext.SpecialistCuts.AsNoTracking();
                if (includeHistory)
                {
                    query = query.Include(c => c.History);
                }

                return await query.FirstOrDefaultAsync(
                    c => c.TenantId == tenantId && c.Id == cutId,
                    cancellationToken);
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<SpecialistCut>> ListAsync(
        CutQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        return TenantPersistenceHelper.ExecuteAsync<IReadOnlyList<SpecialistCut>>(
            contextAccessor,
            filter.TenantId,
            async dbContext =>
            {
                var query = dbContext.SpecialistCuts
                    .AsNoTracking()
                    .Where(c => c.TenantId == filter.TenantId);

                if (filter.SpecialistId is Guid specialistId)
                {
                    query = query.Where(c => c.SpecialistId == specialistId);
                }

                if (filter.Status is SpecialistCutStatus status)
                {
                    query = query.Where(c => c.Status == status);
                }

                if (filter.FromDateGmt6 is DateOnly fromDate)
                {
                    query = query.Where(c => c.PeriodEndGmt6 >= fromDate);
                }

                if (filter.ToDateGmt6 is DateOnly toDate)
                {
                    query = query.Where(c => c.PeriodStartGmt6 <= toDate);
                }

                return await query
                    .OrderByDescending(c => c.PreparedAtUtc)
                    .ThenBy(c => c.Id)
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
            dbContext => dbContext.SpecialistCuts
                .AsNoTracking()
                .CountAsync(c => c.TenantId == tenantId, cancellationToken),
            cancellationToken);
    }

    public Task AddAsync(
        SpecialistCut cut,
        CancellationToken cancellationToken = default)
    {
        return TenantPersistenceHelper.ExecuteAsync(
            contextAccessor,
            cut.TenantId,
            async dbContext =>
            {
                dbContext.SpecialistCuts.Add(cut);
                await dbContext.SaveChangesAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
            },
            cancellationToken);
    }

    public Task UpdateWithEventAsync(
        SpecialistCut cut,
        SpecialistCutEvent historyEvent,
        CancellationToken cancellationToken = default)
    {
        return TenantPersistenceHelper.ExecuteAsync(
            contextAccessor,
            cut.TenantId,
            async dbContext =>
            {
                var tracked = await dbContext.SpecialistCuts
                    .FirstAsync(c => c.Id == cut.Id, cancellationToken);

                dbContext.Entry(tracked).CurrentValues.SetValues(cut);
                dbContext.SpecialistCutEvents.Add(historyEvent);

                await dbContext.SaveChangesAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
            },
            cancellationToken);
    }
}
