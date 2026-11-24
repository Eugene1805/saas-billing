using System.Security.Claims;
using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Api.Endpoints;

public sealed record BillableConceptDto(
    string Code,
    string Label,
    string Category,
    decimal DefaultPriceMxn);

public sealed record BillableConceptCatalogResponse(
    int Total,
    IReadOnlyList<BillableConceptDto> Items);

internal static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/v1/catalog/concepts", (ClaimsPrincipal user) =>
            {
                if (!user.TryGetActorContext(out _))
                {
                    return Results.Unauthorized();
                }

                var items = BillableConcept.DefaultCatalog
                    .Select(c => new BillableConceptDto(
                        c.Code,
                        c.Label,
                        c.Category.ToString(),
                        c.DefaultPriceMxn))
                    .ToList();

                return Results.Ok(new BillableConceptCatalogResponse(items.Count, items));
            })
            .WithTags("Catalog")
            .WithName("ListBillableConcepts")
            .WithSummary("Catálogo base de conceptos cobrables (MXN)")
            .RequireAuthorization()
            .Produces<BillableConceptCatalogResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);
    }
}
