using Saas.Billing.Domain.Common;
using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Application.UseCases.Metrics;

public sealed record KpiEnvelopeDto<TData>(
    string KpiKey,
    string Status,
    TData? Data,
    string EvaluatedAtGmt6,
    string? PeriodStartGmt6,
    string? PeriodEndGmt6,
    string? Reason);

public sealed record PaymentMethodBreakdownDto(
    decimal EFECTIVO,
    decimal TARJETA,
    decimal TRANSFERENCIA);

public sealed record RecordedPaymentsKpiData(
    decimal TotalMxn,
    int PaymentCount,
    PaymentMethodBreakdownDto ByMethod);

public sealed record CutsStatusKpiData(
    int TotalCuts,
    int PreparadoCount,
    int ObservadoCount,
    int ConfirmadoCount,
    decimal ConfirmedAmountMxn,
    decimal ObservedAmountMxn);

internal static class MetricsGuardHelper
{
    public static Result<(DateOnly? FromDate, DateOnly? ToDate)> ValidateActorAndDateRange(
        BillingActorContext actor,
        string? fromDateGmt6,
        string? toDateGmt6)
    {
        if (actor.Role != OrgRole.ADMIN_CLINICA)
        {
            return Result<(DateOnly?, DateOnly?)>.Failure(
                "FORBIDDEN_METRICS_ROLE",
                "Los KPI económicos de clínica corresponden exclusivamente a AdminClinica (CU-16).",
                403);
        }

        DateOnly? fromDate = null;
        DateOnly? toDate = null;

        if (!string.IsNullOrWhiteSpace(fromDateGmt6))
        {
            if (!Gmt6Clock.TryParseGmt6Date(fromDateGmt6, out var parsedFrom))
            {
                return Result<(DateOnly?, DateOnly?)>.Failure(
                    "INVALID_DATE_RANGE",
                    "fromDateGmt6 debe tener formato yyyy-MM-dd.",
                    400);
            }

            fromDate = parsedFrom;
        }

        if (!string.IsNullOrWhiteSpace(toDateGmt6))
        {
            if (!Gmt6Clock.TryParseGmt6Date(toDateGmt6, out var parsedTo))
            {
                return Result<(DateOnly?, DateOnly?)>.Failure(
                    "INVALID_DATE_RANGE",
                    "toDateGmt6 debe tener formato yyyy-MM-dd.",
                    400);
            }

            toDate = parsedTo;
        }

        if (fromDate is DateOnly f && toDate is DateOnly t && f > t)
        {
            return Result<(DateOnly?, DateOnly?)>.Failure(
                "INVALID_DATE_RANGE",
                "fromDateGmt6 no puede ser mayor que toDateGmt6.",
                400);
        }

        return Result<(DateOnly?, DateOnly?)>.Success((fromDate, toDate));
    }
}
