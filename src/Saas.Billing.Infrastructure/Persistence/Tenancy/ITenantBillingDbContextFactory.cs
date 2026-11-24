namespace Saas.Billing.Infrastructure.Persistence.Tenancy;

public interface ITenantBillingDbContextFactory
{
    BillingDbContext CreateDbContext(Guid tenantId);
}
