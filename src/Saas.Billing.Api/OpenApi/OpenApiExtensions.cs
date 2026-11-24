using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Saas.Billing.Api.OpenApi;

internal static class OpenApiExtensions
{
    public static IServiceCollection AddBillingOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer<BillingOpenApiDocumentTransformer>();
        });

        return services;
    }

    public static void MapBillingOpenApi(this WebApplication app)
    {
        var openApiEnabled = app.Environment.IsDevelopment() ||
            app.Configuration.GetValue("OpenApi:Enabled", false);

        if (!openApiEnabled)
        {
            return;
        }

        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options.WithTitle("Vital - saas-billing API v1");
        });
    }
}

internal sealed class BillingOpenApiDocumentTransformer(
    IAuthenticationSchemeProvider authenticationSchemeProvider) : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "Vital - saas-billing API",
            Version = "v1",
            Description = "Microservicio de Billing opcional por clínica y cortes por especialista (CU-20 hojas de cargo, CU-21 cobro completo sin exigir nota cerrada, CU-22/CU-23/PC-05 cortes y discrepancias, ajustes compensatorios vinculados y KPI CU-16 en GMT-6).",
        };

        var schemes = await authenticationSchemeProvider.GetAllSchemesAsync();
        if (!schemes.Any(s => string.Equals(s.Name, "Bearer", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Access Token JWT emitido por saas-identity (/v1/auth/login o /v1/auth/platform/login).",
        };

        var requirement = new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
        };

        foreach (var (pathKey, pathItem) in document.Paths)
        {
            if (pathItem.Operations is null || !pathKey.StartsWith("/v1/", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var operation in pathItem.Operations.Values)
            {
                operation.Security ??= [];
                operation.Security.Add(requirement);
            }
        }
    }
}
