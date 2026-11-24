using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Saas.Billing.Application.UseCases.Metrics;

namespace Saas.Billing.Api.Endpoints;

internal static class MetricsEndpoints
{
    public static void MapMetricsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/metrics")
            .WithTags("Metrics")
            .RequireAuthorization();

        group.MapGet("/clinic/payments", async (
                ClaimsPrincipal user,
                [FromQuery] string? fromDateGmt6,
                [FromQuery] string? toDateGmt6,
                GetClinicPaymentsMetricUseCase useCase,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!user.TryGetActorContext(out var actor))
                {
                    return Results.Unauthorized();
                }

                var result = await useCase.ExecuteAsync(
                    actor,
                    fromDateGmt6,
                    toDateGmt6,
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ProblemDetailsMapper.ToProblem(result.Error, httpContext);
            })
            .WithName("GetClinicPaymentsMetric")
            .WithSummary("Consultar KPI RECORDED_PAYMENTS en GMT-6 (CU-16)")
            .WithDescription("Devuelve cobros por método para AdminClinica o NOT_APPLICABLE si la clínica no utiliza Billing.")
            .Produces<KpiEnvelopeDto<RecordedPaymentsKpiData>>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/clinic/cuts", async (
                ClaimsPrincipal user,
                [FromQuery] string? fromDateGmt6,
                [FromQuery] string? toDateGmt6,
                GetClinicCutsMetricUseCase useCase,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!user.TryGetActorContext(out var actor))
                {
                    return Results.Unauthorized();
                }

                var result = await useCase.ExecuteAsync(
                    actor,
                    fromDateGmt6,
                    toDateGmt6,
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ProblemDetailsMapper.ToProblem(result.Error, httpContext);
            })
            .WithName("GetClinicCutsMetric")
            .WithSummary("Consultar KPI CUTS_STATUS en GMT-6 (CU-16)")
            .WithDescription("Devuelve conteos e importes de cortes por estado para AdminClinica o NOT_APPLICABLE sin Billing.")
            .Produces<KpiEnvelopeDto<CutsStatusKpiData>>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable);
    }
}
