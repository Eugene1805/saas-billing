using Saas.Billing.Application.Ports;
using Saas.Billing.Domain.Common;
using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Application.UseCases.Metrics;

public sealed class GetClinicCutsMetricUseCase(
    ITenantBillingAccessPort tenantPort,
    ISpecialistCutRepository cutRepository,
    IClockPort clock)
{
    public async Task<Result<KpiEnvelopeDto<CutsStatusKpiData>>> ExecuteAsync(
        BillingActorContext actor,
        string? fromDateGmt6,
        string? toDateGmt6,
        CancellationToken cancellationToken = default)
    {
        var guard = MetricsGuardHelper.ValidateActorAndDateRange(actor, fromDateGmt6, toDateGmt6);
        if (!guard.IsSuccess)
        {
            return Result<KpiEnvelopeDto<CutsStatusKpiData>>.Failure(guard.Error);
        }

        var tenantCheck = await BillingUseCaseGuards.CheckTenantStateAsync(actor, tenantPort, cancellationToken);
        if (!tenantCheck.IsSuccess)
        {
            return Result<KpiEnvelopeDto<CutsStatusKpiData>>.Failure(tenantCheck.Error);
        }

        var (fromDate, toDate) = guard.Value;
        var evaluatedAtGmt6 = Gmt6Clock.FormatIsoGmt6(clock.UtcNow);
        var fromLabel = fromDate is DateOnly f ? Gmt6Clock.FormatDateGmt6(f) : null;
        var toLabel = toDate is DateOnly t ? Gmt6Clock.FormatDateGmt6(t) : null;

        if (!tenantCheck.Value.UsesBilling)
        {
            return Result<KpiEnvelopeDto<CutsStatusKpiData>>.Success(
                new KpiEnvelopeDto<CutsStatusKpiData>(
                    "CUTS_STATUS",
                    "NOT_APPLICABLE",
                    null,
                    evaluatedAtGmt6,
                    fromLabel,
                    toLabel,
                    "Esta clínica no tiene Billing habilitado (CU-16 A2)."));
        }

        var cuts = await cutRepository.ListAsync(
            new CutQueryFilter(
                tenantCheck.Value.TenantId,
                null,
                null,
                fromDate,
                toDate),
            cancellationToken);

        var preparado = cuts.Count(c => c.Status == SpecialistCutStatus.PREPARADO);
        var observado = cuts.Where(c => c.Status == SpecialistCutStatus.OBSERVADO).ToList();
        var confirmado = cuts.Where(c => c.Status == SpecialistCutStatus.CONFIRMADO).ToList();

        var data = new CutsStatusKpiData(
            cuts.Count,
            preparado,
            observado.Count,
            confirmado.Count,
            MoneyHelper.RoundMxn(confirmado.Sum(c => c.TotalMxn)),
            MoneyHelper.RoundMxn(observado.Sum(c => c.TotalMxn)));

        return Result<KpiEnvelopeDto<CutsStatusKpiData>>.Success(
            new KpiEnvelopeDto<CutsStatusKpiData>(
                "CUTS_STATUS",
                "AVAILABLE",
                data,
                evaluatedAtGmt6,
                fromLabel,
                toLabel,
                null));
    }
}
