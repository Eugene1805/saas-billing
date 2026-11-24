namespace Saas.Billing.Infrastructure.Persistence.Tenancy;

public interface ITenantConnectionResolver
{
    bool TryResolveConnectionString(Guid tenantId, out string connectionString);

    IReadOnlyCollection<Guid> ListConfiguredTenantIds();
}
