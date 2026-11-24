using Microsoft.EntityFrameworkCore;
using Saas.Billing.Application.Ports;
using Saas.Billing.Domain.Entities;
using Saas.Billing.Domain.ValueObjects;
using Saas.Billing.Infrastructure.Persistence.Tenancy;

namespace Saas.Billing.Infrastructure.Persistence.Repositories;

internal sealed class EfChargeSheetRepository(TenantBillingDbContextAccessor contextAccessor)
    : IChargeSheetRepository
{
    public Task<EncounterChargeSheet?> GetByIdAsync(
        Guid tenantId,
        Guid chargeSheetId,
        bool includeDetails,
        CancellationToken cancellationToken = default)
    {
        return TenantPersistenceHelper.ExecuteAsync(
            contextAccessor,
            tenantId,
            async dbContext =>
            {
                IQueryable<EncounterChargeSheet> query = dbContext.ChargeSheets.AsNoTracking();
                if (includeDetails)
                {
                    query = query
                        .Include(c => c.Items)
                        .Include(c => c.AuditLog);
                }

                return await query.FirstOrDefaultAsync(
                    c => c.TenantId == tenantId && c.Id == chargeSheetId,
                    cancellationToken);
            },
            cancellationToken);
    }

    public Task<EncounterChargeSheet?> GetByEncounterIdAsync(
        Guid tenantId,
        Guid encounterId,
        bool includeDetails,
        CancellationToken cancellationToken = default)
    {
        return TenantPersistenceHelper.ExecuteAsync(
            contextAccessor,
            tenantId,
            async dbContext =>
            {
                IQueryable<EncounterChargeSheet> query = dbContext.ChargeSheets.AsNoTracking();
                if (includeDetails)
                {
                    query = query
                        .Include(c => c.Items)
                        .Include(c => c.AuditLog);
                }

                return await query.FirstOrDefaultAsync(
                    c => c.TenantId == tenantId && c.EncounterId == encounterId,
                    cancellationToken);
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<EncounterChargeSheet>> ListAsync(
        ChargeSheetQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        return TenantPersistenceHelper.ExecuteAsync<IReadOnlyList<EncounterChargeSheet>>(
            contextAccessor,
            filter.TenantId,
            async dbContext =>
            {
                if (filter.RestrictToPatientIds is not null && filter.RestrictToPatientIds.Count == 0)
                {
                    return [];
                }

                var query = dbContext.ChargeSheets
                    .AsNoTracking()
                    .Include(c => c.Items)
                    .Where(c => c.TenantId == filter.TenantId);

                if (filter.EncounterId is Guid encounterId)
                {
                    query = query.Where(c => c.EncounterId == encounterId);
                }

                if (filter.PatientId is Guid patientId)
                {
                    query = query.Where(c => c.PatientId == patientId);
                }

                if (filter.SpecialistId is Guid specialistId)
                {
                    query = query.Where(c => c.SpecialistId == specialistId);
                }

                if (filter.Status is ChargeSheetStatus status)
                {
                    query = query.Where(c => c.Status == status);
                }

                if (filter.RestrictToPatientIds is not null)
                {
                    var allowedIds = filter.RestrictToPatientIds.ToList();
                    query = query.Where(c => allowedIds.Contains(c.PatientId));
                }

                return await query
                    .OrderByDescending(c => c.UpdatedAtUtc)
                    .ThenBy(c => c.Id)
                    .ToListAsync(cancellationToken);
            },
            cancellationToken);
    }

    public Task AddAsync(
        EncounterChargeSheet sheet,
        CancellationToken cancellationToken = default)
    {
        return TenantPersistenceHelper.ExecuteAsync(
            contextAccessor,
            sheet.TenantId,
            async dbContext =>
            {
                dbContext.ChargeSheets.Add(sheet);
                await dbContext.SaveChangesAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
            },
            cancellationToken);
    }

    public Task ReplaceItemsAsync(
        EncounterChargeSheet sheet,
        IReadOnlyList<ChargeLineItem> newItems,
        ChargeSheetAuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        return TenantPersistenceHelper.ExecuteAsync(
            contextAccessor,
            sheet.TenantId,
            async dbContext =>
            {
                var tracked = await dbContext.ChargeSheets
                    .Include(c => c.Items)
                    .FirstAsync(c => c.Id == sheet.Id, cancellationToken);

                if (tracked.Items.Count > 0)
                {
                    dbContext.ChargeLineItems.RemoveRange(tracked.Items);
                }

                dbContext.Entry(tracked).CurrentValues.SetValues(sheet);
                dbContext.ChargeLineItems.AddRange(newItems);
                dbContext.ChargeSheetAuditEvents.Add(auditEvent);

                await dbContext.SaveChangesAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
            },
            cancellationToken);
    }

    public Task UpdateWithAuditAsync(
        EncounterChargeSheet sheet,
        ChargeSheetAuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        return TenantPersistenceHelper.ExecuteAsync(
            contextAccessor,
            sheet.TenantId,
            async dbContext =>
            {
                var tracked = await dbContext.ChargeSheets
                    .FirstAsync(c => c.Id == sheet.Id, cancellationToken);

                dbContext.Entry(tracked).CurrentValues.SetValues(sheet);
                dbContext.ChargeSheetAuditEvents.Add(auditEvent);

                await dbContext.SaveChangesAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
            },
            cancellationToken);
    }
}
