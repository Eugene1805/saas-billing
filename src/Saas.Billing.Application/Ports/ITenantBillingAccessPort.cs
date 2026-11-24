using Saas.Billing.Domain.ValueObjects;

namespace Saas.Billing.Application.Ports;

public sealed record TenantBillingLookupResult(
    bool Found,
    bool DependencyUnavailable,
    TenantBillingState? Tenant)
{
    public static TenantBillingLookupResult WithState(TenantBillingState state) => new(true, false, state);
    public static TenantBillingLookupResult NotFound() => new(false, false, null);
    public static TenantBillingLookupResult Unavailable() => new(false, true, null);
}

public interface ITenantBillingAccessPort
{
    Task<TenantBillingLookupResult> GetByTenantIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
