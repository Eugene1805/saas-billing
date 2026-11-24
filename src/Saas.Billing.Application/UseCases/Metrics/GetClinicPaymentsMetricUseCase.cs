using Saas.Billing.Application.Ports;
using Saas.Billing.Domain.Common;
using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Application.UseCases.Metrics;

public sealed class GetClinicPaymentsMetricUseCase(
    ITenantBillingAccessPort tenantPort,
    IPaymentRepository paymentRepository,
    IClockPort clock)
{
    public async Task<Result<KpiEnvelopeDto<RecordedPaymentsKpiData>>> ExecuteAsync(
        BillingActorContext actor,
        string? fromDateGmt6,
        string? toDateGmt6,
        CancellationToken cancellationToken = default)
    {
        var guard = MetricsGuardHelper.ValidateActorAndDateRange(actor, fromDateGmt6, toDateGmt6);
        if (!guard.IsSuccess)
        {
            return Result<KpiEnvelopeDto<RecordedPaymentsKpiData>>.Failure(guard.Error);
        }

        var tenantCheck = await BillingUseCaseGuards.CheckTenantStateAsync(actor, tenantPort, cancellationToken);
        if (!tenantCheck.IsSuccess)
        {
            return Result<KpiEnvelopeDto<RecordedPaymentsKpiData>>.Failure(tenantCheck.Error);
        }

        var (fromDate, toDate) = guard.Value;
        var evaluatedAtGmt6 = Gmt6Clock.FormatIsoGmt6(clock.UtcNow);
        var fromLabel = fromDate is DateOnly f ? Gmt6Clock.FormatDateGmt6(f) : null;
        var toLabel = toDate is DateOnly t ? Gmt6Clock.FormatDateGmt6(t) : null;

        if (!tenantCheck.Value.UsesBilling)
        {
            return Result<KpiEnvelopeDto<RecordedPaymentsKpiData>>.Success(
                new KpiEnvelopeDto<RecordedPaymentsKpiData>(
                    "RECORDED_PAYMENTS",
                    "NOT_APPLICABLE",
                    null,
                    evaluatedAtGmt6,
                    fromLabel,
                    toLabel,
                    "Esta clínica no tiene Billing habilitado (CU-16 A2)."));
        }

        var fromUtc = fromDate is DateOnly fd ? Gmt6Clock.ToUtcDayBounds(fd, fd).StartUtc : (DateTimeOffset?)null;
        var toExclusiveUtc = toDate is DateOnly td ? Gmt6Clock.ToUtcDayBounds(td, td).EndExclusiveUtc : (DateTimeOffset?)null;

        var payments = await paymentRepository.ListForMetricsAsync(
            tenantCheck.Value.TenantId,
            fromUtc,
            toExclusiveUtc,
            cancellationToken);

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
        var totalMxn = MoneyHelper.RoundMxn(efectivo + tarjeta + transferencia);

        var data = new RecordedPaymentsKpiData(
            totalMxn,
            payments.Count,
            new PaymentMethodBreakdownDto(efectivo, tarjeta, transferencia));

        return Result<KpiEnvelopeDto<RecordedPaymentsKpiData>>.Success(
            new KpiEnvelopeDto<RecordedPaymentsKpiData>(
                "RECORDED_PAYMENTS",
                "AVAILABLE",
                data,
                evaluatedAtGmt6,
                fromLabel,
                toLabel,
                null));
    }
}
