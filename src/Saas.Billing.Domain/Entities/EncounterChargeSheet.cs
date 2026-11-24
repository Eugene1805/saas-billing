using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Domain.Entities;

public sealed record ChargeLineItemDraft(
    string ConceptCode,
    string Description,
    ChargeConceptCategory Category,
    int Quantity,
    decimal UnitPriceMxn);

public sealed class EncounterChargeSheet
{
    private readonly List<ChargeLineItem> _items = [];
    private readonly List<ChargeSheetAuditEvent> _auditLog = [];

    private EncounterChargeSheet()
    {
    }

    private EncounterChargeSheet(
        Guid id,
        Guid tenantId,
        Guid encounterId,
        Guid recordId,
        Guid patientId,
        Guid specialistId,
        ChargeSheetStatus status,
        decimal totalMxn,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        Id = id;
        TenantId = tenantId;
        EncounterId = encounterId;
        RecordId = recordId;
        PatientId = patientId;
        SpecialistId = specialistId;
        Status = status;
        TotalMxn = totalMxn;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        UpdatedAtUtc = updatedAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EncounterId { get; private set; }
    public Guid RecordId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid SpecialistId { get; private set; }
    public ChargeSheetStatus Status { get; private set; }
    public decimal TotalMxn { get; private set; }
    public string? ReviewedByName { get; private set; }
    public DateTimeOffset? ReviewedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public IReadOnlyList<ChargeLineItem> Items => _items;
    public IReadOnlyList<ChargeSheetAuditEvent> AuditLog => _auditLog;

    public static EncounterChargeSheet Create(
        Guid id,
        Guid tenantId,
        Guid encounterId,
        Guid recordId,
        Guid patientId,
        Guid specialistId,
        IReadOnlyList<ChargeLineItemDraft> drafts,
        string actorName,
        DateTimeOffset nowUtc)
    {
        var normalizedNow = nowUtc.ToUniversalTime();
        var total = ComputeTotal(drafts);

        var sheet = new EncounterChargeSheet(
            id,
            tenantId,
            encounterId,
            recordId,
            patientId,
            specialistId,
            ChargeSheetStatus.ABIERTA,
            total,
            normalizedNow,
            normalizedNow);

        sheet.SetLineItems(drafts);
        sheet._auditLog.Add(new ChargeSheetAuditEvent(
            Guid.NewGuid(),
            id,
            tenantId,
            actorName.Trim(),
            normalizedNow,
            "ABIERTA",
            $"Hoja de cargos inicializada ({drafts.Count} concepto(s), {MoneyHelper.FormatMxn(total)})."));

        return sheet;
    }

    public (IReadOnlyList<ChargeLineItem> AddedItems, ChargeSheetAuditEvent AuditEvent) ReplaceItems(
        IReadOnlyList<ChargeLineItemDraft> drafts,
        string actorName,
        DateTimeOffset nowUtc)
    {
        var normalizedNow = nowUtc.ToUniversalTime();
        var total = ComputeTotal(drafts);

        SetLineItems(drafts);
        TotalMxn = total;
        Status = ChargeSheetStatus.ABIERTA;
        ReviewedByName = null;
        ReviewedAtUtc = null;
        UpdatedAtUtc = normalizedNow;

        var audit = new ChargeSheetAuditEvent(
            Guid.NewGuid(),
            Id,
            TenantId,
            actorName.Trim(),
            normalizedNow,
            "ACTUALIZADA",
            $"Cargos actualizados ({drafts.Count} concepto(s), {MoneyHelper.FormatMxn(total)}).");

        _auditLog.Add(audit);
        return (_items.ToList(), audit);
    }

    public ChargeSheetAuditEvent MarkReviewed(string actorName, DateTimeOffset nowUtc)
    {
        var normalizedNow = nowUtc.ToUniversalTime();
        var cleanActor = actorName.Trim();

        Status = ChargeSheetStatus.REVISADA;
        ReviewedByName = cleanActor;
        ReviewedAtUtc = normalizedNow;
        UpdatedAtUtc = normalizedNow;

        var audit = new ChargeSheetAuditEvent(
            Guid.NewGuid(),
            Id,
            TenantId,
            cleanActor,
            normalizedNow,
            "REVISADA",
            $"Cargos confirmados completos ({MoneyHelper.FormatMxn(TotalMxn)}).");

        _auditLog.Add(audit);
        return audit;
    }

    public ChargeSheetAuditEvent Reopen(string actorName, DateTimeOffset nowUtc)
    {
        var normalizedNow = nowUtc.ToUniversalTime();
        var cleanActor = actorName.Trim();

        Status = ChargeSheetStatus.ABIERTA;
        ReviewedByName = null;
        ReviewedAtUtc = null;
        UpdatedAtUtc = normalizedNow;

        var audit = new ChargeSheetAuditEvent(
            Guid.NewGuid(),
            Id,
            TenantId,
            cleanActor,
            normalizedNow,
            "REABIERTA",
            "Hoja devuelta a ABIERTA para ajuste previo al cobro.");

        _auditLog.Add(audit);
        return audit;
    }

    public ChargeSheetAuditEvent MarkPaid(
        string paymentFolio,
        PaymentMethod method,
        string actorName,
        DateTimeOffset nowUtc)
    {
        var normalizedNow = nowUtc.ToUniversalTime();
        var cleanActor = actorName.Trim();

        Status = ChargeSheetStatus.COBRADA;
        UpdatedAtUtc = normalizedNow;

        var audit = new ChargeSheetAuditEvent(
            Guid.NewGuid(),
            Id,
            TenantId,
            cleanActor,
            normalizedNow,
            "COBRADA",
            $"Cobro completo registrado con {method} ({paymentFolio}).");

        _auditLog.Add(audit);
        return audit;
    }

    private void SetLineItems(IReadOnlyList<ChargeLineItemDraft> drafts)
    {
        _items.Clear();
        for (var i = 0; i < drafts.Count; i++)
        {
            var draft = drafts[i];
            _items.Add(new ChargeLineItem(
                Guid.NewGuid(),
                Id,
                i + 1,
                draft.ConceptCode.Trim().ToUpperInvariant(),
                draft.Description.Trim(),
                draft.Category.ToString(),
                draft.Quantity,
                MoneyHelper.RoundMxn(draft.UnitPriceMxn)));
        }
    }

    private static decimal ComputeTotal(IReadOnlyList<ChargeLineItemDraft> drafts) =>
        MoneyHelper.RoundMxn(drafts.Sum(d => d.Quantity * MoneyHelper.RoundMxn(d.UnitPriceMxn)));
}
