using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Domain.Entities;

public sealed class SpecialistCut
{
    private readonly List<SpecialistCutEvent> _history = [];

    private SpecialistCut()
    {
    }

    private SpecialistCut(
        Guid id,
        string folio,
        Guid tenantId,
        Guid specialistId,
        string specialistName,
        DateOnly periodStartGmt6,
        DateOnly periodEndGmt6,
        SpecialistCutStatus status,
        decimal efectivoMxn,
        decimal tarjetaMxn,
        decimal transferenciaMxn,
        decimal totalMxn,
        int paymentCount,
        Guid preparedByAccountId,
        string preparedByName,
        DateTimeOffset preparedAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        Id = id;
        Folio = folio;
        TenantId = tenantId;
        SpecialistId = specialistId;
        SpecialistName = specialistName;
        PeriodStartGmt6 = periodStartGmt6;
        PeriodEndGmt6 = periodEndGmt6;
        Status = status;
        EfectivoMxn = efectivoMxn;
        TarjetaMxn = tarjetaMxn;
        TransferenciaMxn = transferenciaMxn;
        TotalMxn = totalMxn;
        PaymentCount = paymentCount;
        PreparedByAccountId = preparedByAccountId;
        PreparedByName = preparedByName;
        PreparedAtUtc = preparedAtUtc.ToUniversalTime();
        UpdatedAtUtc = updatedAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public string Folio { get; private set; } = string.Empty;
    public Guid TenantId { get; private set; }
    public Guid SpecialistId { get; private set; }
    public string SpecialistName { get; private set; } = string.Empty;
    public DateOnly PeriodStartGmt6 { get; private set; }
    public DateOnly PeriodEndGmt6 { get; private set; }
    public SpecialistCutStatus Status { get; private set; }
    public decimal EfectivoMxn { get; private set; }
    public decimal TarjetaMxn { get; private set; }
    public decimal TransferenciaMxn { get; private set; }
    public decimal TotalMxn { get; private set; }
    public int PaymentCount { get; private set; }
    public Guid PreparedByAccountId { get; private set; }
    public string PreparedByName { get; private set; } = string.Empty;
    public DateTimeOffset PreparedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public string? ConfirmedByName { get; private set; }
    public DateTimeOffset? ConfirmedAtUtc { get; private set; }
    public string? LastObservationNote { get; private set; }

    public IReadOnlyList<SpecialistCutEvent> History => _history;

    public static SpecialistCut Create(
        Guid id,
        string folio,
        Guid tenantId,
        Guid specialistId,
        string specialistName,
        DateOnly periodStartGmt6,
        DateOnly periodEndGmt6,
        IReadOnlyList<PatientPayment> payments,
        Guid preparedByAccountId,
        string preparedByName,
        DateTimeOffset nowUtc,
        string? initialNote = null)
    {
        var normalizedNow = nowUtc.ToUniversalTime();
        var (efectivo, tarjeta, transferencia, total) = Summarize(payments);

        var cut = new SpecialistCut(
            id,
            folio.Trim(),
            tenantId,
            specialistId,
            specialistName.Trim(),
            periodStartGmt6,
            periodEndGmt6,
            SpecialistCutStatus.PREPARADO,
            efectivo,
            tarjeta,
            transferencia,
            total,
            payments.Count,
            preparedByAccountId,
            preparedByName.Trim(),
            normalizedNow,
            normalizedNow);

        foreach (var payment in payments)
        {
            payment.AssignToCut(id);
        }

        cut._history.Add(new SpecialistCutEvent(
            Guid.NewGuid(),
            id,
            tenantId,
            SpecialistCutStatus.PREPARADO,
            preparedByName.Trim(),
            normalizedNow,
            string.IsNullOrWhiteSpace(initialNote)
                ? $"Corte preparado con {payments.Count} cobro(s) ({MoneyHelper.FormatMxn(total)})."
                : initialNote.Trim()));

        return cut;
    }

    public SpecialistCutEvent ReviseAndResubmit(
        DateOnly periodStartGmt6,
        DateOnly periodEndGmt6,
        IReadOnlyList<PatientPayment> previousPayments,
        IReadOnlyList<PatientPayment> nextPayments,
        string adjustmentNote,
        string actorName,
        DateTimeOffset nowUtc)
    {
        var normalizedNow = nowUtc.ToUniversalTime();
        var nextIds = nextPayments.Select(p => p.Id).ToHashSet();

        foreach (var oldPayment in previousPayments)
        {
            if (!nextIds.Contains(oldPayment.Id))
            {
                oldPayment.ReleaseFromCut();
            }
        }

        foreach (var payment in nextPayments)
        {
            payment.AssignToCut(Id);
        }

        var (efectivo, tarjeta, transferencia, total) = Summarize(nextPayments);

        PeriodStartGmt6 = periodStartGmt6;
        PeriodEndGmt6 = periodEndGmt6;
        EfectivoMxn = efectivo;
        TarjetaMxn = tarjeta;
        TransferenciaMxn = transferencia;
        TotalMxn = total;
        PaymentCount = nextPayments.Count;
        Status = SpecialistCutStatus.PREPARADO;
        UpdatedAtUtc = normalizedNow;

        var evt = new SpecialistCutEvent(
            Guid.NewGuid(),
            Id,
            TenantId,
            SpecialistCutStatus.PREPARADO,
            actorName.Trim(),
            normalizedNow,
            $"Corrección previa a confirmación: {adjustmentNote.Trim()} ({MoneyHelper.FormatMxn(total)}).");

        _history.Add(evt);
        return evt;
    }

    public SpecialistCutEvent MarkObserved(string actorName, string comment, DateTimeOffset nowUtc)
    {
        var normalizedNow = nowUtc.ToUniversalTime();
        var cleanComment = comment.Trim();

        Status = SpecialistCutStatus.OBSERVADO;
        LastObservationNote = cleanComment;
        UpdatedAtUtc = normalizedNow;

        var evt = new SpecialistCutEvent(
            Guid.NewGuid(),
            Id,
            TenantId,
            SpecialistCutStatus.OBSERVADO,
            actorName.Trim(),
            normalizedNow,
            cleanComment);

        _history.Add(evt);
        return evt;
    }

    public SpecialistCutEvent MarkConfirmed(string actorName, string? note, DateTimeOffset nowUtc)
    {
        var normalizedNow = nowUtc.ToUniversalTime();
        var cleanActor = actorName.Trim();

        Status = SpecialistCutStatus.CONFIRMADO;
        ConfirmedByName = cleanActor;
        ConfirmedAtUtc = normalizedNow;
        UpdatedAtUtc = normalizedNow;

        var evt = new SpecialistCutEvent(
            Guid.NewGuid(),
            Id,
            TenantId,
            SpecialistCutStatus.CONFIRMADO,
            cleanActor,
            normalizedNow,
            string.IsNullOrWhiteSpace(note)
                ? "Corte revisado y confirmado administrativamente sin discrepancias."
                : note.Trim());

        _history.Add(evt);
        return evt;
    }

    private static (decimal Efectivo, decimal Tarjeta, decimal Transferencia, decimal Total) Summarize(
        IReadOnlyList<PatientPayment> payments)
    {
        decimal efectivo = 0;
        decimal tarjeta = 0;
        decimal transferencia = 0;

        foreach (var payment in payments)
        {
            switch (payment.Method)
            {
                case PaymentMethod.EFECTIVO:
                    efectivo += payment.AmountMxn;
                    break;
                case PaymentMethod.TARJETA:
                    tarjeta += payment.AmountMxn;
                    break;
                case PaymentMethod.TRANSFERENCIA:
                    transferencia += payment.AmountMxn;
                    break;
            }
        }

        efectivo = MoneyHelper.RoundMxn(efectivo);
        tarjeta = MoneyHelper.RoundMxn(tarjeta);
        transferencia = MoneyHelper.RoundMxn(transferencia);
        var total = MoneyHelper.RoundMxn(efectivo + tarjeta + transferencia);
        return (efectivo, tarjeta, transferencia, total);
    }
}
