using Saas.Billing.Application.Ports;
using Saas.Billing.Domain.Entities;
using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.UnitTests;

internal sealed class FakeClockPort(DateTimeOffset initialUtc) : IClockPort
{
    public DateTimeOffset UtcNow { get; set; } = initialUtc.ToUniversalTime();
}

internal sealed class FakeTenantBillingPort : ITenantBillingAccessPort
{
    public bool Unavailable { get; set; }
    public Dictionary<Guid, TenantBillingState> Tenants { get; } = [];

    public Task<TenantBillingLookupResult> GetByTenantIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (Unavailable)
        {
            return Task.FromResult(TenantBillingLookupResult.Unavailable());
        }

        return Task.FromResult(
            Tenants.TryGetValue(tenantId, out var state)
                ? TenantBillingLookupResult.WithState(state)
                : TenantBillingLookupResult.NotFound());
    }
}

internal sealed class FakeBillingAccessPort : IBillingAccessControlPort
{
    public bool Unavailable { get; set; }
    public Dictionary<(Guid ActorId, Guid TenantId, Guid PatientId), AccessControlCheckResult> Decisions { get; } = [];
    public Dictionary<(Guid ActorId, Guid TenantId), HashSet<Guid>> AllowedPatientSets { get; } = [];

    public Task<AccessControlCheckResult> EvaluatePatientAccessAsync(
        Guid actorAccountId,
        Guid tenantId,
        OrgRole actorRole,
        Guid patientId,
        BillingAccessOperation operation,
        CancellationToken cancellationToken = default)
    {
        if (Unavailable)
        {
            return Task.FromResult(AccessControlCheckResult.Unavailable());
        }

        if (actorRole == OrgRole.ADMIN_CLINICA)
        {
            return Task.FromResult(AccessControlCheckResult.Allow());
        }

        if (Decisions.TryGetValue((actorAccountId, tenantId, patientId), out var decision))
        {
            return Task.FromResult(decision);
        }

        return Task.FromResult(AccessControlCheckResult.Denied(
            "OUT_OF_BILLING_SCOPE",
            "Sin permiso vigente sobre el paciente."));
    }

    public Task<AuthorizedPatientsScopeResult> GetAuthorizedPatientIdsAsync(
        Guid actorAccountId,
        Guid tenantId,
        OrgRole actorRole,
        CancellationToken cancellationToken = default)
    {
        if (Unavailable)
        {
            return Task.FromResult(AuthorizedPatientsScopeResult.Unavailable());
        }

        if (actorRole == OrgRole.ADMIN_CLINICA)
        {
            return Task.FromResult(AuthorizedPatientsScopeResult.All());
        }

        if (AllowedPatientSets.TryGetValue((actorAccountId, tenantId), out var allowed))
        {
            return Task.FromResult(AuthorizedPatientsScopeResult.Restricted(allowed));
        }

        return Task.FromResult(AuthorizedPatientsScopeResult.Restricted(new HashSet<Guid>()));
    }
}

internal sealed class FakeClinicalGatewayPort : IClinicalEncounterGatewayPort
{
    public bool Unavailable { get; set; }
    public Dictionary<(Guid TenantId, Guid EncounterId), EncounterSummary> Encounters { get; } = [];

    public Task<EncounterLookupResult> GetEncounterAsync(
        Guid tenantId,
        Guid encounterId,
        CancellationToken cancellationToken = default)
    {
        if (Unavailable)
        {
            return Task.FromResult(EncounterLookupResult.Unavailable());
        }

        return Task.FromResult(
            Encounters.TryGetValue((tenantId, encounterId), out var encounter)
                ? EncounterLookupResult.WithEncounter(encounter)
                : EncounterLookupResult.NotFound());
    }
}

internal sealed class FakeBillingDirectoryPort : IBillingDirectoryPort
{
    public bool Unavailable { get; set; }
    public Dictionary<(Guid TenantId, Guid SpecialistId), SpecialistSummary> Specialists { get; } = [];

