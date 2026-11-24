using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Saas.Billing.Application.UseCases.Cuts;

namespace Saas.Billing.Api.Endpoints;

internal static class CutEndpoints
{
    public static void MapCutEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/cuts")
            .WithTags("Cuts")
            .RequireAuthorization();

        group.MapGet("/", async (
                ClaimsPrincipal user,
                [FromQuery] Guid? specialistId,
                [FromQuery] string? status,
                [FromQuery] string? fromDateGmt6,
                [FromQuery] string? toDateGmt6,
                ListCutsUseCase useCase,
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
                    status,
                    fromDateGmt6,
                    toDateGmt6,
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ProblemDetailsMapper.ToProblem(result.Error, httpContext);
            })
            .WithName("ListSpecialistCuts")
            .WithSummary("Listar cortes por especialista en GMT-6 (CU-22 / CU-23)")
            .Produces<SpecialistCutListResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);

        group.MapGet("/{cutId:guid}", async (
                ClaimsPrincipal user,
                Guid cutId,
                GetCutByIdUseCase useCase,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!user.TryGetActorContext(out var actor))
                {
                    return Results.Unauthorized();
                }

                var result = await useCase.ExecuteAsync(actor, cutId, cancellationToken);
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ProblemDetailsMapper.ToProblem(result.Error, httpContext);
            })
            .WithName("GetSpecialistCutById")
            .WithSummary("Consultar detalle, cobros incluidos, historial y ajustes del corte")
            .Produces<SpecialistCutDetailDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
                ClaimsPrincipal user,
                PrepareCutCommand command,
                PrepareCutUseCase useCase,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!user.TryGetActorContext(out var actor))
                {
                    return Results.Unauthorized();
                }

                var result = await useCase.ExecuteAsync(actor, command, cancellationToken);
                return result.IsSuccess
                    ? Results.Created($"/v1/cuts/{result.Value.Id}", result.Value)
                    : ProblemDetailsMapper.ToProblem(result.Error, httpContext);
            })
            .WithName("PrepareSpecialistCut")
            .WithSummary("Preparar corte por especialista evitando doble inclusión (CU-22)")
            .Produces<SpecialistCutDetailDto>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPut("/{cutId:guid}", async (
                ClaimsPrincipal user,
                Guid cutId,
                ReviseCutCommand command,
                ReviseCutUseCase useCase,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!user.TryGetActorContext(out var actor))
                {
                    return Results.Unauthorized();
                }

                var result = await useCase.ExecuteAsync(actor, cutId, command, cancellationToken);
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ProblemDetailsMapper.ToProblem(result.Error, httpContext);
            })
            .WithName("ReviseSpecialistCut")
            .WithSummary("Corregir corte OBSERVADO o PREPARADO con nota trazable y reenviar (CU-22 / PC-05)")
            .Produces<SpecialistCutDetailDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/{cutId:guid}/confirm", async (
                ClaimsPrincipal user,
                Guid cutId,
                [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] ResolveCutCommand? command,
                ConfirmCutUseCase useCase,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!user.TryGetActorContext(out var actor))
                {
                    return Results.Unauthorized();
                }

                var result = await useCase.ExecuteAsync(actor, cutId, command, cancellationToken);
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ProblemDetailsMapper.ToProblem(result.Error, httpContext);
            })
            .WithName("ConfirmSpecialistCut")
            .WithSummary("Confirmar recepción administrativa del corte por su especialista titular (CU-23)")
            .Produces<SpecialistCutDetailDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/{cutId:guid}/observe", async (
                ClaimsPrincipal user,
                Guid cutId,
                ResolveCutCommand command,
                ObserveCutUseCase useCase,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!user.TryGetActorContext(out var actor))
                {
                    return Results.Unauthorized();
                }

                var result = await useCase.ExecuteAsync(actor, cutId, command, cancellationToken);
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : ProblemDetailsMapper.ToProblem(result.Error, httpContext);
            })
            .WithName("ObserveSpecialistCut")
            .WithSummary("Reportar discrepancia en corte (OBSERVADO con comentario obligatorio) (CU-23 / PC-05)")
            .Produces<SpecialistCutDetailDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
    }
}
