using Microsoft.Extensions.Options;
using Saas.Billing.Application.Ports;
using Saas.Billing.Domain.Common;
using Saas.Billing.Domain.ValueObjects;
using Saas.Billing.Infrastructure.Persistence;

namespace Saas.Billing.Infrastructure.Platform;

public sealed class PlatformOptions
{
    public const string SectionName = "Platform";

    public string Mode { get; init; } = "Local";
    public bool SimulateUnavailable { get; init; }
    public List<PlatformTenantSeedEntry> Tenants { get; init; } = [];
}

public sealed class PlatformTenantSeedEntry
{
    public Guid TenantId { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public TenantOperationalState State { get; init; } = TenantOperationalState.ACTIVA;
    public bool UsesBilling { get; init; } = true;
}

internal sealed class LocalTenantBillingAccessAdapter(
    IOptions<PlatformOptions> optionsAccessor,
    IClockPort clock) : ITenantBillingAccessPort
{
    private static readonly IReadOnlyList<PlatformTenantSeedEntry> DefaultTenants =
    [
        new()
        {
            TenantId = BillingDbInitializer.NorteTenantId,
            Slug = "clinica-norte",
            Name = "Clínica Médica del Norte",
            State = TenantOperationalState.ACTIVA,
            UsesBilling = true,
        },
        new()
        {
            TenantId = BillingDbInitializer.ValleTenantId,
            Slug = "clinica-valle",
            Name = "Consultorios del Valle",
            State = TenantOperationalState.SUSPENDIDA,
            UsesBilling = true,
        },
        new()
        {
            TenantId = BillingDbInitializer.AuroraTenantId,
            Slug = "clinica-aurora",
            Name = "Unidad Clínica Aurora",
            State = TenantOperationalState.PENDIENTE_ADMIN,
            UsesBilling = false,
        },
        new()
        {
            TenantId = BillingDbInitializer.SurNoBillingTenantId,
            Slug = "clinica-sur",
            Name = "Dispensario Sur (Sin Billing)",
            State = TenantOperationalState.ACTIVA,
            UsesBilling = false,
        },
    ];

    public Task<TenantBillingLookupResult> GetByTenantIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var opts = optionsAccessor.Value;
        if (opts.SimulateUnavailable)
        {
            return Task.FromResult(TenantBillingLookupResult.Unavailable());
        }

        var catalog = opts.Tenants.Count > 0 ? opts.Tenants : DefaultTenants;
        var match = catalog.FirstOrDefault(t => t.TenantId == tenantId);
        if (match is null)
        {
            return Task.FromResult(TenantBillingLookupResult.NotFound());
        }

        return Task.FromResult(TenantBillingLookupResult.WithState(new TenantBillingState(
            match.TenantId,
            match.Slug,
            match.Name,
            match.State,
            match.UsesBilling,
            match.State == TenantOperationalState.ACTIVA,
            Gmt6Clock.FormatIsoGmt6(clock.UtcNow))));
    }
}
