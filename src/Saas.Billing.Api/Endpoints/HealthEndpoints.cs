using Microsoft.Extensions.Options;
using Saas.Billing.Domain.Common;
using Saas.Billing.Infrastructure.Persistence.Tenancy;

namespace Saas.Billing.Api.Endpoints;

public sealed record HealthResponse(
    string Status,
    string Service,
    string Database,
    string TimestampGmt6,
    int ConfiguredTenants = 0,
    int ConnectedTenants = 0);

public sealed record LivenessHealthResponse(
    string Status,
    string Service,
    string Probe,
    string Scope,
    string TimestampGmt6);

public sealed record ReadinessHealthResponse(
    string Status,
    string Service,
    string Probe,
    string Scope,
    string Database,
    string TimestampGmt6,
    int ConfiguredTenants = 0,
    int ConnectedTenants = 0);

internal static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health", async (
                ITenantConnectionResolver connectionResolver,
                ITenantBillingDbContextFactory dbContextFactory,
                IOptionsMonitor<TenantDatabasesOptions> optionsMonitor,
                CancellationToken cancellationToken) =>
            {
                var nowGmt6 = Gmt6Clock.FormatIsoGmt6(DateTimeOffset.UtcNow);
                var (healthy, databaseState, configuredCount, connectedCount) = await ProbeTenantsAsync(
                    connectionResolver,
                    dbContextFactory,
                    optionsMonitor.CurrentValue,
                    cancellationToken);

                if (!healthy)
                {
                    return Results.Json(
                        new HealthResponse("Unhealthy", "saas-billing", databaseState, nowGmt6, configuredCount, connectedCount),
                        statusCode: StatusCodes.Status503ServiceUnavailable);
                }

                return Results.Ok(
                    new HealthResponse("Healthy", "saas-billing", databaseState, nowGmt6, configuredCount, connectedCount));
            })
            .WithName("GetHealth")
            .WithTags("Health")
            .WithSummary("Estado de salud del servicio y clústeres PostgreSQL por clínica")
            .AllowAnonymous()
            .Produces<HealthResponse>(StatusCodes.Status200OK)
            .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapGet("/health/live", () =>
            {
                var nowGmt6 = Gmt6Clock.FormatIsoGmt6(DateTimeOffset.UtcNow);
                return Results.Ok(
                    new LivenessHealthResponse("Healthy", "saas-billing", "live", "process-only", nowGmt6));
            })
            .WithName("GetHealthLive")
            .WithTags("Health")
            .WithSummary("Sonda de liveness para Kubernetes")
            .AllowAnonymous()
            .Produces<LivenessHealthResponse>(StatusCodes.Status200OK);

        endpoints.MapGet("/health/ready", async (
                ITenantConnectionResolver connectionResolver,
                ITenantBillingDbContextFactory dbContextFactory,
                IOptionsMonitor<TenantDatabasesOptions> optionsMonitor,
                CancellationToken cancellationToken) =>
            {
                var nowGmt6 = Gmt6Clock.FormatIsoGmt6(DateTimeOffset.UtcNow);
                var (healthy, databaseState, configuredCount, connectedCount) = await ProbeTenantsAsync(
                    connectionResolver,
                    dbContextFactory,
                    optionsMonitor.CurrentValue,
                    cancellationToken);

                if (!healthy)
                {
                    return Results.Json(
                        new ReadinessHealthResponse("Unhealthy", "saas-billing", "ready", "database", databaseState, nowGmt6, configuredCount, connectedCount),
                        statusCode: StatusCodes.Status503ServiceUnavailable);
                }

                return Results.Ok(
                    new ReadinessHealthResponse("Healthy", "saas-billing", "ready", "database", databaseState, nowGmt6, configuredCount, connectedCount));
            })
            .WithName("GetHealthReady")
            .WithTags("Health")
            .WithSummary("Sonda de readiness multi-tenant PostgreSQL")
            .AllowAnonymous()
            .Produces<ReadinessHealthResponse>(StatusCodes.Status200OK)
            .Produces<ReadinessHealthResponse>(StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<(bool Healthy, string DatabaseState, int ConfiguredTenants, int ConnectedTenants)> ProbeTenantsAsync(
        ITenantConnectionResolver connectionResolver,
        ITenantBillingDbContextFactory dbContextFactory,
        TenantDatabasesOptions options,
        CancellationToken cancellationToken)
    {
        var configuredIds = connectionResolver.ListConfiguredTenantIds();
        if (configuredIds.Count == 0)
        {
            var secretsConfigured = !string.IsNullOrWhiteSpace(options.SecretsDirectory) &&
                                    Directory.Exists(options.SecretsDirectory);
            return secretsConfigured
                ? (true, "Connected", 0, 0)
                : (false, "NotConfigured", 0, 0);
        }

        var connected = 0;
        foreach (var tenantId in configuredIds)
        {
            try
            {
                await using var dbContext = dbContextFactory.CreateDbContext(tenantId);
                if (await dbContext.Database.CanConnectAsync(cancellationToken))
                {
                    connected++;
                }
            }
            catch
            {
                // Ignorar falla individual en sonda agregada si al menos un tenant responde.
            }
        }

        return connected > 0
            ? (true, "Connected", configuredIds.Count, connected)
            : (false, "Disconnected", configuredIds.Count, 0);
    }
}