    public Task<SpecialistLookupResult> GetSpecialistAsync(
        Guid tenantId,
        Guid specialistId,
        CancellationToken cancellationToken = default)
    {
        if (Unavailable)
        {
            return Task.FromResult(SpecialistLookupResult.Unavailable());
        }

        return Task.FromResult(
            Specialists.TryGetValue((tenantId, specialistId), out var spec)
                ? SpecialistLookupResult.WithSpecialist(spec)
                : SpecialistLookupResult.NotFound());
    }
}

internal sealed class InMemoryChargeSheetRepository : IChargeSheetRepository
{
    public List<EncounterChargeSheet> Items { get; } = [];

    public Task<EncounterChargeSheet?> GetByIdAsync(
        Guid tenantId,
        Guid chargeSheetId,
        bool includeDetails,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(s => s.TenantId == tenantId && s.Id == chargeSheetId));

    public Task<EncounterChargeSheet?> GetByEncounterIdAsync(
        Guid tenantId,
        Guid encounterId,
        bool includeDetails,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(s => s.TenantId == tenantId && s.EncounterId == encounterId));

    public Task<IReadOnlyList<EncounterChargeSheet>> ListAsync(
        ChargeSheetQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        if (filter.RestrictToPatientIds is not null && filter.RestrictToPatientIds.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<EncounterChargeSheet>>([]);
        }

        var query = Items.Where(s => s.TenantId == filter.TenantId);
        if (filter.EncounterId is Guid eid) query = query.Where(s => s.EncounterId == eid);
        if (filter.PatientId is Guid pid) query = query.Where(s => s.PatientId == pid);
        if (filter.SpecialistId is Guid sid) query = query.Where(s => s.SpecialistId == sid);
        if (filter.Status is ChargeSheetStatus st) query = query.Where(s => s.Status == st);
        if (filter.RestrictToPatientIds is not null)
        {
            query = query.Where(s => filter.RestrictToPatientIds.Contains(s.PatientId));
        }

        return Task.FromResult<IReadOnlyList<EncounterChargeSheet>>(query.ToList());
    }

    public Task AddAsync(EncounterChargeSheet sheet, CancellationToken cancellationToken = default)
    {
        Items.Add(sheet);
        return Task.CompletedTask;
    }

