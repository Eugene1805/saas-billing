using Microsoft.EntityFrameworkCore;
using Npgsql;
using Saas.Billing.Application.Ports;
using Saas.Billing.Domain.Entities;
using Saas.Billing.Domain.ValueObjects;
using Saas.Billing.Infrastructure.Persistence.Tenancy;

namespace Saas.Billing.Infrastructure.Persistence.Repositories;

internal sealed class EfPaymentRepository(TenantBillingDbContextAccessor contextAccessor)
    : IPaymentRepository
{
    public Task<PatientPayment?> GetByIdAsync(
        Guid tenantId,
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        return TenantPersistenceHelper.ExecuteAsync(
            contextAccessor,
            tenantId,
            dbContext => dbContext.PatientPayments
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    p => p.TenantId == tenantId && p.Id == paymentId,
                    cancellationToken),
            cancellationToken);
    }

    public Task<IReadOnlyList<PatientPayment>> ListAsync(
        PaymentQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        return TenantPersistenceHelper.ExecuteAsync<IReadOnlyList<PatientPayment>>(
            contextAccessor,
            filter.TenantId,
            async dbContext =>
            {
                if (filter.RestrictToPatientIds is not null && filter.RestrictToPatientIds.Count == 0)
                {
                    return [];
                }

                var query = dbContext.PatientPayments
                    .AsNoTracking()
                    .Where(p => p.TenantId == filter.TenantId);

                if (filter.SpecialistId is Guid specialistId)
                {
                    query = query.Where(p => p.SpecialistId == specialistId);
                }

                if (filter.PatientId is Guid patientId)
                {
                    query = query.Where(p => p.PatientId == patientId);
                }

                if (filter.Method is PaymentMethod method)
                {
                    query = query.Where(p => p.Method == method);
                }

                if (filter.UnassignedOnly == true)
                {
                    query = query.Where(p => p.CutId == null);
                }

                if (filter.FromUtc is DateTimeOffset fromUtc)
                {
                    var normalized = fromUtc.ToUniversalTime();
                    query = query.Where(p => p.PaidAtUtc >= normalized);
                }

                if (filter.ToExclusiveUtc is DateTimeOffset toExclusiveUtc)
                {
                    var normalized = toExclusiveUtc.ToUniversalTime();
                    query = query.Where(p => p.PaidAtUtc < normalized);
                }

                if (filter.RestrictToPatientIds is not null)
                {
                    var allowedIds = filter.RestrictToPatientIds.ToList();
                    query = query.Where(p => allowedIds.Contains(p.PatientId));
                }

                return await query
                    .OrderByDescending(p => p.PaidAtUtc)
                    .ThenBy(p => p.Id)
                    .ToListAsync(cancellationToken);
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<PatientPayment>> ListByIdsTrackedAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> paymentIds,
        CancellationToken cancellationToken = default)
    {
        return TenantPersistenceHelper.ExecuteAsync<IReadOnlyList<PatientPayment>>(
            contextAccessor,
            tenantId,
            async dbContext =>
            {
                if (paymentIds.Count == 0)
                {
                    return [];
                }

                var idList = paymentIds.ToList();
                return await dbContext.PatientPayments
                    .Where(p => p.TenantId == tenantId && idList.Contains(p.Id))
                    .ToListAsync(cancellationToken);
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<PatientPayment>> ListByCutIdAsync(
        Guid tenantId,
        Guid cutId,
        bool tracked,
        CancellationToken cancellationToken = default)
    {
        return TenantPersistenceHelper.ExecuteAsync<IReadOnlyList<PatientPayment>>(
            contextAccessor,
            tenantId,
            async dbContext =>
            {
                IQueryable<PatientPayment> query = dbContext.PatientPayments;
                if (!tracked)
                {
                    query = query.AsNoTracking();
                }

                return await query
                    .Where(p => p.TenantId == tenantId && p.CutId == cutId)
                    .OrderBy(p => p.PaidAtUtc)
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
            dbContext => dbContext.PatientPayments
                .AsNoTracking()
                .CountAsync(p => p.TenantId == tenantId, cancellationToken),
            cancellationToken);
    }

    public Task<PaymentWriteOutcome> RecordFullPaymentAsync(
        PatientPayment payment,
        EncounterChargeSheet sheet,
        ChargeSheetAuditEvent paidAuditEvent,
        CancellationToken cancellationToken = default)
    {
        return TenantPersistenceHelper.ExecuteAsync(
            contextAccessor,
            payment.TenantId,
            async dbContext =>
            {
                var trackedSheet = await dbContext.ChargeSheets
                    .FirstAsync(c => c.Id == sheet.Id, cancellationToken);

                dbContext.Entry(trackedSheet).CurrentValues.SetValues(sheet);
                dbContext.ChargeSheetAuditEvents.Add(paidAuditEvent);
                dbContext.PatientPayments.Add(payment);

                try
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                    dbContext.ChangeTracker.Clear();
                    return PaymentWriteOutcome.Succeeded;
                }
                catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
                                                   pg.SqlState == PostgresErrorCodes.UniqueViolation)
                {
                    dbContext.ChangeTracker.Clear();
                    return PaymentWriteOutcome.ChargeSheetAlreadyPaid;
                }
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<PatientPayment>> ListForMetricsAsync(
        Guid tenantId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toExclusiveUtc,
        CancellationToken cancellationToken = default)
    {
        return TenantPersistenceHelper.ExecuteAsync<IReadOnlyList<PatientPayment>>(
            contextAccessor,
            tenantId,
            async dbContext =>
            {
                var query = dbContext.PatientPayments
                    .AsNoTracking()
                    .Where(p => p.TenantId == tenantId);

                if (fromUtc is DateTimeOffset f)
                {
                    var normalized = f.ToUniversalTime();
                    query = query.Where(p => p.PaidAtUtc >= normalized);
                }

                if (toExclusiveUtc is DateTimeOffset t)
                {
                    var normalized = t.ToUniversalTime();
                    query = query.Where(p => p.PaidAtUtc < normalized);
                }

                return await query.ToListAsync(cancellationToken);
            },
            cancellationToken);
    }
}
