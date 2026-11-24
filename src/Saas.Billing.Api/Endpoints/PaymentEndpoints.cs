using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Saas.Billing.Application.UseCases.Payments;

namespace Saas.Billing.Api.Endpoints;

internal static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/payments")
            .WithTags("Payments")
            .RequireAuthorization();

        group.MapGet("/", async (
                ClaimsPrincipal user,
                [FromQuery] Guid? specialistId,
                [FromQuery] Guid? patientId,
                [FromQuery] string? method,
                [FromQuery] bool? unassignedOnly,
                [FromQuery] string? fromDateGmt6,
                [FromQuery] string? toDateGmt6,
                ListPaymentsUseCase useCase,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!user.TryGetActorContext(out var actor))
                {
                    return Results.Unauthorized();
                }

                var result = await useCase.ExecuteAsync(
                    actor,
                    specialistId,
                    patientId,
                    method,
                    unassignedOnly,
                    fromDateGmt6,
                    toDateGmt6,
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ProblemDetailsMapper.ToProblem(result.Error, httpContext);
            })
            .WithName("ListPayments")
            .WithSummary("Listar cobros registrados y cobros disponibles para corte (CU-21 / CU-22)")
            .Produces<PaymentListResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);

        group.MapPost("/", async (
                ClaimsPrincipal user,
                RecordPaymentCommand command,
                RecordFullPaymentUseCase useCase,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!user.TryGetActorContext(out var actor))
                {
                    return Results.Unauthorized();
                }

                var result = await useCase.ExecuteAsync(actor, command, cancellationToken);
                return result.IsSuccess
                    ? Results.Created($"/v1/payments/{result.Value.Id}", result.Value)
                    : ProblemDetailsMapper.ToProblem(result.Error, httpContext);
            })
            .WithName("RecordFullPayment")
            .WithSummary("Registrar cobro completo sin exigir nota médica cerrada (CU-21)")
            .Produces<PatientPaymentDto>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
    }
}
