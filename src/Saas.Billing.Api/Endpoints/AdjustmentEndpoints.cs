using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Saas.Billing.Application.UseCases.Adjustments;

namespace Saas.Billing.Api.Endpoints;

internal static class AdjustmentEndpoints
{
    public static void MapAdjustmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/adjustments")
            .WithTags("Adjustments")
            .RequireAuthorization();

        group.MapGet("/", async (
                ClaimsPrincipal user,
                [FromQuery] string? targetType,
                [FromQuery] Guid? targetId,
                ListAdjustmentsUseCase useCase,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!user.TryGetActorContext(out var actor))
                {
                    return Results.Unauthorized();
                }

                var result = await useCase.ExecuteAsync(actor, targetType, targetId, cancellationToken);
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ProblemDetailsMapper.ToProblem(result.Error, httpContext);
            })
            .WithName("ListBillingAdjustments")
            .WithSummary("Listar ajustes compensatorios vinculados a cobros o cortes confirmados")
            .Produces<AdjustmentListResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);

        group.MapPost("/", async (
                ClaimsPrincipal user,
                CreateAdjustmentCommand command,
                CreateAdjustmentUseCase useCase,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!user.TryGetActorContext(out var actor))
                {
                    return Results.Unauthorized();
                }

                var result = await useCase.ExecuteAsync(actor, command, cancellationToken);
                return result.IsSuccess
                    ? Results.Created($"/v1/adjustments/{result.Value.Id}", result.Value)
                    : ProblemDetailsMapper.ToProblem(result.Error, httpContext);
            })
            .WithName("CreateBillingAdjustment")
            .WithSummary("Registrar ajuste compensatorio vinculado sin alterar el folio original (AdminClinica)")
            .Produces<BillingAdjustmentDto>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
    }
}
