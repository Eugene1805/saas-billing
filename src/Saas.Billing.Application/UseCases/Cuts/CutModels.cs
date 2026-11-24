using Saas.Billing.Application.UseCases.Adjustments;
using Saas.Billing.Application.UseCases.Payments;
using Saas.Billing.Domain.Common;
using Saas.Billing.Domain.Entities;

namespace Saas.Billing.Application.UseCases.Cuts;

public sealed record PrepareCutCommand(
    Guid SpecialistId,
    string PeriodStartGmt6,
    string PeriodEndGmt6,
    IReadOnlyList<Guid> PaymentIds,
    string? Note = null);

public sealed record ReviseCutCommand(
    string PeriodStartGmt6,
    string PeriodEndGmt6,
    IReadOnlyList<Guid> PaymentIds,
    string AdjustmentNote);

public sealed record ResolveCutCommand(
    string? Note);

public sealed record TotalsByMethodDto(
    decimal Efectivo,
    decimal Tarjeta,
    decimal Transferencia);

public sealed record CutEventDto(
    Guid Id,
    string Status,
    string ActorName,
    string OccurredAtGmt6,
    string Note);

public sealed record SpecialistCutDto(
    Guid Id,
    string Folio,
    Guid TenantId,
    Guid SpecialistId,
    string SpecialistName,
    string PeriodStartGmt6,
    string PeriodEndGmt6,
    string Status,
    TotalsByMethodDto TotalsByMethod,
    decimal TotalMxn,
    int PaymentCount,
    Guid PreparedByAccountId,
    string PreparedByName,
    string PreparedAtGmt6,
    string UpdatedAtGmt6,
    string? ConfirmedByName,
    string? ConfirmedAtGmt6,
    string? LastObservationNote);

public sealed record SpecialistCutDetailDto(
    Guid Id,
    string Folio,
    Guid TenantId,
    Guid SpecialistId,
    string SpecialistName,
    string PeriodStartGmt6,
    string PeriodEndGmt6,
    string Status,
    TotalsByMethodDto TotalsByMethod,
    decimal TotalMxn,
    int PaymentCount,
    Guid PreparedByAccountId,
    string PreparedByName,
    string PreparedAtGmt6,
    string UpdatedAtGmt6,
    string? ConfirmedByName,
    string? ConfirmedAtGmt6,
    string? LastObservationNote,
    IReadOnlyList<PatientPaymentDto> Payments,
    IReadOnlyList<CutEventDto> History,
    IReadOnlyList<BillingAdjustmentDto> Adjustments);

public sealed record SpecialistCutListResponse(
    int Total,
    IReadOnlyList<SpecialistCutDto> Items);

internal static class CutMapper
{
    public static SpecialistCutDto ToDto(SpecialistCut cut)
    {
        return new SpecialistCutDto(
            cut.Id,
            cut.Folio,
            cut.TenantId,
            cut.SpecialistId,
            cut.SpecialistName,
            Gmt6Clock.FormatDateGmt6(cut.PeriodStartGmt6),
            Gmt6Clock.FormatDateGmt6(cut.PeriodEndGmt6),
            cut.Status.ToString(),
            new TotalsByMethodDto(cut.EfectivoMxn, cut.TarjetaMxn, cut.TransferenciaMxn),
            cut.TotalMxn,
            cut.PaymentCount,
            cut.PreparedByAccountId,
            cut.PreparedByName,
            Gmt6Clock.FormatIsoGmt6(cut.PreparedAtUtc),
            Gmt6Clock.FormatIsoGmt6(cut.UpdatedAtUtc),
            cut.ConfirmedByName,
            cut.ConfirmedAtUtc is DateTimeOffset c ? Gmt6Clock.FormatIsoGmt6(c) : null,
            cut.LastObservationNote);
    }

    public static SpecialistCutDetailDto ToDetailDto(
        SpecialistCut cut,
        IReadOnlyList<PatientPayment> payments,
        IReadOnlyList<BillingAdjustment> adjustments)
    {
        var paymentDtos = payments
            .OrderBy(p => p.PaidAtUtc)
            .ThenBy(p => p.Id)
            .Select(PaymentMapper.ToDto)
            .ToList();

        var historyDtos = cut.History
            .OrderBy(h => h.OccurredAtUtc)
            .ThenBy(h => h.Id)
            .Select(h => new CutEventDto(
                h.Id,
                h.Status.ToString(),
                h.ActorName,
                Gmt6Clock.FormatIsoGmt6(h.OccurredAtUtc),
                h.Note))
            .ToList();

        var adjustmentDtos = adjustments
            .OrderBy(a => a.CreatedAtUtc)
            .ThenBy(a => a.Id)
            .Select(AdjustmentMapper.ToDto)
            .ToList();

        return new SpecialistCutDetailDto(
            cut.Id,
            cut.Folio,
            cut.TenantId,
            cut.SpecialistId,
            cut.SpecialistName,
            Gmt6Clock.FormatDateGmt6(cut.PeriodStartGmt6),
            Gmt6Clock.FormatDateGmt6(cut.PeriodEndGmt6),
            cut.Status.ToString(),
            new TotalsByMethodDto(cut.EfectivoMxn, cut.TarjetaMxn, cut.TransferenciaMxn),
            cut.TotalMxn,
            cut.PaymentCount,
            cut.PreparedByAccountId,
            cut.PreparedByName,
            Gmt6Clock.FormatIsoGmt6(cut.PreparedAtUtc),
            Gmt6Clock.FormatIsoGmt6(cut.UpdatedAtUtc),
            cut.ConfirmedByName,
            cut.ConfirmedAtUtc is DateTimeOffset c ? Gmt6Clock.FormatIsoGmt6(c) : null,
            cut.LastObservationNote,
            paymentDtos,
            historyDtos,
            adjustmentDtos);
    }
}
