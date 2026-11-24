using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Saas.Billing.Application.UseCases.ChargeSheets;

namespace Saas.Billing.Api.Endpoints;

public sealed record UpsertChargeSheetRequest(
    IReadOnlyList<ChargeLineItemInput> Items);

internal static class ChargeSheetEndpoints
{
    public static void MapChargeSheetEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1")
            .WithTags("ChargeSheets")
            .RequireAuthorization();

        group.MapGet("/charge-sheets", async (
                ClaimsPrincipal user,
                [FromQuery] Guid? encounterId,
                [FromQuery] Guid? patientId,
                [FromQuery] Guid? specialistId,
                [FromQuery] string? status,
                ListChargeSheetsUseCase useCase,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!user.TryGetActorContext(out var actor))
                {
                    return Results.Unauthorized();
                }

                var result = await useCase.ExecuteAsync(
                    actor,
                    encounterId,
                    patientId,
                    specialistId,
                    status,
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ProblemDetailsMapper.ToProblem(result.Error, httpContext);
            })
            .WithName("ListChargeSheets")
            .WithSummary("Listar hojas de cargos por atención (CU-20)")
            .Produces<ChargeSheetListResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);

        group.MapGet("/charge-sheets/{chargeSheetId:guid}", async (
                ClaimsPrincipal user,
                Guid chargeSheetId,
                GetChargeSheetByIdUseCase useCase,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!user.TryGetActorContext(out var actor))
                {
                    return Results.Unauthorized();
                }

                var result = await useCase.ExecuteAsync(actor, chargeSheetId, cancellationToken);
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ProblemDetailsMapper.ToProblem(result.Error, httpContext);
            })
            .WithName("GetChargeSheetById")
            .WithSummary("Consultar detalle e historial de hoja de cargos (CU-20)")
            .Produces<ChargeSheetDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPut("/encounters/{encounterId:guid}/charge-sheet", async (
                ClaimsPrincipal user,
                Guid encounterId,
                UpsertChargeSheetRequest request,
                UpsertChargeSheetUseCase useCase,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!user.TryGetActorContext(out var actor))
                {
                    return Results.Unauthorized();
                }

                var result = await useCase.ExecuteAsync(
                    actor,
                    encounterId,
                    request.Items ?? [],
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ProblemDetailsMapper.ToProblem(result.Error, httpContext);
            })
            .WithName("UpsertChargeSheet")
            .WithSummary("Crear o actualizar conceptos en hoja de cargos ABIERTA (CU-20)")
            .Produces<ChargeSheetDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/charge-sheets/{chargeSheetId:guid}/review", async (
                ClaimsPrincipal user,
                Guid chargeSheetId,
                ReviewChargeSheetUseCase useCase,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!user.TryGetActorContext(out var actor))
                {
                    return Results.Unauthorized();
                }

                var result = await useCase.ExecuteAsync(actor, chargeSheetId, cancellationToken);
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ProblemDetailsMapper.ToProblem(result.Error, httpContext);
            })
            .WithName("ReviewChargeSheet")
            .WithSummary("Confirmar cargos completos ABIERTA -> REVISADA (CU-20)")
            .Produces<ChargeSheetDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/charge-sheets/{chargeSheetId:guid}/reopen", async (
                ClaimsPrincipal user,
                Guid chargeSheetId,
                ReopenChargeSheetUseCase useCase,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!user.TryGetActorContext(out var actor))
                {
                    return Results.Unauthorized();
                }

                var result = await useCase.ExecuteAsync(actor, chargeSheetId, cancellationToken);
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ProblemDetailsMapper.ToProblem(result.Error, httpContext);
            })
            .WithName("ReopenChargeSheet")
            .WithSummary("Devolver hoja REVISADA -> ABIERTA antes de cobrar (CU-20)")
            .Produces<ChargeSheetDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
    }
}