    public Task ReplaceItemsAsync(
        EncounterChargeSheet sheet,
        IReadOnlyList<ChargeLineItem> newItems,
        ChargeSheetAuditEvent auditEvent,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task UpdateWithAuditAsync(
        EncounterChargeSheet sheet,
        ChargeSheetAuditEvent auditEvent,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

internal sealed class InMemoryPaymentRepository : IPaymentRepository
{
    public List<PatientPayment> Items { get; } = [];
    public bool SimulateConcurrentDuplicate { get; set; }

    public Task<PatientPayment?> GetByIdAsync(
        Guid tenantId,
        Guid paymentId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(p => p.TenantId == tenantId && p.Id == paymentId));

    public Task<IReadOnlyList<PatientPayment>> ListAsync(
        PaymentQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        if (filter.RestrictToPatientIds is not null && filter.RestrictToPatientIds.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<PatientPayment>>([]);
        }

        var query = Items.Where(p => p.TenantId == filter.TenantId);
        if (filter.SpecialistId is Guid sid) query = query.Where(p => p.SpecialistId == sid);
        if (filter.PatientId is Guid pid) query = query.Where(p => p.PatientId == pid);
        if (filter.Method is PaymentMethod m) query = query.Where(p => p.Method == m);
        if (filter.UnassignedOnly == true) query = query.Where(p => p.CutId == null);
        if (filter.FromUtc is DateTimeOffset fromUtc) query = query.Where(p => p.PaidAtUtc >= fromUtc);
        if (filter.ToExclusiveUtc is DateTimeOffset toUtc) query = query.Where(p => p.PaidAtUtc < toUtc);
        if (filter.RestrictToPatientIds is not null)
        {
            query = query.Where(p => filter.RestrictToPatientIds.Contains(p.PatientId));
        }

        return Task.FromResult<IReadOnlyList<PatientPayment>>(query.ToList());
    }

    public Task<IReadOnlyList<PatientPayment>> ListByIdsTrackedAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> paymentIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PatientPayment>>(
            Items.Where(p => p.TenantId == tenantId && paymentIds.Contains(p.Id)).ToList());

    public Task<IReadOnlyList<PatientPayment>> ListByCutIdAsync(
        Guid tenantId,
        Guid cutId,
        bool tracked,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PatientPayment>>(
            Items.Where(p => p.TenantId == tenantId && p.CutId == cutId).ToList());

    public Task<int> CountByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Count(p => p.TenantId == tenantId));

    public Task<PaymentWriteOutcome> RecordFullPaymentAsync(
        PatientPayment payment,
        EncounterChargeSheet sheet,
        ChargeSheetAuditEvent paidAuditEvent,
        CancellationToken cancellationToken = default)
    {
        if (SimulateConcurrentDuplicate || Items.Any(p => p.ChargeSheetId == sheet.Id))
        {
            return Task.FromResult(PaymentWriteOutcome.ChargeSheetAlreadyPaid);
        }

        Items.Add(payment);
        return Task.FromResult(PaymentWriteOutcome.Succeeded);
    }

    public Task<IReadOnlyList<PatientPayment>> ListForMetricsAsync(
        Guid tenantId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toExclusiveUtc,
        CancellationToken cancellationToken = default)
    {
        var query = Items.Where(p => p.TenantId == tenantId);
        if (fromUtc is DateTimeOffset f) query = query.Where(p => p.PaidAtUtc >= f);
        if (toExclusiveUtc is DateTimeOffset t) query = query.Where(p => p.PaidAtUtc < t);
        return Task.FromResult<IReadOnlyList<PatientPayment>>(query.ToList());
    }
}

internal sealed class InMemoryCutRepository : ISpecialistCutRepository
{
    public List<SpecialistCut> Items { get; } = [];

    public Task<SpecialistCut?> GetByIdAsync(
        Guid tenantId,
        Guid cutId,
        bool includeHistory,
        bool tracked,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(c => c.TenantId == tenantId && c.Id == cutId));

    public Task<IReadOnlyList<SpecialistCut>> ListAsync(
        CutQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = Items.Where(c => c.TenantId == filter.TenantId);
        if (filter.SpecialistId is Guid sid) query = query.Where(c => c.SpecialistId == sid);
        if (filter.Status is SpecialistCutStatus st) query = query.Where(c => c.Status == st);
        if (filter.FromDateGmt6 is DateOnly f) query = query.Where(c => c.PeriodEndGmt6 >= f);
        if (filter.ToDateGmt6 is DateOnly t) query = query.Where(c => c.PeriodStartGmt6 <= t);
        return Task.FromResult<IReadOnlyList<SpecialistCut>>(query.ToList());
    }

    public Task<int> CountByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Count(c => c.TenantId == tenantId));

    public Task AddAsync(SpecialistCut cut, CancellationToken cancellationToken = default)
    {
        Items.Add(cut);
        return Task.CompletedTask;
    }

    public Task UpdateWithEventAsync(
        SpecialistCut cut,
        SpecialistCutEvent historyEvent,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

internal sealed class InMemoryAdjustmentRepository : IBillingAdjustmentRepository
{
    public List<BillingAdjustment> Items { get; } = [];

    public Task<IReadOnlyList<BillingAdjustment>> ListAsync(
        Guid tenantId,
        AdjustmentTargetType? targetType,
        Guid? targetId,
        CancellationToken cancellationToken = default)
    {
        var query = Items.Where(a => a.TenantId == tenantId);
        if (targetType is AdjustmentTargetType t) query = query.Where(a => a.TargetType == t);
        if (targetId is Guid tid) query = query.Where(a => a.TargetId == tid);
        return Task.FromResult<IReadOnlyList<BillingAdjustment>>(query.ToList());
    }

    public Task<int> CountByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Count(a => a.TenantId == tenantId));

    public Task AddAsync(BillingAdjustment adjustment, CancellationToken cancellationToken = default)
    {
        Items.Add(adjustment);
        return Task.CompletedTask;
    }
}
