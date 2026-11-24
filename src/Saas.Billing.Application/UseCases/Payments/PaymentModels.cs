using Saas.Billing.Domain.Common;
using Saas.Billing.Domain.Entities;

namespace Saas.Billing.Application.UseCases.Payments;

public sealed record RecordPaymentCommand(
    Guid ChargeSheetId,
    decimal AmountMxn,
    string Method);

public sealed record PatientPaymentDto(
    Guid Id,
    string Folio,
    Guid TenantId,
    Guid ChargeSheetId,
    Guid EncounterId,
    Guid PatientId,
    Guid SpecialistId,
    decimal AmountMxn,
    string Method,
    string PaidAtGmt6,
    Guid CollectedByAccountId,
    string CollectedByName,
    Guid? CutId);

public sealed record PaymentListResponse(
    int Total,
    IReadOnlyList<PatientPaymentDto> Items);

internal static class PaymentMapper
{
    public static PatientPaymentDto ToDto(PatientPayment payment)
    {
        return new PatientPaymentDto(
            payment.Id,
            payment.Folio,
            payment.TenantId,
            payment.ChargeSheetId,
            payment.EncounterId,
            payment.PatientId,
            payment.SpecialistId,
            payment.AmountMxn,
            payment.Method.ToString(),
            Gmt6Clock.FormatIsoGmt6(payment.PaidAtUtc),
            payment.CollectedByAccountId,
            payment.CollectedByName,
            payment.CutId);
    }
}
